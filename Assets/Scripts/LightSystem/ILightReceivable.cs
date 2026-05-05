using UnityEngine;

public interface ILightReceivable
{
    Vector3 LightReceiverPosition { get; }
    void ReceiveLight(LightExposureSample sample);
}
