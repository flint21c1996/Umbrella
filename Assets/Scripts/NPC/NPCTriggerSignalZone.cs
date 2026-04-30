using UnityEngine;

[RequireComponent(typeof(Collider))]
// NPCBehaviorSignal 값을 바꾸는 간단한 트리거 구역이다.
// 퍼즐 조건을 만들지 않고 "플레이어가 특정 구역에 들어왔다" 같은 조건을 만들 때 사용한다.
public class NPCTriggerSignalZone : MonoBehaviour
{
    [SerializeField] private NPCBehaviorSignal signal;
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool setOnEnter = true;
    [SerializeField] private bool resetOnExit;

    private void Reset()
    {
        signal = GetComponent<NPCBehaviorSignal>();
        if (signal == null)
        {
            signal = GetComponentInParent<NPCBehaviorSignal>();
        }

        EnsureTriggerCollider();
    }

    private void Awake()
    {
        if (signal == null)
        {
            signal = GetComponent<NPCBehaviorSignal>();
        }

        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!setOnEnter || !IsTarget(other))
        {
            return;
        }

        signal?.SetTrue();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!resetOnExit || !IsTarget(other))
        {
            return;
        }

        signal?.SetFalse();
    }

    // 플레이어의 히트박스가 자식 오브젝트에 있을 수 있으므로 부모 방향으로 태그를 확인한다.
    private bool IsTarget(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(targetTag))
        {
            return true;
        }

        Transform current = other.transform;
        while (current != null)
        {
            if (current.gameObject.tag == targetTag)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    // 이 컴포넌트는 트리거 구역으로만 의미가 있으므로 Collider를 자동으로 Trigger로 유지한다.
    private void EnsureTriggerCollider()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
        }
    }
}
