using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private void GetNeighborSnappedTransform(Vector3 targetPosition, Quaternion placementRotation, out Vector3 snappedPosition, out Quaternion snappedRotation)
    {
        // Neighbor snap은 mode별로 성격이 다르다.
        // Face는 face anchor 흐름을 타고, Edge는 두 축 strict 정렬,
        // Vertex는 현재 축 기반 fallback을 유지한다.
        if (neighborSnapMode == NeighborSnapMode.Face)
        {
            if (TryGetFaceAnchorTransform(targetPosition, placementRotation, out snappedPosition, out snappedRotation))
            {
                return;
            }

            ClearHoveredFaceAnchor();
            snappedPosition = targetPosition;
            snappedRotation = placementRotation;
            return;
        }

        if (neighborSnapMode == NeighborSnapMode.Edge &&
            TryGetStrictPlaneNeighborSnappedTransform(targetPosition, placementRotation, out snappedPosition, out snappedRotation))
        {
            return;
        }

        ClearHoveredFaceAnchor();
        snappedPosition = GetAxisNeighborSnappedPosition(targetPosition, placementRotation);
        snappedRotation = placementRotation;
    }

    private bool TryGetStrictPlaneNeighborSnappedTransform(Vector3 targetPosition, Quaternion placementRotation, out Vector3 snappedPosition, out Quaternion snappedRotation)
    {
        // Edge 모드는 support plane 위에서 primary / secondary 두 축을 동시에 맞춰
        // "면은 붙었는데 코너가 안 맞는" 상태를 줄이는 용도다.
        EnsurePreviewInstance();
        if (previewInstance == null)
        {
            snappedPosition = targetPosition;
            snappedRotation = placementRotation;
            return false;
        }

        if (!TryGetSurfaceTangentAxes(placementRotation, out _, out _, out Vector3 upAxis))
        {
            snappedPosition = targetPosition;
            snappedRotation = placementRotation;
            return false;
        }

        if (!TryCollectPreviewWorldPointsAt(targetPosition, placementRotation, out _, out Vector3 previewCenter, out float previewRadius))
        {
            snappedPosition = targetPosition;
            snappedRotation = placementRotation;
            return false;
        }

        float searchRadius = previewRadius + neighborSnapDistance + faceAssistSnapDistance + gridSize;
        Collider[] nearbyColliders = Physics.OverlapSphere(
            previewCenter,
            searchRadius,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        Vector3 bestPosition = targetPosition;
        Quaternion bestRotation = placementRotation;
        bool foundSnap = false;
        float bestScore = float.PositiveInfinity;

        foreach (Collider collider in nearbyColliders)
        {
            if (collider == null ||
                collider == lastSurfaceHitCollider ||
                collider.transform.IsChildOf(previewInstance.transform))
            {
                continue;
            }

            List<Vector3> neighborPoints = new();
            if (!TryCollectColliderWorldPoints(collider, neighborPoints))
            {
                continue;
            }

            Quaternion candidateRotation = placementRotation;
            float rotationPenalty = 0.0f;
            if (autoAlignToNeighbor && TryGetNeighborAlignedRotation(collider, placementRotation, placementRotation * Vector3.up, out Quaternion alignedRotation, out float angleDelta))
            {
                candidateRotation = alignedRotation;
                rotationPenalty = angleDelta * 0.01f;
            }

            if (!TryCollectPreviewWorldPointsAt(targetPosition, candidateRotation, out List<Vector3> candidatePreviewPoints, out _, out _))
            {
                continue;
            }

            if (!TryGetSurfaceTangentAxes(candidateRotation, out Vector3 primaryAxis, out Vector3 secondaryAxis, out Vector3 candidateUpAxis))
            {
                continue;
            }

            if (!TryGetEdgeSnapCandidate(targetPosition, candidatePreviewPoints, neighborPoints, primaryAxis, secondaryAxis, candidateUpAxis, out Vector3 candidatePosition, out float candidateScore))
            {
                continue;
            }

            float totalScore = candidateScore + rotationPenalty;
            if (totalScore < bestScore)
            {
                bestPosition = candidatePosition;
                bestRotation = candidateRotation;
                bestScore = totalScore;
                foundSnap = true;
            }
        }

        snappedPosition = bestPosition;
        snappedRotation = bestRotation;
        return foundSnap;
    }

    private bool TryGetEdgeSnapCandidate(
        Vector3 targetPosition,
        List<Vector3> previewPoints,
        List<Vector3> neighborPoints,
        Vector3 primaryAxis,
        Vector3 secondaryAxis,
        Vector3 upAxis,
        out Vector3 candidatePosition,
        out float candidateScore)
    {
        // 같은 plane 위에서 두 축 delta를 각각 찾아 더하는 단순한 strict edge 정렬.
        candidatePosition = targetPosition;
        candidateScore = 0.0f;

        if (!TryGetBestFaceSnapDelta(previewPoints, neighborPoints, primaryAxis, secondaryAxis, upAxis, out float primaryDelta))
        {
            return false;
        }

        if (!TryGetBestFaceSnapDelta(previewPoints, neighborPoints, secondaryAxis, primaryAxis, upAxis, out float secondaryDelta))
        {
            return false;
        }

        candidatePosition += primaryAxis * primaryDelta;
        candidatePosition += secondaryAxis * secondaryDelta;
        candidateScore = Mathf.Abs(primaryDelta) + Mathf.Abs(secondaryDelta);
        return true;
    }
}
