using UnityEngine;

public class UmbrellaPickup : MonoBehaviour
{
    // 현재 구조에서는 우산 본체 전체를 플레이어의 attach point로 옮기는 편이 자연스럽다.
    // PourOrigin 같은 자식 오브젝트도 같이 따라가야 하므로 시각 루트를 통째로 넘긴다.
    public GameObject pickupVisualRoot;
    public bool destroyOnPickup = true;

    private void Reset()
    {
        if (pickupVisualRoot == null)
        {
            pickupVisualRoot = gameObject;
        }

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }

        triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerUmbrellaController umbrellaController = other.GetComponentInParent<PlayerUmbrellaController>();
        if (umbrellaController == null || umbrellaController.HasUmbrella)
        {
            return;
        }

        GameObject pickupRoot = pickupVisualRoot != null ? pickupVisualRoot : gameObject;
        umbrellaController.AcquireUmbrella(pickupRoot);

        if (pickupRoot == gameObject)
        {
            DisablePickupComponents();
            if (destroyOnPickup)
            {
                Destroy(this);
            }

            return;
        }

        if (destroyOnPickup)
        {
            Destroy(gameObject);
            return;
        }

        DisablePickupComponents();
    }

    private void DisablePickupComponents()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        Rigidbody rigidbodyComponent = GetComponent<Rigidbody>();
        if (rigidbodyComponent != null)
        {
            if (!rigidbodyComponent.isKinematic)
            {
                rigidbodyComponent.linearVelocity = Vector3.zero;
                rigidbodyComponent.angularVelocity = Vector3.zero;
            }

            rigidbodyComponent.isKinematic = true;
        }
    }
}
