using UnityEngine;
using UnityEngine.Serialization;

// 두 무게 소스의 값을 비교해서 양팔저울 퍼즐 조건으로 사용하는 컴포넌트.
// 양쪽 무게 차이가 허용 오차 안에 들어오면 PuzzleConditionGroup에 만족 상태를 알려준다.
[DisallowMultipleComponent]
public class BalanceScaleCondition : PuzzleConditionSource
{
    private const float LabelWidth = 190.0f;
    private const float LabelHeight = 78.0f;

    [Header("Weight Sources")]
    [Tooltip("왼쪽 접시의 무게 소스. IPuzzleWeightSource를 구현한 컴포넌트를 넣어야 한다.")]
    [FormerlySerializedAs("leftSensor")]
    [SerializeField] private MonoBehaviour leftWeightSource;

    [Tooltip("오른쪽 접시의 무게 소스. IPuzzleWeightSource를 구현한 컴포넌트를 넣어야 한다.")]
    [FormerlySerializedAs("rightSensor")]
    [SerializeField] private MonoBehaviour rightWeightSource;

    [Header("Balance")]
    [Tooltip("양쪽 접시가 균형으로 인정될 수 있는 최대 무게 차이.")]
    [SerializeField] private float allowedDifference = 0.25f;

    [Tooltip("양쪽이 모두 0kg일 때 실수로 균형 성공 처리되는 것을 막기 위한 최소 총 무게.")]
    [SerializeField] private float minimumTotalWeight = 0.1f;

    [Header("Debug")]
    [Tooltip("F3 디버그 라벨이 표시될 기준점. 비워두면 이 오브젝트 위치를 사용한다.")]
    [SerializeField] private Transform debugAnchor;

    [Header("Runtime")]
    [SerializeField] private bool isBalanced;
    [SerializeField] private float leftWeight;
    [SerializeField] private float rightWeight;
    [SerializeField] private float weightDifference;

    public override bool IsSatisfied => isBalanced;
    public float LeftWeight => leftWeight;
    public float RightWeight => rightWeight;
    public float WeightDifference => weightDifference;

    // Play가 시작되거나 컴포넌트가 다시 켜졌을 때 현재 배치 기준으로 조건 상태를 한 번 맞춘다.
    private void OnEnable()
    {
        RefreshBalanceState(true);
    }

    // Inspector에서 값을 바꿨을 때 음수 입력을 막고, 에디터 표시용 런타임 값을 바로 갱신한다.
    // 에디터에서 이벤트가 실행되면 의도치 않게 문/플랫폼이 움직일 수 있으므로 NotifyChanged는 보내지 않는다.
    private void OnValidate()
    {
        allowedDifference = Mathf.Max(0.0f, allowedDifference);
        minimumTotalWeight = Mathf.Max(0.0f, minimumTotalWeight);
        RefreshBalanceState(false);
    }

    // 무게 소스 대부분이 Rigidbody/Trigger 기반이라 물리 주기에서 비교한다.
    // 이렇게 해야 WeightSensor가 갱신한 값과 저울 판정이 같은 타이밍에 맞는다.
    private void FixedUpdate()
    {
        RefreshBalanceState(true);
    }

    // 두 센서의 현재 무게를 읽고 저울 조건 만족 여부를 갱신한다.
    // WeightSensor가 FixedUpdate에서 무게를 갱신하므로 이 조건도 같은 물리 주기에서 비교한다.
    private void RefreshBalanceState(bool notifyChanged)
    {
        leftWeight = GetCurrentWeight(leftWeightSource);
        rightWeight = GetCurrentWeight(rightWeightSource);
        weightDifference = Mathf.Abs(leftWeight - rightWeight);

        float totalWeight = leftWeight + rightWeight;
        bool hasEnoughWeight = totalWeight >= minimumTotalWeight;
        bool nextBalanced = hasEnoughWeight && weightDifference <= allowedDifference;

        // 상태가 그대로라면 조건 그룹에 같은 이벤트를 반복해서 보내지 않는다.
        if (isBalanced == nextBalanced)
        {
            return;
        }

        isBalanced = nextBalanced;

        if (notifyChanged)
        {
            NotifyChanged();
        }
    }

    private void OnGUI()
    {
        // F3 디버그 오버레이가 켜진 Play 모드에서만 양팔저울 상태를 표시한다.
        if (!Application.isPlaying || !PuzzleDebugOverlay.OverlayEnabled)
        {
            return;
        }

        Camera targetCamera = PuzzleDebugOverlay.GetCamera();
        if (targetCamera == null)
        {
            return;
        }

        if (!PuzzleDebugOverlay.TryGetGuiPoint(targetCamera, GetDebugAnchorPosition(), out Vector2 labelPoint))
        {
            return;
        }

        string stateText = isBalanced ? "Balanced" : "Unbalanced";
        PuzzleDebugOverlay.DrawLabel(
            labelPoint,
            $"{name}\n{stateText}\nL {leftWeight:F1} / R {rightWeight:F1}\nDiff {weightDifference:F2} <= {allowedDifference:F2}",
            LabelWidth,
            LabelHeight);
    }

    private void OnDrawGizmos()
    {
        // Scene View의 Gizmos가 켜져 있을 때만 무게 소스 연결선을 그린다.
        if (!PuzzleDebugOverlay.GizmosEnabled)
        {
            return;
        }

        Vector3 anchorPosition = GetDebugAnchorPosition();
        Gizmos.color = isBalanced ? Color.green : Color.yellow;
        Gizmos.DrawSphere(anchorPosition, 0.06f);
        Gizmos.DrawWireSphere(anchorPosition, 0.22f);

        DrawWeightSourceLink(leftWeightSource, anchorPosition);
        DrawWeightSourceLink(rightWeightSource, anchorPosition);
    }

    // Scene View에서 각 무게 소스가 어떤 양팔저울 조건과 연결되는지 선으로 보여준다.
    private void DrawWeightSourceLink(MonoBehaviour source, Vector3 anchorPosition)
    {
        if (source == null)
        {
            return;
        }

        Gizmos.color = isBalanced ? Color.green : Color.yellow;
        Gizmos.DrawLine(source.transform.position, anchorPosition);
    }

    private float GetCurrentWeight(MonoBehaviour source)
    {
        // Inspector에는 MonoBehaviour로 넣지만, 실제로는 IPuzzleWeightSource를 구현한 컴포넌트만 유효하다.
        // 잘못된 컴포넌트를 넣어도 게임이 터지지 않도록 0kg으로 취급한다.
        if (source is not IPuzzleWeightSource weightSource)
        {
            return 0.0f;
        }

        // 퍼즐 무게는 음수로 내려가면 조건 판정이 헷갈리므로 최종적으로 0 이상만 사용한다.
        return Mathf.Max(0.0f, weightSource.CurrentWeight);
    }

    private Vector3 GetDebugAnchorPosition()
    {
        // 별도 앵커가 있으면 기획자가 보기 좋은 위치를 직접 지정한 것으로 본다.
        if (debugAnchor != null)
        {
            return debugAnchor.position;
        }

        // 앵커가 없을 때는 조건 오브젝트보다 살짝 위에 라벨을 띄운다.
        return transform.position + Vector3.up * 0.5f;
    }
}
