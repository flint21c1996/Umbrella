using System;
using UnityEngine;

public class UmbrellaWaterTarget : MonoBehaviour
{
    private static bool debugOverlayEnabled;

    [Header("Water")]
    public float requiredWater = 0.5f;

    [Header("Weight")]
    [SerializeField] private Rigidbody weightedRigidbody;
    [SerializeField] private bool addWaterToRigidbodyMass = true;
    [SerializeField] private float waterWeightMultiplier = 1.0f;

    [Header("Visual")]
    public Renderer targetRenderer;
    public Color idleColor = Color.gray;
    public Color activatedColor = Color.green;

    [SerializeField] private float receivedWater;
    [SerializeField] private bool isActivated;

    private float baseMass;

    public bool IsActivated => isActivated;
    public float ReceivedWater => receivedWater;
    public float AddedWeight => receivedWater * waterWeightMultiplier;

    public event Action WaterChanged;

    public static void SetDebugOverlayEnabled(bool enabled)
    {
        debugOverlayEnabled = enabled;
    }

    private void Reset()
    {
        CacheWeightedRigidbody();

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }
    }

    private void Start()
    {
        CacheWeightedRigidbody();

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        baseMass = weightedRigidbody != null ? weightedRigidbody.mass : 0.0f;
        receivedWater = Mathf.Clamp(receivedWater, 0.0f, requiredWater);
        isActivated = receivedWater >= requiredWater;
        RefreshWeight();
        RefreshVisual();
        NotifyWaterChanged();
    }

    private void OnValidate()
    {
        requiredWater = Mathf.Max(0.0f, requiredWater);
        receivedWater = Mathf.Clamp(receivedWater, 0.0f, requiredWater);
        waterWeightMultiplier = Mathf.Max(0.0f, waterWeightMultiplier);
    }

    public void ReceiveWater(float amount)
    {
        if (isActivated || amount <= 0.0f)
        {
            return;
        }

        receivedWater = Mathf.Clamp(receivedWater + amount, 0.0f, requiredWater);

        if (receivedWater >= requiredWater)
        {
            isActivated = true;
        }

        RefreshWeight();
        RefreshVisual();
        NotifyWaterChanged();
    }

    private void CacheWeightedRigidbody()
    {
        if (weightedRigidbody == null)
        {
            weightedRigidbody = GetComponentInParent<Rigidbody>();
        }
    }

    private void RefreshWeight()
    {
        if (!addWaterToRigidbodyMass || weightedRigidbody == null)
        {
            return;
        }

        weightedRigidbody.mass = baseMass + AddedWeight;
    }

    private void NotifyWaterChanged()
    {
        WaterChanged?.Invoke();
    }

    private void RefreshVisual()
    {
        if (targetRenderer == null)
        {
            return;
        }

        Material material = targetRenderer.material;
        Color targetColor = isActivated ? activatedColor : idleColor;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", targetColor);
            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.color = targetColor;
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !debugOverlayEnabled)
        {
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        Vector3 worldLabelPosition = GetLabelWorldPosition();
        Vector3 screenPoint = targetCamera.WorldToScreenPoint(worldLabelPosition);
        if (screenPoint.z <= 0.0f)
        {
            return;
        }

        float width = 180.0f;
        float height = 78.0f;
        float x = screenPoint.x - width * 0.5f;
        float y = Screen.height - screenPoint.y - height;

        GUI.Box(new Rect(x, y, width, height), "Water Target");
        GUI.Label(new Rect(x + 8.0f, y + 20.0f, width - 16.0f, 16.0f), $"Req: {requiredWater:F1}");
        GUI.Label(new Rect(x + 8.0f, y + 36.0f, width - 16.0f, 16.0f), $"Cur: {receivedWater:F2}  Active: {isActivated}");
        GUI.Label(new Rect(x + 8.0f, y + 52.0f, width - 16.0f, 16.0f), $"Water Weight: +{AddedWeight:F2} kg");
    }

    private Vector3 GetLabelWorldPosition()
    {
        if (targetRenderer != null)
        {
            Bounds rendererBounds = targetRenderer.bounds;
            return rendererBounds.center + Vector3.up * (rendererBounds.extents.y + 0.25f);
        }

        Collider targetCollider = GetComponentInChildren<Collider>();
        if (targetCollider != null)
        {
            Bounds colliderBounds = targetCollider.bounds;
            return colliderBounds.center + Vector3.up * (colliderBounds.extents.y + 0.25f);
        }

        return transform.position + Vector3.up * 0.5f;
    }
}
