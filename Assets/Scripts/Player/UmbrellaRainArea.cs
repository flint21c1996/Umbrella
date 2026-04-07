using UnityEngine;

public class UmbrellaRainArea : MonoBehaviour
{
    // 비 영역은 물리 효과 전체를 구현하기보다, 우산이 물을 저장할 수 있는지 먼저 검증하기 위한 테스트 구역이다.
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

        umbrellaController.AddWater(rainFillRate * Time.deltaTime);
    }
}
