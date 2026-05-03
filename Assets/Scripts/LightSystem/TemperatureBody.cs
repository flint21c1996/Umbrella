using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TemperatureBody : MonoBehaviour, ILightReceivable
{
    [Header("Temperature")]
    [SerializeField] private float currentTemperature;
    [SerializeField] private float ambientTemperature;
    [SerializeField] private float minTemperature = -100f;
    [SerializeField] private float maxTemperature = 100f;

    [Header("Response")]
    [SerializeField] private float temperatureChangePerLightSecond = 8f;
    [SerializeField] private bool recoverToAmbient = true;
    [SerializeField] private float ambientRecoveryPerSecond = 1f;
    [SerializeField] private float notifyThreshold = 0.01f;

    [Header("Receiver")]
    [SerializeField] private Transform exposurePoint = null;
    [SerializeField] private Vector3 fallbackLocalOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Runtime")]
    [SerializeField] private float lastReceivedLightIntensity;
    [SerializeField] private float lastExposureTime = float.NegativeInfinity;

    public event Action TemperatureChanged;

    public float CurrentTemperature => currentTemperature;
    public float LastReceivedLightIntensity => lastReceivedLightIntensity;
    public Vector3 LightReceiverPosition => exposurePoint != null ? exposurePoint.position : transform.TransformPoint(fallbackLocalOffset);

    private void OnValidate()
    {
        if (maxTemperature < minTemperature)
        {
            maxTemperature = minTemperature;
        }

        currentTemperature = Mathf.Clamp(currentTemperature, minTemperature, maxTemperature);
        ambientTemperature = Mathf.Clamp(ambientTemperature, minTemperature, maxTemperature);
        ambientRecoveryPerSecond = Mathf.Max(0f, ambientRecoveryPerSecond);
        notifyThreshold = Mathf.Max(0f, notifyThreshold);
    }

    private void Update()
    {
        if (recoverToAmbient && ambientRecoveryPerSecond > 0f)
        {
            SetTemperature(Mathf.MoveTowards(currentTemperature, ambientTemperature, ambientRecoveryPerSecond * Time.deltaTime));
        }

        if (Time.time - lastExposureTime > 0.1f)
        {
            lastReceivedLightIntensity = 0f;
        }
    }

    public void ReceiveLight(LightExposureSample sample)
    {
        lastReceivedLightIntensity = Mathf.Max(lastReceivedLightIntensity, sample.Intensity);
        lastExposureTime = Time.time;

        float temperatureDelta = temperatureChangePerLightSecond * sample.Intensity * sample.DeltaTime;
        SetTemperature(currentTemperature + temperatureDelta);
    }

    public void SetTemperature(float value)
    {
        float nextTemperature = Mathf.Clamp(value, minTemperature, maxTemperature);
        if (Mathf.Abs(nextTemperature - currentTemperature) <= notifyThreshold)
        {
            currentTemperature = nextTemperature;
            return;
        }

        currentTemperature = nextTemperature;
        TemperatureChanged?.Invoke();
    }
}
