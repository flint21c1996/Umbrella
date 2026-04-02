using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private void TryPlacePrefab(Vector2 mousePosition, bool isDragPlacement = false)
    {
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

        ReplacePrimitiveCollidersWithMeshColliders(instance);

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

        if (!snapToSurface)
        {
            Plane plane = new Plane(Vector3.up, new Vector3(0.0f, GetCurrentHeight(), 0.0f));
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 worldPoint = ray.GetPoint(enter);
                snappedSurfacePosition = SnapToPlacement(worldPoint);
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

            if (topSurfaceOnly && hit.normal.y < 0.5f)
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
        return heightLevel * heightStep;
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
        EnsurePreviewInstance();
        if (previewInstance == null)
        {
            return surfacePosition;
        }

        previewInstance.transform.rotation = placementRotation;
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
