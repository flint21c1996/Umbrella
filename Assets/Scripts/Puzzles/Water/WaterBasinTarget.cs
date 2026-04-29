using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WaterBasinTarget : MonoBehaviour
{
    private const float Epsilon = 0.0001f;
    private static bool debugOverlayEnabled = true;
    private static bool debugGizmosEnabled = true;

    private enum BottomHeightMode
    {
        TransformY,
        ManualWorldY,
        RendererBoundsMinY,
        ColliderBoundsMinY
    }

    [Header("Umbrella Adapter")]
    [SerializeField] private UmbrellaWaterTarget umbrellaWaterTarget;
    [SerializeField] private bool findUmbrellaTargetInParent = true;

    [Tooltip("When UmbrellaWaterTarget receives water directly, copy that added amount into this basin group.")]
    [SerializeField] private bool useUmbrellaTargetAsInput = true;

    [Header("Connections")]
    [Tooltip("연결된 물")]
    [SerializeField] private List<WaterBasinTarget> connectedTargets = new List<WaterBasinTarget>();

    [Header("Volume")]
    [Tooltip("바닥 면적")]
    [SerializeField] private float surfaceArea = 1.0f;

    [Tooltip("최대 물 높이")]
    [SerializeField] private float maxWaterHeight = 1.0f;

    [Tooltip("기본 물의 양")]
    [SerializeField] private float initialVolume;

    [Header("Height")]
    [Tooltip("물이 차기 시작하는 높이")]
    [SerializeField] private BottomHeightMode bottomHeightMode = BottomHeightMode.ColliderBoundsMinY;
    [Tooltip("useTransformYAsBottom이 false일때 사용할 바닥의 월드Y좌표")]
    [SerializeField] private float manualBottomWorldY;

    [Header("Runtime")]

    [Tooltip("현재 블록의 물 부피")]
    [SerializeField] private float currentVolume;

    [Tooltip("현재 물 수면의 높이(월드기준)")]
    [SerializeField] private float waterSurfaceWorldY;

    [Tooltip("연결된 물의 높이(월드기준)")]
    [SerializeField] private float groupWaterSurfaceWorldY;

    [Tooltip("연결된 물의 수면 높이 차이")]
    [SerializeField] private float groupSurfaceSpread;

    [Tooltip("연결된 물의 최대 양")]
    [SerializeField] private float groupVolume;

    [Tooltip("연결된 물의 최대 양")]
    [SerializeField] private float groupCapacity;

    [Tooltip("연결된 물의 개수")]
    [SerializeField] private int groupTargetCount;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawSurfaceGizmo = true;
    [SerializeField] private bool showGameViewDebug = true;
    [SerializeField] private bool showGroupDebug = true;
    [SerializeField] private Vector3 labelOffset = new Vector3(0.0f, 1.25f, 0.0f);
    [SerializeField] private Vector2 labelSize = new Vector2(240.0f, 148.0f);
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
    public float GroupWaterSurfaceWorldY => groupWaterSurfaceWorldY;
    public float GroupSurfaceSpread => groupSurfaceSpread;
    public float GroupVolume => groupVolume;
    public float GroupCapacity => groupCapacity;
    public int GroupTargetCount => groupTargetCount;
    public float BottomWorldY => GetBottomWorldY();
    public float TopWorldY => BottomWorldY + maxWaterHeight;
    public float Capacity => surfaceArea * maxWaterHeight;
    public float WaterDepth => Mathf.Max(0.0f, waterSurfaceWorldY - BottomWorldY);
    public float Fill01 => Capacity <= Epsilon ? 0.0f : Mathf.Clamp01(currentVolume / Capacity);

    public static void SetDebugVisible(bool showOverlay, bool showGizmos)
    {
        debugOverlayEnabled = showOverlay;
        debugGizmosEnabled = showGizmos;
    }

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
        groupWaterSurfaceWorldY = waterSurfaceWorldY;
        groupVolume = currentVolume;
        groupCapacity = Capacity;
        groupTargetCount = 1;
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
            Mathf.Max(126.0f, labelSize.y));

        if (bottomHeightMode == BottomHeightMode.TransformY)
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

    public void AddWaterToThisTarget(float amount)
    {
        if (amount <= Epsilon)
        {
            return;
        }

        SetThisTargetVolume(currentVolume + amount);
    }

    public void RemoveWaterFromThisTarget(float amount)
    {
        if (amount <= Epsilon)
        {
            return;
        }

        SetThisTargetVolume(currentVolume - amount);
    }

    public void FillThisTarget()
    {
        SetThisTargetVolume(Capacity);
    }

    public void RemoveAllWaterFromThisTarget()
    {
        SetThisTargetVolume(0.0f);
    }

    public void RemoveAllWater()
    {
        SolveConnectedGroup(-GetConnectedGroupVolume());
    }

    public float GetConnectedGroupVolume()
    {
        List<WaterBasinTarget> group = CollectConnectedGroup();
        float totalVolume = 0.0f;

        for (int i = 0; i < group.Count; i++)
        {
            totalVolume += group[i].currentVolume;
        }

        return totalVolume;
    }

    public float GetConnectedGroupCapacity()
    {
        List<WaterBasinTarget> group = CollectConnectedGroup();
        float totalCapacity = 0.0f;

        for (int i = 0; i < group.Count; i++)
        {
            totalCapacity += group[i].Capacity;
        }

        return totalCapacity;
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

        bool anyChanged = false;
        float surfaceY = SolveSurfaceY(group, totalVolume);
        for (int i = 0; i < group.Count; i++)
        {
            WaterBasinTarget target = group[i];
            anyChanged |= target.ApplySolvedState(target.GetVolumeAtSurface(surfaceY), surfaceY);
        }

        RefreshDebugStateForGroup(group, surfaceY);

        if (anyChanged)
        {
            NotifyWaterStateChangedForGroup(group);
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

    private bool ApplySolvedState(float solvedVolume, float solvedSurfaceY)
    {
        float nextVolume = Mathf.Clamp(solvedVolume, 0.0f, Capacity);
        float nextSurfaceY = solvedSurfaceY;
        bool changed = Mathf.Abs(currentVolume - nextVolume) > Epsilon
            || Mathf.Abs(waterSurfaceWorldY - nextSurfaceY) > Epsilon;

        currentVolume = nextVolume;
        waterSurfaceWorldY = nextSurfaceY;

        return changed;
    }

    private void SetThisTargetVolume(float volume)
    {
        float nextVolume = Mathf.Clamp(volume, 0.0f, Capacity);
        float nextSurfaceY = GetSurfaceForLocalVolume(nextVolume);
        bool changed = ApplySolvedState(nextVolume, nextSurfaceY);

        groupWaterSurfaceWorldY = waterSurfaceWorldY;
        groupSurfaceSpread = 0.0f;
        groupVolume = currentVolume;
        groupCapacity = Capacity;
        groupTargetCount = 1;

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

    private float GetBottomWorldY()
    {
        switch (bottomHeightMode)
        {
            case BottomHeightMode.ManualWorldY:
                return manualBottomWorldY;
            case BottomHeightMode.RendererBoundsMinY:
                return GetRendererBoundsMinY();
            case BottomHeightMode.ColliderBoundsMinY:
                return GetColliderBoundsMinY();
            default:
                return transform.position.y;
        }
    }

    private float GetRendererBoundsMinY()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            return transform.position.y;
        }

        float minY = renderers[0].bounds.min.y;
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].GetComponentInParent<WaterBasinVisual>() != null)
            {
                continue;
            }

            if (!found)
            {
                minY = renderers[i].bounds.min.y;
                found = true;
                continue;
            }

            minY = Mathf.Min(minY, renderers[i].bounds.min.y);
        }

        return found ? minY : transform.position.y;
    }

    private float GetColliderBoundsMinY()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            return transform.position.y;
        }

        float minY = colliders[0].bounds.min.y;
        bool found = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].GetComponentInParent<WaterBasinVisual>() != null)
            {
                continue;
            }

            if (!found)
            {
                minY = colliders[i].bounds.min.y;
                found = true;
                continue;
            }

            minY = Mathf.Min(minY, colliders[i].bounds.min.y);
        }

        return found ? minY : transform.position.y;
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
        if (!debugGizmosEnabled || !drawGizmos)
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

        if (drawSurfaceGizmo)
        {
            float surfaceY = Application.isPlaying
                ? groupWaterSurfaceWorldY
                : BottomWorldY + Mathf.Min(maxWaterHeight, initialVolume / surfaceArea);
            Vector3 surfaceCenter = new Vector3(transform.position.x, surfaceY, transform.position.z);
            Vector3 surfaceSize = new Vector3(side, 0.02f, side);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(surfaceCenter, surfaceSize);
        }

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
        if (!Application.isPlaying || !debugOverlayEnabled || !showGameViewDebug)
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
        GUI.Label(new Rect(x + 8.0f, y + 38.0f, labelSize.x - 16.0f, 18.0f), $"Depth: {WaterDepth:F2}  Local SurfaceY: {waterSurfaceWorldY:F2}");
        GUI.Label(new Rect(x + 8.0f, y + 56.0f, labelSize.x - 16.0f, 18.0f), $"Group SurfaceY: {groupWaterSurfaceWorldY:F2}  Spread: {groupSurfaceSpread:F4}");
        GUI.Label(new Rect(x + 8.0f, y + 74.0f, labelSize.x - 16.0f, 18.0f), $"Area: {surfaceArea:F2}  MaxH: {maxWaterHeight:F2}");
        GUI.Label(new Rect(x + 8.0f, y + 92.0f, labelSize.x - 16.0f, 18.0f), $"Links: {connectedTargets.Count}");

        if (!showGroupDebug)
        {
            return;
        }

        RefreshConnectedGroupDebugState();
        float groupFill = groupCapacity <= Epsilon ? 0.0f : groupVolume / groupCapacity;
        GUI.Label(new Rect(x + 8.0f, y + 110.0f, labelSize.x - 16.0f, 18.0f), $"Group: {groupTargetCount}  {groupVolume:F2}/{groupCapacity:F2}  {groupFill:P0}");
    }

    private void RefreshConnectedGroupDebugState()
    {
        List<WaterBasinTarget> group = CollectConnectedGroup();
        if (group.Count == 0)
        {
            return;
        }

        float totalVolume = 0.0f;
        float totalCapacity = 0.0f;
        float minSurfaceY = group[0].waterSurfaceWorldY;
        float maxSurfaceY = group[0].waterSurfaceWorldY;

        for (int i = 0; i < group.Count; i++)
        {
            WaterBasinTarget target = group[i];
            totalVolume += target.currentVolume;
            totalCapacity += target.Capacity;
            minSurfaceY = Mathf.Min(minSurfaceY, target.waterSurfaceWorldY);
            maxSurfaceY = Mathf.Max(maxSurfaceY, target.waterSurfaceWorldY);
        }

        groupSurfaceSpread = maxSurfaceY - minSurfaceY;
        groupVolume = totalVolume;
        groupCapacity = totalCapacity;
        groupTargetCount = group.Count;
    }

    private static void RefreshDebugStateForGroup(List<WaterBasinTarget> group, float sharedSurfaceY)
    {
        if (group.Count == 0)
        {
            return;
        }

        float totalVolume = 0.0f;
        float totalCapacity = 0.0f;
        float minSurfaceY = group[0].waterSurfaceWorldY;
        float maxSurfaceY = group[0].waterSurfaceWorldY;

        for (int i = 0; i < group.Count; i++)
        {
            WaterBasinTarget target = group[i];
            totalVolume += target.currentVolume;
            totalCapacity += target.Capacity;
            minSurfaceY = Mathf.Min(minSurfaceY, target.waterSurfaceWorldY);
            maxSurfaceY = Mathf.Max(maxSurfaceY, target.waterSurfaceWorldY);
        }

        float spread = maxSurfaceY - minSurfaceY;
        for (int i = 0; i < group.Count; i++)
        {
            WaterBasinTarget target = group[i];
            target.groupWaterSurfaceWorldY = sharedSurfaceY;
            target.groupSurfaceSpread = spread;
            target.groupVolume = totalVolume;
            target.groupCapacity = totalCapacity;
            target.groupTargetCount = group.Count;
        }
    }

    private static void NotifyWaterStateChangedForGroup(List<WaterBasinTarget> group)
    {
        for (int i = 0; i < group.Count; i++)
        {
            group[i].WaterStateChanged?.Invoke();
        }
    }
}
