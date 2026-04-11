using UnityEngine;

public class UmbrellaRainArea : MonoBehaviour
{
    // 비 영역 전체를 물리 효과로 구현하기보다,
    // 플레이어 젖음과 우산 물 저장이 상태에 따라 어떻게 달라지는지 먼저 검증하기 위한 테스트 구역이다.
    public float rainFillRate = 1.0f;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }

        triggerCollider.isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        PlayerUmbrellaController umbrellaController = other.GetComponentInParent<PlayerUmbrellaController>();
        if (umbrellaController == null)
        {
            return;
        }

        umbrellaController.ApplyRainExposure(rainFillRate * Time.deltaTime);
    }
}
