using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private void EnsurePreviewInstance()
    {
        // preview는 실제 prefab 인스턴스를 재사용하는 편이
        // 배치 결과와 시각적으로 가장 덜 어긋난다.
        if (previewInstance != null)
        {
            if (previewInstance.name != $"{selectedPrefab.name}_Preview")
            {
                DestroyPreviewInstance();
            }
            else
            {
                return;
            }
        }

        previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(selectedPrefab);
        if (previewInstance == null)
        {
            return;
        }

        previewInstance.name = $"{selectedPrefab.name}_Preview";
        previewInstance.hideFlags = HideFlags.HideAndDontSave;

        foreach (Collider collider in previewInstance.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (MonoBehaviour behaviour in previewInstance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
        }

        UpdatePreviewMaterial(false);
        UpdatePreviewVisibility(true);
    }

    private void UpdatePreviewTransform(Vector3 position, Quaternion rotation)
    {
        EnsurePreviewInstance();
        if (previewInstance == null)
        {
            return;
        }

        previewInstance.transform.position = position;
        previewInstance.transform.rotation = rotation;
        UpdatePreviewVisibility(true);
    }

    private bool TryGetObjectBounds(GameObject targetObject, out Bounds combinedBounds)
    {
        bool hasBounds = false;
        combinedBounds = new Bounds();

        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = targetObject.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (!hasBounds)
            {
                combinedBounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private void UpdatePreviewVisibility(bool visible)
    {
        if (previewInstance == null)
        {
            return;
        }

        previewInstance.SetActive(visible);
    }

    private void UpdatePreviewMaterial(bool occupied)
    {
        // 점유 상태만 색으로 바꾸고, material 자체는 하나를 공유해서 preview 비용을 줄인다.
        EnsurePreviewMaterial();
        EnsurePreviewInstance();

        if (previewMaterial == null || previewInstance == null)
        {
            return;
        }

        Color previewColor = occupied
            ? new Color(1.0f, 0.3f, 0.3f, 0.35f)
            : new Color(0.35f, 0.85f, 1.0f, 0.35f);

        previewMaterial.SetColor("_BaseColor", previewColor);

        foreach (Renderer renderer in previewInstance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] previewMaterials = new Material[renderer.sharedMaterials.Length];

            for (int i = 0; i < previewMaterials.Length; i++)
            {
                previewMaterials[i] = previewMaterial;
            }

            renderer.sharedMaterials = previewMaterials;
        }
    }

    private void EnsurePreviewMaterial()
    {
        // URP Lit 우선, 없으면 Standard로 fallback.
        // preview는 알파만 필요한 단순 transparent material이면 충분하다.
        if (previewMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return;
        }

        previewMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        if (previewMaterial.HasProperty("_Surface"))
        {
            previewMaterial.SetFloat("_Surface", 1.0f);
        }

        if (previewMaterial.HasProperty("_Blend"))
        {
            previewMaterial.SetFloat("_Blend", 0.0f);
        }

        if (previewMaterial.HasProperty("_SrcBlend"))
        {
            previewMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (previewMaterial.HasProperty("_DstBlend"))
        {
            previewMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (previewMaterial.HasProperty("_ZWrite"))
        {
            previewMaterial.SetFloat("_ZWrite", 0.0f);
        }

        previewMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void DestroyPreviewInstance()
    {
        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
            previewInstance = null;
        }

        if (previewMaterial != null)
        {
            DestroyImmediate(previewMaterial);
            previewMaterial = null;
        }
    }

}
