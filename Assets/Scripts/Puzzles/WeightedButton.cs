using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

[DisallowMultipleComponent]
public class WeightedButton : MonoBehaviour
{
    private const float ButtonLabelWidth = 170.0f;
    private const float ButtonLabelHeight = 64.0f;
    private const float EventLabelWidth = 220.0f;
    private const float EventLabelHeight = 24.0f;

    [Header("Weight")]
    [Tooltip("무게를 읽는 센서. 보통 버튼 아래쪽 Trigger Collider 오브젝트에 붙인다.")]
    [SerializeField] private WeightSensor sensor;

    [Tooltip("이 무게 이상이면 버튼이 눌린다.")]
    [SerializeField] private float pressWeight = 5.0f;

    [Tooltip("이 무게 이하로 내려가야 버튼이 풀린다. pressWeight보다 낮게 둬서 덜덜거림을 막는다.")]
    [SerializeField] private float releaseWeight = 4.5f;

    [Header("Motion")]
    [Tooltip("실제로 위아래로 움직일 버튼 메시나 발판.")]
    [SerializeField] private Transform buttonVisual;

    [Tooltip("버튼이 안 눌렸을 때 위치.")]
    [SerializeField] private Transform releasedPoint;

    [Tooltip("버튼이 눌렸을 때 위치.")]
    [SerializeField] private Transform pressedPoint;

    [Tooltip("버튼이 목표 위치까지 움직이는 속도.")]
    [SerializeField] private float moveSpeed = 1.5f;

    [Header("Events")]
    [Tooltip("버튼이 처음 눌리는 순간 호출된다. 문 열기, 플랫폼 이동 같은 반응을 연결한다.")]
    [SerializeField] private UnityEvent onPressed = new UnityEvent();

    [Tooltip("버튼이 다시 풀리는 순간 호출된다.")]
    [SerializeField] private UnityEvent onReleased = new UnityEvent();

    [Header("Debug")]
    [Tooltip("F3 퍼즐 디버그 선이 시작될 기준점. 비워두면 Button Visual의 위쪽 중앙을 사용한다.")]
    [SerializeField] private Transform debugAnchor;

    [Header("Debug Line")]
    [Tooltip("F3 연결 점선의 두께.")]
    [SerializeField] private float debugLineThickness = 1.0f;

    [Tooltip("F3 연결 점선의 투명도.")]
    [Range(0.05f, 1.0f)]
    [SerializeField] private float debugLineAlpha = 0.38f;

    [Tooltip("Game View에서 보이는 점 하나의 길이.")]
    [SerializeField] private float debugDashLength = 10.0f;

    [Tooltip("Game View에서 점과 점 사이의 간격.")]
    [SerializeField] private float debugGapLength = 9.0f;

    [Tooltip("점선이 연결 방향으로 흐르는 속도.")]
    [SerializeField] private float debugFlowSpeed = 38.0f;

    [SerializeField] private bool isPressed;
    [SerializeField] private float currentWeight;

    public bool IsPressed => isPressed;
    public float CurrentWeight => currentWeight;

    private void Reset()
    {
        sensor = GetComponentInChildren<WeightSensor>();
        buttonVisual = transform;
    }

    private void Awake()
    {
        if (sensor == null)
        {
            sensor = GetComponentInChildren<WeightSensor>();
        }

        if (buttonVisual == null)
        {
            buttonVisual = transform;
        }
    }

    private void OnValidate()
    {
        pressWeight = Mathf.Max(0.0f, pressWeight);
        releaseWeight = Mathf.Clamp(releaseWeight, 0.0f, pressWeight);
        moveSpeed = Mathf.Max(0.0f, moveSpeed);
        debugLineThickness = Mathf.Max(0.1f, debugLineThickness);
        debugDashLength = Mathf.Max(1.0f, debugDashLength);
        debugGapLength = Mathf.Max(1.0f, debugGapLength);
        debugFlowSpeed = Mathf.Max(0.0f, debugFlowSpeed);
    }

    private void FixedUpdate()
    {
        RefreshPressedState();
        MoveButtonVisual();
    }

    private void RefreshPressedState()
    {
        currentWeight = sensor != null ? sensor.CurrentWeight : 0.0f;

        if (!isPressed && currentWeight >= pressWeight)
        {
            SetPressed(true);
            return;
        }

        if (isPressed && currentWeight <= releaseWeight)
        {
            SetPressed(false);
        }
    }

    private void SetPressed(bool pressed)
    {
        if (isPressed == pressed)
        {
            return;
        }

        isPressed = pressed;

        if (isPressed)
        {
            onPressed.Invoke();
            return;
        }

        onReleased.Invoke();
    }

    private void MoveButtonVisual()
    {
        if (buttonVisual == null)
        {
            return;
        }

        Transform targetPoint = isPressed ? pressedPoint : releasedPoint;
        if (targetPoint == null)
        {
            return;
        }

        buttonVisual.position = Vector3.MoveTowards(
            buttonVisual.position,
            targetPoint.position,
            moveSpeed * Time.fixedDeltaTime);
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !PuzzleDebugOverlay.OverlayEnabled)
        {
            return;
        }

        Camera targetCamera = PuzzleDebugOverlay.GetCamera();
        if (targetCamera == null)
        {
            return;
        }

        Vector3 sourcePosition = GetDebugAnchorPosition();
        DrawButtonLabel(targetCamera, sourcePosition);
        DrawEventLinks(targetCamera, sourcePosition, onPressed, "Pressed", Color.green, 4.0f);
        DrawEventLinks(targetCamera, sourcePosition, onReleased, "Released", Color.cyan, -4.0f);
    }

    private void OnDrawGizmos()
    {
        if (!PuzzleDebugOverlay.GizmosEnabled)
        {
            return;
        }

        Vector3 sourcePosition = GetDebugAnchorPosition();
        DrawSceneAnchor(sourcePosition, isPressed ? Color.green : Color.white, 0.22f);
        DrawEventGizmos(sourcePosition, onPressed, Color.green);
        DrawEventGizmos(sourcePosition, onReleased, Color.cyan);
    }

    private void DrawButtonLabel(Camera targetCamera, Vector3 sourcePosition)
    {
        if (!PuzzleDebugOverlay.TryGetGuiPoint(targetCamera, sourcePosition, out Vector2 labelPoint))
        {
            return;
        }

        string stateText = isPressed ? "Pressed" : "Released";
        int linkCount = onPressed.GetPersistentEventCount() + onReleased.GetPersistentEventCount();
        string label = $"{name}\n{stateText}  {currentWeight:F1}/{pressWeight:F1}\nLinks: {linkCount}";
        PuzzleDebugOverlay.DrawLabel(labelPoint, label, ButtonLabelWidth, ButtonLabelHeight);
    }

    private void DrawEventLinks(
        Camera targetCamera,
        Vector3 sourcePosition,
        UnityEvent sourceEvent,
        string eventName,
        Color color,
        float screenOffset)
    {
        if (!TryGetButtonLineStart(targetCamera, sourcePosition, out Vector2 lineStart))
        {
            return;
        }

        int eventCount = sourceEvent.GetPersistentEventCount();
        for (int i = 0; i < eventCount; i++)
        {
            if (!TryGetEventTarget(sourceEvent, i, out Transform targetTransform, out string targetLabel))
            {
                continue;
            }

            Vector3 targetPosition = GetTargetAnchorPosition(targetTransform);
            Color lineColor = GetDebugLineColor(color);
            float dashOffset = GetScreenDashOffset(i);

            PuzzleDebugOverlay.DrawDashedGuiToWorldLine(
                targetCamera,
                lineStart,
                targetPosition,
                lineColor,
                debugLineThickness,
                debugDashLength,
                debugGapLength,
                screenOffset,
                dashOffset);

            DrawEventLabel(targetCamera, lineStart, targetPosition, $"{eventName} -> {targetLabel}", screenOffset);
        }
    }

    private void DrawEventLabel(
        Camera targetCamera,
        Vector2 lineStart,
        Vector3 targetPosition,
        string label,
        float screenOffset)
    {
        if (!PuzzleDebugOverlay.TryGetGuiPoint(targetCamera, targetPosition, out Vector2 targetPoint))
        {
            return;
        }

        Vector2 labelPoint = Vector2.Lerp(lineStart, targetPoint, 0.5f);
        labelPoint.y += screenOffset;
        PuzzleDebugOverlay.DrawLabel(labelPoint, label, EventLabelWidth, EventLabelHeight);
    }

    private bool TryGetButtonLineStart(Camera targetCamera, Vector3 sourcePosition, out Vector2 lineStart)
    {
        lineStart = Vector2.zero;

        if (!PuzzleDebugOverlay.TryGetGuiPoint(targetCamera, sourcePosition, out Vector2 labelBottomCenter))
        {
            return false;
        }

        // 버튼 라벨은 기준점 위로 그려지므로, 연결선은 라벨의 윗변에서 시작시킨다.
        lineStart = labelBottomCenter + Vector2.up * -ButtonLabelHeight;
        return true;
    }

    private void DrawEventGizmos(Vector3 sourcePosition, UnityEvent sourceEvent, Color color)
    {
        Color lineColor = GetSceneGizmoColor(color);

        int eventCount = sourceEvent.GetPersistentEventCount();
        for (int i = 0; i < eventCount; i++)
        {
            if (!TryGetEventTarget(sourceEvent, i, out Transform targetTransform, out _))
            {
                continue;
            }

            Vector3 targetPosition = GetTargetAnchorPosition(targetTransform);
            DrawSceneAnchor(targetPosition, lineColor, 0.18f);

            PuzzleDebugOverlay.DrawDashedGizmoLine(
                sourcePosition,
                targetPosition,
                lineColor,
                0.35f,
                0.2f,
                GetWorldDashOffset(i));
        }
    }

    private void DrawSceneAnchor(Vector3 position, Color color, float radius)
    {
        color.a = 0.95f;
        Gizmos.color = color;
        Gizmos.DrawSphere(position, radius * 0.35f);
        Gizmos.DrawWireSphere(position, radius);
    }

    private Color GetDebugLineColor(Color color)
    {
        color.a = debugLineAlpha;
        return color;
    }

    private Color GetSceneGizmoColor(Color color)
    {
        color.a = 0.95f;
        return color;
    }

    private float GetScreenDashOffset(int eventIndex)
    {
        float patternLength = debugDashLength + debugGapLength;
        return Time.unscaledTime * debugFlowSpeed + eventIndex * patternLength * 0.35f;
    }

    private float GetWorldDashOffset(int eventIndex)
    {
        return Time.unscaledTime * 0.75f + eventIndex * 0.12f;
    }

    private bool TryGetEventTarget(UnityEvent sourceEvent, int eventIndex, out Transform targetTransform, out string label)
    {
        targetTransform = null;
        label = string.Empty;

        Object target = sourceEvent.GetPersistentTarget(eventIndex);
        string methodName = sourceEvent.GetPersistentMethodName(eventIndex);

        if (target == null || string.IsNullOrEmpty(methodName))
        {
            return false;
        }

        targetTransform = GetTargetTransform(target);
        if (targetTransform == null)
        {
            return false;
        }

        label = $"{target.name}.{methodName}()";
        return true;
    }

    private Transform GetTargetTransform(Object target)
    {
        if (target is Component targetComponent)
        {
            return targetComponent.transform;
        }

        if (target is GameObject targetGameObject)
        {
            return targetGameObject.transform;
        }

        return null;
    }

    private Vector3 GetTargetAnchorPosition(Transform targetTransform)
    {
        PuzzleMover mover = targetTransform.GetComponent<PuzzleMover>();
        if (mover != null)
        {
            return mover.DebugAnchorPosition;
        }

        return targetTransform.position + Vector3.up * 0.5f;
    }

    private Vector3 GetDebugAnchorPosition()
    {
        if (debugAnchor != null)
        {
            return debugAnchor.position;
        }

        Transform anchor = buttonVisual != null ? buttonVisual : transform;
        Renderer targetRenderer = anchor.GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
        {
            Bounds bounds = targetRenderer.bounds;
            return bounds.center + Vector3.up * (bounds.extents.y + 0.25f);
        }

        Collider targetCollider = anchor.GetComponentInChildren<Collider>();
        if (targetCollider != null)
        {
            Bounds bounds = targetCollider.bounds;
            return bounds.center + Vector3.up * (bounds.extents.y + 0.25f);
        }

        return anchor.position + Vector3.up * 0.5f;
    }
}
