using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WaterBasinTarget : MonoBehaviour
{
    private const float Epsilon = 0.0001f;
    private static bool debugOverlayEnabled = true;
    private static bool debugGizmosEnabled = true;
    private static GameDebugController.WaterBasinDebugOverlayScope debugOverlayScope =
        GameDebugController.WaterBasinDebugOverlayScope.SelectedTargets;
    private static WaterBasinTarget debugOverlayTarget;

#if UNITY_EDITOR
    private static int debugSelectionCacheFrame = -1;
    private static readonly HashSet<WaterBasinTarget> selectedDebugTargets = new HashSet<WaterBasinTarget>();
    private static readonly HashSet<WaterBasinTarget> selectedDebugGroupTargets = new HashSet<WaterBasinTarget>();
    private static bool selectedDebugGroupCacheBuilt;
#endif

    private enum BottomHeightMode
    {
        // 오브젝트 트랜스폼의 월드 Y 위치를 물이 차기 시작하는 바닥 높이로 사용합니다.
        TransformY,

        // 인스펙터에 입력한 수동 바닥 월드 Y 값을 바닥 높이로 사용합니다.
        ManualWorldY,

        // 물 시각화 오브젝트를 제외한 렌더러 경계의 최저 Y를 바닥 높이로 사용합니다.
        RendererBoundsMinY,

        // 물 시각화 오브젝트를 제외한 콜라이더 경계의 최저 Y를 바닥 높이로 사용합니다.
        ColliderBoundsMinY
    }

    private enum VolumeSizeMode
    {
        // 인스펙터에 직접 입력한 바닥 면적과 최대 물 높이를 부피 계산에 사용합니다.
        Manual,

        // 트랜스폼의 월드 스케일을 크기로 보고 X*Z를 면적, Y를 최대 수위로 사용합니다.
        TransformScale,

        // 물 시각화 오브젝트를 제외한 렌더러 경계 크기로 면적과 최대 수위를 계산합니다.
        RendererBounds,

        // 물 시각화 오브젝트를 제외한 콜라이더 경계 크기로 면적과 최대 수위를 계산합니다.
        ColliderBounds
    }

    [Header("Umbrella Adapter")]
    [SerializeField] private UmbrellaWaterTarget umbrellaWaterTarget;
    [SerializeField] private bool findUmbrellaTargetInParent = true;

    [Tooltip("직접 물을 받으면 증가한 물의 양을 그룹에 반영합니다.")]
    [SerializeField] private bool useUmbrellaTargetAsInput = true;

    [Header("연결")]
    [InspectorName("연결된 물 타겟")]
    [Tooltip("게임 중 실제 연결에 사용하는 물 타겟 목록입니다. 떨어져 있는 타겟도 직접 넣을 수 있습니다.")]
    [SerializeField] private List<WaterBasinTarget> connectedTargets = new List<WaterBasinTarget>();

    [Header("Volume")]
    [Tooltip("부피 계산 기준")]
    [SerializeField] private VolumeSizeMode volumeSizeMode = VolumeSizeMode.ColliderBounds;

    [Tooltip("직접 입력 모드이거나 경계 정보를 찾지 못했을 때 사용할 바닥 면적")]
    [SerializeField] private float surfaceArea = 1.0f;

    [Tooltip("직접 입력 모드이거나 경계 정보를 찾지 못했을 때 사용할 최대 물 높이")]
    [SerializeField] private float maxWaterHeight = 1.0f;

    [Tooltip("기본 물의 양")]
    [SerializeField] private float initialVolume;

    [Header("Height")]
    [Tooltip("물이 차기 시작하는 높이")]
    [SerializeField] private BottomHeightMode bottomHeightMode = BottomHeightMode.ColliderBoundsMinY;
    [Tooltip("직접 입력 바닥 모드에서 사용할 바닥의 월드 Y 좌표")]
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

    [Tooltip("연결된 물 그룹의 현재 물 부피")]
    [SerializeField] private float groupVolume;

    [Tooltip("연결된 물 그룹의 최대 물 부피")]
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
    public Vector3 VolumeWorldSize => GetVolumeWorldSize();
    public Vector3 VolumeWorldCenter => GetVolumeWorldCenter();
    public float SurfaceArea => GetSurfaceArea();
    public float MaxWaterHeight => GetMaxWaterHeight();
    public float CurrentVolume => currentVolume;
    public float WaterSurfaceWorldY => waterSurfaceWorldY;
    public float GroupWaterSurfaceWorldY => groupWaterSurfaceWorldY;
    public float GroupSurfaceSpread => groupSurfaceSpread;
    public float GroupVolume => groupVolume;
    public float GroupCapacity => groupCapacity;
    public int GroupTargetCount => groupTargetCount;
    public float BottomWorldY => GetBottomWorldY();
    public float TopWorldY => BottomWorldY + MaxWaterHeight;
    public float Capacity => SurfaceArea * MaxWaterHeight;
    public float WaterDepth => Mathf.Max(0.0f, waterSurfaceWorldY - BottomWorldY);
    public float Fill01 => Capacity <= Epsilon ? 0.0f : Mathf.Clamp01(currentVolume / Capacity);

    public static void SetDebugVisible(bool showOverlay, bool showGizmos)
    {
        debugOverlayEnabled = showOverlay;
        debugGizmosEnabled = showGizmos;
    }

    public static void SetDebugOverlayFilter(
        GameDebugController.WaterBasinDebugOverlayScope scope,
        WaterBasinTarget target)
    {
        debugOverlayScope = scope;
        debugOverlayTarget = target;
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
            Mathf.Max(148.0f, labelSize.y));

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

    public void SetWaterDepth(float depth)
    {
        SetWaterSurfaceWorldY(BottomWorldY + Mathf.Max(0.0f, depth));
    }

    public void SetWaterSurfaceWorldY(float surfaceWorldY)
    {
        List<WaterBasinTarget> group = CollectConnectedGroup();
        if (group.Count == 0)
        {
            return;
        }

        float currentGroupVolume = 0.0f;
        float targetGroupVolume = 0.0f;
        for (int i = 0; i < group.Count; i++)
        {
            WaterBasinTarget basinTarget = group[i];
            currentGroupVolume += basinTarget.currentVolume;
            targetGroupVolume += basinTarget.GetVolumeAtSurface(surfaceWorldY);
        }

        SolveConnectedGroup(targetGroupVolume - currentGroupVolume);
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

    public void SetThisTargetWaterDepth(float depth)
    {
        SetThisTargetWaterSurfaceWorldY(BottomWorldY + Mathf.Max(0.0f, depth));
    }

    public void SetThisTargetWaterSurfaceWorldY(float surfaceWorldY)
    {
        SetThisTargetVolume(GetVolumeAtSurface(surfaceWorldY));
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
        if (target == null)
        {
            return false;
        }

        return HasManualConnectionTo(target)
            || target.HasManualConnectionTo(this);
    }

#if UNITY_EDITOR
    public bool CanAddConnectedTarget(WaterBasinTarget target)
    {
        return target != null
            && target != this
            && !connectedTargets.Contains(target);
    }

    public bool AddConnectedTarget(WaterBasinTarget target)
    {
        if (!CanAddConnectedTarget(target))
        {
            return false;
        }

        connectedTargets.Add(target);
        return true;
    }

    public int ClearConnectedTargets()
    {
        int removedCount = connectedTargets.Count;
        connectedTargets.Clear();
        return removedCount;
    }
#endif

    public float GetVolumeAtSurface(float surfaceWorldY)
    {
        float height = Mathf.Clamp(surfaceWorldY - BottomWorldY, 0.0f, MaxWaterHeight);
        return height * SurfaceArea;
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
                if (candidate == null || candidate == current)
                {
                    continue;
                }

                if (candidate.HasManualConnectionTo(current))
                {
                    TryQueueTarget(candidate, visited, queue);
                }
            }
        }

        return group;
    }

    private bool HasManualConnectionTo(WaterBasinTarget target)
    {
        return target != null && connectedTargets.Contains(target);
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

        return BottomWorldY + Mathf.Clamp(volume / SurfaceArea, 0.0f, MaxWaterHeight);
    }

    private float GetSurfaceArea()
    {
        Vector3 size = GetVolumeWorldSize();
        return Mathf.Max(Epsilon, size.x * size.z);
    }

    private float GetMaxWaterHeight()
    {
        return Mathf.Max(Epsilon, GetVolumeWorldSize().y);
    }

    private Vector3 GetVolumeWorldSize()
    {
        switch (volumeSizeMode)
        {
            case VolumeSizeMode.TransformScale:
                return ClampVolumeSize(transform.lossyScale);
            case VolumeSizeMode.RendererBounds:
                return TryGetRendererLocalBounds(out Bounds rendererBounds)
                    ? ScaleLocalSizeToWorld(rendererBounds.size)
                    : GetManualVolumeSize();
            case VolumeSizeMode.ColliderBounds:
                return TryGetColliderLocalBounds(out Bounds colliderBounds)
                    ? ScaleLocalSizeToWorld(colliderBounds.size)
                    : GetManualVolumeSize();
            default:
                return GetManualVolumeSize();
        }
    }

    private Vector3 GetVolumeWorldCenter()
    {
        return transform.TransformPoint(GetVolumeLocalCenter());
    }

    private Vector3 GetVolumeLocalCenter()
    {
        switch (volumeSizeMode)
        {
            case VolumeSizeMode.RendererBounds:
                return TryGetRendererLocalBounds(out Bounds rendererBounds)
                    ? rendererBounds.center
                    : Vector3.zero;
            case VolumeSizeMode.ColliderBounds:
                return TryGetColliderLocalBounds(out Bounds colliderBounds)
                    ? colliderBounds.center
                    : Vector3.zero;
            default:
                return Vector3.zero;
        }
    }

    private Vector3 GetManualVolumeSize()
    {
        float side = Mathf.Sqrt(Mathf.Max(Epsilon, surfaceArea));
        return new Vector3(side, Mathf.Max(Epsilon, maxWaterHeight), side);
    }

    private static Vector3 ClampVolumeSize(Vector3 size)
    {
        return new Vector3(
            Mathf.Max(Epsilon, Mathf.Abs(size.x)),
            Mathf.Max(Epsilon, Mathf.Abs(size.y)),
            Mathf.Max(Epsilon, Mathf.Abs(size.z)));
    }

    private Vector3 ScaleLocalSizeToWorld(Vector3 localSize)
    {
        Vector3 scale = transform.lossyScale;
        return new Vector3(
            Mathf.Max(Epsilon, Mathf.Abs(localSize.x * scale.x)),
            Mathf.Max(Epsilon, Mathf.Abs(localSize.y * scale.y)),
            Mathf.Max(Epsilon, Mathf.Abs(localSize.z * scale.z)));
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
        return TryGetRendererLocalBounds(out Bounds bounds)
            ? GetLocalBoundsWorldMinY(bounds)
            : transform.position.y;
    }

    private float GetColliderBoundsMinY()
    {
        return TryGetColliderLocalBounds(out Bounds bounds)
            ? GetLocalBoundsWorldMinY(bounds)
            : transform.position.y;
    }

    private bool TryGetRendererLocalBounds(out Bounds bounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || ShouldIgnoreVolumeBounds(renderer))
            {
                continue;
            }

            if (!found)
            {
                bounds = CreateLocalBounds(renderer.localBounds, renderer.transform.localToWorldMatrix);
                found = true;
                continue;
            }

            EncapsulateLocalBounds(ref bounds, renderer.localBounds, renderer.transform.localToWorldMatrix);
        }

        return found;
    }

    private bool TryGetColliderLocalBounds(out Bounds bounds)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool found = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || ShouldIgnoreVolumeBounds(collider))
            {
                continue;
            }

            if (!found)
            {
                bounds = CreateColliderLocalBounds(collider);
                found = true;
                continue;
            }

            EncapsulateColliderLocalBounds(ref bounds, collider);
        }

        return found;
    }

    private Bounds CreateColliderLocalBounds(Collider collider)
    {
        if (collider is BoxCollider boxCollider)
        {
            Bounds boxBounds = new Bounds(boxCollider.center, boxCollider.size);
            return CreateLocalBounds(boxBounds, boxCollider.transform.localToWorldMatrix);
        }

        return CreateLocalBounds(collider.bounds);
    }

    private void EncapsulateColliderLocalBounds(ref Bounds targetBounds, Collider collider)
    {
        if (collider is BoxCollider boxCollider)
        {
            Bounds boxBounds = new Bounds(boxCollider.center, boxCollider.size);
            EncapsulateLocalBounds(ref targetBounds, boxBounds, boxCollider.transform.localToWorldMatrix);
            return;
        }

        EncapsulateLocalBounds(ref targetBounds, collider.bounds);
    }

    private Bounds CreateLocalBounds(Bounds sourceBounds, Matrix4x4 sourceLocalToWorld)
    {
        Bounds localBounds = new Bounds(
            transform.InverseTransformPoint(sourceLocalToWorld.MultiplyPoint3x4(sourceBounds.center)),
            Vector3.zero);
        EncapsulateLocalBounds(ref localBounds, sourceBounds, sourceLocalToWorld);
        return localBounds;
    }

    private Bounds CreateLocalBounds(Bounds worldBounds)
    {
        Bounds localBounds = new Bounds(transform.InverseTransformPoint(worldBounds.center), Vector3.zero);
        EncapsulateLocalBounds(ref localBounds, worldBounds);
        return localBounds;
    }

    private void EncapsulateLocalBounds(ref Bounds targetBounds, Bounds sourceBounds, Matrix4x4 sourceLocalToWorld)
    {
        EncapsulateBoundsCorners(ref targetBounds, sourceBounds, sourcePoint =>
        {
            Vector3 worldPoint = sourceLocalToWorld.MultiplyPoint3x4(sourcePoint);
            return transform.InverseTransformPoint(worldPoint);
        });
    }

    private void EncapsulateLocalBounds(ref Bounds targetBounds, Bounds worldBounds)
    {
        EncapsulateBoundsCorners(ref targetBounds, worldBounds, worldPoint =>
        {
            return transform.InverseTransformPoint(worldPoint);
        });
    }

    private float GetLocalBoundsWorldMinY(Bounds localBounds)
    {
        float minY = float.MaxValue;

        ForEachBoundsCorner(localBounds, localPoint =>
        {
            minY = Mathf.Min(minY, transform.TransformPoint(localPoint).y);
        });

        return minY;
    }

    private static void EncapsulateBoundsCorners(
        ref Bounds targetBounds,
        Bounds sourceBounds,
        Func<Vector3, Vector3> convertPoint)
    {
        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int z = 0; z <= 1; z++)
                {
                    targetBounds.Encapsulate(convertPoint(GetBoundsCorner(sourceBounds, x, y, z)));
                }
            }
        }
    }

    private static void ForEachBoundsCorner(Bounds bounds, Action<Vector3> action)
    {
        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int z = 0; z <= 1; z++)
                {
                    action(GetBoundsCorner(bounds, x, y, z));
                }
            }
        }
    }

    private static Vector3 GetBoundsCorner(Bounds bounds, int x, int y, int z)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        return new Vector3(
            x == 0 ? min.x : max.x,
            y == 0 ? min.y : max.y,
            z == 0 ? min.z : max.z);
    }

    private bool ShouldIgnoreVolumeBounds(Component component)
    {
        if (IsChildOfNamedTransform(component, "WaterVisualRoot")
            || IsChildOfNamedTransform(component, "Water Visual Root")
            || IsChildOfNamedTransform(component, "WaterFillMesh")
            || IsChildOfNamedTransform(component, "Water Fill Mesh"))
        {
            return true;
        }

        WaterBasinVisual parentVisual = component.GetComponentInParent<WaterBasinVisual>();
        return parentVisual != null && parentVisual.transform != transform;
    }

    private bool IsChildOfNamedTransform(Component component, string childName)
    {
        Transform child = transform.Find(childName);
        return child != null && component.transform.IsChildOf(child);
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

        Vector3 volumeCenter = VolumeWorldCenter;
        Vector3 volumeSize = VolumeWorldSize;
        float depth = Application.isPlaying ? WaterDepth : Mathf.Min(MaxWaterHeight, initialVolume / SurfaceArea);
        Vector3 center = new Vector3(volumeCenter.x, BottomWorldY + depth * 0.5f, volumeCenter.z);
        Vector3 size = new Vector3(volumeSize.x, Mathf.Max(depth, 0.02f), volumeSize.z);

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
                : BottomWorldY + Mathf.Min(MaxWaterHeight, initialVolume / SurfaceArea);
            Vector3 surfaceCenter = new Vector3(volumeCenter.x, surfaceY, volumeCenter.z);
            Vector3 surfaceSize = new Vector3(volumeSize.x, 0.02f, volumeSize.z);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(surfaceCenter, surfaceSize);
        }

    }

    private bool ShouldDrawGameViewDebug()
    {
        switch (debugOverlayScope)
        {
            case GameDebugController.WaterBasinDebugOverlayScope.AllTargets:
                return true;
            case GameDebugController.WaterBasinDebugOverlayScope.SpecificTarget:
                return debugOverlayTarget == this;
            case GameDebugController.WaterBasinDebugOverlayScope.SpecificConnectedGroup:
                return IsInDebugConnectedGroup(debugOverlayTarget);
            case GameDebugController.WaterBasinDebugOverlayScope.SelectedConnectedGroup:
                return IsInSelectedDebugConnectedGroup();
            default:
                return IsSelectedForDebug();
        }
    }

    private bool IsInDebugConnectedGroup(WaterBasinTarget groupRoot)
    {
        if (groupRoot == null)
        {
            return false;
        }

        if (groupRoot == this)
        {
            return true;
        }

        List<WaterBasinTarget> group = groupRoot.CollectConnectedGroup();
        return group.Contains(this);
    }

#if UNITY_EDITOR
    private bool IsSelectedForDebug()
    {
        RefreshSelectedDebugTargets();
        return selectedDebugTargets.Contains(this);
    }

    private bool IsInSelectedDebugConnectedGroup()
    {
        RefreshSelectedDebugTargets();
        BuildSelectedDebugGroupTargets();
        return selectedDebugGroupTargets.Contains(this);
    }

    private static void RefreshSelectedDebugTargets()
    {
        if (debugSelectionCacheFrame == Time.frameCount)
        {
            return;
        }

        debugSelectionCacheFrame = Time.frameCount;
        selectedDebugTargets.Clear();
        selectedDebugGroupTargets.Clear();
        selectedDebugGroupCacheBuilt = false;

        UnityEngine.Object[] selectedObjects = UnityEditor.Selection.objects;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            AddSelectedObjectDebugTargets(selectedObjects[i]);
        }
    }

    private static void AddSelectedObjectDebugTargets(UnityEngine.Object selectedObject)
    {
        if (selectedObject == null)
        {
            return;
        }

        WaterBasinTarget selectedTarget = selectedObject as WaterBasinTarget;
        if (selectedTarget != null)
        {
            selectedDebugTargets.Add(selectedTarget);
            return;
        }

        Component selectedComponent = selectedObject as Component;
        if (selectedComponent != null)
        {
            AddGameObjectDebugTargets(selectedComponent.gameObject);
            return;
        }

        GameObject selectedGameObject = selectedObject as GameObject;
        if (selectedGameObject != null)
        {
            AddGameObjectDebugTargets(selectedGameObject);
        }
    }

    private static void AddGameObjectDebugTargets(GameObject selectedGameObject)
    {
        if (selectedGameObject == null)
        {
            return;
        }

        WaterBasinTarget parentTarget = selectedGameObject.GetComponentInParent<WaterBasinTarget>(true);
        if (parentTarget != null)
        {
            selectedDebugTargets.Add(parentTarget);
        }

        WaterBasinTarget[] childTargets = selectedGameObject.GetComponentsInChildren<WaterBasinTarget>(true);
        for (int childIndex = 0; childIndex < childTargets.Length; childIndex++)
        {
            if (childTargets[childIndex] != null)
            {
                selectedDebugTargets.Add(childTargets[childIndex]);
            }
        }
    }

    private static void BuildSelectedDebugGroupTargets()
    {
        if (selectedDebugGroupCacheBuilt)
        {
            return;
        }

        selectedDebugGroupCacheBuilt = true;
        selectedDebugGroupTargets.Clear();

        foreach (WaterBasinTarget selectedTarget in selectedDebugTargets)
        {
            if (selectedTarget == null)
            {
                continue;
            }

            List<WaterBasinTarget> group = selectedTarget.CollectConnectedGroup();
            for (int i = 0; i < group.Count; i++)
            {
                if (group[i] != null)
                {
                    selectedDebugGroupTargets.Add(group[i]);
                }
            }
        }
    }
#else
    private bool IsSelectedForDebug()
    {
        return false;
    }

    private bool IsInSelectedDebugConnectedGroup()
    {
        return false;
    }
#endif

    private void OnGUI()
    {
        if (!Application.isPlaying || !debugOverlayEnabled || !showGameViewDebug || !ShouldDrawGameViewDebug())
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
        Vector3 volumeSize = VolumeWorldSize;
        Vector3 volumeCenter = VolumeWorldCenter;
        GUI.Label(new Rect(x + 8.0f, y + 20.0f, labelSize.x - 16.0f, 18.0f), $"Vol: {currentVolume:F2} / {Capacity:F2}  Fill: {Fill01:P0}");
        GUI.Label(new Rect(x + 8.0f, y + 38.0f, labelSize.x - 16.0f, 18.0f), $"Depth: {WaterDepth:F2}  Local SurfaceY: {waterSurfaceWorldY:F2}");
        GUI.Label(new Rect(x + 8.0f, y + 56.0f, labelSize.x - 16.0f, 18.0f), $"Group SurfaceY: {groupWaterSurfaceWorldY:F2}  Spread: {groupSurfaceSpread:F4}");
        GUI.Label(new Rect(x + 8.0f, y + 74.0f, labelSize.x - 16.0f, 18.0f), $"Area: {SurfaceArea:F2}  MaxH: {MaxWaterHeight:F2}");
        GUI.Label(new Rect(x + 8.0f, y + 92.0f, labelSize.x - 16.0f, 18.0f), $"Size: {volumeSize.x:F2},{volumeSize.y:F2},{volumeSize.z:F2}  Links: {connectedTargets.Count}");
        GUI.Label(new Rect(x + 8.0f, y + 110.0f, labelSize.x - 16.0f, 18.0f), $"Center: {volumeCenter.x:F2},{volumeCenter.y:F2},{volumeCenter.z:F2}");

        if (!showGroupDebug)
        {
            return;
        }

        RefreshConnectedGroupDebugState();
        float groupFill = groupCapacity <= Epsilon ? 0.0f : groupVolume / groupCapacity;
        GUI.Label(new Rect(x + 8.0f, y + 128.0f, labelSize.x - 16.0f, 18.0f), $"Group: {groupTargetCount}  {groupVolume:F2}/{groupCapacity:F2}  {groupFill:P0}");
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
