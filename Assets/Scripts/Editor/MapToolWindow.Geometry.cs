using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private Vector3[] GetBoundsCorners(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        return new Vector3[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
        };
    }

    private Vector3[] GetBoxColliderLocalCorners(BoxCollider boxCollider)
    {
        Vector3 halfSize = boxCollider.size * 0.5f;
        Vector3 center = boxCollider.center;

        return new Vector3[]
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
    }

    private Vector3[] GetTransformedBoundsCorners(Bounds localBounds, Matrix4x4 localToWorld)
    {
        Vector3[] localCorners = GetBoundsCorners(localBounds);
        Vector3[] worldCorners = new Vector3[localCorners.Length];

        for (int i = 0; i < localCorners.Length; i++)
        {
            worldCorners[i] = localToWorld.MultiplyPoint3x4(localCorners[i]);
        }

        return worldCorners;
    }

    private bool TryCollectPreviewWorldPoints(List<Vector3> points, out Vector3 center, out float radius)
    {
        points.Clear();

        MeshFilter[] meshFilters = previewInstance.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            Vector3[] vertices = meshFilter.sharedMesh.vertices;
            Matrix4x4 localToWorld = meshFilter.transform.localToWorldMatrix;
            foreach (Vector3 localVertex in vertices)
            {
                points.Add(localToWorld.MultiplyPoint3x4(localVertex));
            }
        }

        SkinnedMeshRenderer[] skinnedRenderers = previewInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer skinnedRenderer in skinnedRenderers)
        {
            Vector3[] worldCorners = GetTransformedBoundsCorners(skinnedRenderer.localBounds, skinnedRenderer.transform.localToWorldMatrix);
            foreach (Vector3 corner in worldCorners)
            {
                points.Add(corner);
            }
        }

        if (points.Count == 0)
        {
            center = Vector3.zero;
            radius = 0.0f;
            return false;
        }

        center = GetPointAverage(points);
        radius = GetMaxDistanceFromPoint(points, center);
        return true;
    }

    private Vector3 GetPointAverage(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 point in points)
        {
            sum += point;
        }

        return sum / points.Count;
    }

    private float GetMaxDistanceFromPoint(List<Vector3> points, Vector3 origin)
    {
        float maxDistance = 0.0f;
        foreach (Vector3 point in points)
        {
            maxDistance = Mathf.Max(maxDistance, Vector3.Distance(origin, point));
        }

        return maxDistance;
    }

}
