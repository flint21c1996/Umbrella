using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

[DisallowMultipleComponent]
public class PuzzleConditionGroup : MonoBehaviour
{
    private const float LabelWidth = 180.0f;
    private const float LabelHeight = 58.0f;
    private const float EventLabelWidth = 240.0f;
    private const float EventLabelHeight = 24.0f;

    [Header("Conditions")]
    [Tooltip("Every condition in this list must be satisfied before this group activates.")]
    [SerializeField] private PuzzleConditionSource[] conditionSources = new PuzzleConditionSource[0];

    [Header("Events")]
    [Tooltip("Called once when every condition becomes satisfied.")]
    [SerializeField] private UnityEvent onSatisfied = new UnityEvent();

    [Tooltip("Called once when at least one condition becomes unsatisfied again.")]
    [SerializeField] private UnityEvent onUnsatisfied = new UnityEvent();

    [Header("Debug")]
    [Tooltip("Optional F3 debug label/link anchor. If empty, this object's position is used.")]
    [SerializeField] private Transform debugAnchor;

    [Tooltip("F3 debug link thickness.")]
    [SerializeField] private float debugLineThickness = 1.0f;

    [Tooltip("F3 debug link alpha.")]
    [Range(0.05f, 1.0f)]
    [SerializeField] private float debugLineAlpha = 0.38f;

    [Header("Runtime")]
    [SerializeField] private bool satisfied;

    public bool IsSatisfied => satisfied;
    public int ConditionCount => conditionSources != null ? conditionSources.Length : 0;

    private void OnEnable()
    {
        SubscribeConditions();
        RefreshSatisfiedState(true);
    }

    private void OnDisable()
    {
        UnsubscribeConditions();
    }

    private void OnValidate()
    {
        debugLineThickness = Mathf.Max(0.1f, debugLineThickness);
        RefreshSatisfiedState(false);
    }

    private void SubscribeConditions()
    {
        if (conditionSources == null)
        {
            return;
        }

        for (int i = 0; i < conditionSources.Length; i++)
        {
            PuzzleConditionSource condition = conditionSources[i];
            if (condition == null)
            {
                continue;
            }

            condition.Changed -= OnConditionChanged;
            condition.Changed += OnConditionChanged;
        }
    }

    private void UnsubscribeConditions()
    {
        if (conditionSources == null)
        {
            return;
        }

        for (int i = 0; i < conditionSources.Length; i++)
        {
            PuzzleConditionSource condition = conditionSources[i];
            if (condition == null)
            {
                continue;
            }

            condition.Changed -= OnConditionChanged;
        }
    }

    private void OnConditionChanged()
    {
        RefreshSatisfiedState(true);
    }

    public void RefreshNow()
    {
        RefreshSatisfiedState(true);
    }

    private void RefreshSatisfiedState(bool invokeEvents)
    {
        bool nextSatisfied = AreAllConditionsSatisfied();
        if (satisfied == nextSatisfied)
        {
            return;
        }

        satisfied = nextSatisfied;

        if (!invokeEvents)
        {
            return;
        }

        if (satisfied)
        {
            onSatisfied.Invoke();
            return;
        }

        onUnsatisfied.Invoke();
    }

    private bool AreAllConditionsSatisfied()
    {
        if (conditionSources == null || conditionSources.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < conditionSources.Length; i++)
        {
            PuzzleConditionSource condition = conditionSources[i];
            if (condition == null || !condition.IsSatisfied)
            {
                return false;
            }
        }

        return true;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !PuzzleDebugOverlay.OverlayEnabled)
        {
            return;
        }

        Camera targetCamera = PuzzleDebugOverlay.GetCamera();
        Vector3 sourcePosition = GetDebugAnchorPosition();
        if (!PuzzleDebugOverlay.TryGetGuiPoint(targetCamera, sourcePosition, out Vector2 labelPoint))
        {
            return;
        }

        int satisfiedCount = GetSatisfiedCount();
        string stateText = satisfied ? "Satisfied" : "Waiting";
        PuzzleDebugOverlay.DrawLabel(
            labelPoint,
            $"{name}\n{stateText}  {satisfiedCount}/{ConditionCount}",
            LabelWidth,
            LabelHeight);

        DrawConditionLinks(targetCamera, sourcePosition);
        DrawEventLinks(targetCamera, sourcePosition, onSatisfied, "Satisfied", Color.green, 4.0f);
        DrawEventLinks(targetCamera, sourcePosition, onUnsatisfied, "Unsatisfied", Color.cyan, -4.0f);
    }

    private void OnDrawGizmos()
    {
        if (!PuzzleDebugOverlay.GizmosEnabled)
        {
            return;
        }

        Vector3 sourcePosition = GetDebugAnchorPosition();
        DrawSceneAnchor(sourcePosition, satisfied ? Color.green : Color.white, 0.22f);
        DrawConditionGizmos(sourcePosition);
        DrawEventGizmos(sourcePosition, onSatisfied, Color.green);
        DrawEventGizmos(sourcePosition, onUnsatisfied, Color.cyan);
    }

    private int GetSatisfiedCount()
    {
        if (conditionSources == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < conditionSources.Length; i++)
        {
            if (conditionSources[i] != null && conditionSources[i].IsSatisfied)
            {
                count++;
            }
        }

        return count;
    }

    private void DrawConditionLinks(Camera targetCamera, Vector3 sourcePosition)
    {
        if (conditionSources == null)
        {
            return;
        }

        for (int i = 0; i < conditionSources.Length; i++)
        {
            PuzzleConditionSource condition = conditionSources[i];
            if (condition == null)
            {
                continue;
            }

            Vector3 conditionPosition = GetTargetAnchorPosition(condition.transform);
            Color color = condition.IsSatisfied ? Color.green : Color.yellow;
            color.a = debugLineAlpha;

            PuzzleDebugOverlay.DrawWorldLine(
                targetCamera,
                conditionPosition,
                sourcePosition,
                color,
                debugLineThickness);
        }
    }

    private void DrawEventLinks(
        Camera targetCamera,
        Vector3 sourcePosition,
        UnityEvent sourceEvent,
        string eventName,
        Color color,
        float screenOffset)
    {
        if (!PuzzleDebugOverlay.TryGetGuiPoint(targetCamera, sourcePosition, out Vector2 lineStart))
        {
            return;
        }

        lineStart += Vector2.up * -LabelHeight;

        int eventCount = sourceEvent.GetPersistentEventCount();
        for (int i = 0; i < eventCount; i++)
        {
            if (!TryGetEventTarget(sourceEvent, i, out Transform targetTransform, out string targetLabel))
            {
                continue;
            }

            Vector3 targetPosition = GetTargetAnchorPosition(targetTransform);
            Color lineColor = GetDebugLineColor(color);

            PuzzleDebugOverlay.DrawGuiToWorldLine(
                targetCamera,
                lineStart,
                targetPosition,
                lineColor,
                debugLineThickness,
                screenOffset);

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

    private void DrawConditionGizmos(Vector3 sourcePosition)
    {
        if (conditionSources == null)
        {
            return;
        }

        for (int i = 0; i < conditionSources.Length; i++)
        {
            PuzzleConditionSource condition = conditionSources[i];
            if (condition == null)
            {
                continue;
            }

            Color color = condition.IsSatisfied ? Color.green : Color.yellow;
            color.a = 0.95f;

            Vector3 conditionPosition = GetTargetAnchorPosition(condition.transform);
            DrawSceneAnchor(conditionPosition, color, 0.18f);

            Gizmos.color = color;
            Gizmos.DrawLine(conditionPosition, sourcePosition);
        }
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

            Gizmos.color = lineColor;
            Gizmos.DrawLine(sourcePosition, targetPosition);
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
        WeightedButton button = targetTransform.GetComponent<WeightedButton>();
        if (button != null)
        {
            return button.DebugAnchorPosition;
        }

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

        Renderer targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
        {
            Bounds bounds = targetRenderer.bounds;
            return bounds.center + Vector3.up * (bounds.extents.y + 0.25f);
        }

        Collider targetCollider = GetComponentInChildren<Collider>();
        if (targetCollider != null)
        {
            Bounds bounds = targetCollider.bounds;
            return bounds.center + Vector3.up * (bounds.extents.y + 0.25f);
        }

        return transform.position + Vector3.up * 0.5f;
    }
}
