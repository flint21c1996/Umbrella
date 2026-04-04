using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private bool TryGetBestAssistDelta(List<Vector3> previewPoints, List<Vector3> neighborPoints, Vector3 axis, float assistDistance, out float bestDelta)
    {
        GetProjectionInterval(previewPoints, axis, out float previewMin, out float previewMax);
        GetProjectionInterval(neighborPoints, axis, out float neighborMin, out float neighborMax);

        bool foundSnap = false;
        bestDelta = 0.0f;
        UpdateBestAssistDelta(neighborMin - previewMin, assistDistance, ref foundSnap, ref bestDelta);
        UpdateBestAssistDelta(neighborMax - previewMax, assistDistance, ref foundSnap, ref bestDelta);
        UpdateBestAssistDelta(neighborMin - previewMax, assistDistance, ref foundSnap, ref bestDelta);
        UpdateBestAssistDelta(neighborMax - previewMin, assistDistance, ref foundSnap, ref bestDelta);
        return foundSnap;
    }

    private void UpdateBestAssistDelta(float delta, float assistDistance, ref bool foundSnap, ref float bestDelta)
    {
        if (Mathf.Abs(delta) > assistDistance)
        {
            return;
        }

        if (foundSnap && Mathf.Abs(delta) >= Mathf.Abs(bestDelta))
        {
            return;
        }

        bestDelta = delta;
        foundSnap = true;
    }

    private bool TryGetNeighborAlignedRotation(Collider collider, Quaternion placementRotation, Vector3 upAxis, out Quaternion alignedRotation, out float angleDelta)
    {
        alignedRotation = placementRotation;
        angleDelta = 0.0f;

        Vector3 previewForward = Vector3.ProjectOnPlane(placementRotation * Vector3.forward, upAxis);
        Vector3 previewRight = Vector3.ProjectOnPlane(placementRotation * Vector3.right, upAxis);
        Vector3 neighborForward = Vector3.ProjectOnPlane(collider.transform.forward, upAxis);
        Vector3 neighborRight = Vector3.ProjectOnPlane(collider.transform.right, upAxis);

        if (previewForward.sqrMagnitude <= 0.000001f || previewRight.sqrMagnitude <= 0.000001f ||
            neighborForward.sqrMagnitude <= 0.000001f || neighborRight.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        previewForward.Normalize();
        previewRight.Normalize();
        neighborForward.Normalize();
        neighborRight.Normalize();

        Vector3[] sourceAxes = { previewForward, previewRight };
        Vector3[] targetAxes = { neighborForward, -neighborForward, neighborRight, -neighborRight };

        float bestAbsDelta = float.PositiveInfinity;
        float bestDelta = 0.0f;

        foreach (Vector3 sourceAxis in sourceAxes)
        {
            foreach (Vector3 targetAxis in targetAxes)
            {
                float delta = Vector3.SignedAngle(sourceAxis, targetAxis, upAxis);
                float absDelta = Mathf.Abs(delta);
                if (absDelta < bestAbsDelta)
                {
                    bestAbsDelta = absDelta;
                    bestDelta = delta;
                }
            }
        }

        if (bestAbsDelta > neighborAngleSnapThreshold)
        {
            return false;
        }

        alignedRotation = Quaternion.AngleAxis(bestDelta, upAxis) * placementRotation;
        angleDelta = bestAbsDelta;
        return true;
    }

    private bool TryCollectPreviewWorldPointsAt(Vector3 position, Quaternion rotation, out List<Vector3> points, out Vector3 center, out float radius)
    {
        previewInstance.transform.position = position;
        previewInstance.transform.rotation = rotation;
        points = new List<Vector3>();
        return TryCollectPreviewWorldPoints(points, out center, out radius);
    }

    private bool TryGetSurfaceTangentAxes(Quaternion placementRotation, out Vector3 primaryAxis, out Vector3 secondaryAxis, out Vector3 upAxis)
    {
        upAxis = lastSurfaceNormal.sqrMagnitude > 0.000001f ? lastSurfaceNormal.normalized : Vector3.up;
        primaryAxis = Vector3.ProjectOnPlane(placementRotation * Vector3.right, upAxis);

        if (primaryAxis.sqrMagnitude <= 0.000001f)
        {
            primaryAxis = Vector3.ProjectOnPlane(Vector3.right, upAxis);
        }

        if (primaryAxis.sqrMagnitude <= 0.000001f)
        {
            secondaryAxis = Vector3.zero;
            return false;
        }

        primaryAxis.Normalize();
        secondaryAxis = Vector3.Cross(upAxis, primaryAxis).normalized;
        return secondaryAxis.sqrMagnitude > 0.000001f;
    }

    private bool TryGetBestFaceSnapDelta(List<Vector3> previewPoints, List<Vector3> neighborPoints, Vector3 axis, Vector3 overlapAxis, Vector3 upAxis, out float bestDelta)
    {
        GetProjectionInterval(previewPoints, axis, out float previewMin, out float previewMax);
        GetProjectionInterval(neighborPoints, axis, out float neighborMin, out float neighborMax);
        GetProjectionInterval(previewPoints, overlapAxis, out float previewOverlapMin, out float previewOverlapMax);
        GetProjectionInterval(neighborPoints, overlapAxis, out float neighborOverlapMin, out float neighborOverlapMax);
        GetProjectionInterval(previewPoints, upAxis, out float previewUpMin, out float previewUpMax);
        GetProjectionInterval(neighborPoints, upAxis, out float neighborUpMin, out float neighborUpMax);

        float verticalTolerance = Mathf.Max(neighborSnapDistance, faceAssistSnapDistance);

        if (!IntervalsOverlap(previewOverlapMin, previewOverlapMax, neighborOverlapMin, neighborOverlapMax) ||
            !IntervalsOverlapOrWithinDistance(previewUpMin, previewUpMax, neighborUpMin, neighborUpMax, verticalTolerance))
        {
            bestDelta = 0.0f;
            return false;
        }

        bool foundSnap = false;
        bestDelta = 0.0f;
        UpdateBestAxisSnap(neighborMin - previewMax, ref foundSnap, ref bestDelta);
        UpdateBestAxisSnap(neighborMax - previewMin, ref foundSnap, ref bestDelta);
        return foundSnap;
    }

    private bool IntervalsOverlapOrWithinDistance(float minA, float maxA, float minB, float maxB, float tolerance)
    {
        if (IntervalsOverlap(minA, maxA, minB, maxB))
        {
            return true;
        }

        if (maxA < minB)
        {
            return (minB - maxA) <= tolerance;
        }

        if (maxB < minA)
        {
            return (minA - maxB) <= tolerance;
        }

        return false;
    }

    private void GetProjectionInterval(List<Vector3> points, Vector3 axis, out float minProjection, out float maxProjection)
    {
        minProjection = float.PositiveInfinity;
        maxProjection = float.NegativeInfinity;

        foreach (Vector3 point in points)
        {
            float projection = Vector3.Dot(point, axis);
            minProjection = Mathf.Min(minProjection, projection);
            maxProjection = Mathf.Max(maxProjection, projection);
        }
    }

    private bool TryCollectColliderWorldPoints(Collider collider, List<Vector3> points)
    {
        points.Clear();

        if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
        {
            Vector3[] vertices = meshCollider.sharedMesh.vertices;
            Matrix4x4 localToWorld = meshCollider.transform.localToWorldMatrix;
            foreach (Vector3 localVertex in vertices)
            {
                points.Add(localToWorld.MultiplyPoint3x4(localVertex));
            }

            return points.Count > 0;
        }

        if (collider is BoxCollider boxCollider)
        {
            Vector3 halfSize = boxCollider.size * 0.5f;
            Vector3 center = boxCollider.center;
            Vector3[] localCorners =
            {
                center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
                center + new Vector3(-halfSize.x, -halfSize.y,  halfSize.z),
                center + new Vector3(-halfSize.x,  halfSize.y, -halfSize.z),
                center + new Vector3(-halfSize.x,  halfSize.y,  halfSize.z),
                center + new Vector3( halfSize.x, -halfSize.y, -halfSize.z),
                center + new Vector3( halfSize.x, -halfSize.y,  halfSize.z),
                center + new Vector3( halfSize.x,  halfSize.y, -halfSize.z),
                center + new Vector3( halfSize.x,  halfSize.y,  halfSize.z),
            };

            Matrix4x4 localToWorld = boxCollider.transform.localToWorldMatrix;
            foreach (Vector3 localCorner in localCorners)
            {
                points.Add(localToWorld.MultiplyPoint3x4(localCorner));
            }

            return true;
        }

        return false;
    }

    private Vector3 GetAxisNeighborSnappedPosition(Vector3 targetPosition, Quaternion placementRotation)
    {
        EnsurePreviewInstance();
        if (previewInstance == null)
        {
            return targetPosition;
        }

        previewInstance.transform.position = targetPosition;
        previewInstance.transform.rotation = placementRotation;

        if (!TryGetObjectBounds(previewInstance, out Bounds previewBounds))
        {
            return targetPosition;
        }

        float searchRadius = Mathf.Max(previewBounds.extents.x, previewBounds.extents.z) + neighborSnapDistance + gridSize;
        Collider[] nearbyColliders = Physics.OverlapSphere(
            previewBounds.center,
            searchRadius,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        Vector3 bestPosition = targetPosition;
        bool foundSnapX = false;
        bool foundSnapZ = false;
        float bestDeltaX = neighborSnapDistance;
        float bestDeltaZ = neighborSnapDistance;

        foreach (Collider collider in nearbyColliders)
        {
            if (collider == null ||
                collider == lastSurfaceHitCollider ||
                collider.transform.IsChildOf(previewInstance.transform))
            {
                continue;
            }

            Bounds neighborBounds = collider.bounds;
            bool overlapY = IntervalsOverlap(previewBounds.min.y, previewBounds.max.y, neighborBounds.min.y, neighborBounds.max.y);
            if (!overlapY)
            {
                continue;
            }

            bool overlapZ = IntervalsOverlap(previewBounds.min.z, previewBounds.max.z, neighborBounds.min.z, neighborBounds.max.z);
            bool overlapX = IntervalsOverlap(previewBounds.min.x, previewBounds.max.x, neighborBounds.min.x, neighborBounds.max.x);

            if (overlapZ)
            {
                UpdateBestAxisSnap(neighborBounds.min.x - previewBounds.max.x, ref foundSnapX, ref bestDeltaX);
                UpdateBestAxisSnap(neighborBounds.max.x - previewBounds.min.x, ref foundSnapX, ref bestDeltaX);
            }

            if (overlapX)
            {
                UpdateBestAxisSnap(neighborBounds.min.z - previewBounds.max.z, ref foundSnapZ, ref bestDeltaZ);
                UpdateBestAxisSnap(neighborBounds.max.z - previewBounds.min.z, ref foundSnapZ, ref bestDeltaZ);
            }
        }

        if (foundSnapX)
        {
            bestPosition.x += bestDeltaX;
        }

        if (foundSnapZ)
        {
            bestPosition.z += bestDeltaZ;
        }

        return bestPosition;
    }

    private bool IntervalsOverlap(float minA, float maxA, float minB, float maxB)
    {
        return maxA >= minB && maxB >= minA;
    }

    private void UpdateBestAxisSnap(float delta, ref bool foundSnap, ref float bestDelta)
    {
        if (Mathf.Abs(delta) > neighborSnapDistance)
        {
            return;
        }

        if (foundSnap && Mathf.Abs(delta) >= Mathf.Abs(bestDelta))
        {
            return;
        }

        bestDelta = delta;
        foundSnap = true;
    }
}
