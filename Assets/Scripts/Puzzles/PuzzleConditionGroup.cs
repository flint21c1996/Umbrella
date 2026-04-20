using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

// 여러 PuzzleConditionSource를 하나로 묶어서 결과 이벤트를 실행하는 퍼즐 중간 관리자.
// 버튼 두 개를 눌러야 문이 열린다거나, 물 저장량과 발판 상태를 동시에 요구할 때 사용한다.
// 각 조건은 Changed 이벤트로만 갱신되므로 매 프레임 조건을 감시하지 않는다.
[DisallowMultipleComponent]
public class PuzzleConditionGroup : MonoBehaviour
{
    private const float LabelWidth = 180.0f;
    private const float LabelHeight = 58.0f;
    private const float EventLabelWidth = 240.0f;
    private const float EventLabelHeight = 24.0f;

    [Header("Conditions")]
    [Tooltip("이 목록의 모든 조건이 만족되어야 그룹이 활성화된다.")]
    [SerializeField] private PuzzleConditionSource[] conditionSources = new PuzzleConditionSource[0];

    [Header("Events")]
    [Tooltip("모든 조건이 만족되는 순간 한 번 호출된다.")]
    [SerializeField] private UnityEvent onSatisfied = new UnityEvent();

    [Tooltip("조건 중 하나라도 다시 불만족 상태가 되는 순간 한 번 호출된다.")]
    [SerializeField] private UnityEvent onUnsatisfied = new UnityEvent();

    [Header("Debug")]
    [Tooltip("F3 디버그 라벨과 연결선의 기준점. 비워두면 이 오브젝트 위치를 사용한다.")]
    [SerializeField] private Transform debugAnchor;

    [Tooltip("F3 디버그 연결선의 두께.")]
    [SerializeField] private float debugLineThickness = 1.0f;

    [Tooltip("F3 디버그 연결선의 투명도.")]
    [Range(0.05f, 1.0f)]
    [SerializeField] private float debugLineAlpha = 0.38f;

    [Header("Runtime")]
    [SerializeField] private bool satisfied;

    public bool IsSatisfied => satisfied;
    public int ConditionCount => conditionSources != null ? conditionSources.Length : 0;

    // 활성화될 때 조건 이벤트를 구독하고 현재 상태를 한 번 계산한다.
    // 씬 시작 시 이미 눌려 있는 버튼이나 물이 차 있는 상태도 반영하기 위함이다.
    private void OnEnable()
    {
        SubscribeConditions();
        RefreshSatisfiedState(true);
    }

    // 비활성화될 때 이벤트 구독을 해제해 중복 호출과 누수 가능성을 막는다.
    private void OnDisable()
    {
        UnsubscribeConditions();
    }

    // Inspector에서 값을 만질 때 디버그 설정을 보정하고 상태 표시 값을 갱신한다.
    // OnValidate에서는 이벤트를 호출하지 않아 에디터에서 의도치 않게 문이 움직이지 않게 한다.
    private void OnValidate()
    {
        debugLineThickness = Mathf.Max(0.1f, debugLineThickness);
        RefreshSatisfiedState(false);
    }

    // 각 조건의 Changed 이벤트에 반응하도록 등록한다.
    // 먼저 -= 한 뒤 += 하는 이유는 OnEnable이 여러 번 불려도 중복 구독되지 않게 하기 위해서다.
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

    // ConditionGroup이 꺼질 때 더 이상 조건 변화에 반응하지 않도록 정리한다.
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

    // 조건 하나가 바뀐 순간 전체 조건을 다시 계산한다.
    private void OnConditionChanged()
    {
        RefreshSatisfiedState(true);
    }

    // Inspector 이벤트나 다른 스크립트에서 강제로 상태를 다시 계산하고 싶을 때 쓰는 공개 함수.
    public void RefreshNow()
    {
        RefreshSatisfiedState(true);
    }

    // 모든 조건이 만족되었는지 계산하고, 상태가 바뀌었을 때만 이벤트를 호출한다.
    // 매번 Invoke하면 문/플랫폼이 같은 명령을 반복해서 받으므로 상태 변화 순간만 처리한다.
    private void RefreshSatisfiedState(bool invokeEvents)
    {
        bool nextSatisfied = AreAllConditionsSatisfied();
        if (satisfied == nextSatisfied)
        {
            return;
        }

        satisfied = nextSatisfied;

        // OnValidate처럼 상태 표시만 갱신해야 하는 상황에서는 결과 이벤트를 호출하지 않는다.
        if (!invokeEvents)
        {
            return;
        }

        // 모든 조건이 만족되는 순간에만 On Satisfied를 호출한다.
        if (satisfied)
        {
            onSatisfied.Invoke();
            return;
        }

        // 하나라도 조건이 풀리는 순간 On Unsatisfied를 호출한다.
        onUnsatisfied.Invoke();
    }

    // 등록된 조건이 모두 만족되었는지 확인한다.
    // 조건이 하나도 없으면 실수로 항상 성공하는 퍼즐이 되지 않게 false로 본다.
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
        // F3 디버그 오버레이가 켜져 있을 때만 Game View 라벨과 연결선을 그린다.
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
        // Scene View 기즈모는 별도 토글로 관리한다.
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

    // 현재 만족된 조건 수를 계산해 디버그 라벨에 1/2, 2/2처럼 보여준다.
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
        // 조건 소스에서 ConditionGroup으로 향하는 선을 그린다.
        // 노란색은 아직 대기 중, 초록색은 만족된 조건이다.
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
        // 이벤트 연결선은 그룹 라벨의 윗변에서 결과 오브젝트로 이어지게 한다.
        // 라벨 중앙을 지나가면 글자를 읽기 어렵기 때문이다.
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
        // 선 중간에 어떤 이벤트가 어떤 메서드를 호출하는지 표시한다.
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
        // Scene View에서는 OnGUI 대신 Gizmos.DrawLine을 사용해 조건 연결을 확인한다.
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
        // UnityEvent에 연결된 대상들을 Scene View에서 확인하기 위한 선을 그린다.
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

    // 선 끝점을 눈으로 찾기 쉽게 작은 구체와 와이어 구체를 같이 그린다.
    private void DrawSceneAnchor(Vector3 position, Color color, float radius)
    {
        color.a = 0.95f;
        Gizmos.color = color;
        Gizmos.DrawSphere(position, radius * 0.35f);
        Gizmos.DrawWireSphere(position, radius);
    }

    // Game View 연결선은 알파를 낮춰 플레이 화면을 덜 가리게 한다.
    private Color GetDebugLineColor(Color color)
    {
        color.a = debugLineAlpha;
        return color;
    }

    // Scene View 기즈모는 조금 더 선명하게 보여도 되므로 높은 알파를 사용한다.
    private Color GetSceneGizmoColor(Color color)
    {
        color.a = 0.95f;
        return color;
    }

    // UnityEvent의 Persistent Call에서 대상 Transform과 표시용 라벨을 뽑는다.
    // Inspector에 연결한 결과가 디버그 오버레이에 보이게 하는 핵심 함수다.
    private bool TryGetEventTarget(UnityEvent sourceEvent, int eventIndex, out Transform targetTransform, out string label)
    {
        targetTransform = null;
        label = string.Empty;

        Object target = sourceEvent.GetPersistentTarget(eventIndex);
        string methodName = sourceEvent.GetPersistentMethodName(eventIndex);

        // 대상이나 메서드가 비어 있으면 아직 연결되지 않은 이벤트 슬롯으로 본다.
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

    // UnityEvent 대상이 Component인지 GameObject인지에 따라 Transform을 찾아낸다.
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

    // 연결선의 끝점으로 쓸 위치를 계산한다.
    // 대상 컴포넌트가 자기 디버그 앵커를 제공하면 그 위치를 우선 사용한다.
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

    // ConditionGroup 라벨과 선이 시작될 위치를 계산한다.
    // 별도 앵커가 없으면 Renderer/Collider의 위쪽을 사용해 오브젝트를 덜 가리게 한다.
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
