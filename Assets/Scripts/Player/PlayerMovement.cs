using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerMovement : MonoBehaviour
{
    private const float MoveInputThreshold = 0.0001f;

    [Header("Move")]
    // 목표 최대 이동 속도
    public float moveSpeed = 5.0f;

    // 지상에서 목표 속도까지 붙는 가속도
    public float groundAcceleration = 30.0f;

    // 지상에서 입력을 놓았을 때 줄어드는 감속도
    public float groundDeceleration = 35.0f;

    // 공중에서의 가속도
    public float airAcceleration = 12.0f;

    // 공중에서 입력을 놓았을 때의 감속도
    public float airDeceleration = 4.0f;

    // 이동 방향을 바라보는 회전 속도
    public float turnSpeed = 720.0f;

    // 우산 붓기처럼 명확한 조준 상태일 때는 더 빠르게 시선을 맞춘다.
    public float facingOverrideTurnSpeedMultiplier = 2.5f;

    // 이 각도부터 회전 중 감속을 시작한다
    public float turnSlowdownStartAngle = 35.0f;

    // 이 각도 이상 벌어지면 이동을 거의 멈추고 회전에 집중한다
    public float turnLockAngle = 140.0f;

    [Header("References")]
    // 이동 기준이 되는 카메라 리그
    public Transform cameraRig;

    // 이동 입력 액션
    public InputActionReference moveAction;

    // Rigidbody 참조
    private Rigidbody rb;

    // 현재 이동 입력값 저장
    private Vector2 moveInput;

    // 다른 시스템이 잠깐 바라보는 방향을 강제로 지정할 때 사용한다.
    private bool hasFacingOverride;
    private Vector3 facingOverrideDirection;

    // 상자 잡기처럼 플레이어가 바라보는 방향을 고정해야 하는 상태에서 사용한다.
    private bool isRotationLocked;

    // 바닥 체크용
    public bool isGrounded;

    // 밀기/당기기 같은 외부 시스템이 플레이어 이동 속도를 잠시 낮출 때 사용한다.
    private float externalMoveSpeedMultiplier = 1.0f;

    // 밀기/당기기처럼 다른 컴포넌트가 직접 수평 속도를 정할 때 사용한다.
    private bool hasExternalHorizontalVelocity;
    private Vector3 externalHorizontalVelocity;

    public Vector2 MoveInput => moveInput;
    public float ExternalMoveSpeedMultiplier => externalMoveSpeedMultiplier;
    public Vector3 RigidbodyPosition => rb != null ? rb.position : transform.position;

    // 필수 컴포넌트와 공중 액션 참조를 준비한다.
    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("Rigidbody is missing on the Player object.");
        }

        EnsureRigidbodyRotationConstraints();

        if (cameraRig == null)
        {
            Debug.LogError("CameraRig is not assigned in the Inspector.");
        }

        InitializeJumpReferences();
    }

    // Inspector 값이 바뀔 때 이동/점프 설정이 안전한 범위에 있도록 보정한다.
    void OnValidate()
    {
        ValidateJumpSettings();
        externalMoveSpeedMultiplier = Mathf.Max(0.0f, externalMoveSpeedMultiplier);
    }

    // 플레이어가 넘어지지 않게 X/Z 회전은 잠그되, 이동 방향과 회전 발판을 따라갈 수 있게 Y 회전은 열어둔다.
    void EnsureRigidbodyRotationConstraints()
    {
        if (rb == null)
        {
            return;
        }

        rb.constraints &= ~RigidbodyConstraints.FreezeRotationY;
        rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    // 이동 입력과 점프 입력을 활성화한다.
    void OnEnable()
    {
        EnableAction(moveAction);
        EnableJumpAction();
    }

    // 입력 비활성화와 함께 활공 같은 공중 상태도 정리한다.
    void OnDisable()
    {
        DisableAction(moveAction);
        DisableJumpAction();
    }

    // 입력은 프레임 단위로 읽어 저장한다.
    // Rigidbody에 힘을 주는 처리는 FixedUpdate에서 수행한다.
    void Update()
    {
        if (rb == null || cameraRig == null)
        {
            return;
        }

        // 입력은 Update에서 읽어서 저장
        InputAction moveInputAction = moveAction != null ? moveAction.action : null;
        moveInput = moveInputAction != null ? moveInputAction.ReadValue<Vector2>() : Vector2.zero;

        UpdateJumpInput();
    }

    // 물리 이동과 회전, 활공 낙하 제한을 적용한다.
    void FixedUpdate()
    {
        if (rb == null || cameraRig == null)
        {
            return;
        }

        ClearPhysicsAngularVelocity();
        ApplyMovingPlatformMotion();

        Vector3 moveDirection = GetCameraRelativeMoveDirection(moveInput);

        // 상자 잡기처럼 외부 컴포넌트가 수평 속도를 직접 지정하는 동안에는
        // 기본 이동 힘을 더하지 않고 외부 속도만 적용한다.
        if (hasExternalHorizontalVelocity)
        {
            ApplyExternalHorizontalVelocity();
            ApplyGlideFallLimit();
            ClearPhysicsAngularVelocity();
            return;
        }

        Vector3 effectiveMoveDirection = GetEffectiveMoveDirection(moveDirection);

        ApplyMovementForce(effectiveMoveDirection);

        // 상자를 잡고 있을 때는 플레이어가 상자 방향을 계속 바라보도록 회전을 잠글 수 있다.
        if (!isRotationLocked)
        {
            RotateTowardsMoveDirection(GetLookDirection(moveDirection));
        }

        ApplyGlideFallLimit();
        ClearPhysicsAngularVelocity();
    }

    // 외부 시스템이 기본 이동 속도에 배율을 걸 때 사용한다.
    // 예: 상자를 잡는 동안 기본 이동 힘은 0으로 두고 PushPullInteractor가 직접 속도를 준다.
    public void SetExternalMoveSpeedMultiplier(float multiplier)
    {
        externalMoveSpeedMultiplier = Mathf.Max(0.0f, multiplier);
    }

    // 외부 이동 속도 배율을 원래대로 되돌린다.
    public void ResetExternalMoveSpeedMultiplier()
    {
        externalMoveSpeedMultiplier = 1.0f;
    }

    // 외부 시스템이 플레이어 회전을 잠글 때 사용한다.
    public void SetRotationLocked(bool locked)
    {
        isRotationLocked = locked;
    }

    // 외부 시스템이 플레이어의 수평 속도를 직접 지정할 때 사용한다.
    // Y 속도는 점프/낙하를 위해 그대로 유지한다.
    public void SetHorizontalVelocity(Vector3 horizontalVelocity)
    {
        if (rb == null)
        {
            return;
        }

        horizontalVelocity.y = 0.0f;
        externalHorizontalVelocity = horizontalVelocity;
        hasExternalHorizontalVelocity = true;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
    }

    // 외부 수평 속도 제어를 해제해 기본 이동 로직이 다시 동작하도록 한다.
    public void ClearHorizontalVelocityOverride()
    {
        hasExternalHorizontalVelocity = false;
        externalHorizontalVelocity = Vector3.zero;
    }

    // 잡기 해제 같은 순간에 플레이어가 미끄러지지 않도록 수평 속도를 0으로 만든다.
    public void StopHorizontalMovement()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = new Vector3(0.0f, rb.linearVelocity.y, 0.0f);
    }

    // 잡기 시작 순간처럼 즉시 특정 방향을 바라봐야 할 때 사용한다.
    public void FaceDirectionImmediately(Vector3 worldDirection)
    {
        if (rb == null)
        {
            return;
        }

        worldDirection.y = 0.0f;
        if (worldDirection.sqrMagnitude < MoveInputThreshold)
        {
            return;
        }

        rb.rotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
    }

    // 카메라 기준 입력값을 월드 이동 방향으로 변환한다.
    // PushPullInteractor도 같은 기준을 써야 플레이어 입력과 상자 이동 방향이 어긋나지 않는다.
    public Vector3 GetCameraRelativeMoveDirection(Vector2 input)
    {
        if (cameraRig == null)
        {
            return Vector3.zero;
        }

        Vector3 forward = cameraRig.forward;
        Vector3 right = cameraRig.right;

        forward.y = 0.0f;
        right.y = 0.0f;

        forward.Normalize();
        right.Normalize();

        // 입력값을 카메라 기준 이동 방향으로 변환
        Vector3 moveDirection = forward * input.y + right * input.x;

        if (moveDirection.sqrMagnitude > 1.0f)
        {
            moveDirection.Normalize();
        }

        return moveDirection;
    }

    // 기본적으로는 이동 방향을 바라보지만, 우산 붓기처럼 외부 조준 방향이 있으면 그쪽을 우선한다.
    Vector3 GetLookDirection(Vector3 moveDirection)
    {
        if (hasFacingOverride && facingOverrideDirection.sqrMagnitude > MoveInputThreshold)
        {
            return facingOverrideDirection;
        }

        return moveDirection;
    }

    // 캐릭터가 이동 방향을 충분히 바라보기 전까지 이동량을 줄여 발 미끄럼을 완화한다.
    Vector3 GetEffectiveMoveDirection(Vector3 desiredMoveDirection)
    {
        if (desiredMoveDirection.sqrMagnitude < MoveInputThreshold)
        {
            return Vector3.zero;
        }

        Vector3 facingDirection = rb.rotation * Vector3.forward;
        facingDirection.y = 0.0f;
        facingDirection.Normalize();

        float angleToMoveDirection = Vector3.Angle(facingDirection, desiredMoveDirection);
        float moveAlignmentFactor = GetMoveAlignmentFactor(angleToMoveDirection);
        return desiredMoveDirection * moveAlignmentFactor;
    }

    float GetMoveAlignmentFactor(float angleToMoveDirection)
    {
        // 회전 중에도 완전히 멈추지 않게 하되,
        // 크게 반대 방향으로 꺾을 때는 속도를 줄여 발 미끄럼을 완화한다.
        if (angleToMoveDirection <= turnSlowdownStartAngle)
        {
            return 1.0f;
        }

        if (angleToMoveDirection >= turnLockAngle)
        {
            return 0.0f;
        }

        float t = Mathf.InverseLerp(turnLockAngle, turnSlowdownStartAngle, angleToMoveDirection);
        return Mathf.SmoothStep(0.0f, 1.0f, t);
    }

    // 목표 수평 속도에 가까워지도록 Rigidbody에 가속도를 준다.
    void ApplyMovementForce(Vector3 moveDirection)
    {
        Vector3 targetVelocity = moveDirection * (moveSpeed * externalMoveSpeedMultiplier);
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 currentHorizontalVelocity = new Vector3(currentVelocity.x, 0.0f, currentVelocity.z);

        bool hasMoveInput = moveDirection.sqrMagnitude > MoveInputThreshold;
        float acceleration = GetCurrentAcceleration(hasMoveInput);

        Vector3 velocityDelta = targetVelocity - currentHorizontalVelocity;
        Vector3 requiredAcceleration = velocityDelta / Time.fixedDeltaTime;
        Vector3 clampedAcceleration = Vector3.ClampMagnitude(requiredAcceleration, acceleration);

        rb.AddForce(clampedAcceleration, ForceMode.Acceleration);

        Vector3 newHorizontalVelocity = new Vector3(rb.linearVelocity.x, 0.0f, rb.linearVelocity.z);
        float currentMoveSpeedLimit = moveSpeed * externalMoveSpeedMultiplier;
        if (newHorizontalVelocity.magnitude > currentMoveSpeedLimit)
        {
            newHorizontalVelocity = newHorizontalVelocity.normalized * currentMoveSpeedLimit;
            rb.linearVelocity = new Vector3(newHorizontalVelocity.x, rb.linearVelocity.y, newHorizontalVelocity.z);
        }
    }

    // 외부에서 지정한 수평 속도를 그대로 적용한다.
    // 기본 이동 가속도 계산을 거치지 않으므로 상자 잡기 로직이 이동을 온전히 제어할 수 있다.
    void ApplyExternalHorizontalVelocity()
    {
        rb.linearVelocity = new Vector3(externalHorizontalVelocity.x, rb.linearVelocity.y, externalHorizontalVelocity.z);
    }

    // 지상/공중, 입력 유무에 따라 현재 사용할 가속도 또는 감속도를 고른다.
    float GetCurrentAcceleration(bool hasMoveInput)
    {
        if (isGrounded)
        {
            return hasMoveInput ? groundAcceleration : groundDeceleration;
        }

        return hasMoveInput ? airAcceleration : airDeceleration;
    }

    // 이동하거나 조준하는 방향으로 Rigidbody를 회전시킨다.
    void RotateTowardsMoveDirection(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude < MoveInputThreshold)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        float effectiveTurnSpeed = hasFacingOverride
            ? turnSpeed * facingOverrideTurnSpeedMultiplier
            : turnSpeed;
        Quaternion nextRotation = Quaternion.RotateTowards(
            rb.rotation,
            targetRotation,
            effectiveTurnSpeed * Time.fixedDeltaTime
        );

        rb.MoveRotation(nextRotation);
    }

    // 다른 시스템이 잠시 바라볼 방향을 지정한다.
    // 현재는 우산 붓기 조준 같은 상황에서 사용한다.
    public void SetFacingOverride(Vector3 worldDirection)
    {
        worldDirection.y = 0.0f;

        if (worldDirection.sqrMagnitude < MoveInputThreshold)
        {
            return;
        }

        hasFacingOverride = true;
        facingOverrideDirection = worldDirection.normalized;
    }

    // 외부 조준 방향을 해제하고 다시 이동 방향을 바라보게 한다.
    public void ClearFacingOverride()
    {
        hasFacingOverride = false;
        facingOverrideDirection = Vector3.zero;
    }

    // InputActionReference가 있을 때만 안전하게 활성화한다.
    void EnableAction(InputActionReference actionReference)
    {
        if (actionReference == null)
        {
            return;
        }

        actionReference.action.Enable();
    }

    // InputActionReference가 있을 때만 안전하게 비활성화한다.
    void DisableAction(InputActionReference actionReference)
    {
        if (actionReference == null)
        {
            return;
        }

        actionReference.action.Disable();
    }
}
