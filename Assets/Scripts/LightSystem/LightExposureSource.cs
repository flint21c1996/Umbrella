using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightExposureSource : MonoBehaviour
{
    private const int MaxReceiverColliders = 96;
    private const int MaxOcclusionHits = 32;
    private static readonly IComparer<RaycastHit> RaycastHitDistanceComparer = Comparer<RaycastHit>.Create((left, right) => left.distance.CompareTo(right.distance));

    [Header("Shape")]
    [SerializeField] private float maxDistance = 8f;
    [SerializeField, Range(1f, 179f)] private float coneAngle = 45f;
    [SerializeField, Range(0f, 1f)] private float innerConeRatio = 0.55f;

    [Header("Exposure")]
    [SerializeField] private float intensity = 1f;
    [SerializeField] private bool useDistanceFalloff = true;
    [SerializeField] private bool useAngleFalloff = true;
    [SerializeField] private float sampleInterval;

    [Header("Collision")]
    [SerializeField] private LayerMask receiverMask = ~0;
    [SerializeField] private QueryTriggerInteraction receiverTriggerInteraction = QueryTriggerInteraction.Collide;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask occlusionMask = ~0;
    [SerializeField] private QueryTriggerInteraction occlusionTriggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private bool ignoreOwnColliders = true;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;
    [SerializeField] private int lastReceiverCount;
    [SerializeField] private int lastLitCount;
    [SerializeField] private int lastBlockedCount;

    private readonly Collider[] receiverResults = new Collider[MaxReceiverColliders];
    private readonly RaycastHit[] occlusionResults = new RaycastHit[MaxOcclusionHits];
    private readonly HashSet<MonoBehaviour> processedReceivers = new HashSet<MonoBehaviour>();
    private float pendingDeltaTime;

    public float MaxDistance => maxDistance;
    public float ConeAngle => coneAngle;
    public float Intensity => intensity;

    private void OnValidate()
    {
        maxDistance = Mathf.Max(0.1f, maxDistance);
        coneAngle = Mathf.Clamp(coneAngle, 1f, 179f);
        innerConeRatio = Mathf.Clamp01(innerConeRatio);
        intensity = Mathf.Max(0f, intensity);
        sampleInterval = Mathf.Max(0f, sampleInterval);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        pendingDeltaTime += deltaTime;
        if (sampleInterval > 0f && pendingDeltaTime < sampleInterval)
        {
            return;
        }

        Emit(pendingDeltaTime);
        pendingDeltaTime = 0f;
    }

    public void Configure(float newMaxDistance, float newConeAngle, float newIntensity)
    {
        maxDistance = Mathf.Max(0.1f, newMaxDistance);
        coneAngle = Mathf.Clamp(newConeAngle, 1f, 179f);
        intensity = Mathf.Max(0f, newIntensity);
    }

    public void SetMasks(LayerMask newReceiverMask, LayerMask newOcclusionMask)
    {
        receiverMask = newReceiverMask;
        occlusionMask = newOcclusionMask;
    }

    public void Emit(float deltaTime)
    {
        processedReceivers.Clear();
        lastReceiverCount = 0;
        lastLitCount = 0;
        lastBlockedCount = 0;

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            maxDistance,
            receiverResults,
            receiverMask,
            receiverTriggerInteraction);

        for (int i = 0; i < hitCount; i++)
        {
            Collider receiverCollider = receiverResults[i];
            if (receiverCollider == null)
            {
                continue;
            }

            MonoBehaviour[] behaviours = receiverCollider.GetComponentsInParent<MonoBehaviour>();
            for (int j = 0; j < behaviours.Length; j++)
            {
                MonoBehaviour behaviour = behaviours[j];
                if (behaviour == null || processedReceivers.Contains(behaviour))
                {
                    continue;
                }

                ILightReceivable receiver = behaviour as ILightReceivable;
                if (receiver == null)
                {
                    continue;
                }

                processedReceivers.Add(behaviour);
                lastReceiverCount++;

                if (TryBuildSample(receiver, behaviour, deltaTime, out LightExposureSample sample))
                {
                    receiver.ReceiveLight(sample);
                    lastLitCount++;
                }
            }
        }
    }

    private bool TryBuildSample(ILightReceivable receiver, Component receiverComponent, float deltaTime, out LightExposureSample sample)
    {
        sample = default;

        Vector3 sourcePosition = transform.position;
        Vector3 receiverPosition = receiver.LightReceiverPosition;
        Vector3 toReceiver = receiverPosition - sourcePosition;
        float distance = toReceiver.magnitude;
        if (distance <= 0.001f || distance > maxDistance)
        {
            return false;
        }

        Vector3 direction = toReceiver / distance;
        float halfConeAngle = coneAngle * 0.5f;
        float angle = Vector3.Angle(transform.forward, direction);
        if (angle > halfConeAngle)
        {
            return false;
        }

        if (requireLineOfSight && IsOccluded(sourcePosition, direction, distance, receiverComponent))
        {
            lastBlockedCount++;
            return false;
        }

        float distanceFactor = useDistanceFalloff ? 1f - Mathf.Clamp01(distance / maxDistance) : 1f;
        float innerHalfAngle = halfConeAngle * innerConeRatio;
        float angleFactor = useAngleFalloff ? 1f - Mathf.InverseLerp(innerHalfAngle, halfConeAngle, angle) : 1f;
        float finalIntensity = intensity * Mathf.Clamp01(distanceFactor) * Mathf.Clamp01(angleFactor);
        if (finalIntensity <= 0f)
        {
            return false;
        }

        sample = new LightExposureSample(
            this,
            sourcePosition,
            receiverPosition,
            direction,
            distance,
            finalIntensity,
            distanceFactor,
            angleFactor,
            deltaTime);

        return true;
    }

    private bool IsOccluded(Vector3 sourcePosition, Vector3 direction, float distance, Component receiverComponent)
    {
        int hitCount = Physics.RaycastNonAlloc(
            sourcePosition,
            direction,
            occlusionResults,
            Mathf.Max(0f, distance - 0.02f),
            occlusionMask,
            occlusionTriggerInteraction);

        if (hitCount == 0)
        {
            return false;
        }

        Array.Sort(occlusionResults, 0, hitCount, RaycastHitDistanceComparer);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = occlusionResults[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            if (ignoreOwnColliders && hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (receiverComponent != null && hitCollider.transform.IsChildOf(receiverComponent.transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, maxDistance);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * maxDistance);
    }
}
