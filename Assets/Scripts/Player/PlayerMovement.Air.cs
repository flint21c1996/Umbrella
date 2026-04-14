using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// PlayerMovement의 공중 액션 파트.
// 지상 점프, 2단 점프, 우산 활공 전환, 활공 중 낙하 속도 제한을 담당한다.
// 나중에 상승기류나 바람 같은 공중 퍼즐도 이 파일을 중심으로 확장할 수 있다.
public partial class PlayerMovement
{
    private const float GroundContactNormalY = 0.55f;

    [Header("Jump")]
    // 점프 입력 액션
    public InputActionReference jumpAction;

    // 1단 점프 힘
    public float jumpForce = 5.0f;

    // 2단 점프 힘
    public float doubleJumpForce = 5.0f;

    // 총 점프 가능 횟수. 2면 지상 점프 1번, 공중 점프 1번이 가능하다.
    [Min(1)]
    public int maxJumpCount = 2;

    // 점프할 때 아래로 떨어지던 속도를 지워서 버튼 입력이 더 또렷하게 느껴지도록 한다.
    public bool cancelDownwardVelocityOnJump = true;

    // 점프 직후 아직 충돌이 남아 있는 짧은 순간을 바닥으로 다시 판정하지 않게 하는 시간
    public float jumpGroundIgnoreTime = 0.1f;

    [Header("Glide")]
    // 2단 점프와 활공 전환 사이의 짧은 여유 시간. 이 시간 동안 우산은 닫힌 상태를 유지한다.
    public float glideStartHoldDelay = 0.15f;

    // 활공 중 아래로 떨어지는 최대 속도
    public float glideMaxFallSpeed = 2.0f;

    [Header("Double Jump Visual")]
    // 캐릭터 메시 같은 시각 루트를 넣으면 2단 점프 때 한 바퀴 도는 임시 연출을 한다.
    // Rigidbody가 붙은 플레이어 루트보다는 모델 자식 오브젝트를 넣는 편이 안전하다.
    public Transform doubleJumpSpinRoot;

    public float doubleJumpSpinDuration = 0.35f;
    public float doubleJumpSpinDegrees = 360.0f;

    [Header("Umbrella Jump")]
    // 우산 보유 여부를 확인할 때 사용한다. 비워두면 같은 오브젝트에서 자동으로 찾는다.
    public PlayerUmbrellaController umbrellaController;

    [Header("Jump Events")]
    public UnityEvent onJump = new UnityEvent();
    public UnityEvent onDoubleJump = new UnityEvent();
    public UnityEvent onGlideStarted = new UnityEvent();
    public UnityEvent onGlideEnded = new UnityEvent();

    [SerializeField] private int jumpCount;
    [SerializeField] private bool isGliding;

    private bool isDoubleJumpSpinning;
    private bool canStartGlideAfterDoubleJump;
    private float doubleJumpSpinTimer;
    private float glideHoldTimer;
    private float groundIgnoreTimer;
    private Quaternion doubleJumpSpinStartRotation;

    public int JumpCount => jumpCount;
    public bool IsGliding => isGliding;

    // 우산 상태와 연동하기 위해 같은 Player에 있는 PlayerUmbrellaController를 찾는다.
    void InitializeJumpReferences()
    {
        // Inspector에서 직접 넣지 않았을 때만 같은 오브젝트에서 자동으로 찾는다.
        if (umbrellaController == null)
        {
            umbrellaController = GetComponent<PlayerUmbrellaController>();
        }
    }

    // Inspector에서 음수나 의미 없는 값이 들어가지 않도록 점프 관련 설정을 보정한다.
    void ValidateJumpSettings()
    {
        maxJumpCount = Mathf.Max(1, maxJumpCount);
        jumpForce = Mathf.Max(0.0f, jumpForce);
        doubleJumpForce = Mathf.Max(0.0f, doubleJumpForce);
        jumpGroundIgnoreTime = Mathf.Max(0.0f, jumpGroundIgnoreTime);
        glideStartHoldDelay = Mathf.Max(0.0f, glideStartHoldDelay);
        glideMaxFallSpeed = Mathf.Max(0.1f, glideMaxFallSpeed);
        doubleJumpSpinDuration = Mathf.Max(0.0f, doubleJumpSpinDuration);
    }

    // Input System의 점프 액션을 활성화한다.
    void EnableJumpAction()
    {
        EnableAction(jumpAction);
    }

    // 점프 액션을 끄고, 공중 액션이 남아 있지 않도록 정리한다.
    void DisableJumpAction()
    {
        DisableAction(jumpAction);
        EndGlide();
        CancelGlideHold();
        isDoubleJumpSpinning = false;
    }

    // 매 프레임 점프 입력, 활공 유지 입력, 2단 점프 회전 연출을 갱신한다.
    void UpdateJumpInput()
    {
        // 점프 직후에는 이전 바닥 충돌이 잠깐 남을 수 있으므로,
        // 짧은 시간 동안 바닥 판정을 무시해서 점프 횟수가 바로 초기화되지 않게 한다.
        if (groundIgnoreTimer > 0.0f)
        {
            groundIgnoreTimer -= Time.deltaTime;
        }

        // 바닥에 닿아 있으면 공중에서 사용한 점프/활공 상태를 다시 사용할 수 있게 초기화한다.
        if (isGrounded)
        {
            ResetAirActions();
        }

        InputAction jumpInputAction = jumpAction != null ? jumpAction.action : null;
        // 점프 액션이 비어 있으면 입력 처리는 건너뛰되, 진행 중인 회전 연출은 마저 갱신한다.
        if (jumpInputAction == null)
        {
            UpdateDoubleJumpSpin();
            return;
        }

        // Space를 새로 누른 순간에만 점프를 시도한다. 누르고 있는 동안 반복 점프되지 않게 한다.
        if (jumpInputAction.WasPressedThisFrame())
        {
            TryJump();
        }

        UpdateGlideHold(jumpInputAction);

        UpdateDoubleJumpSpin();
    }

    // 현재 상태를 기준으로 1단 점프 또는 2단 점프를 시도한다.
    void TryJump()
    {
        // 우산을 뒤집었거나 물을 붓는 중이면 점프 입력을 무시한다.
        if (IsJumpBlockedByUmbrellaState())
        {
            return;
        }

        // 바닥에 있으면 항상 1단 점프.
        // 공중이라면 조건을 검사한 뒤 2단 점프를 시도한다.
        if (isGrounded)
        {
            PerformJump(jumpForce, false);
            return;
        }

        // 이미 공중 점프까지 사용했다면 더 이상 점프하지 않는다.
        if (!CanDoubleJump())
        {
            return;
        }

        // 발판에서 걸어 떨어진 뒤 처음 누르는 공중 점프도 2단 점프로 취급한다.
        if (jumpCount == 0)
        {
            jumpCount = 1;
        }

        PerformJump(doubleJumpForce, true);
    }

    // 공중에서 추가 점프를 사용할 수 있는지 판단한다.
    bool CanDoubleJump()
    {
        // 허용된 점프 횟수를 모두 썼으면 2단 점프를 막는다.
        if (jumpCount >= maxJumpCount)
        {
            return false;
        }

        // 우산이 필요한 공중 액션이므로, 우산이 없으면 2단 점프를 막는다.
        return umbrellaController == null || umbrellaController.HasUmbrella;
    }

    // 물 받기/붓기처럼 우산 조작 중인 상태에서는 점프를 막는다.
    bool IsJumpBlockedByUmbrellaState()
    {
        // 우산 컨트롤러가 없거나 아직 우산을 얻지 않았다면 우산 상태로 점프를 제한하지 않는다.
        if (umbrellaController == null || !umbrellaController.HasUmbrella)
        {
            return false;
        }

        PlayerUmbrellaController.UmbrellaState currentUmbrellaState = umbrellaController.CurrentState;
        return currentUmbrellaState == PlayerUmbrellaController.UmbrellaState.UpsideDown
            || currentUmbrellaState == PlayerUmbrellaController.UmbrellaState.Pouring;
    }

    // 실제 점프 힘을 적용하고, 2단 점프라면 회전 연출과 활공 대기 상태를 시작한다.
    void PerformJump(float force, bool isDoubleJump)
    {
        EndGlide();
        CancelGlideHold();
        umbrellaController?.CloseUmbrella();

        // 떨어지는 중 점프하면 하강 속도를 지우고 위로 튀는 느낌을 또렷하게 만든다.
        if (cancelDownwardVelocityOnJump && rb.linearVelocity.y < 0.0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0.0f, rb.linearVelocity.z);
        }

        rb.AddForce(Vector3.up * force, ForceMode.Impulse);
        jumpCount = Mathf.Min(jumpCount + 1, maxJumpCount);
        isGrounded = false;
        groundIgnoreTimer = jumpGroundIgnoreTime;

        // 1단 점프는 여기서 끝난다. 회전/활공 대기는 2단 점프에서만 시작한다.
        if (!isDoubleJump)
        {
            onJump.Invoke();
            return;
        }

        BeginDoubleJumpSpin();
        StartGlideHold();
        onDoubleJump.Invoke();
    }

    // 2단 점프 직후 Space를 계속 누르는지 확인하기 위한 활공 대기 상태를 시작한다.
    void StartGlideHold()
    {
        // 2단 점프 직후 바로 활공하지 않고,
        // Space를 계속 누르고 있는지 확인하는 대기 상태로 들어간다.
        canStartGlideAfterDoubleJump = true;
        glideHoldTimer = 0.0f;
    }

    // Space 유지 여부에 따라 활공을 시작하거나, 활공 중이면 종료한다.
    void UpdateGlideHold(InputAction jumpInputAction)
    {
        // 활공 중 Space를 떼면 즉시 우산을 접고 낙하한다.
        if (isGliding)
        {
            // Space를 계속 누르고 있을 때만 활공을 유지한다.
            if (!jumpInputAction.IsPressed())
            {
                EndGlide();
            }

            return;
        }

        // 2단 점프 직후의 대기 상태가 아니라면 활공 입력을 검사하지 않는다.
        if (!canStartGlideAfterDoubleJump)
        {
            return;
        }

        // 활공이 시작되기 전에 Space를 떼면 이번 활공 시도는 취소한다.
        if (!jumpInputAction.IsPressed())
        {
            CancelGlideHold();
            return;
        }

        glideHoldTimer += Time.deltaTime;
        // 짧은 대기 시간을 넘길 때까지 Space를 유지해야 활공으로 넘어간다.
        if (glideHoldTimer >= glideStartHoldDelay)
        {
            BeginGlide();
        }
    }

    // 활공 시작 대기 상태를 취소한다.
    void CancelGlideHold()
    {
        canStartGlideAfterDoubleJump = false;
        glideHoldTimer = 0.0f;
    }

    // 활공 상태로 전환하고 우산을 펼친다.
    void BeginGlide()
    {
        // 활공으로 넘어가는 순간에만 우산을 연다.
        // 그래서 1단/2단 점프 자체는 우산이 닫힌 상태로 유지된다.
        // 이미 활공 중이면 중복으로 이벤트를 호출하지 않는다.
        if (isGliding)
        {
            return;
        }

        CancelGlideHold();
        umbrellaController?.OpenUmbrella();
        isGliding = true;
        onGlideStarted.Invoke();
    }

    // 활공 상태를 끝내고 우산을 접는다.
    void EndGlide()
    {
        // 활공 중이 아닐 때는 우산 상태나 이벤트를 건드리지 않는다.
        if (!isGliding)
        {
            return;
        }

        isGliding = false;
        umbrellaController?.CloseUmbrella();
        onGlideEnded.Invoke();
    }

    // 착지했을 때 점프 횟수와 활공 상태를 초기화한다.
    void ResetAirActions()
    {
        jumpCount = 0;
        CancelGlideHold();
        EndGlide();
    }

    // 활공 중이면 아래로 떨어지는 최대 속도를 제한한다.
    void ApplyGlideFallLimit()
    {
        // 활공은 위로 띄우는 힘이 아니라, 아래로 너무 빠르게 떨어지지 않게 막는 상태다.
        // 상승기류는 나중에 별도 Area 컴포넌트가 추가 힘을 주는 방식으로 확장하면 된다.
        // 활공 중이 아니거나 이미 충분히 천천히 떨어지고 있으면 속도를 수정하지 않는다.
        if (!isGliding || rb.linearVelocity.y >= -glideMaxFallSpeed)
        {
            return;
        }

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, -glideMaxFallSpeed, rb.linearVelocity.z);
    }

    // 2단 점프가 시작될 때 시각 모델의 회전 연출 시작값을 저장한다.
    void BeginDoubleJumpSpin()
    {
        // 회전시킬 시각 루트가 없거나 연출 시간이 0이면 회전 연출을 생략한다.
        if (doubleJumpSpinRoot == null || doubleJumpSpinDuration <= 0.0f || Mathf.Approximately(doubleJumpSpinDegrees, 0.0f))
        {
            return;
        }

        isDoubleJumpSpinning = true;
        doubleJumpSpinTimer = 0.0f;
        doubleJumpSpinStartRotation = doubleJumpSpinRoot.localRotation;
    }

    // 2단 점프 중 시각 모델을 Y축으로 한 바퀴 회전시킨다.
    void UpdateDoubleJumpSpin()
    {
        // 회전 연출 중이 아니거나 대상 Transform이 없으면 갱신하지 않는다.
        if (!isDoubleJumpSpinning || doubleJumpSpinRoot == null)
        {
            return;
        }

        doubleJumpSpinTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(doubleJumpSpinTimer / doubleJumpSpinDuration);
        float spinAngle = doubleJumpSpinDegrees * progress;

        doubleJumpSpinRoot.localRotation = doubleJumpSpinStartRotation * Quaternion.Euler(0.0f, spinAngle, 0.0f);

        // 한 바퀴 연출이 끝나면 시작 회전값으로 되돌려 다음 이동 회전이 어긋나지 않게 한다.
        if (progress >= 1.0f)
        {
            doubleJumpSpinRoot.localRotation = doubleJumpSpinStartRotation;
            isDoubleJumpSpinning = false;
        }
    }

    // 충돌이 유지되는 동안 바닥으로 인정할 수 있는 접촉이 있는지 확인한다.
    void OnCollisionStay(Collision collision)
    {
        // 점프 직후에는 아직 남아 있는 바닥 접촉을 무시한다.
        if (groundIgnoreTimer > 0.0f)
        {
            return;
        }

        // 충돌 중 하나라도 바닥 접촉이면 착지 상태로 바꾼다.
        if (HasGroundContact(collision))
        {
            isGrounded = true;
        }
    }

    // 충돌에서 벗어나면 일단 공중 상태로 본다.
    void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }

    // 충돌 지점 중 위쪽을 향한 면이 있을 때만 바닥 접촉으로 인정한다.
    bool HasGroundContact(Collision collision)
    {
        // 벽이나 옆면 충돌을 바닥으로 착각하지 않게 위쪽을 향한 접촉만 바닥으로 인정한다.
        for (int i = 0; i < collision.contactCount; i++)
        {
            // normal.y가 충분히 크면 플레이어 아래쪽에서 받쳐주는 면으로 판단한다.
            if (collision.GetContact(i).normal.y >= GroundContactNormalY)
            {
                return true;
            }
        }

        return false;
    }
}
