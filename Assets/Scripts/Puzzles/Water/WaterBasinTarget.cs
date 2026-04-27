using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WaterBasinTarget : MonoBehaviour
{
    private const float Epsilon = 0.0001f;

    [Header("Umbrella Adapter")]
    [SerializeField] private UmbrellaWaterTarget umbrellaWaterTarget;
    [SerializeField] private bool findUmbrellaTargetInParent = true;

    [Tooltip("When UmbrellaWaterTarget receives water directly, copy that added amount into this basin group.")]
    [SerializeField] private bool useUmbrellaTargetAsInput = true;

    [Header("Connections")]
    [Tooltip("Targets connected to this target. Connections are treated as bidirectional while solving.")]
    [SerializeField] private List<WaterBasinTarget> connectedTargets = new List<WaterBasinTarget>();

    [Header("Volume")]
    [Tooltip("Horizontal area used by the connected-vessel solver. Larger area needs more volume for the same height.")]
    [SerializeField] private float surfaceArea = 1.0f;

    [Tooltip("Maximum visible/storable water height for this target.")]
    [SerializeField] private float maxWaterHeight = 1.0f;

    [SerializeField] private float initialVolume;

    [Header("Height")]
    [SerializeField] private bool useTransformYAsBottom = true;
    [SerializeField] private float manualBottomWorldY;

    [Header("Runtime")]
    [SerializeField] private float currentVolume;
    [SerializeField] private float waterSurfaceWorldY;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool showGameViewDebug = true;
    [SerializeField] private bool showGroupDebug = true;
    [SerializeField] private Vector3 labelOffset = new Vector3(0.0f, 1.25f, 0.0f);
    [SerializeField] private Vector2 labelSize = new Vector2(220.0f, 112.0f);
    [SerializeField] private Color debugColor = new Color(0.1f, 0.45f, 1.0f, 0.35f);

    private float observedUmbrellaWater;
    private bool isApplyingUmbrellaInput;

    public event Action WaterStateChanged;

    public UmbrellaWaterTarget UmbrellaTarget => umbrellaWaterTarget;
    public IReadOnlyList<WaterBasinTarget> ConnectedTargets => connectedTargets;
    public float SurfaceArea => surfaceArea;
    public float MaxWaterHeight => maxWaterHeight;
    public float CurrentVolume => currentVolume;
    public float WaterSurfaceWorldY => waterSurfaceWorldY;
    public float BottomWorldY => useTransformYAsBottom ? transform.position.y : manualBottomWorldY;
    public float TopWorldY => BottomWorldY + maxWaterHeight;
    public float Capacity => surfaceArea * maxWaterHeight;
    public float WaterDepth => Mathf.Max(0.0f, waterSurfaceWorldY - BottomWorldY);
    public float Fill01 => Capacity <= Epsilon ? 0.0f : Mathf.Clamp01(currentVolume / Capacity);

    private void Reset()
    {
        CacheUmbrellaTarget();
        manualBottomWorldY = transform.position.y;
    }

    private void Awake()
    {
        CacheUmbrellaTarget();
        currentVolume = Mathf.Clamp(initialVolume, 0.0f, Capacity);
        waterSurfaceWorldY = GetSurfaceForLocalVolume(currentVolume);
        observedUmbrellaWater = umbrellaWaterTarget != null ? umbrellaWaterTarget.ReceivedWater : 0.0f;
    }

    private void OnEnable()
    {
        SubscribeUmbrellaTarget();
    }

    private void Start()
    {
        RedistributeConnected();
    }

    private void OnDisable()
    {
        UnsubscribeUmbrellaTarget();
    }

    private void OnValidate()
    {
        surfaceArea = Mathf.Max(Epsilon, surfaceArea);
        maxWaterHeight = Mathf.Max(Epsilon, maxWaterHeight);
        initialVolume = Mathf.Max(0.0f, initialVolume);
        currentVolume = Mathf.Clamp(currentVolume, 0.0f, Capacity);
        labelSize = new Vector2(
            Mathf.Max(160.0f, labelSize.x),
            Mathf.Max(72.0f, labelSize.y));

        if (useTransformYAsBottom)
        {
            manualBottomWorldY = transform.position.y;
        }

        CacheUmbrellaTarget();
    }

    public void AddWater(float amount)
    {
        if (amount <= Epsilon)
        {
            return;
        }

        SolveConnectedGroup(amount);
    }

    public void RemoveWater(float amount)
    {
        if (amount <= Epsilon)
        {
            return;
        }

        SolveConnectedGroup(-amount);
    }

    public void RedistributeConnected()
    {
        SolveConnectedGroup(0.0f);
    }

    public bool IsConnectedTo(WaterBasinTarget target)
    {
        return target != null && connectedTargets.Contains(target);
    }

    public float GetVolumeAtSurface(float surfaceWorldY)
    {
        float height = Mathf.Clamp(surfaceWorldY - BottomWorldY, 0.0f, maxWaterHeight);
        return height * surfaceArea;
    }

    private void SolveConnectedGroup(float volumeDelta)
    {
        List<WaterBasinTarget> group = CollectConnectedGroup();
        if (group.Count == 0)
        {
            return;
        }

        float totalVolume = volumeDelta;
        float totalCapacity = 0.0f;

        for (int i = 0; i < group.Count; i++)
        {
            totalVolume += group[i].currentVolume;
            totalCapacity += group[i].Capacity;
        }

        totalVolume = Mathf.Clamp(totalVolume, 0.0f, totalCapacity);

        float surfaceY = SolveSurfaceY(group, totalVolume);
        for (int i = 0; i < group.Count; i++)
        {
            WaterBasinTarget target = group[i];
            target.ApplySolvedState(target.GetVolumeAtSurface(surfaceY), surfaceY);
        }
    }

    private List<WaterBasinTarget> CollectConnectedGroup()
    {
        WaterBasinTarget[] allTargets = FindObjectsByType<WaterBasinTarget>();
        List<WaterBasinTarget> group = new List<WaterBasinTarget>();
        Queue<WaterBasinTarget> queue = new Queue<WaterBasinTarget>();
        HashSet<WaterBasinTarget> visited = new HashSet<WaterBasinTarget>();

        visited.Add(this);
        queue.Enqueue(this);

        while (queue.Count > 0)
        {
            WaterBasinTarget current = queue.Dequeue();
            group.Add(current);

            for (int i = 0; i < current.connectedTargets.Count; i++)
            {
                TryQueueTarget(current.connectedTargets[i], visited, queue);
            }

            for (int i = 0; i < allTargets.Length; i++)
            {
                WaterBasinTarget candidate = allTargets[i];
                if (candidate != null && candidate.connectedTargets.Contains(current))
                {
                    TryQueueTarget(candidate, visited, queue);
                }
            }
        }

        return group;
    }

    private static void TryQueueTarget(
        WaterBasinTarget target,
        HashSet<WaterBasinTarget> visited,
        Queue<WaterBasinTarget> queue)
    {
        if (target == null || !target.isActiveAndEnabled || visited.Contains(target))
        {
            return;
        }

        visited.Add(target);
        queue.Enqueue(target);
    }

    private static float SolveSurfaceY(List<WaterBasinTarget> group, float totalVolume)
    {
        if (totalVolume <= Epsilon)
        {
            return GetLowestBottom(group);
        }

        float minY = GetLowestBottom(group);
        float maxY = GetHighestTop(group);

        for (int i = 0; i < 32; i++)
        {
            float middleY = (minY + maxY) * 0.5f;
            float volumeAtMiddle = 0.0f;

            for (int targetIndex = 0; targetIndex < group.Count; targetIndex++)
            {
                volumeAtMiddle += group[targetIndex].GetVolumeAtSurface(middleY);
            }

            if (volumeAtMiddle < totalVolume)
            {
                minY = middleY;
            }
            else
            {
                maxY = middleY;
            }
        }

        return (minY + maxY) * 0.5f;
    }

    private static float GetLowestBottom(List<WaterBasinTarget> group)
    {
        float result = group[0].BottomWorldY;
        for (int i = 1; i < group.Count; i++)
        {
            result = Mathf.Min(result, group[i].BottomWorldY);
        }

        return result;
    }

    private static float GetHighestTop(List<WaterBasinTarget> group)
    {
        float result = group[0].TopWorldY;
        for (int i = 1; i < group.Count; i++)
        {
            result = Mathf.Max(result, group[i].TopWorldY);
        }

        return result;
    }

    private void ApplySolvedState(float solvedVolume, float solvedSurfaceY)
    {
        float nextVolume = Mathf.Clamp(solvedVolume, 0.0f, Capacity);
        float nextSurfaceY = nextVolume <= Epsilon ? BottomWorldY : solvedSurfaceY;
        bool changed = Mathf.Abs(currentVolume - nextVolume) > Epsilon
            || Mathf.Abs(waterSurfaceWorldY - nextSurfaceY) > Epsilon;

        currentVolume = nextVolume;
        waterSurfaceWorldY = nextSurfaceY;

        if (changed)
        {
            WaterStateChanged?.Invoke();
        }
    }

    private float GetSurfaceForLocalVolume(float volume)
    {
        if (volume <= Epsilon)
        {
            return BottomWorldY;
        }

        return BottomWorldY + Mathf.Clamp(volume / surfaceArea, 0.0f, maxWaterHeight);
    }

    private void SubscribeUmbrellaTarget()
    {
        if (umbrellaWaterTarget == null)
        {
            return;
        }

        umbrellaWaterTarget.WaterChanged -= OnUmbrellaWaterChanged;
        umbrellaWaterTarget.WaterChanged += OnUmbrellaWaterChanged;
        observedUmbrellaWater = umbrellaWaterTarget.ReceivedWater;
    }

    private void UnsubscribeUmbrellaTarget()
    {
        if (umbrellaWaterTarget == null)
        {
            return;
        }

        umbrellaWaterTarget.WaterChanged -= OnUmbrellaWaterChanged;
    }

    private void OnUmbrellaWaterChanged()
    {
        if (!useUmbrellaTargetAsInput || isApplyingUmbrellaInput || umbrellaWaterTarget == null)
        {
            return;
        }

        float nextObservedWater = umbrellaWaterTarget.ReceivedWater;
        float delta = nextObservedWater - observedUmbrellaWater;
        observedUmbrellaWater = nextObservedWater;

        if (delta <= Epsilon)
        {
            return;
        }

        isApplyingUmbrellaInput = true;
        AddWater(delta);
        isApplyingUmbrellaInput = false;
    }

    private void CacheUmbrellaTarget()
    {
        if (umbrellaWaterTarget != null || !findUmbrellaTargetInParent)
        {
            return;
        }

        umbrellaWaterTarget = GetComponentInParent<UmbrellaWaterTarget>();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        float side = Mathf.Sqrt(Mathf.Max(surfaceArea, Epsilon));
        float depth = Application.isPlaying ? WaterDepth : Mathf.Min(maxWaterHeight, initialVolume / surfaceArea);
        Vector3 center = new Vector3(transform.position.x, BottomWorldY + depth * 0.5f, transform.position.z);
        Vector3 size = new Vector3(side, Mathf.Max(depth, 0.02f), side);

        Gizmos.color = debugColor;
        Gizmos.DrawCube(center, size);

        Color lineColor = debugColor;
        lineColor.a = 1.0f;
        Gizmos.color = lineColor;
        Gizmos.DrawWireCube(center, size);

        for (int i = 0; i < connectedTargets.Count; i++)
        {
            WaterBasinTarget connected = connectedTargets[i];
            if (connected == null)
            {
                continue;
            }

            Gizmos.DrawLine(transform.position, connected.transform.position);
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !showGameViewDebug)
        {
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        Vector3 labelWorldPosition = transform.position + labelOffset;
        Vector3 screenPoint = targetCamera.WorldToScreenPoint(labelWorldPosition);
        if (screenPoint.z <= 0.0f)
        {
            return;
        }

        float x = screenPoint.x - labelSize.x * 0.5f;
        float y = Screen.height - screenPoint.y - labelSize.y;

        GUI.Box(new Rect(x, y, labelSize.x, labelSize.y), "Water Basin Target");
        GUI.Label(new Rect(x + 8.0f, y + 20.0f, labelSize.x - 16.0f, 18.0f), $"Vol: {currentVolume:F2} / {Capacity:F2}  Fill: {Fill01:P0}");
        GUI.Label(new Rect(x + 8.0f, y + 38.0f, labelSize.x - 16.0f, 18.0f), $"Depth: {WaterDepth:F2}  SurfaceY: {waterSurfaceWorldY:F2}");
        GUI.Label(new Rect(x + 8.0f, y + 56.0f, labelSize.x - 16.0f, 18.0f), $"Area: {surfaceArea:F2}  MaxH: {maxWaterHeight:F2}");
        GUI.Label(new Rect(x + 8.0f, y + 74.0f, labelSize.x - 16.0f, 18.0f), $"Links: {connectedTargets.Count}");

        if (!showGroupDebug)
        {
            return;
        }

        GetConnectedGroupDebugStats(out int groupCount, out float groupVolume, out float groupCapacity);
        float groupFill = groupCapacity <= Epsilon ? 0.0f : groupVolume / groupCapacity;
        GUI.Label(new Rect(x + 8.0f, y + 92.0f, labelSize.x - 16.0f, 18.0f), $"Group: {groupCount}  {groupVolume:F2}/{groupCapacity:F2}  {groupFill:P0}");
    }

    private void GetConnectedGroupDebugStats(out int groupCount, out float groupVolume, out float groupCapacity)
    {
        List<WaterBasinTarget> group = CollectConnectedGroup();
        groupCount = group.Count;
        groupVolume = 0.0f;
        groupCapacity = 0.0f;

        for (int i = 0; i < group.Count; i++)
        {
            WaterBasinTarget target = group[i];
            groupVolume += target.currentVolume;
            groupCapacity += target.Capacity;
        }
    }
}
