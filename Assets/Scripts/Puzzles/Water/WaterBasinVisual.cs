using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
public class WaterBasinVisual : MonoBehaviour
{
    private const float Epsilon = 0.0001f;

    [Header("References")]
    [SerializeField] private WaterBasinTarget basinTarget;
    [SerializeField] private Transform visualRoot;

    [FormerlySerializedAs("waterVisual")]
    [SerializeField] private Transform fillMesh;

    [SerializeField] private Renderer waterRenderer;

    [Header("Shape")]
    [Tooltip("활성화하면 X/Z 크기를 수조 타겟의 부피 경계에서 가져옵니다.")]
    [FormerlySerializedAs("useTargetSurfaceArea")]
    [SerializeField] private bool useTargetVolumeSize = true;

    [SerializeField] private Vector2 footprintSize = Vector2.one;

    [Tooltip("물이 거의 없을 때 시각적으로 보이도록 사용할 최소 두께입니다.")]
    [SerializeField] private float minVisibleDepth = 0.02f;

    [Header("Behavior")]
    [SerializeField] private bool hideWhenEmpty = true;
    [SerializeField] private bool useGroupSurfaceHeight = true;
    [SerializeField] private bool smoothChanges = true;
    [SerializeField] private float smoothingSpeed = 12.0f;

    [Header("Material")]
    [SerializeField] private bool tintMaterial = true;
    [SerializeField] private Color waterColor = new Color(0.1f, 0.55f, 1.0f, 0.55f);

    [Header("Runtime")]
    [SerializeField] private float visibleDepth;
    [SerializeField] private float visualSurfaceWorldY;

    private MaterialPropertyBlock materialPropertyBlock;
    private Renderer[] waterRenderers;

    public Transform VisualRoot => visualRoot;
    public Transform FillMesh => fillMesh;
    public float VisibleDepth => visibleDepth;
    public float VisualSurfaceWorldY => visualSurfaceWorldY;

    private void Reset()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        SubscribeTarget();
        RefreshImmediate();
    }

    private void OnDisable()
    {
        UnsubscribeTarget();
    }

    private void OnValidate()
    {
        footprintSize = new Vector2(
            Mathf.Max(0.01f, footprintSize.x),
            Mathf.Max(0.01f, footprintSize.y));
        minVisibleDepth = Mathf.Max(0.0f, minVisibleDepth);
        smoothingSpeed = Mathf.Max(0.0f, smoothingSpeed);
        CacheReferences();
        ApplyMaterialTint();
        RefreshImmediate();
    }

    private void LateUpdate()
    {
        Refresh(smoothChanges && Application.isPlaying);
    }

    [ContextMenu("물 시각화 즉시 갱신")]
    public void RefreshImmediate()
    {
        Refresh(false);
    }

    private void Refresh(bool smooth)
    {
        if (basinTarget == null || fillMesh == null)
        {
            return;
        }

        Transform root = GetActiveVisualRoot();
        if (root == null)
        {
            return;
        }

        float targetDepth = CalculateTargetDepth();
        bool hasVisibleWater = targetDepth > Epsilon;

        if (root != basinTarget.transform && !root.gameObject.activeSelf)
        {
            root.gameObject.SetActive(true);
        }

        if (!fillMesh.gameObject.activeSelf)
        {
            fillMesh.gameObject.SetActive(true);
        }

        SetRendererVisible(!hideWhenEmpty || hasVisibleWater);

        if (!hasVisibleWater && hideWhenEmpty)
        {
            visibleDepth = 0.0f;
            visualSurfaceWorldY = basinTarget.BottomWorldY;
            ApplyVisualTransform(Mathf.Max(minVisibleDepth, Epsilon));
            return;
        }

        float displayDepth = Mathf.Max(targetDepth, minVisibleDepth);
        float nextDepth = smooth
            ? Mathf.Lerp(visibleDepth, displayDepth, GetSmoothingFactor())
            : displayDepth;

        visibleDepth = nextDepth;
        visualSurfaceWorldY = basinTarget.BottomWorldY + visibleDepth;

        ApplyVisualTransform(visibleDepth);
        ApplyMaterialTint();
    }

    private float CalculateTargetDepth()
    {
        float localSurfaceY = basinTarget.WaterSurfaceWorldY;
        float surfaceY = useGroupSurfaceHeight
            ? basinTarget.GroupWaterSurfaceWorldY
            : localSurfaceY;

        if (useGroupSurfaceHeight
            && surfaceY <= basinTarget.BottomWorldY + Epsilon
            && localSurfaceY > basinTarget.BottomWorldY + Epsilon)
        {
            surfaceY = localSurfaceY;
        }

        return Mathf.Clamp(
            surfaceY - basinTarget.BottomWorldY,
            0.0f,
            basinTarget.MaxWaterHeight);
    }

    private void ApplyVisualTransform(float depth)
    {
        Transform root = GetActiveVisualRoot();
        if (root == null || basinTarget == null || fillMesh == null)
        {
            return;
        }

        ApplyRootTransform(root);
        ApplyFillTransform(root, depth);
    }

    private void ApplyRootTransform(Transform root)
    {
        if (root == basinTarget.transform)
        {
            return;
        }

        root.position = basinTarget.VolumeWorldCenter;
        root.rotation = basinTarget.transform.rotation;
        ApplyWorldScale(root, GetVisualWorldSize());
    }

    private void ApplyFillTransform(Transform root, float depth)
    {
        Vector3 visualWorldSize = GetVisualWorldSize();
        float fillDepth = Mathf.Max(Epsilon, Mathf.Clamp(depth, 0.0f, visualWorldSize.y));
        Vector3 visualCenter = basinTarget.VolumeWorldCenter;
        Vector3 fillCenter = new Vector3(
            visualCenter.x,
            basinTarget.BottomWorldY + fillDepth * 0.5f,
            visualCenter.z);

        fillMesh.position = fillCenter;
        fillMesh.rotation = root.rotation;
        ApplyWorldScale(fillMesh, new Vector3(visualWorldSize.x, fillDepth, visualWorldSize.z));
    }

    private Vector3 GetVisualWorldSize()
    {
        if (useTargetVolumeSize || basinTarget == null)
        {
            return basinTarget != null ? basinTarget.VolumeWorldSize : Vector3.one;
        }

        return new Vector3(
            Mathf.Max(0.01f, footprintSize.x),
            Mathf.Max(Epsilon, basinTarget.MaxWaterHeight),
            Mathf.Max(0.01f, footprintSize.y));
    }

    private Transform GetActiveVisualRoot()
    {
        if (visualRoot != null)
        {
            return visualRoot;
        }

        return basinTarget != null ? basinTarget.transform : null;
    }

    private static void ApplyWorldScale(Transform target, Vector3 worldScale)
    {
        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            DivideByScale(worldScale.x, parentScale.x),
            DivideByScale(worldScale.y, parentScale.y),
            DivideByScale(worldScale.z, parentScale.z));
    }

    private static float DivideByScale(float value, float scale)
    {
        float denominator = Mathf.Abs(scale);
        return denominator <= Epsilon ? value : value / denominator;
    }

    private float GetSmoothingFactor()
    {
        if (smoothingSpeed <= Epsilon)
        {
            return 1.0f;
        }

        return 1.0f - Mathf.Exp(-smoothingSpeed * Time.deltaTime);
    }

    private void SubscribeTarget()
    {
        if (basinTarget == null)
        {
            return;
        }

        basinTarget.WaterStateChanged -= OnWaterStateChanged;
        basinTarget.WaterStateChanged += OnWaterStateChanged;
    }

    private void UnsubscribeTarget()
    {
        if (basinTarget == null)
        {
            return;
        }

        basinTarget.WaterStateChanged -= OnWaterStateChanged;
    }

    private void OnWaterStateChanged()
    {
        Refresh(false);
    }

    private void CacheReferences()
    {
        if (basinTarget == null)
        {
            basinTarget = GetComponentInParent<WaterBasinTarget>();
        }

        if (visualRoot == null)
        {
            visualRoot = FindChildOrSelf("Water Visual Root");
        }

        if (fillMesh == null)
        {
            fillMesh = FindFillMeshCandidate();
        }

        if (visualRoot == null && fillMesh != null && fillMesh.parent != null && fillMesh.parent != transform)
        {
            visualRoot = fillMesh.parent;
        }

        if (waterRenderer == null && fillMesh != null)
        {
            waterRenderer = fillMesh.GetComponentInChildren<Renderer>(true);
        }

        waterRenderers = fillMesh != null
            ? fillMesh.GetComponentsInChildren<Renderer>(true)
            : null;
    }

    private Transform FindFillMeshCandidate()
    {
        if (visualRoot != null)
        {
            Transform child = visualRoot.Find("Water Fill Mesh");
            if (child != null)
            {
                return child;
            }
        }

        Transform directFill = transform.Find("Water Fill Mesh");
        if (directFill != null)
        {
            return directFill;
        }

        Transform oldVisual = transform.Find("Water Visual");
        return oldVisual != null ? oldVisual : null;
    }

    private Transform FindChildOrSelf(string childName)
    {
        if (gameObject.name == childName)
        {
            return transform;
        }

        Transform child = transform.Find(childName);
        return child != null ? child : null;
    }

    private void SetRendererVisible(bool visible)
    {
        if (waterRenderers == null || waterRenderers.Length == 0)
        {
            CacheReferences();
        }

        if (waterRenderers == null)
        {
            return;
        }

        for (int i = 0; i < waterRenderers.Length; i++)
        {
            Renderer renderer = waterRenderers[i];
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    private void ApplyMaterialTint()
    {
        if (!tintMaterial || waterRenderer == null)
        {
            return;
        }

        Material material = waterRenderer.sharedMaterial;
        if (material == null)
        {
            return;
        }

        string colorProperty = null;
        if (material.HasProperty("_BaseColor"))
        {
            colorProperty = "_BaseColor";
        }
        else if (material.HasProperty("_Color"))
        {
            colorProperty = "_Color";
        }

        if (string.IsNullOrEmpty(colorProperty))
        {
            return;
        }

        if (materialPropertyBlock == null)
        {
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        waterRenderer.GetPropertyBlock(materialPropertyBlock);
        materialPropertyBlock.SetColor(colorProperty, waterColor);
        waterRenderer.SetPropertyBlock(materialPropertyBlock);
    }
}
