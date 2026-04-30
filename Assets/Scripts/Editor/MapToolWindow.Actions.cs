using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private Transform GetOrCreateRootParent()
    {
        // 루트 부모를 고정해두면 Map Tool이 배치한 결과물만 한곳에 모여
        // 선택, 삭제, 점유 검사 흐름을 단순하게 유지할 수 있다.
        GameObject root = GameObject.Find(RootObjectName);
        if (root == null)
        {
            root = new GameObject(RootObjectName);
            Undo.RegisterCreatedObjectUndo(root, "Create Map Root");
        }

        return root.transform;
    }

    private void ApplyMaterialToSelection()
    {
        // 블록아웃 단계에서는 배치 직후 material을 여러 개 한 번에 갈아끼우는 일이 잦다.
        // 그래서 배치 툴 안에서 바로 material preset 적용까지 처리한다.
        if (selectedMaterial == null)
        {
            Debug.LogWarning("Map Tool: Assign a material preset before applying.");
            return;
        }

        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            ApplyMaterialToObject(selectedObject, selectedMaterial, "Apply Material Preset");
        }
    }

    private void ApplyPlacementMaterial(GameObject targetObject)
    {
        if (placementMaterial == null)
        {
            return;
        }

        ApplyMaterialToObject(targetObject, placementMaterial, "Apply Placement Material");
    }

    private void ApplyMaterialToObject(GameObject targetObject, Material material, string undoName)
    {
        if (targetObject == null || material == null)
        {
            return;
        }

        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            Undo.RecordObject(renderer, undoName);
            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
        }
    }

    private void SnapSelectionToGrid()
    {
        // 이미 배치한 오브젝트도 grid 규칙으로 다시 정리할 수 있어야
        // 손으로 만지다가 흐트러진 블록아웃을 빠르게 복구할 수 있다.
        foreach (Transform selectedTransform in Selection.transforms)
        {
            Undo.RecordObject(selectedTransform, "Snap To Grid");
            selectedTransform.position = SnapToPlacement(selectedTransform.position);

            Vector3 currentEuler = selectedTransform.eulerAngles;
            float snappedY = Mathf.Round(currentEuler.y / 90.0f) * 90.0f;
            selectedTransform.rotation = Quaternion.Euler(0.0f, snappedY, 0.0f);
            EditorUtility.SetDirty(selectedTransform);
        }
    }

    private void AddMeshCollidersToSelection()
    {
        // Mesh 기반 face anchor를 쓰려면 primitive collider보다 MeshCollider 쪽이
        // 실제 배치 결과와 훨씬 덜 어긋난다.
        if (Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("Map Tool: Select one or more objects before adding MeshColliders.");
            return;
        }

        int updatedCount = 0;

        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            updatedCount += ReplacePrimitiveCollidersWithMeshColliders(selectedObject);
        }

        Debug.Log($"Map Tool: Updated {updatedCount} MeshCollider component(s).");
    }

    private int ReplacePrimitiveCollidersWithMeshColliders(GameObject rootObject)
    {
        // Face Anchor / Mesh 기반 snap을 쓰려면 primitive collider보다
        // 실제 mesh를 읽는 collider가 훨씬 예측 가능하다.
        int updatedCount = 0;

        MeshFilter[] meshFilters = rootObject.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = Undo.AddComponent<MeshCollider>(meshFilter.gameObject);
            }
            else
            {
                Undo.RecordObject(meshCollider, "Update MeshCollider");
            }

            RemovePrimitiveColliders(meshFilter.gameObject);

            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;
            EditorUtility.SetDirty(meshCollider);
            updatedCount++;
        }

        SkinnedMeshRenderer[] skinnedRenderers = rootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer skinnedRenderer in skinnedRenderers)
        {
            if (skinnedRenderer.sharedMesh == null)
            {
                continue;
            }

            MeshCollider meshCollider = skinnedRenderer.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = Undo.AddComponent<MeshCollider>(skinnedRenderer.gameObject);
            }
            else
            {
                Undo.RecordObject(meshCollider, "Update MeshCollider");
            }

            RemovePrimitiveColliders(skinnedRenderer.gameObject);

            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = skinnedRenderer.sharedMesh;
            meshCollider.convex = false;
            EditorUtility.SetDirty(meshCollider);
            updatedCount++;
        }

        return updatedCount;
    }

    private void RemovePrimitiveColliders(GameObject targetObject)
    {
        // MeshCollider와 primitive collider가 같이 있으면 hit 결과가 섞여서
        // "무엇을 맞고 있는지" 예측하기 어려워진다.
        Collider[] colliders = targetObject.GetComponents<Collider>();
        foreach (Collider collider in colliders)
        {
            if (collider is MeshCollider)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(collider);
        }
    }

    private void DeleteSelectedObjects()
    {
        // 배치 모드에서는 즉시 지우는 흐름이 더 중요하므로
        // selection 삭제도 별도 액션으로 유지한다.
        if (Selection.gameObjects.Length == 0)
        {
            return;
        }

        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            Undo.DestroyObjectImmediate(selectedObject);
        }
    }

    private void SetSelectedPrefab(GameObject prefab)
    {
        if (selectedPrefab == prefab)
        {
            return;
        }

        // preview는 prefab별로 인스턴스를 재사용하므로,
        // 선택 prefab이 바뀌면 기존 preview를 정리하고 다시 만든다.
        selectedPrefab = prefab;
        hasLastPreviewTransform = false;
        hasScaleEditPose = false;
        hasHeightEditPose = false;
        DestroyPreviewInstance();
        Repaint();
    }

    private bool IsCellOccupied(Vector3 targetPosition, Quaternion targetRotation, Vector3 targetLocalScale)
    {
        // 배치를 막는 조건은 "같은 snapped 위치를 다시 찍는 경우"로만 둔다.
        // 실제 부피 겹침은 rough blockout이나 높이 차이 표현에 필요할 수 있으므로
        // 아래 UpdatePreviewOverlapState에서 경고/디버그 용도로만 따로 표시한다.
        Transform parent = placementParent != null ? placementParent : GameObject.Find(RootObjectName)?.transform;
        if (parent == null)
        {
            return false;
        }

        const float epsilon = 0.01f;

        foreach (Transform child in parent)
        {
            if (Vector3.Distance(child.position, targetPosition) <= epsilon)
            {
                return true;
            }
        }

        return false;
    }

    private bool UpdatePreviewOverlapState(Vector3 targetPosition, Quaternion targetRotation, Vector3 targetLocalScale)
    {
        lastPreviewOverlapCollider = null;
        lastOverlapColliderName = "None";

        if (!showOverlapWarning)
        {
            lastPreviewOverlapsPlacedObject = false;
            return false;
        }

        bool overlaps = TryFindPreviewOverlap(targetPosition, targetRotation, targetLocalScale, out Collider overlapCollider);
        if (overlaps)
        {
            lastPreviewOverlapCollider = overlapCollider;
            lastOverlapColliderName = overlapCollider != null ? overlapCollider.name : "Unknown";
        }

        lastPreviewOverlapsPlacedObject = overlaps;
        return overlaps;
    }

    private bool TryFindPreviewOverlap(Vector3 targetPosition, Quaternion targetRotation, Vector3 targetLocalScale, out Collider overlapCollider)
    {
        overlapCollider = null;

        // renderer.bounds / collider.bounds는 월드 축 AABB라서 회전된 블록에서 실제보다 크게 부풀 수 있다.
        // 그래서 preview와 주변 collider의 실제 world point를 모아, 각 오브젝트 축에 투영해 겹침을 확인한다.
        if (!TryCollectPreviewWorldPointsAt(targetPosition, targetRotation, targetLocalScale, out List<Vector3> previewPoints, out Vector3 previewCenter, out float previewRadius))
        {
            return false;
        }

        Collider[] nearbyColliders = Physics.OverlapSphere(
            previewCenter,
            previewRadius + gridSize * 0.25f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        float overlapTolerance = Mathf.Max(0.001f, gridSize * 0.001f);
        List<Vector3> neighborPoints = new();

        foreach (Collider collider in nearbyColliders)
        {
            if (collider == null ||
                previewInstance != null && collider.transform.IsChildOf(previewInstance.transform))
            {
                continue;
            }

            if (!TryCollectColliderWorldPoints(collider, neighborPoints))
            {
                neighborPoints.Clear();
                neighborPoints.AddRange(GetBoundsCorners(collider.bounds));
            }

            if (HasProjectedPointCloudOverlap(previewPoints, neighborPoints, targetRotation, collider.transform.rotation, overlapTolerance))
            {
                overlapCollider = collider;
                return true;
            }
        }

        return false;
    }

    private bool HasProjectedPointCloudOverlap(List<Vector3> lhs, List<Vector3> rhs, Quaternion lhsRotation, Quaternion rhsRotation, float tolerance)
    {
        Vector3[] axes =
        {
            lhsRotation * Vector3.right,
            lhsRotation * Vector3.up,
            lhsRotation * Vector3.forward,
            rhsRotation * Vector3.right,
            rhsRotation * Vector3.up,
            rhsRotation * Vector3.forward,
        };

        foreach (Vector3 rawAxis in axes)
        {
            if (rawAxis.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            Vector3 axis = rawAxis.normalized;
            GetProjectionInterval(lhs, axis, out float lhsMin, out float lhsMax);
            GetProjectionInterval(rhs, axis, out float rhsMin, out float rhsMax);

            // 한 축이라도 분리되어 있으면 실제 부피는 겹치지 않는다.
            if (Mathf.Min(lhsMax, rhsMax) - Mathf.Max(lhsMin, rhsMin) <= tolerance)
            {
                return false;
            }
        }

        return true;
    }

    private struct PlacementAxisContact
    {
        public bool HasMin;
        public bool HasMax;
        public float MinPlane;
        public float MaxPlane;
    }

    private bool ApplyContactAnchoredScale(ref Vector3 targetPosition, Quaternion targetRotation, Vector3 anchorLocalScale, ref Vector3 targetLocalScale, bool allowNearbyHorizontalContacts)
    {
        // 기준 크기로 배치했을 때 닿아 있거나 가까이 있는 face를 기억한 뒤,
        // scale을 바꿔도 해당 face가 계속 붙어 있게 위치와 크기를 보정한다.
        if (!TryGetPlacementContactConstraints(targetPosition, targetRotation, anchorLocalScale, allowNearbyHorizontalContacts, out PlacementAxisContact[] contacts))
        {
            return false;
        }

        bool changed = false;
        for (int axisIndex = 0; axisIndex < contacts.Length; axisIndex++)
        {
            if (axisIndex == 1)
            {
                continue;
            }

            if (!contacts[axisIndex].HasMin && !contacts[axisIndex].HasMax)
            {
                continue;
            }

            changed |= ApplyContactAnchoredScaleAxis(ref targetPosition, targetRotation, ref targetLocalScale, axisIndex, contacts[axisIndex]);
        }

        return changed;
    }

    private bool TryGetPlacementContactConstraints(Vector3 targetPosition, Quaternion targetRotation, Vector3 localScale, bool allowNearbyHorizontalContacts, out PlacementAxisContact[] contacts)
    {
        contacts = new PlacementAxisContact[3];

        if (!TryCollectPreviewWorldPointsAt(targetPosition, targetRotation, localScale, out List<Vector3> previewPoints, out Vector3 previewCenter, out float previewRadius))
        {
            return false;
        }

        Vector3[] axes =
        {
            GetPlacementLocalAxis(targetRotation, 0),
            GetPlacementLocalAxis(targetRotation, 1),
            GetPlacementLocalAxis(targetRotation, 2),
        };

        float[] previewMin = new float[3];
        float[] previewMax = new float[3];
        for (int i = 0; i < axes.Length; i++)
        {
            GetProjectionInterval(previewPoints, axes[i], out previewMin[i], out previewMax[i]);
        }

        float searchRadius = previewRadius + neighborSnapDistance + faceAssistSnapDistance + gridSize;
        Collider[] nearbyColliders = Physics.OverlapSphere(
            previewCenter,
            searchRadius,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        bool foundContact = false;
        float contactTolerance = Mathf.Max(0.025f, gridSize * 0.025f);
        float nearbyHorizontalContactTolerance = Mathf.Max(contactTolerance, neighborSnapDistance + faceAssistSnapDistance + gridSize * 0.15f);
        float overlapTolerance = Mathf.Max(0.0025f, gridSize * 0.0025f);
        List<Vector3> neighborPoints = new();

        foreach (Collider collider in nearbyColliders)
        {
            if (collider == null ||
                previewInstance != null && collider.transform.IsChildOf(previewInstance.transform))
            {
                continue;
            }

            if (!TryCollectColliderWorldPoints(collider, neighborPoints))
            {
                Vector3[] boundsCorners = GetBoundsCorners(collider.bounds);
                neighborPoints.Clear();
                neighborPoints.AddRange(boundsCorners);
            }

            float[] neighborMin = new float[3];
            float[] neighborMax = new float[3];
            for (int i = 0; i < axes.Length; i++)
            {
                GetProjectionInterval(neighborPoints, axes[i], out neighborMin[i], out neighborMax[i]);
            }

            for (int axisIndex = 0; axisIndex < axes.Length; axisIndex++)
            {
                // 랜덤 scale이 켜져 있으면 X/Z 방향은 "이미 닿은 면"뿐 아니라
                // 바로 옆에 있는 면도 붙일 후보로 본다. 그래야 랜덤으로 작아진 블록이
                // 사진처럼 옆 블록과 미세하게 떨어지지 않는다.
                float axisContactTolerance = allowNearbyHorizontalContacts && axisIndex != 1
                    ? nearbyHorizontalContactTolerance
                    : contactTolerance;
                GetOtherAxisIndices(axisIndex, out int firstOtherAxis, out int secondOtherAxis);
                if (!IntervalsOverlapMoreThan(previewMin[firstOtherAxis], previewMax[firstOtherAxis], neighborMin[firstOtherAxis], neighborMax[firstOtherAxis], overlapTolerance) ||
                    !IntervalsOverlapMoreThan(previewMin[secondOtherAxis], previewMax[secondOtherAxis], neighborMin[secondOtherAxis], neighborMax[secondOtherAxis], overlapTolerance))
                {
                    continue;
                }

                if (Mathf.Abs(previewMin[axisIndex] - neighborMax[axisIndex]) <= axisContactTolerance)
                {
                    SetMinContact(ref contacts[axisIndex], neighborMax[axisIndex]);
                    foundContact = true;
                }

                if (Mathf.Abs(previewMax[axisIndex] - neighborMin[axisIndex]) <= axisContactTolerance)
                {
                    SetMaxContact(ref contacts[axisIndex], neighborMin[axisIndex]);
                    foundContact = true;
                }
            }
        }

        return foundContact;
    }

    private bool ApplyContactAnchoredScaleAxis(ref Vector3 targetPosition, Quaternion targetRotation, ref Vector3 targetLocalScale, int axisIndex, PlacementAxisContact contact)
    {
        Vector3 axis = GetPlacementLocalAxis(targetRotation, axisIndex);
        float tolerance = Mathf.Max(0.0001f, gridSize * 0.0001f);

        if (contact.HasMin && contact.HasMax)
        {
            float targetLength = contact.MaxPlane - contact.MinPlane;
            if (targetLength <= tolerance)
            {
                return false;
            }

            if (!TryCollectPreviewWorldPointsAt(targetPosition, targetRotation, targetLocalScale, out List<Vector3> pointsBeforeScale, out _, out _))
            {
                return false;
            }

            GetProjectionInterval(pointsBeforeScale, axis, out float currentMin, out float currentMax);
            float currentLength = currentMax - currentMin;
            if (currentLength <= tolerance)
            {
                return false;
            }

            targetLocalScale = ApplyLocalScaleRatio(targetLocalScale, axisIndex, targetLength / currentLength);

            if (!TryCollectPreviewWorldPointsAt(targetPosition, targetRotation, targetLocalScale, out List<Vector3> pointsAfterScale, out _, out _))
            {
                return false;
            }

            GetProjectionInterval(pointsAfterScale, axis, out currentMin, out currentMax);
            float targetCenter = (contact.MinPlane + contact.MaxPlane) * 0.5f;
            float currentCenter = (currentMin + currentMax) * 0.5f;
            targetPosition += axis * (targetCenter - currentCenter);
            return true;
        }

        if (!TryCollectPreviewWorldPointsAt(targetPosition, targetRotation, targetLocalScale, out List<Vector3> points, out _, out _))
        {
            return false;
        }

        GetProjectionInterval(points, axis, out float previewMin, out float previewMax);

        if (contact.HasMin)
        {
            targetPosition += axis * (contact.MinPlane - previewMin);
            return true;
        }

        targetPosition += axis * (contact.MaxPlane - previewMax);
        return true;
    }

    private Vector3 GetPlacementLocalAxis(Quaternion targetRotation, int axisIndex)
    {
        if (axisIndex == 0)
        {
            return (targetRotation * Vector3.right).normalized;
        }

        if (axisIndex == 1)
        {
            return (targetRotation * Vector3.up).normalized;
        }

        return (targetRotation * Vector3.forward).normalized;
    }

    private void GetOtherAxisIndices(int axisIndex, out int firstOtherAxis, out int secondOtherAxis)
    {
        if (axisIndex == 0)
        {
            firstOtherAxis = 1;
            secondOtherAxis = 2;
            return;
        }

        if (axisIndex == 1)
        {
            firstOtherAxis = 0;
            secondOtherAxis = 2;
            return;
        }

        firstOtherAxis = 0;
        secondOtherAxis = 1;
    }

    private void SetMinContact(ref PlacementAxisContact contact, float plane)
    {
        if (!contact.HasMin || plane > contact.MinPlane)
        {
            contact.HasMin = true;
            contact.MinPlane = plane;
        }
    }

    private void SetMaxContact(ref PlacementAxisContact contact, float plane)
    {
        if (!contact.HasMax || plane < contact.MaxPlane)
        {
            contact.HasMax = true;
            contact.MaxPlane = plane;
        }
    }

    private Vector3 ApplyLocalScaleRatio(Vector3 localScale, int axisIndex, float ratio)
    {
        if (axisIndex == 0)
        {
            localScale.x = Mathf.Max(MinimumPlacementScale, localScale.x * ratio);
        }
        else if (axisIndex == 1)
        {
            localScale.y = Mathf.Max(MinimumPlacementScale, localScale.y * ratio);
        }
        else
        {
            localScale.z = Mathf.Max(MinimumPlacementScale, localScale.z * ratio);
        }

        return localScale;
    }

    private bool IntervalsOverlapMoreThan(float minA, float maxA, float minB, float maxB, float tolerance)
    {
        return Mathf.Min(maxA, maxB) - Mathf.Max(minA, minB) > tolerance;
    }

}
