using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
public class GameDebugController : MonoBehaviour
{
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

    [Tooltip("우산 상태/F3 패널과 플레이어 우산 Gizmo를 표시한다.")]
    [FormerlySerializedAs("controlUmbrellaDebug")]
    [SerializeField] private bool showUmbrellaDebug = true;

    [Tooltip("물 타겟의 월드 라벨을 표시한다.")]
    [FormerlySerializedAs("controlWaterTargetDebug")]
    [SerializeField] private bool showWaterTargetDebug = true;

    [Tooltip("퍼즐 연결선과 퍼즐 상태 라벨을 표시한다.")]
    [FormerlySerializedAs("controlPuzzleDebug")]
    [SerializeField] private bool showPuzzleDebug = true;

    public bool ShowDebugOverlay => showDebugOverlay;
    public bool ShowSceneViewGizmos => showSceneViewGizmos;

    private bool hasAppliedDebugState;
    private bool lastAppliedOverlay;
    private bool lastAppliedSceneViewGizmos;
    private bool lastAppliedUmbrellaDebug;
    private bool lastAppliedWaterTargetDebug;
    private bool lastAppliedPuzzleDebug;

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
            lastAppliedPuzzleDebug != showPuzzleDebug)
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
            lastAppliedPuzzleDebug == showPuzzleDebug)
        {
            return;
        }

        if (autoFindUmbrellaControllers)
        {
            RefreshUmbrellaControllers();
        }

        ApplyUmbrellaDebugState();
        UmbrellaWaterTarget.SetDebugOverlayEnabled(showDebugOverlay && showWaterTargetDebug);
        PuzzleDebugOverlay.SetVisible(
            showDebugOverlay && showPuzzleDebug,
            showSceneViewGizmos && showPuzzleDebug);

        hasAppliedDebugState = true;
        lastAppliedOverlay = showDebugOverlay;
        lastAppliedSceneViewGizmos = showSceneViewGizmos;
        lastAppliedUmbrellaDebug = showUmbrellaDebug;
        lastAppliedWaterTargetDebug = showWaterTargetDebug;
        lastAppliedPuzzleDebug = showPuzzleDebug;
    }

    private void RefreshUmbrellaControllers()
    {
        umbrellaControllers = UnityEngine.Object.FindObjectsByType<PlayerUmbrellaController>(
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
}
