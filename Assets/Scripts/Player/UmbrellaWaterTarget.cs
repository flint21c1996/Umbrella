using System;
using UnityEngine;

// 우산에서 부은 물을 저장하는 대상.
// 저장량이 기준치에 도달하면 활성화 상태가 되고, 선택적으로 Rigidbody 질량에도 물 무게를 더한다.
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

    // 물 저장량이 바뀔 때 WaterAmountCondition 같은 외부 조건 컴포넌트가 반응하도록 알린다.
    public event Action WaterChanged;

    // GameDebugController가 F3 디버그 UI 토글을 전달할 때 사용한다.
    public static void SetDebugOverlayEnabled(bool enabled)
    {
        debugOverlayEnabled = enabled;
    }

    // 컴포넌트를 처음 붙였을 때 자주 필요한 참조를 자동으로 채운다.
    private void Reset()
    {
        CacheWeightedRigidbody();

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }
    }

    // 런타임 시작 시 기준 질량과 초기 물 상태를 정리한다.
    // baseMass를 먼저 저장해야 물 무게를 여러 번 더하는 문제가 생기지 않는다.
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

    // Inspector에서 음수 값이나 현재 저장량이 요구량을 넘는 상태를 방지한다.
    private void OnValidate()
    {
        requiredWater = Mathf.Max(0.0f, requiredWater);
        receivedWater = Mathf.Clamp(receivedWater, 0.0f, requiredWater);
        waterWeightMultiplier = Mathf.Max(0.0f, waterWeightMultiplier);
    }

    // 우산에서 물을 부을 때 호출된다.
    // 물은 requiredWater를 넘지 않게 저장하고, 상태/무게/시각/조건 이벤트를 함께 갱신한다.
    public void ReceiveWater(float amount)
    {
        // 이미 활성화됐거나 의미 없는 양이면 아무것도 하지 않는다.
        if (isActivated || amount <= 0.0f)
        {
            return;
        }

        receivedWater = Mathf.Clamp(receivedWater + amount, 0.0f, requiredWater);

        // 요구량에 도달하면 한 번 활성화된다.
        if (receivedWater >= requiredWater)
        {
            isActivated = true;
        }

        RefreshWeight();
        RefreshVisual();
        NotifyWaterChanged();
    }

    // 물 무게를 더할 Rigidbody를 찾는다.
    // 물 저장 오브젝트가 상자의 자식으로 들어가는 구조를 고려해 부모에서도 찾는다.
    private void CacheWeightedRigidbody()
    {
        if (weightedRigidbody == null)
        {
            weightedRigidbody = GetComponentInParent<Rigidbody>();
        }
    }

    // 저장된 물의 양을 Rigidbody 질량에 반영한다.
    // 기준 질량 + 물 무게 방식이라 물이 추가될 때마다 누적 중복이 생기지 않는다.
    private void RefreshWeight()
    {
        if (!addWaterToRigidbodyMass || weightedRigidbody == null)
        {
            return;
        }

        weightedRigidbody.mass = baseMass + AddedWeight;
    }

    // 물 저장량이 바뀌었다는 사실만 외부에 알린다.
    // 어떤 퍼즐이 반응할지는 WaterAmountCondition/ConditionGroup이 결정한다.
    private void NotifyWaterChanged()
    {
        WaterChanged?.Invoke();
    }

    // 활성화 여부에 따라 머티리얼 색을 바꾼다.
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
        // F3 디버그 오버레이가 켜진 Play 모드에서만 물 저장 정보를 표시한다.
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

    // 디버그 라벨을 띄울 월드 위치를 계산한다.
    // Renderer/Collider의 위쪽을 우선 사용해서 라벨이 물체 중앙을 가리지 않게 한다.
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
