using UnityEngine;

public class UmbrellaWaterTarget : MonoBehaviour
{
    private static bool debugOverlayEnabled;

    public float requiredWater = 0.5f;
    public Renderer targetRenderer;
    public Color idleColor = Color.gray;
    public Color activatedColor = Color.green;

    [SerializeField] private float receivedWater;
    [SerializeField] private bool isActivated;

    public bool IsActivated => isActivated;
    public float ReceivedWater => receivedWater;

    public static void SetDebugOverlayEnabled(bool enabled)
    {
        debugOverlayEnabled = enabled;
    }

    private void Reset()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }
    }

    private void Start()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        RefreshVisual();
    }

    public void ReceiveWater(float amount)
    {
        if (isActivated || amount <= 0.0f)
        {
            return;
        }

        receivedWater += amount;

        if (receivedWater >= requiredWater)
        {
            isActivated = true;
        }

        RefreshVisual();
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
        float height = 60.0f;
        float x = screenPoint.x - width * 0.5f;
        float y = Screen.height - screenPoint.y - height;

        GUI.Box(new Rect(x, y, width, height), "Water Target");
        GUI.Label(new Rect(x + 8.0f, y + 20.0f, width - 16.0f, 16.0f), $"Req: {requiredWater:F1}");
        GUI.Label(new Rect(x + 8.0f, y + 36.0f, width - 16.0f, 16.0f), $"Cur: {receivedWater:F2}  Active: {isActivated}");
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
