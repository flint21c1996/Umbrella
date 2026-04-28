using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private void TryPlacePrefab(Vector2 mousePosition, bool isDragPlacement = false)
    {
        // 실제 배치는 preview와 동일한 계산 경로를 타야
        // 클릭 직전 화면과 결과물이 어긋나지 않는다.
        if (selectedPrefab == null)
        {
            Debug.LogWarning("Map Tool: Assign a prefab before placing.");
            return;
        }

        if (!TryGetPlacementPosition(mousePosition, out Vector3 surfacePosition, out Vector3 surfaceNormal))
        {
            return;
        }

        Quaternion placementRotation = GetPlacementRotation(surfaceNormal);
        bool useNeighborSnap = snapToNeighbor || Event.current.alt;
        Vector3 snappedPosition = GetAlignedPreviewPosition(surfacePosition, placementRotation, surfaceNormal);
        if (useNeighborSnap)
        {
            GetNeighborSnappedTransform(snappedPosition, placementRotation, out snappedPosition, out placementRotation);
        }

        if (isDragPlacement &&
            lastDragPlacementPosition != Vector3.positiveInfinity &&
            Vector3.Distance(lastDragPlacementPosition, snappedPosition) <= 0.01f)
        {
            return;
        }

        if (IsCellOccupied(snappedPosition))
        {
            if (isDragPlacement)
            {
                lastDragPlacementPosition = snappedPosition;
                return;
            }

            Debug.LogWarning("Map Tool: A placement already exists at this snapped cell.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
        if (instance == null)
        {
            Debug.LogError("Map Tool: Failed to instantiate prefab.");
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Place Map Prefab");

        instance.transform.position = snappedPosition;
        instance.transform.rotation = placementRotation;
        instance.transform.localScale = GetPlacementLocalScale();

        ReplacePrimitiveCollidersWithMeshColliders(instance);
        ApplyPlacementMaterial(instance);

        Transform parent = placementParent != null ? placementParent : GetOrCreateRootParent();
        instance.transform.SetParent(parent, true);

        recentPlacements.Add(instance);
        lastDragPlacementPosition = snappedPosition;
        Selection.activeGameObject = instance;
    }

    private void UpdatePreviewState()
    {
        if (!placementEnabled || selectedPrefab == null)
        {
            UpdatePreviewVisibility(false);
            return;
        }

        EnsurePreviewInstance();
    }

    private void TryDeleteUnderCursor(Vector2 mousePosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 500.0f))
        {
            Undo.DestroyObjectImmediate(hit.collider.gameObject);
        }
    }

    private bool TryGetPlacementPosition(Vector2 mousePosition, out Vector3 snappedSurfacePosition, out Vector3 surfaceNormal)
    {
        // 현재는 support surface를 먼저 고르고,
        // neighbor / face anchor는 그 다음 단계에서 위치를 보정한다.
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        if (snapToSurface && TryGetBestSurfaceHit(ray, out RaycastHit hit))
        {
            Vector3 worldPoint = hit.point;
            float surfaceY = hit.point.y + GetCurrentHeight();
            snappedSurfacePosition = SnapToPlacement(new Vector3(worldPoint.x, surfaceY, worldPoint.z));
            surfaceNormal = hit.normal.normalized;
            lastHitColliderName = hit.collider != null ? $"{hit.collider.name} [{hit.collider.GetType().Name}]" : "None";
            lastSurfaceHitCollider = hit.collider;
            lastSurfaceHit = hit;
            hasLastSurfaceHit = true;
            lastSurfaceHitPoint = hit.point;
            lastSurfaceNormal = surfaceNormal;
            return true;
        }

        if (!snapToSurface && snapToNeighbor && TryGetFrontMostColliderHit(ray, out RaycastHit neighborHit))
        {
            // Surface snap을 끈 상태에서 Neighbor Snap을 쓰는 경우엔
            // height plane이 아니라 "마우스가 실제로 먼저 맞은 face"를 기준점으로 삼는다.
            // 그래야 옆면에 붙일 때 preview가 뒤 평면으로 밀리지 않는다.
            snappedSurfacePosition = neighborHit.point;
            surfaceNormal = neighborHit.normal.normalized;
            lastHitColliderName = neighborHit.collider != null ? $"{neighborHit.collider.name} [{neighborHit.collider.GetType().Name}]" : "None";
            lastSurfaceHitCollider = neighborHit.collider;
            lastSurfaceHit = neighborHit;
            hasLastSurfaceHit = true;
            lastSurfaceHitPoint = neighborHit.point;
            lastSurfaceNormal = surfaceNormal;
            return true;
        }

        if (!snapToSurface)
        {
            // 마우스 ray를 현재 높이 평면에 직접 쏘면 높이가 바뀔 때 교차점의 X/Z도 카메라 방향으로 밀린다.
            // 그래서 X/Z 기준점은 월드 기준 바닥 평면에서 고정하고, 높이 오프셋은 Y값에만 따로 적용한다.
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 worldPoint = ray.GetPoint(enter);
                snappedSurfacePosition = SnapToPlacement(new Vector3(worldPoint.x, GetCurrentHeight(), worldPoint.z));
                surfaceNormal = Vector3.up;
                lastHitColliderName = "Height Plane";
                lastSurfaceHitCollider = null;
                lastSurfaceHit = default;
                hasLastSurfaceHit = false;
                lastSurfaceHitPoint = worldPoint;
                lastSurfaceNormal = Vector3.up;
                return true;
            }
        }

        snappedSurfacePosition = Vector3.zero;
        surfaceNormal = Vector3.up;
        lastHitColliderName = "None";
        lastSurfaceHitCollider = null;
        lastSurfaceHit = default;
        hasLastSurfaceHit = false;
        lastSurfaceHitPoint = Vector3.zero;
        lastSurfaceNormal = Vector3.up;
        return false;
    }

    private bool TryGetBestSurfaceHit(Ray ray, out RaycastHit bestHit)
    {
        // "맨 앞의 유효한 윗면"만 support로 쓴다.
        // 옆면에 붙는 처리는 Surface가 아니라 Neighbor Snap이 담당한다.
        bool previousQueriesHitBackfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        RaycastHit[] hits = Physics.RaycastAll(ray, 500.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Physics.queriesHitBackfaces = previousQueriesHitBackfaces;

        if (hits.Length == 0)
        {
            bestHit = default;
            return false;
        }
        System.Array.Sort(hits, (lhs, rhs) => lhs.distance.CompareTo(rhs.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            if (previewInstance != null && hit.collider.transform.IsChildOf(previewInstance.transform))
            {
                continue;
            }

            if (hit.normal.y < 0.5f)
            {
                continue;
            }

            bestHit = hit;
            return true;
        }

        bestHit = default;
        return false;
    }

    private bool TryGetFrontMostColliderHit(Ray ray, out RaycastHit bestHit)
    {
        bool previousQueriesHitBackfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        RaycastHit[] hits = Physics.RaycastAll(ray, 500.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Physics.queriesHitBackfaces = previousQueriesHitBackfaces;

        if (hits.Length == 0)
        {
            bestHit = default;
            return false;
        }

        System.Array.Sort(hits, (lhs, rhs) => lhs.distance.CompareTo(rhs.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            if (previewInstance != null && hit.collider.transform.IsChildOf(previewInstance.transform))
            {
                continue;
            }

            bestHit = hit;
            return true;
        }

        bestHit = default;
        return false;
    }

    private Vector3 SnapToPlacement(Vector3 worldPoint)
    {
        if (!snapToGrid)
        {
            return worldPoint;
        }

        float snappedX = Mathf.Round(worldPoint.x / gridSize) * gridSize;
        float snappedZ = Mathf.Round(worldPoint.z / gridSize) * gridSize;

        return new Vector3(snappedX, worldPoint.y, snappedZ);
    }

    private float GetCurrentHeight()
    {
        return heightOffset;
    }

    private Quaternion GetPlacementRotation(Vector3 surfaceNormal)
    {
        if (!alignToSurfaceNormal)
        {
            return Quaternion.Euler(0.0f, currentRotationDegrees, 0.0f);
        }

        Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
        Quaternion localSpin = Quaternion.AngleAxis(currentRotationDegrees, Vector3.up);
        return surfaceRotation * localSpin;
    }

    private Vector3 GetAlignedPreviewPosition(Vector3 surfacePosition, Quaternion placementRotation, Vector3 surfaceNormal)
    {
        // preview mesh의 실제 바닥/접촉면이 surface에 닿도록 normal 방향 offset을 계산한다.
        // 중심점만 맞추면 긴 메쉬나 회전된 메쉬가 공중에 뜨는 문제가 생긴다.
        EnsurePreviewInstance();
        if (previewInstance == null)
        {
            return surfacePosition;
        }

        previewInstance.transform.rotation = placementRotation;
        previewInstance.transform.localScale = GetPlacementLocalScale();
        previewInstance.transform.position = surfacePosition;

        List<Vector3> previewPoints = new List<Vector3>();
        if (!TryCollectPreviewWorldPoints(previewPoints, out _, out _))
        {
            return surfacePosition;
        }

        float nearestFaceOffset = GetNearestFaceOffsetAlongNormal(previewPoints, surfacePosition, surfaceNormal);
        return surfacePosition + surfaceNormal * nearestFaceOffset;
    }

    private float GetNearestFaceOffsetAlongNormal(List<Vector3> worldPoints, Vector3 origin, Vector3 normal)
    {
        // 주어진 normal 방향에서 preview의 가장 "먼저 닿는 점"을 찾는다.
        // Face Anchor와 Surface Snap 둘 다 결국 이 offset 계산에 기대고 있다.
        float minProjection = float.PositiveInfinity;

        foreach (Vector3 point in worldPoints)
        {
            float projection = Vector3.Dot(point - origin, normal);
            minProjection = Mathf.Min(minProjection, projection);
        }

        if (float.IsPositiveInfinity(minProjection))
        {
            return 0.0f;
        }

        return -minProjection;
    }

}
