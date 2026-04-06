using UnityEngine;

public class UmbrellaWaterTarget : MonoBehaviour
{
    // 우산에서 붓는 물이 실제 퍼즐 결과로 이어지는지 확인하기 위한 최소 타깃이다.
    // 처음에는 색상 변화만으로도 입력과 결과의 연결을 검증할 수 있다.
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
        // 이 단계에서는 물의 총량만 누적해서 문턱값을 넘으면 활성화한다.
        // 이후 필요하면 시간 경과에 따른 증발이나 상태 변화로 확장할 수 있다.
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
