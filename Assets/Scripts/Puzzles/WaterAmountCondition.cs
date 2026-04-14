using UnityEngine;

[DisallowMultipleComponent]
public class WaterAmountCondition : PuzzleConditionSource
{
    [Header("Water")]
    [Tooltip("Water target whose stored water decides this condition.")]
    [SerializeField] private UmbrellaWaterTarget waterTarget;

    [Tooltip("If empty, this condition uses the target's Required Water value.")]
    [SerializeField] private bool useTargetRequiredWater = true;

    [Tooltip("Water amount required when Use Target Required Water is off.")]
    [SerializeField] private float requiredWater = 1.0f;

    [Header("Runtime")]
    [SerializeField] private bool satisfied;

    public override bool IsSatisfied => IsWaterRequirementMet();

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

    private void OnEnable()
    {
        CacheWaterTarget();
        SubscribeWaterTarget();
        RefreshSatisfiedState(false);
    }

    private void OnDisable()
    {
        UnsubscribeWaterTarget();
    }

    private void OnValidate()
    {
        requiredWater = Mathf.Max(0.0f, requiredWater);
        CacheWaterTarget();
        RefreshSatisfiedState(false);
    }

    private void SubscribeWaterTarget()
    {
        if (waterTarget == null)
        {
            return;
        }

        waterTarget.WaterChanged -= OnWaterChanged;
        waterTarget.WaterChanged += OnWaterChanged;
    }

    private void UnsubscribeWaterTarget()
    {
        if (waterTarget == null)
        {
            return;
        }

        waterTarget.WaterChanged -= OnWaterChanged;
    }

    private void OnWaterChanged()
    {
        RefreshSatisfiedState(true);
    }

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

    private bool IsWaterRequirementMet()
    {
        return waterTarget != null && waterTarget.ReceivedWater >= RequiredWater;
    }

    private void CacheWaterTarget()
    {
        if (waterTarget == null)
        {
            waterTarget = GetComponent<UmbrellaWaterTarget>();
        }
    }
}
