using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum LightZonePrototypeMode
{
    SpotLightOnly,
    MeshZone,
    DecalZone
}

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LightZoneComparisonRig : MonoBehaviour
{
    private const string MeshShaderName = "Umbrella/Prototype Light Zone Mesh";
    private const string BeamShaderName = "Umbrella/Prototype Light Shaft";
    private const int DiscSegments = 96;

    [Header("Layout")]
    [SerializeField] private float variantSpacing = 5f;
    [SerializeField] private float zoneRadius = 2.2f;
    [SerializeField] private float groundOffset = 0.035f;

    [Header("Spot Light")]
    [SerializeField] private float spotHeight = 4.5f;
    [SerializeField] private Vector2 spotHorizontalOffset = new Vector2(-1.4f, -1.4f);
    [SerializeField] private bool fitSpotAngleToRadius = true;
    [SerializeField, Range(8f, 80f)] private float manualSpotAngle = 34f;
    [SerializeField] private float spotRange = 8f;
    [SerializeField] private float spotIntensity = 200f;
    [SerializeField, Range(0f, 1f)] private float shadowStrength = 0.86f;
    [SerializeField] private Color lightColor = new Color(1f, 0.78f, 0.22f, 1f);

    [Header("Light Shaft")]
    [SerializeField] private bool showLightShaft = true;
    [SerializeField, Range(0.02f, 0.6f)] private float beamStartRadius = 0.16f;
    [SerializeField, Range(0f, 1f)] private float beamOpacity = 0.36f;
    [SerializeField, Range(0f, 1f)] private float beamCoreStrength = 0.42f;
    [SerializeField, Range(2, 8)] private int beamSheetCount = 4;

    [Header("Readable Zone")]
    [SerializeField, Range(0f, 1f)] private float zoneOpacity = 0.62f;
    [SerializeField, Range(0.02f, 0.8f)] private float edgeSoftness = 0.28f;
    [SerializeField, Range(0f, 1f)] private float rimStrength = 0.35f;
    [SerializeField] private bool createDecalVariant = true;

    [Header("Prototype Logic")]
    [SerializeField] private bool createLightExposureSources = false;
    [SerializeField] private float gameplayLightIntensity = 1f;
    [SerializeField] private LayerMask gameplayReceiverMask = ~0;
    [SerializeField] private LayerMask gameplayOcclusionMask = ~0;
    [SerializeField] private float triggerHeight = 2f;

    private Mesh discMesh;
    private Mesh beamMesh;
    private Material meshMaterial;
    private Material beamMaterial;
    private Material decalMaterial;
    private Texture2D decalTexture;
    private Color cachedDecalColor;
    private float cachedDecalOpacity = -1f;
    private float cachedDecalEdgeSoftness = -1f;

    private float EffectiveSpotAngle
    {
        get
        {
            if (!fitSpotAngleToRadius)
            {
                return manualSpotAngle;
            }

            Vector3 source = GetSpotLocalPosition();
            float distanceToTarget = Mathf.Max(0.1f, source.magnitude);
            float angle = Mathf.Atan2(zoneRadius, distanceToTarget) * Mathf.Rad2Deg * 2f * 1.15f;
            return Mathf.Clamp(angle, 8f, 80f);
        }
    }

    private void OnEnable()
    {
        EnsureRig();
    }

    private void OnValidate()
    {
        EnsureRig();
    }

    private void EnsureRig()
    {
        zoneRadius = Mathf.Max(0.25f, zoneRadius);
        spotHeight = Mathf.Max(0.25f, spotHeight);
        spotRange = Mathf.Max(GetSpotLocalPosition().magnitude + zoneRadius + 0.5f, spotRange);
        gameplayLightIntensity = Mathf.Max(0f, gameplayLightIntensity);
        triggerHeight = Mathf.Max(0.1f, triggerHeight);
        beamSheetCount = Mathf.Clamp(beamSheetCount, 2, 8);

        EnsureVariant("A_SpotLightOnly", new Vector3(-variantSpacing, 0f, 0f), LightZonePrototypeMode.SpotLightOnly).gameObject.SetActive(true);
        EnsureVariant("B_MeshLightZone", Vector3.zero, LightZonePrototypeMode.MeshZone).gameObject.SetActive(true);

        Transform decalVariant = EnsureVariant("C_DecalLightZone", new Vector3(variantSpacing, 0f, 0f), LightZonePrototypeMode.DecalZone);
        decalVariant.gameObject.SetActive(createDecalVariant);
    }

    private Transform EnsureVariant(string variantName, Vector3 localPosition, LightZonePrototypeMode mode)
    {
        Transform variant = EnsureChild(transform, variantName);
        variant.localPosition = localPosition;
        variant.localRotation = Quaternion.identity;
        variant.localScale = Vector3.one;

        RemovePrototypeAreaMarkers(variant.gameObject);

        SphereCollider trigger = GetOrAdd<SphereCollider>(variant.gameObject);
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, triggerHeight * 0.5f, 0f);
        trigger.radius = zoneRadius;

        EnsureSpotLight(variant, mode);
        EnsureLightShaft(variant);
        EnsureMeshZone(variant, mode == LightZonePrototypeMode.MeshZone);
        EnsureDecalZone(variant, mode == LightZonePrototypeMode.DecalZone);

        return variant;
    }

    private void EnsureSpotLight(Transform parent, LightZonePrototypeMode mode)
    {
        Transform lightTransform = EnsureChild(parent, "Actual Spot Light");
        lightTransform.localPosition = GetSpotLocalPosition();
        lightTransform.localRotation = Quaternion.LookRotation(-lightTransform.localPosition.normalized, Vector3.up);
        lightTransform.localScale = Vector3.one;

        Light spot = GetOrAdd<Light>(lightTransform.gameObject);
        spot.type = LightType.Spot;
        spot.color = lightColor;
        spot.intensity = spotIntensity;
        spot.range = spotRange;
        spot.spotAngle = EffectiveSpotAngle;
        spot.innerSpotAngle = Mathf.Clamp(EffectiveSpotAngle * 0.55f, 1f, EffectiveSpotAngle);
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = shadowStrength;
        spot.renderMode = LightRenderMode.ForcePixel;

        LightExposureSource exposureSource = GetOrAdd<LightExposureSource>(lightTransform.gameObject);
        exposureSource.enabled = createLightExposureSources;
        exposureSource.Configure(spotRange, EffectiveSpotAngle, gameplayLightIntensity);
        exposureSource.SetMasks(gameplayReceiverMask, gameplayOcclusionMask);
    }

    private void EnsureLightShaft(Transform parent)
    {
        Transform beamTransform = EnsureChild(parent, "Visible Light Shaft");
        beamTransform.gameObject.SetActive(showLightShaft);
        if (!showLightShaft)
        {
            return;
        }

        Vector3 source = GetSpotLocalPosition();
        float length = Mathf.Max(0.1f, source.magnitude);

        beamTransform.localPosition = source;
        beamTransform.localRotation = Quaternion.LookRotation(-source.normalized, Vector3.up);
        beamTransform.localScale = Vector3.one;

        MeshFilter meshFilter = GetOrAdd<MeshFilter>(beamTransform.gameObject);
        meshFilter.sharedMesh = GetBeamMesh(length);

        MeshRenderer meshRenderer = GetOrAdd<MeshRenderer>(beamTransform.gameObject);
        meshRenderer.sharedMaterial = GetBeamMaterial();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private void EnsureMeshZone(Transform parent, bool active)
    {
        Transform meshTransform = EnsureChild(parent, "Readable Mesh Zone");
        meshTransform.gameObject.SetActive(active);
        if (!active)
        {
            return;
        }

        meshTransform.localPosition = new Vector3(0f, groundOffset, 0f);
        meshTransform.localRotation = Quaternion.identity;
        meshTransform.localScale = new Vector3(zoneRadius, 1f, zoneRadius);

        MeshFilter meshFilter = GetOrAdd<MeshFilter>(meshTransform.gameObject);
        meshFilter.sharedMesh = GetDiscMesh();

        MeshRenderer meshRenderer = GetOrAdd<MeshRenderer>(meshTransform.gameObject);
        meshRenderer.sharedMaterial = GetMeshMaterial();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private void EnsureDecalZone(Transform parent, bool active)
    {
        Transform decalTransform = EnsureChild(parent, "Readable Decal Zone");
        decalTransform.gameObject.SetActive(active);
        if (!active)
        {
            return;
        }

        float projectionDepth = Mathf.Max(spotHeight + 1f, 1.5f);
        decalTransform.localPosition = new Vector3(0f, spotHeight, 0f);
        decalTransform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        decalTransform.localScale = Vector3.one;

        DecalProjector projector = GetOrAdd<DecalProjector>(decalTransform.gameObject);
        projector.material = GetDecalMaterial();
        projector.size = new Vector3(zoneRadius * 2f, zoneRadius * 2f, projectionDepth);
        projector.pivot = new Vector3(0f, 0f, projectionDepth * 0.5f);
        projector.drawDistance = Mathf.Max(spotRange * 2f, 20f);
        projector.fadeScale = 0.9f;
        projector.fadeFactor = 1f;
        projector.startAngleFade = 70f;
        projector.endAngleFade = 88f;
        projector.uvScale = Vector2.one;
        projector.uvBias = Vector2.zero;
        projector.scaleMode = DecalScaleMode.ScaleInvariant;
    }

    private Mesh GetDiscMesh()
    {
        if (discMesh != null)
        {
            return discMesh;
        }

        Vector3[] vertices = new Vector3[DiscSegments + 1];
        Vector2[] uvs = new Vector2[DiscSegments + 1];
        int[] triangles = new int[DiscSegments * 3];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < DiscSegments; i++)
        {
            float radians = (float)i / DiscSegments * Mathf.PI * 2f;
            float x = Mathf.Cos(radians);
            float z = Mathf.Sin(radians);
            vertices[i + 1] = new Vector3(x, 0f, z);
            uvs[i + 1] = new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f);
        }

        for (int i = 0; i < DiscSegments; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i == DiscSegments - 1 ? 1 : i + 2;
        }

        discMesh = new Mesh
        {
            name = "Generated Light Zone Disc",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = vertices,
            uv = uvs,
            triangles = triangles
        };
        discMesh.RecalculateBounds();
        discMesh.RecalculateNormals();

        return discMesh;
    }

    private Mesh GetBeamMesh(float length)
    {
        int sheetCount = Mathf.Clamp(beamSheetCount, 2, 8);
        int vertexCount = sheetCount * 4;
        int triangleIndexCount = sheetCount * 6;

        if (beamMesh == null)
        {
            beamMesh = new Mesh
            {
                name = "Generated Light Shaft Sheets",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[triangleIndexCount];

        for (int i = 0; i < sheetCount; i++)
        {
            float radians = (float)i / sheetCount * Mathf.PI;
            Vector3 widthDirection = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
            int vertexIndex = i * 4;
            int triangleIndex = i * 6;

            vertices[vertexIndex] = -widthDirection * beamStartRadius;
            vertices[vertexIndex + 1] = widthDirection * beamStartRadius;
            vertices[vertexIndex + 2] = -widthDirection * zoneRadius + Vector3.forward * length;
            vertices[vertexIndex + 3] = widthDirection * zoneRadius + Vector3.forward * length;

            uvs[vertexIndex] = new Vector2(0f, 0f);
            uvs[vertexIndex + 1] = new Vector2(1f, 0f);
            uvs[vertexIndex + 2] = new Vector2(0f, 1f);
            uvs[vertexIndex + 3] = new Vector2(1f, 1f);

            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 2;
            triangles[triangleIndex + 2] = vertexIndex + 1;
            triangles[triangleIndex + 3] = vertexIndex + 1;
            triangles[triangleIndex + 4] = vertexIndex + 2;
            triangles[triangleIndex + 5] = vertexIndex + 3;
        }

        beamMesh.Clear();
        beamMesh.vertices = vertices;
        beamMesh.uv = uvs;
        beamMesh.triangles = triangles;
        beamMesh.RecalculateBounds();
        beamMesh.RecalculateNormals();

        return beamMesh;
    }

    private Material GetMeshMaterial()
    {
        if (meshMaterial == null)
        {
            Shader shader = Shader.Find(MeshShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                return null;
            }

            meshMaterial = new Material(shader)
            {
                name = "Generated Light Zone Mesh Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        SetMaterialColor(meshMaterial, "_Tint", lightColor);
        SetMaterialFloat(meshMaterial, "_FillAlpha", Mathf.Clamp01(zoneOpacity * 0.45f));
        SetMaterialFloat(meshMaterial, "_EdgeSoftness", edgeSoftness);
        SetMaterialFloat(meshMaterial, "_RimAlpha", Mathf.Clamp01(rimStrength * 1.35f + 0.2f));
        return meshMaterial;
    }

    private Material GetBeamMaterial()
    {
        if (beamMaterial == null)
        {
            Shader shader = Shader.Find(BeamShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                return null;
            }

            beamMaterial = new Material(shader)
            {
                name = "Generated Light Shaft Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        SetMaterialColor(beamMaterial, "_Tint", lightColor);
        SetMaterialFloat(beamMaterial, "_BeamAlpha", beamOpacity);
        SetMaterialFloat(beamMaterial, "_CoreAlpha", beamCoreStrength);
        SetMaterialFloat(beamMaterial, "_NoiseStrength", 0.08f);
        return beamMaterial;
    }

    private Material GetDecalMaterial()
    {
        if (decalMaterial == null)
        {
            Shader shader = Shader.Find("Shader Graphs/Decal");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Decal");
            }

            if (shader == null)
            {
                return null;
            }

            decalMaterial = new Material(shader)
            {
                name = "Generated Light Zone Decal Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        Texture2D radialTexture = GetDecalTexture();
        SetMaterialTexture(decalMaterial, radialTexture, "Base_Map", "_Base_Map", "_BaseMap", "_MainTex");
        SetMaterialColor(decalMaterial, "_Color", lightColor);
        SetMaterialFloat(decalMaterial, "Alpha", 1f);
        SetMaterialFloat(decalMaterial, "Normal_Blend", 0f);
        return decalMaterial;
    }

    private Texture2D GetDecalTexture()
    {
        const int size = 128;
        if (decalTexture == null)
        {
            decalTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Generated Light Zone Radial Decal",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        if (cachedDecalColor != lightColor || !Mathf.Approximately(cachedDecalOpacity, zoneOpacity) || !Mathf.Approximately(cachedDecalEdgeSoftness, edgeSoftness))
        {
            UpdateDecalTexture(size);
        }

        return decalTexture;
    }

    private void UpdateDecalTexture(int size)
    {
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float distance = Vector2.Distance(uv, new Vector2(0.5f, 0.5f)) * 2f;
                float fill = 1f - SmoothStep(1f - edgeSoftness, 1f, distance);
                float centerGlow = 1f - SmoothStep(0f, 0.55f, distance);
                float alpha = Mathf.Clamp01(fill * 0.75f + centerGlow * 0.25f) * zoneOpacity;

                Color pixel = lightColor;
                pixel.a = alpha;
                pixels[y * size + x] = pixel;
            }
        }

        decalTexture.SetPixels(pixels);
        decalTexture.Apply(false, false);

        cachedDecalColor = lightColor;
        cachedDecalOpacity = zoneOpacity;
        cachedDecalEdgeSoftness = edgeSoftness;
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private Vector3 GetSpotLocalPosition()
    {
        return new Vector3(spotHorizontalOffset.x, spotHeight, spotHorizontalOffset.y);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return target.AddComponent<T>();
    }

    private static void RemovePrototypeAreaMarkers(GameObject target)
    {
        LightZonePrototypeArea[] areas = target.GetComponents<LightZonePrototypeArea>();
        foreach (LightZonePrototypeArea area in areas)
        {
            DestroyComponent(area);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
        }
#endif
    }

    private static void DestroyComponent(Component component)
    {
        if (component == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(component);
        }
        else
        {
            DestroyImmediate(component);
        }
    }

    private static void SetMaterialColor(Material material, string propertyName, Color value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetMaterialTexture(Material material, Texture texture, params string[] propertyNames)
    {
        if (material == null)
        {
            return;
        }

        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
