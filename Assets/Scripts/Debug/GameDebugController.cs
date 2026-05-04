using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
public class GameDebugController : MonoBehaviour
{
    public enum WaterBasinConnectionPreviewScope
    {
        SelectedTargets,
        SpecificTarget,
        AllTargets
    }

    public enum WaterBasinDebugOverlayScope
    {
        SelectedTargets,
        SelectedConnectedGroup,
        SpecificTarget,
        SpecificConnectedGroup,
        AllTargets
    }

    [Header("Input")]
    [Tooltip("F3 키로 전체 디버그 표시를 켜고 끈다.")]
    [SerializeField] private bool enableDebugToggleKey = true;

    [Header("Visibility")]
    [Tooltip("Game View에 그리는 디버그 UI 표시 여부.")]
    [SerializeField] private bool showDebugOverlay = true;

    [Tooltip("Scene View에 그리는 디버그 Gizmo 표시 여부. Game View의 라벨/점선은 Show Debug Overlay가 제어한다.")]
    [SerializeField] private bool showSceneViewGizmos = true;

    [Header("Targets")]
    [Tooltip("씬 안의 PlayerUmbrellaController를 자동으로 찾아 디버그 상태를 전달한다.")]
    [SerializeField] private bool autoFindUmbrellaControllers = true;

    [SerializeField] private PlayerUmbrellaController[] umbrellaControllers;

    // PlayerAnimationController가 그리는 Animation Debug 창의 표시 여부를 함께 제어하기 위한 대상 목록.
    // 디버그 UI의 실제 내용은 각 PlayerAnimationController가 소유하고, 이 컨트롤러는 표시 상태만 전달한다.
    [SerializeField] private PlayerAnimationController[] animationControllers;

    [Tooltip("우산 상태/F3 패널과 플레이어 우산 Gizmo를 표시한다.")]
    [FormerlySerializedAs("controlUmbrellaDebug")]
    [SerializeField] private bool showUmbrellaDebug = true;

    [Tooltip("물 타겟의 월드 라벨을 표시한다.")]
    [FormerlySerializedAs("controlWaterTargetDebug")]
    [SerializeField] private bool showWaterTargetDebug = true;

    [Tooltip("퍼즐 연결선과 퍼즐 상태 라벨을 표시한다.")]
    [FormerlySerializedAs("controlPuzzleDebug")]
    [SerializeField] private bool showPuzzleDebug = true;

    // Animation Debug 창은 PlayerAnimationController가 직접 그린다.
    // GameDebugController는 F3 전체 토글과 표시 여부만 조율한다.
    [SerializeField] private bool showAnimationDebug = true;


    //물 높낮이에 대한 변화와 제어 기능
    [Header("Water")]
    [SerializeField] private bool showWaterBasinDebug = true;

    [InspectorName("Game View 라벨 표시 범위")]
    [Tooltip("Game View에서 WaterBasinTarget 디버그 라벨을 표시할 범위입니다.")]
    [SerializeField] private WaterBasinDebugOverlayScope waterBasinDebugOverlayScope =
        WaterBasinDebugOverlayScope.SelectedTargets;

    [InspectorName("Game View 라벨 기준 타겟")]
    [Tooltip("표시 범위가 Specific Target 또는 Specific Connected Group일 때 기준으로 사용할 WaterBasinTarget입니다.")]
    [SerializeField] private WaterBasinTarget waterBasinDebugOverlayTarget;

    [Tooltip("에디터 Scene View에서 WaterBasinTarget 자동 연결 후보선을 표시한다.")]
    [SerializeField] private bool showWaterBasinAutoConnectionPreview = true;

    [Tooltip("WaterBasinTarget 자동 연결 툴에서 두 타겟 영역이 이 거리 이내에 있으면 연결 후보로 판단한다.")]
    [SerializeField] private float waterBasinAutoConnectionSearchDistance = 0.1f;

    [Tooltip("에디터 Scene View에서 WaterBasinTarget에 저장된 연결선을 표시한다.")]
    [SerializeField] private bool showWaterBasinSavedConnectionPreview = true;

    [Tooltip("물 연결선과 자동 연결 후보선을 표시할 기준 범위입니다.")]
    [InspectorName("Scene View 연결선 표시 범위")]
    [SerializeField] private WaterBasinConnectionPreviewScope waterBasinConnectionPreviewScope =
        WaterBasinConnectionPreviewScope.SelectedTargets;

    [Tooltip("표시 범위가 Specific Target일 때 연결선을 표시할 기준 WaterBasinTarget입니다.")]
    [InspectorName("Scene View 연결선 기준 타겟")]
    [SerializeField] private WaterBasinTarget waterBasinConnectionPreviewTarget;

    public bool ShowDebugOverlay => showDebugOverlay;
    public bool ShowSceneViewGizmos => showSceneViewGizmos;
    public static bool ShowWaterBasinAutoConnectionPreview { get; private set; } = true;
    public static float WaterBasinAutoConnectionSearchDistance { get; private set; } = 0.1f;
    public static bool ShowWaterBasinSavedConnectionPreview { get; private set; } = true;
    public static WaterBasinDebugOverlayScope WaterBasinOverlayScope { get; private set; } =
        WaterBasinDebugOverlayScope.SelectedTargets;
    public static WaterBasinTarget WaterBasinOverlayTarget { get; private set; }
    public static WaterBasinConnectionPreviewScope WaterBasinPreviewScope { get; private set; } =
        WaterBasinConnectionPreviewScope.SelectedTargets;
    public static WaterBasinTarget WaterBasinPreviewTarget { get; private set; }

    private bool hasAppliedDebugState;
    private bool lastAppliedOverlay;
    private bool lastAppliedSceneViewGizmos;
    private bool lastAppliedUmbrellaDebug;
    private bool lastAppliedWaterTargetDebug;
    private bool lastAppliedWaterBasinDebug;
    private WaterBasinDebugOverlayScope lastAppliedWaterBasinDebugOverlayScope;
    private WaterBasinTarget lastAppliedWaterBasinDebugOverlayTarget;
    private bool lastAppliedWaterBasinAutoConnectionPreview;
    private float lastAppliedWaterBasinAutoConnectionSearchDistance;
    private bool lastAppliedWaterBasinSavedConnectionPreview;
    private WaterBasinConnectionPreviewScope lastAppliedWaterBasinConnectionPreviewScope;
    private WaterBasinTarget lastAppliedWaterBasinConnectionPreviewTarget;
    private bool lastAppliedPuzzleDebug;
    private bool lastAppliedAnimationDebug;

    private void OnEnable()
    {
        ApplyDebugState(true);
    }

    private void Start()
    {
        ApplyDebugState(true);
    }

    private void OnValidate()
    {
        ApplyDebugState(true);
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            HandleDebugToggleInput();
        }

        ApplyDebugStateIfChanged();
    }

    public void SetDebugVisible(bool visible)
    {
        SetDebugVisible(visible, visible);
    }

    public void SetDebugVisible(bool showOverlay, bool showGizmos)
    {
        this.showDebugOverlay = showOverlay;
        this.showSceneViewGizmos = showGizmos;
        ApplyDebugState(true);
    }

    public void ToggleDebugVisible()
    {
        bool nextValue = !showDebugOverlay;
        SetDebugVisible(nextValue, nextValue);
    }

    private void HandleDebugToggleInput()
    {
        if (!enableDebugToggleKey || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            ToggleDebugVisible();
        }
    }

    private void ApplyDebugStateIfChanged()
    {
        if (!hasAppliedDebugState ||
            lastAppliedOverlay != showDebugOverlay ||
            lastAppliedSceneViewGizmos != showSceneViewGizmos ||
            lastAppliedUmbrellaDebug != showUmbrellaDebug ||
            lastAppliedWaterTargetDebug != showWaterTargetDebug ||
            lastAppliedWaterBasinDebug != showWaterBasinDebug ||
            lastAppliedWaterBasinDebugOverlayScope != waterBasinDebugOverlayScope ||
            lastAppliedWaterBasinDebugOverlayTarget != waterBasinDebugOverlayTarget ||
            lastAppliedWaterBasinAutoConnectionPreview != showWaterBasinAutoConnectionPreview ||
            !Mathf.Approximately(
                lastAppliedWaterBasinAutoConnectionSearchDistance,
                waterBasinAutoConnectionSearchDistance) ||
            lastAppliedWaterBasinSavedConnectionPreview != showWaterBasinSavedConnectionPreview ||
            lastAppliedWaterBasinConnectionPreviewScope != waterBasinConnectionPreviewScope ||
            lastAppliedWaterBasinConnectionPreviewTarget != waterBasinConnectionPreviewTarget ||
            lastAppliedPuzzleDebug != showPuzzleDebug ||
            lastAppliedAnimationDebug != showAnimationDebug)
        {
            ApplyDebugState(true);
        }
    }

    private void ApplyDebugState(bool force = false)
    {
        if (!force &&
            hasAppliedDebugState &&
            lastAppliedOverlay == showDebugOverlay &&
            lastAppliedSceneViewGizmos == showSceneViewGizmos &&
            lastAppliedUmbrellaDebug == showUmbrellaDebug &&
            lastAppliedWaterTargetDebug == showWaterTargetDebug &&
            lastAppliedWaterBasinDebug == showWaterBasinDebug &&
            lastAppliedWaterBasinDebugOverlayScope == waterBasinDebugOverlayScope &&
            lastAppliedWaterBasinDebugOverlayTarget == waterBasinDebugOverlayTarget &&
            lastAppliedWaterBasinAutoConnectionPreview == showWaterBasinAutoConnectionPreview &&
            Mathf.Approximately(
                lastAppliedWaterBasinAutoConnectionSearchDistance,
                waterBasinAutoConnectionSearchDistance) &&
            lastAppliedWaterBasinSavedConnectionPreview == showWaterBasinSavedConnectionPreview &&
            lastAppliedWaterBasinConnectionPreviewScope == waterBasinConnectionPreviewScope &&
            lastAppliedWaterBasinConnectionPreviewTarget == waterBasinConnectionPreviewTarget &&
            lastAppliedPuzzleDebug == showPuzzleDebug &&
            lastAppliedAnimationDebug == showAnimationDebug)
        {
            return;
        }

        if (autoFindUmbrellaControllers)
        {
            RefreshUmbrellaControllers();
        }

        waterBasinAutoConnectionSearchDistance = Mathf.Max(0.0f, waterBasinAutoConnectionSearchDistance);

        ApplyUmbrellaDebugState();
        ApplyAnimationDebugState();
        UmbrellaWaterTarget.SetDebugOverlayEnabled(showDebugOverlay && showWaterTargetDebug);
        WaterBasinTarget.SetDebugVisible(
            showDebugOverlay && showWaterBasinDebug,
            showSceneViewGizmos && showWaterBasinDebug);
        WaterBasinTarget.SetDebugOverlayFilter(waterBasinDebugOverlayScope, waterBasinDebugOverlayTarget);
        WaterBasinOverlayScope = waterBasinDebugOverlayScope;
        WaterBasinOverlayTarget = waterBasinDebugOverlayTarget;
        ShowWaterBasinAutoConnectionPreview =
            showSceneViewGizmos && showWaterBasinDebug && showWaterBasinAutoConnectionPreview;
        WaterBasinAutoConnectionSearchDistance = waterBasinAutoConnectionSearchDistance;
        ShowWaterBasinSavedConnectionPreview =
            showSceneViewGizmos && showWaterBasinDebug && showWaterBasinSavedConnectionPreview;
        WaterBasinPreviewScope = waterBasinConnectionPreviewScope;
        WaterBasinPreviewTarget = waterBasinConnectionPreviewTarget;
        PuzzleDebugOverlay.SetVisible(
            showDebugOverlay && showPuzzleDebug,
            showSceneViewGizmos && showPuzzleDebug);

        hasAppliedDebugState = true;
        lastAppliedOverlay = showDebugOverlay;
        lastAppliedSceneViewGizmos = showSceneViewGizmos;
        lastAppliedUmbrellaDebug = showUmbrellaDebug;
        lastAppliedWaterTargetDebug = showWaterTargetDebug;
        lastAppliedWaterBasinDebug = showWaterBasinDebug;
        lastAppliedWaterBasinDebugOverlayScope = waterBasinDebugOverlayScope;
        lastAppliedWaterBasinDebugOverlayTarget = waterBasinDebugOverlayTarget;
        lastAppliedWaterBasinAutoConnectionPreview = showWaterBasinAutoConnectionPreview;
        lastAppliedWaterBasinAutoConnectionSearchDistance = waterBasinAutoConnectionSearchDistance;
        lastAppliedWaterBasinSavedConnectionPreview = showWaterBasinSavedConnectionPreview;
        lastAppliedWaterBasinConnectionPreviewScope = waterBasinConnectionPreviewScope;
        lastAppliedWaterBasinConnectionPreviewTarget = waterBasinConnectionPreviewTarget;
        lastAppliedPuzzleDebug = showPuzzleDebug;
        lastAppliedAnimationDebug = showAnimationDebug;

#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    private void RefreshUmbrellaControllers()
    {
        umbrellaControllers = UnityEngine.Object.FindObjectsByType<PlayerUmbrellaController>(
            FindObjectsInactive.Include);

        // Animation Debug도 F3 전체 디버그 토글에 맞춰 함께 켜고 끌 수 있도록 자동 수집한다.
        // 실제 애니메이션 상태 계산이나 버튼 처리는 PlayerAnimationController에 남겨둔다.
        animationControllers = UnityEngine.Object.FindObjectsByType<PlayerAnimationController>(
            FindObjectsInactive.Include);
    }

    private void ApplyUmbrellaDebugState()
    {
        if (umbrellaControllers == null)
        {
            return;
        }

        for (int i = 0; i < umbrellaControllers.Length; i++)
        {
            PlayerUmbrellaController controller = umbrellaControllers[i];
            if (controller == null)
            {
                continue;
            }

            controller.SetDebugVisible(
                showDebugOverlay && showUmbrellaDebug,
                showSceneViewGizmos && showUmbrellaDebug);
        }
    }

    // 수집된 PlayerAnimationController들에게 Animation Debug 창 표시 여부만 전달한다.
    // 우산 디버그, 퍼즐 디버그처럼 각 시스템의 디버그 UI 소유권은 해당 컴포넌트에 둔다.
    private void ApplyAnimationDebugState()
    {
        if (animationControllers == null)
        {
            return;
        }

        for (int i = 0; i < animationControllers.Length; i++)
        {
            PlayerAnimationController controller = animationControllers[i];
            if (controller == null)
            {
                continue;
            }

            controller.SetDebugVisible(showDebugOverlay && showAnimationDebug);
        }
    }
}
