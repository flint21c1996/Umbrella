using UnityEngine;

[RequireComponent(typeof(Collider))]
public class UmbrellaRainArea : MonoBehaviour
{
    // 비 영역은 실제 파티클이나 날씨 시스템보다 먼저,
    // 우산이 물을 저장하는 루프가 동작하는지만 검증하기 위한 테스트 컴포넌트다.
    public float rainFillRate = 1.0f;

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // 플레이어 쪽 상태 판단은 UmbrellaController가 맡고,
        // 이 영역은 초당 얼마만큼 물을 줄지만 결정한다.
        if (!other.TryGetComponent(out PlayerUmbrellaController umbrellaController))
        {
            return;
        }

        umbrellaController.AddWater(rainFillRate * Time.deltaTime);
    }
}
