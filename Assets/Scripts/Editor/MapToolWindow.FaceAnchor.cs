using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private void ClearHoveredFaceAnchor()
    {
        hasHoveredFaceAnchor = false;
        hoveredFaceCollider = null;
        hoveredFacePoints.Clear();
        hoveredFaceCenter = Vector3.zero;
        hoveredFaceNormal = Vector3.up;
        hoveredFaceHitPoint = Vector3.zero;
    }

    private bool TryGetFaceAnchorTransform(Vector3 targetPosition, Quaternion placementRotation, out Vector3 snappedPosition, out Quaternion snappedRotation)
    {
        // Face 모드의 핵심 흐름:
        // 1) anchor가 될 MeshCollider face를 찾고
        // 2) preview를 그 면에 밀착시킨 뒤
        // 3) face plane 안에서 추가 정렬을 수행한다.
        EnsurePreviewInstance();
        if (previewInstance == null)
        {
            snappedPosition = targetPosition;
            snappedRotation = placementRotation;
            ClearHoveredFaceAnchor();
            return false;
        }

        if (!TryCollectPreviewWorldPointsAt(targetPosition, placementRotation, out _, out Vector3 previewCenter, out float previewRadius))
        {
            snappedPosition = targetPosition;
            snappedRotation = placementRotation;
            ClearHoveredFaceAnchor();
            return false;
        }

        float searchDistance = previewRadius + neighborSnapDistance + faceAssistSnapDistance + gridSize;

        bool foundBestHit = TryGetSurfaceFaceAnchorHit(out RaycastHit bestHit);
        if (!foundBestHit)
        {
            foundBestHit = TryFindBestFaceAnchorHit(previewCenter, searchDistance, placementRotation, out bestHit);
        }

        if (!foundBestHit)
        {
            snappedPosition = targetPosition;
            snappedRotation = placementRotation;
            ClearHoveredFaceAnchor();
            return false;
        }

        if (bestHit.collider is not MeshCollider meshCollider || meshCollider.sharedMesh == null)
        {
            snappedPosition = targetPosition;
            snappedRotation = placementRotation;
            ClearHoveredFaceAnchor();
            return false;
        }

        if (!TryStoreHoveredFaceAnchor(meshCollider, bestHit))
        {
            snappedPosition = targetPosition;
            snappedRotation = placementRotation;
            ClearHoveredFaceAnchor();
            return false;
        }

        snappedRotation = placementRotation;
        if (autoAlignToNeighbor &&
            TryGetNeighborAlignedRotation(meshCollider, placementRotation, placementRotation * Vector3.up, out Quaternion alignedRotation, out _))
        {
            snappedRotation = alignedRotation;
        }

        if (!TryCollectPreviewWorldPointsAt(targetPosition, snappedRotation, out List<Vector3> rotatedPreviewPoints, out _, out _))
        {
            snappedPosition = targetPosition;
            return true;
        }

        float surfaceOffset = GetNearestFaceOffsetAlongNormal(rotatedPreviewPoints, hoveredFaceHitPoint, hoveredFaceNormal);
        snappedPosition = targetPosition + hoveredFaceNormal * surfaceOffset;

        if (TryCollectPreviewWorldPointsAt(snappedPosition, snappedRotation, out _, out _, out _) &&
            TryGetHoveredFacePlaneAxes(out Vector3 primaryAxis, out Vector3 secondaryAxis) &&
            TryCollectPreviewContactFacePoints(hoveredFaceHitPoint, hoveredFaceNormal, out List<Vector3> previewFacePoints))
        {
            if (hoveredFaceCollider == lastSurfaceHitCollider &&
                IsSupportFaceTooLargeForAnchor(previewFacePoints, hoveredFacePoints, hoveredFaceNormal))
            {
                snappedPosition = targetPosition;
                snappedRotation = placementRotation;
                ClearHoveredFaceAnchor();
                return false;
            }

            float planarSnapDistance = Mathf.Max(neighborSnapDistance + faceAssistSnapDistance, gridSize * 0.5f);
            Vector3 planarDelta = Vector3.zero;
            bool usedStrictPlanarSnap = false;

            if (TryGetBestPlanarFaceDelta(previewFacePoints, hoveredFacePoints, primaryAxis, planarSnapDistance, out float primaryExactDelta))
            {
                planarDelta += primaryAxis * primaryExactDelta;
                usedStrictPlanarSnap = true;
            }

            if (TryGetBestPlanarFaceDelta(previewFacePoints, hoveredFacePoints, secondaryAxis, planarSnapDistance, out float secondaryExactDelta))
            {
                planarDelta += secondaryAxis * secondaryExactDelta;
                usedStrictPlanarSnap = true;
            }

            if (!usedStrictPlanarSnap)
            {
                if (TryGetBestAssistDelta(previewFacePoints, hoveredFacePoints, primaryAxis, planarSnapDistance, out float primaryDelta))
                {
                    planarDelta += primaryAxis * primaryDelta;
                }

                if (TryGetBestAssistDelta(previewFacePoints, hoveredFacePoints, secondaryAxis, planarSnapDistance, out float secondaryDelta))
                {
                    planarDelta += secondaryAxis * secondaryDelta;
                }
            }

            snappedPosition += planarDelta;
        }

        return true;
    }

    private bool TryGetSurfaceFaceAnchorHit(out RaycastHit faceHit)
    {
        // support surface 자체가 이미 MeshCollider hit라면
        // 별도 탐색보다 그 face를 anchor로 재사용하는 편이 더 자연스럽다.
        faceHit = default;

        if (!hasLastSurfaceHit ||
            lastSurfaceHitCollider == null ||
            previewInstance == null ||
            lastSurfaceHitCollider.transform.IsChildOf(previewInstance.transform))
        {
            return false;
        }

        if (lastSurfaceHitCollider is not MeshCollider meshCollider || meshCollider.sharedMesh == null)
        {
            return false;
        }

        if (lastSurfaceHit.triangleIndex < 0)
        {
            return false;
        }

        faceHit = lastSurfaceHit;
        return true;
    }

    private bool TryFindBestFaceAnchorHit(Vector3 previewCenter, float searchDistance, Quaternion placementRotation, out RaycastHit bestHit)
    {
        // preview 주변 여섯 방향으로 ray를 쏴서,
        // 각 방향에서 가장 앞에 있는 유효한 MeshCollider face만 후보로 본다.
        Vector3 rightAxis = placementRotation * Vector3.right;
        Vector3 forwardAxis = placementRotation * Vector3.forward;
        Vector3 upAxis = placementRotation * Vector3.up;

        Vector3[] directions =
        {
            rightAxis.normalized,
            -rightAxis.normalized,
            forwardAxis.normalized,
            -forwardAxis.normalized,
            upAxis.normalized,
            -upAxis.normalized,
        };

        bool foundHit = false;
        float bestScore = float.PositiveInfinity;
        bestHit = default;

        bool previousQueriesHitBackfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;

        foreach (Vector3 direction in directions)
        {
            Vector3 rayOrigin = previewCenter - direction * Mathf.Max(0.05f, gridSize * 0.1f);
            Ray ray = new Ray(rayOrigin, direction);
            RaycastHit[] hits = Physics.RaycastAll(ray, searchDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
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

                if (hit.collider is not MeshCollider meshCollider || meshCollider.sharedMesh == null)
                {
                    break;
                }

                float facing = Vector3.Dot(hit.normal.normalized, -direction);
                if (facing < 0.35f)
                {
                    break;
                }

                float score = hit.distance - facing * 0.05f;
                if (score >= bestScore)
                {
                    break;
                }

                bestHit = hit;
                bestScore = score;
                foundHit = true;
                break;
            }
        }

        Physics.queriesHitBackfaces = previousQueriesHitBackfaces;
        return foundHit;
    }
}
