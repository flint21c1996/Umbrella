using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class WaterBasinVisual : MonoBehaviour
{
    private const float Epsilon = 0.0001f;

    [Header("References")]
    [SerializeField] private WaterBasinTarget basinTarget;
    [SerializeField] private Transform waterVisual;
    [SerializeField] private Renderer waterRenderer;

    [Header("Shape")]
    [Tooltip("If enabled, X/Z size is derived from the basin target's Surface Area as a square.")]
    [SerializeField] private bool useTargetSurfaceArea = true;

    [SerializeField] private Vector2 footprintSize = Vector2.one;

    [Tooltip("Small visual thickness used when the basin has very little water.")]
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

    [ContextMenu("Refresh Visual Now")]
    public void RefreshImmediate()
    {
        Refresh(false);
    }

    private void Refresh(bool smooth)
    {
        if (basinTarget == null || waterVisual == null)
        {
            return;
        }

        float targetDepth = CalculateTargetDepth();
        bool hasVisibleWater = targetDepth > Epsilon;

        if (!waterVisual.gameObject.activeSelf)
        {
            waterVisual.gameObject.SetActive(true);
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
        Vector2 size = GetFootprintSize();
        Vector3 worldCenter = new Vector3(
            basinTarget.transform.position.x,
            basinTarget.BottomWorldY + depth * 0.5f,
            basinTarget.transform.position.z);

        waterVisual.position = worldCenter;
        waterVisual.rotation = basinTarget.transform.rotation;
        waterVisual.localScale = new Vector3(size.x, depth, size.y);
    }

    private Vector2 GetFootprintSize()
    {
        if (!useTargetSurfaceArea || basinTarget == null)
        {
            return footprintSize;
        }

        float side = Mathf.Sqrt(Mathf.Max(Epsilon, basinTarget.SurfaceArea));
        return new Vector2(side, side);
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

        if (waterVisual == null)
        {
            Transform existing = transform.Find("Water Visual");
            if (existing != null)
            {
                waterVisual = existing;
            }
        }

        if (waterRenderer == null && waterVisual != null)
        {
            waterRenderer = waterVisual.GetComponentInChildren<Renderer>(true);
        }

        waterRenderers = waterVisual != null
            ? waterVisual.GetComponentsInChildren<Renderer>(true)
            : null;
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
