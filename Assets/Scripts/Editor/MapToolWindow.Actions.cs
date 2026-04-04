using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private Transform GetOrCreateRootParent()
    {
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
        if (selectedMaterial == null)
        {
            Debug.LogWarning("Map Tool: Assign a material preset before applying.");
            return;
        }

        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            Renderer[] renderers = selectedObject.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                Undo.RecordObject(renderer, "Apply Material Preset");
                Material[] materials = renderer.sharedMaterials;

                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = selectedMaterial;
                }

                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
        }
    }

    private void SnapSelectionToGrid()
    {
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

        selectedPrefab = prefab;
        DestroyPreviewInstance();
        Repaint();
    }

    private bool IsCellOccupied(Vector3 targetPosition)
    {
        // 현재는 "같은 snapped position이 이미 있느냐"만 보는 단순 점유 검사다.
        // 겹침 허용/정밀 충돌 검사까지 필요해지면 이 부분을 별도 정책으로 빼는 게 좋다.
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
}
