using UnityEngine;

// Player 애니메이션의 locomotion 모드.
public enum PlayerLocomotionMode
{
    Empty = 0,
    UmbrellaClosed = 1,
    UmbrellaOpen = 2,
    UmbrellaUpsideDown = 3,

    // 바람이나 퍼즐 기믹으로 우산살이 뒤집힌 상태의 locomotion 모드.
    UmbrellaInvert = 4,

    // 아직 실제 기획단계에서 어떻게 될지는 모르지만, 물건 들고 옮기기, 밀기/당기기같은 모드를 추가해놨다.
    CarryObject = 5,
    PushPull = 6
}

// PlayerMovement와 PlayerUmbrellaController의 상태를 읽어 Animator 파라미터로 전달한다.
public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    // 플레이어 상태를 시각 모델 애니메이션에 반영할 Animator.
    [SerializeField] private Animator ModelAnimator;

    // 실제 이동 속도와 수직 속도를 읽기 위한 Rigidbody.
    [SerializeField] private Rigidbody PlayerRigidbody;

    // 지면 상태처럼 이동 컴포넌트가 이미 판단한 값을 읽는다.
    [SerializeField] private PlayerMovement PlayerMovement;

    // 우산 보유 여부와 현재 우산 상태를 애니메이션 모드로 변환할 때 사용한다.
    [SerializeField] private PlayerUmbrellaController UmbrellaController;

    [Header("Debug")]
    [HideInInspector] public bool ShowDebugOverlay = true;

    // ScoopWater 트리거 테스트 시 우산에 임시로 채워 넣을 물의 양.
    [SerializeField] private float DebugScoopWaterAmount = 1.0f;

    [Header("Locomotion Override")]
    // 임시 애니메이션 테스트용 수동 모드. 실제 플레이에서는 꺼두는 것을 기본으로 한다.
    [SerializeField] private bool UseLocomotionModeOverride;
    [SerializeField] private PlayerLocomotionMode LocomotionModeOverride = PlayerLocomotionMode.Empty;

    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int LocomotionModeHash = Animator.StringToHash("LocomotionMode");
    private static readonly int PickupUmbrellaHash = Animator.StringToHash("PickupUmbrella");
    private static readonly int OpenUmbrellaHash = Animator.StringToHash("OpenUmbrella");
    private static readonly int CloseUmbrellaHash = Animator.StringToHash("CloseUmbrella");
    private static readonly int InvertUmbrellaHash = Animator.StringToHash("InvertUmbrella");
    private static readonly int UpsideDownUmbrellaHash = Animator.StringToHash("UpsideDownUmbrella");
    private static readonly int ScoopWaterHash = Animator.StringToHash("ScoopWater");
    private static readonly int PourWaterHash = Animator.StringToHash("PourWater");

    private bool hasAnimatorParameterCache;
    private bool hasPickupUmbrellaTrigger;
    private bool hasOpenUmbrellaTrigger;
    private bool hasCloseUmbrellaTrigger;
    private bool hasInvertUmbrellaTrigger;
    private bool hasUpsideDownUmbrellaTrigger;
    private bool hasScoopWaterTrigger;
    private bool hasPourWaterTrigger;

    private bool hasPreviousAnimationState;
    private bool previousHasUmbrella;
    private PlayerUmbrellaController.UmbrellaState previousUmbrellaState;
    private PlayerLocomotionMode previousLocomotionMode;
    private float previousStoredWater;

    private float currentMoveSpeed;
    private float currentVerticalSpeed;
    private PlayerLocomotionMode currentLocomotionMode;
    private string lastAnimationDebugEvent = "None";

    void Awake()
    {
        ResolveReferences();
        RefreshAnimatorParameterCache();
    }

    void Reset()
    {
        ResolveReferences();
    }

    void LateUpdate()
    {
        // 이동/우산 상태 갱신이 끝난 뒤 같은 프레임의 최종 상태를 Animator에 반영한다.
        UpdateAnimatorParameters();
        UpdateAnimationStateDebug();
    }

    public void SetDebugVisible(bool showOverlay)
    {
        ShowDebugOverlay = showOverlay;
    }

    void ResolveReferences()
    {
        if (PlayerRigidbody == null)
        {
            PlayerRigidbody = GetComponent<Rigidbody>();
        }

        if (PlayerMovement == null)
        {
            PlayerMovement = GetComponent<PlayerMovement>();
        }

        if (UmbrellaController == null)
        {
            UmbrellaController = GetComponent<PlayerUmbrellaController>();
        }

        if (ModelAnimator == null)
        {
            // 기본 구조는 Player 아래 VisualRoot에 Animator가 붙는 형태를 기대한다.
            ModelAnimator = GetComponentInChildren<Animator>();
        }

        hasAnimatorParameterCache = false;
    }

    // 현재 플레이어 상태를 Animator 파라미터로 전달한다.
    void UpdateAnimatorParameters()
    {
        if (ModelAnimator == null || PlayerRigidbody == null || PlayerMovement == null)
        {
            return;
        }

        Vector3 velocity = PlayerRigidbody.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0.0f, velocity.z);

        currentMoveSpeed = horizontalVelocity.magnitude;
        currentVerticalSpeed = velocity.y;
        currentLocomotionMode = GetCurrentLocomotionMode();

        ModelAnimator.SetFloat(MoveSpeedHash, currentMoveSpeed);
        ModelAnimator.SetFloat(VerticalSpeedHash, currentVerticalSpeed);
        ModelAnimator.SetBool(IsGroundedHash, PlayerMovement.isGrounded);
        ModelAnimator.SetInteger(LocomotionModeHash, (int)currentLocomotionMode);
    }

    PlayerLocomotionMode GetCurrentLocomotionMode()
    {
        if (UseLocomotionModeOverride)
        {
            return LocomotionModeOverride;
        }

        if (UmbrellaController == null || !UmbrellaController.HasUmbrella)
        {
            return PlayerLocomotionMode.Empty;
        }

        // 현재 우산 상태를 애니메이션용 locomotion 모드로 변환한다.
        // 세부 기획이 확정되면 갈고리, 물건 들기, 밀기/당기기 상태를 여기서 확장한다.
        switch (UmbrellaController.CurrentState)
        {
            case PlayerUmbrellaController.UmbrellaState.Open:
                return PlayerLocomotionMode.UmbrellaOpen;
            case PlayerUmbrellaController.UmbrellaState.UpsideDown:
                return PlayerLocomotionMode.UmbrellaUpsideDown;
            case PlayerUmbrellaController.UmbrellaState.Pouring:
                return PlayerLocomotionMode.UmbrellaUpsideDown;
            default:
                return PlayerLocomotionMode.UmbrellaClosed;
        }
    }

    void UpdateAnimationStateDebug()
    {
        if (UmbrellaController == null)
        {
            return;
        }

        bool hasUmbrella = UmbrellaController.HasUmbrella;
        PlayerUmbrellaController.UmbrellaState umbrellaState = UmbrellaController.CurrentState;
        PlayerLocomotionMode locomotionMode = currentLocomotionMode;

        if (!hasPreviousAnimationState)
        {
            previousHasUmbrella = hasUmbrella;
            previousUmbrellaState = umbrellaState;
            previousLocomotionMode = locomotionMode;
            previousStoredWater = UmbrellaController.CurrentStoredWater;
            hasPreviousAnimationState = true;
            return;
        }

        // 게임플레이 상태의 변화량을 보고 필요한 Animator Trigger만 한 번 발동한다.
        // 지속 상태는 LocomotionMode가 담당하고, 순간 동작은 Trigger가 담당한다.
        if (previousHasUmbrella != hasUmbrella)
        {
            if (hasUmbrella)
            {
                TrySetTrigger(PickupUmbrellaHash, hasPickupUmbrellaTrigger);
                lastAnimationDebugEvent = "Trigger: PickupUmbrella / Umbrella acquired";
            }
            else
            {
                lastAnimationDebugEvent = "Umbrella removed -> LocomotionMode changed";
            }
        }
        else if (previousUmbrellaState != umbrellaState)
        {
            HandleUmbrellaStateChanged(previousUmbrellaState, umbrellaState);
        }
        else if (previousLocomotionMode != locomotionMode)
        {
            lastAnimationDebugEvent = $"LocomotionMode changed: {previousLocomotionMode} -> {locomotionMode}";
        }

        UpdateWaterAnimationDebug();

        previousHasUmbrella = hasUmbrella;
        previousUmbrellaState = umbrellaState;
        previousLocomotionMode = locomotionMode;
        previousStoredWater = UmbrellaController.CurrentStoredWater;
    }

    void HandleUmbrellaStateChanged(
        PlayerUmbrellaController.UmbrellaState previousState,
        PlayerUmbrellaController.UmbrellaState nextState)
    {
        if (previousState == PlayerUmbrellaController.UmbrellaState.Closed &&
            nextState == PlayerUmbrellaController.UmbrellaState.Open)
        {
            TrySetTrigger(OpenUmbrellaHash, hasOpenUmbrellaTrigger);
            lastAnimationDebugEvent = "Trigger: OpenUmbrella / State: Closed -> Open";
            return;
        }

        if (previousState == PlayerUmbrellaController.UmbrellaState.Open &&
            nextState == PlayerUmbrellaController.UmbrellaState.Closed)
        {
            TrySetTrigger(CloseUmbrellaHash, hasCloseUmbrellaTrigger);
            lastAnimationDebugEvent = "Trigger: CloseUmbrella / State: Open -> Closed";
            return;
        }

        if (nextState == PlayerUmbrellaController.UmbrellaState.UpsideDown)
        {
            TrySetTrigger(UpsideDownUmbrellaHash, hasUpsideDownUmbrellaTrigger);
            lastAnimationDebugEvent = $"Trigger: UpsideDownUmbrella / State: {previousState} -> {nextState}";
            return;
        }

        if (nextState == PlayerUmbrellaController.UmbrellaState.Pouring)
        {
            TrySetTrigger(PourWaterHash, hasPourWaterTrigger);
            lastAnimationDebugEvent = $"Trigger: PourWater / State: {previousState} -> {nextState}";
            return;
        }

        lastAnimationDebugEvent = $"Umbrella state changed: {previousState} -> {nextState}";
    }

    void UpdateWaterAnimationDebug()
    {
        if (UmbrellaController == null)
        {
            return;
        }

        float currentStoredWater = UmbrellaController.CurrentStoredWater;
        bool gainedWater = currentStoredWater > previousStoredWater + 0.001f;
        if (!gainedWater || UmbrellaController.CurrentState != PlayerUmbrellaController.UmbrellaState.UpsideDown)
        {
            return;
        }

        // 물 보유량이 증가한 순간을 ScoopWater 액션으로 취급한다.
        // 액션 이후 유지 자세는 LocomotionMode 3, UmbrellaUpsideDown이 담당한다.
        TrySetTrigger(ScoopWaterHash, hasScoopWaterTrigger);
        lastAnimationDebugEvent = $"Trigger: ScoopWater / StoredWater: {previousStoredWater:F2} -> {currentStoredWater:F2}";
    }

    void TrySetTrigger(int triggerHash, bool hasTrigger)
    {
        if (!hasTrigger || ModelAnimator == null)
        {
            return;
        }

        ModelAnimator.SetTrigger(triggerHash);
    }

    void DebugAcquireUmbrella()
    {
        if (UmbrellaController == null)
        {
            return;
        }

        // 우산 획득 애니메이션 진입을 확인하기 위한 테스트 진입점.
        UmbrellaController.AcquireUmbrella();
        lastAnimationDebugEvent = "Debug: Acquire umbrella";
    }

    void DebugRemoveUmbrella()
    {
        if (UmbrellaController == null)
        {
            return;
        }

        UmbrellaController.RemoveUmbrella();
        lastAnimationDebugEvent = "Debug: Remove umbrella";
    }

    void DebugCloseUmbrella()
    {
        if (UmbrellaController == null)
        {
            return;
        }

        if (!UmbrellaController.HasUmbrella)
        {
            UmbrellaController.AcquireUmbrella();
        }

        UmbrellaController.CloseUmbrella();
        lastAnimationDebugEvent = "Debug: Force Closed";
    }

    void DebugOpenUmbrella()
    {
        if (UmbrellaController == null)
        {
            return;
        }

        if (!UmbrellaController.HasUmbrella)
        {
            UmbrellaController.AcquireUmbrella();
        }

        UmbrellaController.OpenUmbrella();
        lastAnimationDebugEvent = "Debug: Force Open";
    }

    void DebugUpsideDownUmbrella()
    {
        if (UmbrellaController == null)
        {
            return;
        }

        if (!UmbrellaController.HasUmbrella)
        {
            UmbrellaController.AcquireUmbrella();
        }

        UmbrellaController.TurnUmbrellaUpsideDown();
        lastAnimationDebugEvent = "Debug: Force UpsideDown";
    }

    void DebugScoopWater()
    {
        if (UmbrellaController == null)
        {
            return;
        }

        if (!UmbrellaController.HasUmbrella)
        {
            UmbrellaController.AcquireUmbrella();
        }

        // 물을 퍼올릴 수 있는 자세를 먼저 만든 뒤 물 보유량을 증가시켜 ScoopWater 트리거를 검증한다.
        UmbrellaController.TurnUmbrellaUpsideDown();
        UmbrellaController.AddWater(DebugScoopWaterAmount);
        lastAnimationDebugEvent = $"Debug: Scoop water +{DebugScoopWaterAmount:F1}";
    }

    void DebugTogglePourWater()
    {
        if (UmbrellaController == null)
        {
            return;
        }

        if (UmbrellaController.CurrentState == PlayerUmbrellaController.UmbrellaState.Pouring)
        {
            UmbrellaController.StopPouring();
            lastAnimationDebugEvent = "Debug: Stop pouring";
            return;
        }

        if (!UmbrellaController.HasUmbrella)
        {
            UmbrellaController.AcquireUmbrella();
        }

        // PourWater는 UpsideDown 상태에서 물이 있을 때만 시작되므로,
        // 디버그 테스트에서는 필요한 선행 조건을 자동으로 맞춘다.
        UmbrellaController.TurnUmbrellaUpsideDown();
        if (UmbrellaController.CurrentStoredWater <= 0.0f)
        {
            UmbrellaController.AddWater(DebugScoopWaterAmount);
        }

        UmbrellaController.StartPouring();
        lastAnimationDebugEvent = "Debug: Start pouring";
    }

    void RefreshAnimatorParameterCache()
    {
        // Animator Controller에 아직 없는 Trigger는 호출하지 않도록 런타임에 존재 여부를 확인한다.
        // 임시 애니메이션 작업 중 파라미터 이름이 빠져도 NullReference 대신 디버그 문구로 확인할 수 있다.
        hasPickupUmbrellaTrigger = false;
        hasOpenUmbrellaTrigger = false;
        hasCloseUmbrellaTrigger = false;
        hasInvertUmbrellaTrigger = false;
        hasUpsideDownUmbrellaTrigger = false;
        hasScoopWaterTrigger = false;
        hasPourWaterTrigger = false;

        if (ModelAnimator == null)
        {
            hasAnimatorParameterCache = true;
            return;
        }

        AnimatorControllerParameter[] parameters = ModelAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type != AnimatorControllerParameterType.Trigger)
            {
                continue;
            }

            if (parameter.nameHash == PickupUmbrellaHash)
            {
                hasPickupUmbrellaTrigger = true;
            }
            else if (parameter.nameHash == OpenUmbrellaHash)
            {
                hasOpenUmbrellaTrigger = true;
            }
            else if (parameter.nameHash == CloseUmbrellaHash)
            {
                hasCloseUmbrellaTrigger = true;
            }
            else if (parameter.nameHash == InvertUmbrellaHash)
            {
                hasInvertUmbrellaTrigger = true;
            }
            else if (parameter.nameHash == UpsideDownUmbrellaHash)
            {
                hasUpsideDownUmbrellaTrigger = true;
            }
            else if (parameter.nameHash == ScoopWaterHash)
            {
                hasScoopWaterTrigger = true;
            }
            else if (parameter.nameHash == PourWaterHash)
            {
                hasPourWaterTrigger = true;
            }
        }

        hasAnimatorParameterCache = true;
    }

    void OnGUI()
    {
        if (!Application.isPlaying || !ShowDebugOverlay)
        {
            return;
        }

        if (!hasAnimatorParameterCache)
        {
            RefreshAnimatorParameterCache();
        }

        Rect panelRect = new Rect(Screen.width - 336.0f, 16.0f, 320.0f, 322.0f);
        GUI.Box(panelRect, "Animation Debug");

        float lineY = panelRect.y + 28.0f;
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"Locomotion: {currentLocomotionMode}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"MoveSpeed: {currentMoveSpeed:F2}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"VerticalSpeed: {currentVerticalSpeed:F2}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"IsGrounded: {PlayerMovement != null && PlayerMovement.isGrounded}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"Umbrella: {(UmbrellaController != null ? UmbrellaController.CurrentState.ToString() : "None")}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"Last Event: {lastAnimationDebugEvent}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"Triggers: Pick:{hasPickupUmbrellaTrigger} O:{hasOpenUmbrellaTrigger} C:{hasCloseUmbrellaTrigger} I:{hasInvertUmbrellaTrigger} U:{hasUpsideDownUmbrellaTrigger} S:{hasScoopWaterTrigger} P:{hasPourWaterTrigger}");

        lineY += 4.0f;
        DrawDebugButtons(panelRect.x + 12.0f, ref lineY);
    }

    void DrawDebugLabel(float x, ref float y, string text)
    {
        GUI.Label(new Rect(x, y, 296.0f, 18.0f), text);
        y += 20.0f;
    }

    void DrawDebugButtons(float x, ref float y)
    {
        float buttonWidth = 92.0f;
        float buttonHeight = 24.0f;
        float gap = 8.0f;

        // 테스트 입력은 플레이 조작과 충돌하지 않도록 단축키 대신 버튼으로만 제공한다.
        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "우산 획득"))
        {
            DebugAcquireUmbrella();
        }

        if (GUI.Button(new Rect(x + buttonWidth + gap, y, buttonWidth, buttonHeight), "우산 제거"))
        {
            DebugRemoveUmbrella();
        }

        y += buttonHeight + gap;

        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "접힘"))
        {
            DebugCloseUmbrella();
        }

        if (GUI.Button(new Rect(x + buttonWidth + gap, y, buttonWidth, buttonHeight), "펼침"))
        {
            DebugOpenUmbrella();
        }

        if (GUI.Button(new Rect(x + (buttonWidth + gap) * 2.0f, y, buttonWidth, buttonHeight), "뒤집힘"))
        {
            DebugUpsideDownUmbrella();
        }

        y += buttonHeight + gap;

        if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), "물 퍼올림"))
        {
            DebugScoopWater();
        }

        if (GUI.Button(new Rect(x + buttonWidth + gap, y, buttonWidth, buttonHeight), "물 붓기"))
        {
            DebugTogglePourWater();
        }

        y += buttonHeight + gap;
    }
}
