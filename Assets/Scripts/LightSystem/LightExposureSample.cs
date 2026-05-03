using UnityEngine;

public readonly struct LightExposureSample
{
    public readonly LightExposureSource Source;
    public readonly Vector3 SourcePosition;
    public readonly Vector3 ReceiverPosition;
    public readonly Vector3 DirectionFromSource;
    public readonly float Distance;
    public readonly float Intensity;
    public readonly float DistanceFactor;
    public readonly float AngleFactor;
    public readonly float DeltaTime;

    public LightExposureSample(
        LightExposureSource source,
        Vector3 sourcePosition,
        Vector3 receiverPosition,
        Vector3 directionFromSource,
        float distance,
        float intensity,
        float distanceFactor,
        float angleFactor,
        float deltaTime)
    {
        Source = source;
        SourcePosition = sourcePosition;
        ReceiverPosition = receiverPosition;
        DirectionFromSource = directionFromSource;
        Distance = distance;
        Intensity = intensity;
        DistanceFactor = distanceFactor;
        AngleFactor = angleFactor;
        DeltaTime = deltaTime;
    }
}
