using UnityEngine;

public enum TemperatureConditionMode
{
    AtLeast,
    AtMost,
    Between
}

[DisallowMultipleComponent]
public sealed class TemperatureCondition : PuzzleConditionSource
{
    [Header("Temperature")]
    [SerializeField] private TemperatureBody temperatureBody;
    [SerializeField] private TemperatureConditionMode mode = TemperatureConditionMode.AtLeast;
    [SerializeField] private float requiredTemperature = 20f;
    [SerializeField] private float minimumTemperature = 0f;
    [SerializeField] private float maximumTemperature = 30f;

    [Header("Runtime")]
    [SerializeField] private bool satisfied;

    public override bool IsSatisfied => IsRequirementMet();

    private void Reset()
    {
        CacheTemperatureBody();
    }

    private void OnEnable()
    {
        CacheTemperatureBody();
        SubscribeTemperatureBody();
        RefreshSatisfiedState(false);
    }

    private void OnDisable()
    {
        UnsubscribeTemperatureBody();
    }

    private void OnValidate()
    {
        if (maximumTemperature < minimumTemperature)
        {
            maximumTemperature = minimumTemperature;
        }

        CacheTemperatureBody();
        RefreshSatisfiedState(false);
    }

    private void SubscribeTemperatureBody()
    {
        if (temperatureBody == null)
        {
            return;
        }

        temperatureBody.TemperatureChanged -= OnTemperatureChanged;
        temperatureBody.TemperatureChanged += OnTemperatureChanged;
    }

    private void UnsubscribeTemperatureBody()
    {
        if (temperatureBody == null)
        {
            return;
        }

        temperatureBody.TemperatureChanged -= OnTemperatureChanged;
    }

    private void OnTemperatureChanged()
    {
        RefreshSatisfiedState(true);
    }

    private void RefreshSatisfiedState(bool notifyChanged)
    {
        bool nextSatisfied = IsRequirementMet();
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

    private bool IsRequirementMet()
    {
        if (temperatureBody == null)
        {
            return false;
        }

        float temperature = temperatureBody.CurrentTemperature;
        switch (mode)
        {
            case TemperatureConditionMode.AtMost:
                return temperature <= requiredTemperature;
            case TemperatureConditionMode.Between:
                return temperature >= minimumTemperature && temperature <= maximumTemperature;
            default:
                return temperature >= requiredTemperature;
        }
    }

    private void CacheTemperatureBody()
    {
        if (temperatureBody == null)
        {
            temperatureBody = GetComponent<TemperatureBody>();
        }
    }
}
