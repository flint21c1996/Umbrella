using UnityEngine;

// UmbrellaWaterTarget에 저장된 물의 양을 퍼즐 조건으로 바꿔주는 어댑터.
// UmbrellaWaterTarget 자체가 PuzzleConditionSource를 상속하지 않게 해서,
// "물을 받을 수 있는 대상"과 "퍼즐 조건"의 책임을 분리한다.
[DisallowMultipleComponent]
public class WaterAmountCondition : PuzzleConditionSource
{
    [Header("Water")]
    [Tooltip("저장된 물의 양으로 이 조건을 판단할 물 저장 대상.")]
    [SerializeField] private UmbrellaWaterTarget waterTarget;

    [Tooltip("켜두면 Water Target의 Required Water 값을 필요 물 양으로 사용한다.")]
    [SerializeField] private bool useTargetRequiredWater = true;

    [Tooltip("Use Target Required Water를 끈 경우 직접 사용할 필요 물 양.")]
    [SerializeField] private float requiredWater = 1.0f;

    [Header("Runtime")]
    [SerializeField] private bool satisfied;

    // ConditionGroup은 이 값을 통해 물 조건이 만족되었는지 읽는다.
    public override bool IsSatisfied => IsWaterRequirementMet();

    // 기본적으로는 WaterTarget의 Required Water를 사용하고,
    // 필요하면 이 컴포넌트에서 별도 요구량을 지정할 수 있다.
    private float RequiredWater
    {
        get
        {
            if (useTargetRequiredWater && waterTarget != null)
            {
                return waterTarget.requiredWater;
            }

            return requiredWater;
        }
    }

    private void Reset()
    {
        CacheWaterTarget();
    }

    // 활성화될 때 WaterTarget의 변경 이벤트를 구독하고 현재 상태를 한 번 반영한다.
    private void OnEnable()
    {
        CacheWaterTarget();
        SubscribeWaterTarget();
        RefreshSatisfiedState(false);
    }

    // 비활성화될 때 이벤트 구독을 해제한다.
    private void OnDisable()
    {
        UnsubscribeWaterTarget();
    }

    // Inspector 값이 바뀔 때 음수 요구량을 막고, 에디터 표시용 상태를 갱신한다.
    // 여기서는 NotifyChanged를 호출하지 않아 에디터 조작만으로 퍼즐 이벤트가 실행되지 않게 한다.
    private void OnValidate()
    {
        requiredWater = Mathf.Max(0.0f, requiredWater);
        CacheWaterTarget();
        RefreshSatisfiedState(false);
    }

    // 물이 들어오거나 초기화될 때만 조건을 다시 계산하도록 이벤트를 구독한다.
    private void SubscribeWaterTarget()
    {
        if (waterTarget == null)
        {
            return;
        }

        // 중복 구독을 막기 위해 먼저 제거한 뒤 다시 등록한다.
        waterTarget.WaterChanged -= OnWaterChanged;
        waterTarget.WaterChanged += OnWaterChanged;
        /*
        여기서 -= 후 +=를 하는 이유는 중복 구독을 막기 위해서다.

        +=는 이벤트에 함수를 등록한다는 뜻이다.
        그런데 같은 함수를 여러 번 등록하면 이벤트가 발생했을 때 그 함수도 여러 번 호출된다.

        그래서 먼저 -=로 혹시 이미 등록되어 있는 핸들러를 제거하고,
        그 다음 +=로 다시 등록한다.

        이렇게 하면 OnWaterChanged는 항상 한 번만 등록된다.
        */
    }

    // WaterTarget 교체/비활성화 시 이전 구독을 정리한다.
    private void UnsubscribeWaterTarget()
    {
        if (waterTarget == null)
        {
            return;
        }

        waterTarget.WaterChanged -= OnWaterChanged;
    }

    // 물 저장량이 바뀐 순간 ConditionGroup에 알릴지 판단한다.
    private void OnWaterChanged()
    {
        RefreshSatisfiedState(true);
    }

    // 만족 상태가 실제로 바뀐 경우에만 NotifyChanged를 호출한다.
    // 물이 계속 들어오는 동안 같은 상태를 반복 통지하지 않기 위함이다.
    private void RefreshSatisfiedState(bool notifyChanged)
    {
        bool nextSatisfied = IsWaterRequirementMet();
        if (satisfied == nextSatisfied)
        {
            return;
        }

        satisfied = nextSatisfied;

        if (notifyChanged)
        {
            NotifyChanged();
        }
    }

    // 현재 저장된 물이 요구량 이상인지 확인한다.
    private bool IsWaterRequirementMet()
    {
        return waterTarget != null && waterTarget.ReceivedWater >= RequiredWater;
    }

    // 같은 오브젝트에 UmbrellaWaterTarget이 붙어 있으면 자동으로 연결한다.
    private void CacheWaterTarget()
    {
        if (waterTarget == null)
        {
            waterTarget = GetComponent<UmbrellaWaterTarget>();
        }
    }
}
