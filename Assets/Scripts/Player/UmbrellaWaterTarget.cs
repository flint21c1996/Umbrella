using UnityEngine;

public class UmbrellaWaterTarget : MonoBehaviour
{
    // 물을 받았을 때 실제 반응이 일어나는지를 빠르게 확인하기 위한 최소 타깃이다.
    public float requiredWater = 2.0f;
    public Renderer targetRenderer;
    public Color idleColor = Color.gray;
    public Color activatedColor = Color.green;

    [SerializeField] private float receivedWater;
    [SerializeField] private bool isActivated;

    public bool IsActivated => isActivated;

    private void Start()
    {
        RefreshVisual();
    }

    public void ReceiveWater(float amount)
    {
        // 현재 단계에서는 누적량만 체크하고, 기준치를 넘으면 바로 활성화하는 단순한 구조로 둔다.
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
        material.color = isActivated ? activatedColor : idleColor;
    }
}
