using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private void DrawMeshColliderBounds()
    {
        // 디버그 드로잉은 "무엇을 맞고 있는지"와 "어떤 face를 anchor로 보는지"를
        // 눈으로 바로 확인하게 해주는 용도다.
        bool drewCurrentHitCollider = false;

        if (previewInstance != null)
        {
            DrawPreviewMeshBounds(new Color(1.0f, 0.65f, 0.2f, 0.9f));
        }

        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (selectedObject == null)
            {
                continue;
            }

            MeshCollider[] meshColliders = selectedObject.GetComponentsInChildren<MeshCollider>(true);
            foreach (MeshCollider meshCollider in meshColliders)
            {
                if (meshCollider == null || !meshCollider.enabled || meshCollider.sharedMesh == null)
                {
                    continue;
                }

                bool isCurrentHit = meshCollider == lastSurfaceHitCollider;
                drewCurrentHitCollider |= isCurrentHit;
                DrawMeshColliderWire(
                    meshCollider,
                    isCurrentHit
                        ? new Color(0.2f, 0.9f, 1.0f, 1.0f)
                        : new Color(1.0f, 0.65f, 0.2f, 0.9f)
                );

                Handles.Label(
                    meshCollider.bounds.center + Vector3.up * 0.1f,
                    isCurrentHit ? $"MeshCollider: {meshCollider.name} (Hit)" : $"MeshCollider: {meshCollider.name}"
                );
            }
        }

        if (!drewCurrentHitCollider && lastSurfaceHitCollider != null)
        {
            DrawColliderWire(lastSurfaceHitCollider, new Color(0.2f, 0.9f, 1.0f, 1.0f));
            Handles.Label(
                lastSurfaceHitCollider.bounds.center + Vector3.up * 0.15f,
                $"Current Hit: {lastSurfaceHitCollider.name} [{lastSurfaceHitCollider.GetType().Name}]"
            );
        }

        DrawHoveredFaceAnchor();
        DrawPlacementDebugLines();
    }

    private void DrawPlacementDebugLines()
    {
        // Surface / Face Anchor 계산이 어느 축 기준으로 돌아가는지 시각화한다.
        if (previewInstance == null || !previewInstance.activeInHierarchy)
        {
            return;
        }

        float axisLength = Mathf.Max(0.35f, gridSize * 0.75f);
        Vector3 origin = lastPreviewPosition;

        Vector3 rightAxis = lastPreviewRotation * Vector3.right;
        Vector3 forwardAxis = lastPreviewRotation * Vector3.forward;
        Vector3 upAxis = lastSurfaceNormal.sqrMagnitude > 0.000001f ? lastSurfaceNormal.normalized : Vector3.up;

        Handles.color = new Color(1.0f, 0.25f, 0.25f, 0.95f);
        Handles.DrawAAPolyLine(3.0f, origin, origin + rightAxis * axisLength);
        Handles.Label(origin + rightAxis * axisLength, "Right");

        Handles.color = new Color(0.25f, 0.55f, 1.0f, 0.95f);
        Handles.DrawAAPolyLine(3.0f, origin, origin + forwardAxis * axisLength);
        Handles.Label(origin + forwardAxis * axisLength, "Forward");

        Handles.color = new Color(0.25f, 1.0f, 0.35f, 0.95f);
        Handles.DrawAAPolyLine(3.0f, origin, origin + upAxis * axisLength);
        Handles.Label(origin + upAxis * axisLength, "Surface Normal");

        if (lastSurfaceHitPoint != Vector3.zero)
        {
            Handles.color = new Color(1.0f, 0.9f, 0.2f, 0.95f);
            Handles.DrawAAPolyLine(2.5f, lastSurfaceHitPoint, origin);
            Handles.SphereHandleCap(0, lastSurfaceHitPoint, Quaternion.identity, Mathf.Max(0.05f, gridSize * 0.08f), EventType.Repaint);
            Handles.Label(lastSurfaceHitPoint + Vector3.up * 0.08f, "Hit Point");
        }

        if (TryGetSurfaceTangentAxes(lastPreviewRotation, out Vector3 primaryAxis, out Vector3 secondaryAxis, out _))
        {
            Vector3 tangentOrigin = origin + upAxis * Mathf.Max(0.05f, gridSize * 0.08f);
            float tangentLength = Mathf.Max(0.25f, gridSize * 0.5f);

            Handles.color = new Color(1.0f, 0.7f, 0.2f, 0.9f);
            Handles.DrawAAPolyLine(2.5f, tangentOrigin, tangentOrigin + primaryAxis * tangentLength);
            Handles.Label(tangentOrigin + primaryAxis * tangentLength, "Primary Axis");

            Handles.color = new Color(1.0f, 0.35f, 0.9f, 0.9f);
            Handles.DrawAAPolyLine(2.5f, tangentOrigin, tangentOrigin + secondaryAxis * tangentLength);
            Handles.Label(tangentOrigin + secondaryAxis * tangentLength, "Secondary Axis");
        }

        if (lastUsedNeighborSnap)
        {
            Handles.color = new Color(0.2f, 1.0f, 1.0f, 0.95f);
            Handles.Label(origin + upAxis * (axisLength + 0.1f), $"Neighbor Snap: {neighborSnapMode}");
        }
    }

    private void DrawHoveredFaceAnchor()
    {
        // 현재 anchor로 선택된 coplanar face polygon을 그대로 보여준다.
        // triangle 하나가 아니라 "면 전체"가 잡혔는지 확인하는 데 중요하다.
        if (!hasHoveredFaceAnchor || hoveredFaceCollider == null || hoveredFacePoints.Count < 3)
        {
            return;
        }

        Color fillColor = new Color(1.0f, 0.25f, 0.25f, 0.18f);
        Color edgeColor = new Color(1.0f, 0.35f, 0.35f, 0.95f);
        List<Vector3> orderedPoints = GetOrderedFacePoints(hoveredFacePoints, hoveredFaceCenter, hoveredFaceNormal);

        Handles.color = fillColor;
        Handles.DrawAAConvexPolygon(orderedPoints.ToArray());

        Handles.color = edgeColor;
        List<Vector3> linePoints = new List<Vector3>(orderedPoints)
        {
            orderedPoints[0]
        };
        Handles.DrawAAPolyLine(3.0f, linePoints.ToArray());
        Handles.DrawAAPolyLine(2.0f, hoveredFaceCenter, hoveredFaceCenter + hoveredFaceNormal * Mathf.Max(0.2f, gridSize * 0.4f));
        Handles.Label(hoveredFaceCenter + hoveredFaceNormal * Mathf.Max(0.2f, gridSize * 0.45f), $"Face Anchor: {hoveredFaceCollider.name}");
    }

    private void DrawPreviewMeshBounds(Color color)
    {
        MeshFilter[] meshFilters = previewInstance.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            DrawWireMesh(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix, color);
        }

        SkinnedMeshRenderer[] skinnedRenderers = previewInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer skinnedRenderer in skinnedRenderers)
        {
            DrawTransformedBoundsWire(skinnedRenderer.localBounds, skinnedRenderer.transform.localToWorldMatrix, color);
        }
    }

    private void DrawMeshColliderWire(MeshCollider meshCollider, Color color)
    {
        DrawWireMesh(meshCollider.sharedMesh, meshCollider.transform.localToWorldMatrix, color);
    }

    private void DrawColliderWire(Collider collider, Color color)
    {
        if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
        {
            DrawMeshColliderWire(meshCollider, color);
            return;
        }

        if (collider is BoxCollider boxCollider)
        {
            DrawBoxColliderWire(boxCollider, color);
            return;
        }

        DrawBoundsWire(collider.bounds, color);
    }

    private void DrawBoxColliderWire(BoxCollider boxCollider, Color color)
    {
        Vector3[] localCorners = GetBoxColliderLocalCorners(boxCollider);
        Vector3[] worldCorners = new Vector3[localCorners.Length];
        Matrix4x4 localToWorld = boxCollider.transform.localToWorldMatrix;

        for (int i = 0; i < localCorners.Length; i++)
        {
            worldCorners[i] = localToWorld.MultiplyPoint3x4(localCorners[i]);
        }

        DrawCornerWire(worldCorners, color);
    }

    private void DrawTransformedBoundsWire(Bounds localBounds, Matrix4x4 localToWorld, Color color)
    {
        Vector3[] corners = GetTransformedBoundsCorners(localBounds, localToWorld);
        DrawCornerWire(corners, color);
    }

    private void DrawWireMesh(Mesh mesh, Matrix4x4 localToWorld, Color color)
    {
        if (mesh == null)
        {
            return;
        }

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        Handles.color = color;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = localToWorld.MultiplyPoint3x4(vertices[triangles[i]]);
            Vector3 b = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
            Vector3 c = localToWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);

            Handles.DrawAAPolyLine(1.5f, a, b);
            Handles.DrawAAPolyLine(1.5f, b, c);
            Handles.DrawAAPolyLine(1.5f, c, a);
        }
    }

    private void DrawBoundsWire(Bounds bounds, Color color)
    {
        Vector3[] corners = GetBoundsCorners(bounds);
        DrawCornerWire(corners, color);
    }

    private void DrawCornerWire(Vector3[] corners, Color color)
    {
        Handles.color = color;

        Handles.DrawAAPolyLine(2.0f, corners[0], corners[1], corners[3], corners[2], corners[0]);
        Handles.DrawAAPolyLine(2.0f, corners[4], corners[5], corners[7], corners[6], corners[4]);
        Handles.DrawAAPolyLine(2.0f, corners[0], corners[4]);
        Handles.DrawAAPolyLine(2.0f, corners[1], corners[5]);
        Handles.DrawAAPolyLine(2.0f, corners[2], corners[6]);
        Handles.DrawAAPolyLine(2.0f, corners[3], corners[7]);
    }

}
