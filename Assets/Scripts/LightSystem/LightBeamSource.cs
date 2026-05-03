using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LightBeamSource : MonoBehaviour
{
    private const string SpotLightName = "Actual Spot Light";
    private const string LightShaftName = "Visible Light Shaft";
    private const string DecalZoneName = "Readable Decal Zone";
    private const string BeamShaderName = "Umbrella/Prototype Light Shaft";

    [Header("Target")]
    [SerializeField] private Transform target = null;
    [SerializeField] private float fallbackDistance = 8f;

    [Header("Light")]
    [SerializeField] private Color lightColor = new Color(1f, 0.78f, 0.22f, 1f);
    [SerializeField] private float spotRange = 8f;
    [SerializeField, Range(8f, 80f)] private float spotAngle = 34f;
    [SerializeField] private float spotIntensity = 200f;
    [SerializeField, Range(0f, 1f)] private float shadowStrength = 0.85f;

    [Header("Gameplay")]
    [SerializeField] private bool emitGameplayLight = true;
    [SerializeField] private float gameplayLightIntensity = 1f;
    [SerializeField] private LayerMask receiverMask = ~0;
    [SerializeField] private LayerMask occlusionMask = ~0;

    [Header("Light Shaft")]
    [SerializeField] private bool showLightShaft = true;
    [SerializeField, Range(0.02f, 0.6f)] private float beamStartRadius = 0.14f;
    [SerializeField, Range(0.2f, 4f)] private float beamEndRadius = 1.2f;
    [SerializeField, Range(0f, 1f)] private float beamOpacity = 0.36f;
    [SerializeField, Range(0f, 1f)] private float beamCoreStrength = 0.42f;
    [SerializeField, Range(2, 8)] private int beamSheetCount = 4;

    [Header("Decal Zone")]
    [SerializeField] private bool showDecalZone = true;
    [SerializeField, Range(0.2f, 5f)] private float decalRadius = 1.4f;
    [SerializeField] private float decalProjectionHeight = 4.5f;
    [SerializeField, Range(0f, 1f)] private float decalOpacity = 0.62f;
    [SerializeField, Range(0.02f, 0.8f)] private float decalEdgeSoftness = 0.28f;

    private Mesh beamMesh;
    private Material beamMaterial;
    private Material decalMaterial;
    private Texture2D decalTexture;
    private Color cachedDecalColor;
    private float cachedDecalOpacity = -1f;
    private float cachedDecalEdgeSoftness = -1f;

    private Vector3 BeamDirection
    {
        get
        {
            if (target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    return toTarget.normalized;
                }
            }

            return transform.forward;
        }
    }

    private float BeamLength
    {
        get
        {
            if (target != null)
            {
                return Mathf.Max(0.1f, Vector3.Distance(transform.position, target.position));
            }

            return Mathf.Max(0.1f, fallbackDistance);
        }
    }

    private void OnEnable()
    {
        EnsureSetup();
    }

    private void OnValidate()
    {
        EnsureSetup();
    }

    private void EnsureSetup()
    {
        fallbackDistance = Mathf.Max(0.1f, fallbackDistance);
        spotRange = Mathf.Max(0.1f, spotRange);
        spotIntensity = Mathf.Max(0f, spotIntensity);
        gameplayLightIntensity = Mathf.Max(0f, gameplayLightIntensity);
        beamSheetCount = Mathf.Clamp(beamSheetCount, 2, 8);
        decalProjectionHeight = Mathf.Max(0.1f, decalProjectionHeight);

        Vector3 direction = BeamDirection;
        float length = BeamLength;

        EnsureSpotLight(direction);
        EnsureLightExposure(direction);
        EnsureLightShaft(direction, length);
        EnsureDecalZone();
    }

    private void EnsureSpotLight(Vector3 direction)
    {
        Transform spotTransform = EnsureChild(transform, SpotLightName);
        spotTransform.localPosition = Vector3.zero;
        spotTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        spotTransform.localScale = Vector3.one;

        Light spot = GetOrAdd<Light>(spotTransform.gameObject);
        spot.type = LightType.Spot;
        spot.color = lightColor;
        spot.intensity = spotIntensity;
        spot.range = spotRange;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = Mathf.Clamp(spotAngle * 0.55f, 1f, spotAngle);
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = shadowStrength;
        spot.renderMode = LightRenderMode.ForcePixel;
    }

    private void EnsureLightExposure(Vector3 direction)
    {
        Transform spotTransform = EnsureChild(transform, SpotLightName);
        spotTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        LightExposureSource exposureSource = GetOrAdd<LightExposureSource>(spotTransform.gameObject);
        exposureSource.enabled = emitGameplayLight;
        exposureSource.Configure(spotRange, spotAngle, gameplayLightIntensity);
        exposureSource.SetMasks(receiverMask, occlusionMask);
    }

    private void EnsureLightShaft(Vector3 direction, float length)
    {
        Transform beamTransform = EnsureChild(transform, LightShaftName);
        beamTransform.gameObject.SetActive(showLightShaft);
        if (!showLightShaft)
        {
            return;
        }

        beamTransform.localPosition = Vector3.zero;
        beamTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        beamTransform.localScale = Vector3.one;

        MeshFilter meshFilter = GetOrAdd<MeshFilter>(beamTransform.gameObject);
        meshFilter.sharedMesh = GetBeamMesh(length);

        MeshRenderer meshRenderer = GetOrAdd<MeshRenderer>(beamTransform.gameObject);
        meshRenderer.sharedMaterial = GetBeamMaterial();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private void EnsureDecalZone()
    {
        Transform decalTransform = EnsureChild(transform, DecalZoneName);
        decalTransform.gameObject.SetActive(showDecalZone);
        if (!showDecalZone)
        {
            return;
        }

        Vector3 center = target != null ? target.position : transform.position + BeamDirection * BeamLength;
        decalTransform.position = new Vector3(center.x, center.y + decalProjectionHeight, center.z);
        decalTransform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        decalTransform.localScale = Vector3.one;

        DecalProjector projector = GetOrAdd<DecalProjector>(decalTransform.gameObject);
        projector.material = GetDecalMaterial();
        projector.size = new Vector3(decalRadius * 2f, decalRadius * 2f, decalProjectionHeight + 1f);
        projector.pivot = new Vector3(0f, 0f, projector.size.z * 0.5f);
        projector.drawDistance = Mathf.Max(spotRange * 2f, 20f);
        projector.fadeScale = 0.9f;
        projector.fadeFactor = 1f;
        projector.startAngleFade = 70f;
        projector.endAngleFade = 88f;
        projector.uvScale = Vector2.one;
        projector.uvBias = Vector2.zero;
        projector.scaleMode = DecalScaleMode.ScaleInvariant;
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
                name = "Generated Gameplay Light Shaft",
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
            vertices[vertexIndex + 2] = -widthDirection * beamEndRadius + Vector3.forward * length;
            vertices[vertexIndex + 3] = widthDirection * beamEndRadius + Vector3.forward * length;

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
                name = "Generated Gameplay Light Shaft Material",
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
                name = "Generated Gameplay Light Decal Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        SetMaterialTexture(decalMaterial, GetDecalTexture(), "Base_Map", "_Base_Map", "_BaseMap", "_MainTex");
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
                name = "Generated Gameplay Light Radial Decal",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        if (cachedDecalColor != lightColor || !Mathf.Approximately(cachedDecalOpacity, decalOpacity) || !Mathf.Approximately(cachedDecalEdgeSoftness, decalEdgeSoftness))
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
                float fill = 1f - SmoothStep(1f - decalEdgeSoftness, 1f, distance);
                float centerGlow = 1f - SmoothStep(0f, 0.55f, distance);
                float alpha = Mathf.Clamp01(fill * 0.75f + centerGlow * 0.25f) * decalOpacity;

                Color pixel = lightColor;
                pixel.a = alpha;
                pixels[y * size + x] = pixel;
            }
        }

        decalTexture.SetPixels(pixels);
        decalTexture.Apply(false, false);

        cachedDecalColor = lightColor;
        cachedDecalOpacity = decalOpacity;
        cachedDecalEdgeSoftness = decalEdgeSoftness;
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

    private static T GetOrAdd<T>(GameObject targetObject) where T : Component
    {
        T component = targetObject.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return targetObject.AddComponent<T>();
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
