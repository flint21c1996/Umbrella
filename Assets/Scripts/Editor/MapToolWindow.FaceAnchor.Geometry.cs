using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private bool TryStoreHoveredFaceAnchor(MeshCollider meshCollider, RaycastHit hit)
    {
        // triangle 하나만 저장하면 "면 전체"가 아니라 삼각형 한 장만 잡힌다.
        // 그래서 같은 plane에 있는 coplanar triangle을 묶어서 anchor face로 확장한다.
        if (meshCollider.sharedMesh == null || hit.triangleIndex < 0)
        {
            return false;
        }

        int triangleStart = hit.triangleIndex * 3;
        int[] triangles = meshCollider.sharedMesh.triangles;
        Vector3[] vertices = meshCollider.sharedMesh.vertices;

        if (triangleStart + 2 >= triangles.Length)
        {
            return false;
        }

        Matrix4x4 localToWorld = meshCollider.transform.localToWorldMatrix;
        Vector3 a = localToWorld.MultiplyPoint3x4(vertices[triangles[triangleStart]]);
        Vector3 b = localToWorld.MultiplyPoint3x4(vertices[triangles[triangleStart + 1]]);
        Vector3 c = localToWorld.MultiplyPoint3x4(vertices[triangles[triangleStart + 2]]);

        Vector3 triangleNormal = Vector3.Cross(b - a, c - a).normalized;
        if (triangleNormal.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        if (Vector3.Dot(triangleNormal, hit.normal) < 0.0f)
        {
            triangleNormal = -triangleNormal;
        }

        hoveredFacePoints.Clear();
        float planeTolerance = Mathf.Max(0.005f, gridSize * 0.01f);
        float normalTolerance = 0.995f;
        float planeDistance = Vector3.Dot(triangleNormal, a);

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 wa = localToWorld.MultiplyPoint3x4(vertices[triangles[i]]);
            Vector3 wb = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
            Vector3 wc = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);

            Vector3 candidateNormal = Vector3.Cross(wb - wa, wc - wa).normalized;
            if (candidateNormal.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            if (Vector3.Dot(candidateNormal, triangleNormal) < normalTolerance)
            {
                continue;
            }

            float candidatePlaneDistance = Vector3.Dot(triangleNormal, wa);
            if (Mathf.Abs(candidatePlaneDistance - planeDistance) > planeTolerance)
            {
                continue;
            }

            AddUniqueFacePoint(hoveredFacePoints, wa, planeTolerance);
            AddUniqueFacePoint(hoveredFacePoints, wb, planeTolerance);
            AddUniqueFacePoint(hoveredFacePoints, wc, planeTolerance);
        }

        if (hoveredFacePoints.Count < 3)
        {
            hoveredFacePoints.Clear();
            hoveredFacePoints.Add(a);
            hoveredFacePoints.Add(b);
            hoveredFacePoints.Add(c);
        }

        hoveredFaceCenter = GetPointAverage(hoveredFacePoints);
        hoveredFaceNormal = triangleNormal;
        hoveredFaceHitPoint = hit.point;
        hoveredFaceCollider = meshCollider;
        hasHoveredFaceAnchor = true;
        return true;
    }

    private void AddUniqueFacePoint(List<Vector3> points, Vector3 point, float tolerance)
    {
        // coplanar triangle을 모을 때 동일 vertex가 여러 번 들어오는 것을 막는다.
        float sqrTolerance = tolerance * tolerance;
        foreach (Vector3 existingPoint in points)
        {
            if ((existingPoint - point).sqrMagnitude <= sqrTolerance)
            {
                return;
            }
        }

        points.Add(point);
    }

    private bool TryGetHoveredFacePlaneAxes(out Vector3 primaryAxis, out Vector3 secondaryAxis)
    {
        // hovered face polygon의 가장 긴 edge를 primary axis로 보고,
        // 나머지 면 내 정렬은 secondary axis로 처리한다.
        primaryAxis = Vector3.zero;
        secondaryAxis = Vector3.zero;

        if (hoveredFacePoints.Count < 2)
        {
            return false;
        }

        List<Vector3> orderedPoints = GetOrderedFacePoints(hoveredFacePoints, hoveredFaceCenter, hoveredFaceNormal);
        float bestEdgeLength = 0.0f;

        for (int i = 0; i < orderedPoints.Count; i++)
        {
            Vector3 start = orderedPoints[i];
            Vector3 end = orderedPoints[(i + 1) % orderedPoints.Count];
            Vector3 edge = Vector3.ProjectOnPlane(end - start, hoveredFaceNormal);
            float edgeLength = edge.magnitude;

            if (edgeLength <= bestEdgeLength || edgeLength <= 0.0001f)
            {
                continue;
            }

            bestEdgeLength = edgeLength;
            primaryAxis = edge / edgeLength;
        }

        if (bestEdgeLength <= 0.0001f)
        {
            return false;
        }

        secondaryAxis = Vector3.Cross(hoveredFaceNormal, primaryAxis).normalized;
        return secondaryAxis.sqrMagnitude > 0.000001f;
    }

    private List<Vector3> GetOrderedFacePoints(List<Vector3> points, Vector3 center, Vector3 normal)
    {
        // Convex polygon을 그리거나 edge를 순회하려면 점 순서가 필요하다.
        // face plane 위에서 극각 정렬로 순서를 맞춘다.
        Vector3 basisX = Vector3.ProjectOnPlane(Vector3.right, normal);
        if (basisX.sqrMagnitude <= 0.000001f)
        {
            basisX = Vector3.ProjectOnPlane(Vector3.forward, normal);
        }

        basisX.Normalize();
        Vector3 basisY = Vector3.Cross(normal, basisX).normalized;

        List<Vector3> orderedPoints = new(points);
        orderedPoints.Sort((lhs, rhs) =>
        {
            Vector3 lhsOffset = lhs - center;
            Vector3 rhsOffset = rhs - center;
            float lhsAngle = Mathf.Atan2(Vector3.Dot(lhsOffset, basisY), Vector3.Dot(lhsOffset, basisX));
            float rhsAngle = Mathf.Atan2(Vector3.Dot(rhsOffset, basisY), Vector3.Dot(rhsOffset, basisX));
            return lhsAngle.CompareTo(rhsAngle);
        });

        return orderedPoints;
    }

    private bool TryCollectPreviewContactFacePoints(Vector3 planePoint, Vector3 planeNormal, out List<Vector3> facePoints)
    {
        // support face에 실제로 닿게 될 preview 쪽 면을 추정한다.
        // 성공하면 "preview contact face vs hovered face" 정렬이 가능해진다.
        facePoints = new List<Vector3>();
        if (previewInstance == null)
        {
            return false;
        }

        float bestScore = float.PositiveInfinity;
        List<Vector3> bestFacePoints = null;

        MeshFilter[] meshFilters = previewInstance.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            if (TryCollectCoplanarPreviewFacePoints(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix, planePoint, planeNormal, out List<Vector3> candidateFacePoints, out float candidateScore) &&
                candidateScore < bestScore)
            {
                bestScore = candidateScore;
                bestFacePoints = candidateFacePoints;
            }
        }

        if (bestFacePoints != null && bestFacePoints.Count >= 3)
        {
            facePoints.AddRange(bestFacePoints);
            return true;
        }

        if (!TryCollectPreviewWorldPointsAt(previewInstance.transform.position, previewInstance.transform.rotation, out List<Vector3> previewPoints, out _, out _))
        {
            return false;
        }

        float minProjection = float.PositiveInfinity;
        foreach (Vector3 point in previewPoints)
        {
            float projection = Vector3.Dot(point - planePoint, planeNormal);
            if (projection < minProjection)
            {
                minProjection = projection;
            }
        }

        float tolerance = Mathf.Max(0.005f, gridSize * 0.01f);
        foreach (Vector3 point in previewPoints)
        {
            float projection = Vector3.Dot(point - planePoint, planeNormal);
            if (Mathf.Abs(projection - minProjection) <= tolerance)
            {
                AddUniqueFacePoint(facePoints, point, tolerance);
            }
        }

        return facePoints.Count >= 2;
    }

    private bool IsSupportFaceTooLargeForAnchor(List<Vector3> previewFacePoints, List<Vector3> supportFacePoints, Vector3 faceNormal)
    {
        // 맨바닥 Plane처럼 너무 큰 면을 무조건 anchor로 쓰면
        // support가 과하게 강해져 neighbor face 의도가 죽는다.
        if (previewFacePoints.Count < 3 || supportFacePoints.Count < 3)
        {
            return false;
        }

        if (!TryGetFacePlaneAxes(supportFacePoints, faceNormal, out Vector3 primaryAxis, out Vector3 secondaryAxis))
        {
            return false;
        }

        GetProjectionInterval(previewFacePoints, primaryAxis, out float previewPrimaryMin, out float previewPrimaryMax);
        GetProjectionInterval(previewFacePoints, secondaryAxis, out float previewSecondaryMin, out float previewSecondaryMax);
        GetProjectionInterval(supportFacePoints, primaryAxis, out float supportPrimaryMin, out float supportPrimaryMax);
        GetProjectionInterval(supportFacePoints, secondaryAxis, out float supportSecondaryMin, out float supportSecondaryMax);

        float previewPrimarySize = Mathf.Max(0.0001f, previewPrimaryMax - previewPrimaryMin);
        float previewSecondarySize = Mathf.Max(0.0001f, previewSecondaryMax - previewSecondaryMin);
        float supportPrimarySize = supportPrimaryMax - supportPrimaryMin;
        float supportSecondarySize = supportSecondaryMax - supportSecondaryMin;

        const float supportScaleThreshold = 4.0f;
        return supportPrimarySize > previewPrimarySize * supportScaleThreshold ||
               supportSecondarySize > previewSecondarySize * supportScaleThreshold;
    }

    private bool TryGetFacePlaneAxes(List<Vector3> facePoints, Vector3 normal, out Vector3 primaryAxis, out Vector3 secondaryAxis)
    {
        // hovered face뿐 아니라 preview contact face 쪽에서도
        // 동일한 기준축을 뽑기 위해 공통 helper로 분리했다.
        primaryAxis = Vector3.zero;
        secondaryAxis = Vector3.zero;

        if (facePoints.Count < 2)
        {
            return false;
        }

        Vector3 center = GetPointAverage(facePoints);
        List<Vector3> orderedPoints = GetOrderedFacePoints(facePoints, center, normal);
        float bestEdgeLength = 0.0f;

        for (int i = 0; i < orderedPoints.Count; i++)
        {
            Vector3 start = orderedPoints[i];
            Vector3 end = orderedPoints[(i + 1) % orderedPoints.Count];
            Vector3 edge = Vector3.ProjectOnPlane(end - start, normal);
            float edgeLength = edge.magnitude;

            if (edgeLength <= bestEdgeLength || edgeLength <= 0.0001f)
            {
                continue;
            }

            bestEdgeLength = edgeLength;
            primaryAxis = edge / edgeLength;
        }

        if (bestEdgeLength <= 0.0001f)
        {
            return false;
        }

        secondaryAxis = Vector3.Cross(normal, primaryAxis).normalized;
        return secondaryAxis.sqrMagnitude > 0.000001f;
    }

    private bool TryCollectCoplanarPreviewFacePoints(
        Mesh mesh,
        Matrix4x4 localToWorld,
        Vector3 planePoint,
        Vector3 planeNormal,
        out List<Vector3> facePoints,
        out float faceScore)
    {
        // preview 메쉬에서 plane normal과 가장 잘 마주보는 face를 찾고,
        // 그 face와 같은 plane에 있는 삼각형들을 contact face로 묶는다.
        facePoints = null;
        faceScore = float.PositiveInfinity;

        if (mesh == null)
        {
            return false;
        }

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        if (triangles == null || triangles.Length < 3)
        {
            return false;
        }

        int bestTriangleStart = -1;
        Vector3 bestTriangleNormal = Vector3.zero;
        float bestTriangleScore = float.PositiveInfinity;
        const float normalFacingThreshold = 0.75f;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = localToWorld.MultiplyPoint3x4(vertices[triangles[i]]);
            Vector3 b = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
            Vector3 c = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);

            Vector3 triangleNormal = Vector3.Cross(b - a, c - a).normalized;
            if (triangleNormal.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            float facing = Vector3.Dot(triangleNormal, -planeNormal);
            if (facing < normalFacingThreshold)
            {
                continue;
            }

            float pa = Mathf.Abs(Vector3.Dot(a - planePoint, planeNormal));
            float pb = Mathf.Abs(Vector3.Dot(b - planePoint, planeNormal));
            float pc = Mathf.Abs(Vector3.Dot(c - planePoint, planeNormal));
            float candidateScore = pa + pb + pc - facing * 0.05f;

            if (candidateScore >= bestTriangleScore)
            {
                continue;
            }

            bestTriangleScore = candidateScore;
            bestTriangleStart = i;
            bestTriangleNormal = triangleNormal;
        }

        if (bestTriangleStart < 0)
        {
            return false;
        }

        Vector3 bestA = localToWorld.MultiplyPoint3x4(vertices[triangles[bestTriangleStart]]);
        float planeTolerance = Mathf.Max(0.005f, gridSize * 0.01f);
        float normalTolerance = 0.995f;
        float facePlaneDistance = Vector3.Dot(bestTriangleNormal, bestA);

        List<Vector3> collectedPoints = new();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = localToWorld.MultiplyPoint3x4(vertices[triangles[i]]);
            Vector3 b = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
            Vector3 c = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);

            Vector3 triangleNormal = Vector3.Cross(b - a, c - a).normalized;
            if (triangleNormal.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            if (Vector3.Dot(triangleNormal, bestTriangleNormal) < normalTolerance)
            {
                continue;
            }

            float candidatePlaneDistance = Vector3.Dot(bestTriangleNormal, a);
            if (Mathf.Abs(candidatePlaneDistance - facePlaneDistance) > planeTolerance)
            {
                continue;
            }

            AddUniqueFacePoint(collectedPoints, a, planeTolerance);
            AddUniqueFacePoint(collectedPoints, b, planeTolerance);
            AddUniqueFacePoint(collectedPoints, c, planeTolerance);
        }

        if (collectedPoints.Count < 3)
        {
            return false;
        }

        facePoints = collectedPoints;
        faceScore = bestTriangleScore;
        return true;
    }

    private bool TryGetBestPlanarFaceDelta(List<Vector3> previewFacePoints, List<Vector3> targetFacePoints, Vector3 axis, float tolerance, out float bestDelta)
    {
        bestDelta = 0.0f;

        GetProjectionInterval(previewFacePoints, axis, out float previewMin, out float previewMax);
        GetProjectionInterval(targetFacePoints, axis, out float targetMin, out float targetMax);

        bool found = false;
        UpdateBestPlanarDelta(targetMin - previewMin, tolerance, ref found, ref bestDelta);
        UpdateBestPlanarDelta(targetMax - previewMax, tolerance, ref found, ref bestDelta);
        UpdateBestPlanarDelta(targetMin - previewMax, tolerance, ref found, ref bestDelta);
        UpdateBestPlanarDelta(targetMax - previewMin, tolerance, ref found, ref bestDelta);

        return found;
    }

    private void UpdateBestPlanarDelta(float delta, float tolerance, ref bool found, ref float bestDelta)
    {
        if (Mathf.Abs(delta) > tolerance)
        {
            return;
        }

        if (found && Mathf.Abs(delta) >= Mathf.Abs(bestDelta))
        {
            return;
        }

        bestDelta = delta;
        found = true;
    }
}
