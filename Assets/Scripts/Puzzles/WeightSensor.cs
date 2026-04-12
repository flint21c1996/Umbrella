using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class WeightSensor : MonoBehaviour
{
    [SerializeField] private float currentWeight;

    // 같은 Rigidbody가 여러 Collider로 센서에 닿을 수 있어서 접촉 횟수를 함께 기록한다.
    private readonly Dictionary<Rigidbody, int> bodyContactCounts = new Dictionary<Rigidbody, int>();
    private readonly List<Rigidbody> missingBodies = new List<Rigidbody>();

    public float CurrentWeight => currentWeight;
    public int BodyCount => bodyContactCounts.Count;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnDisable()
    {
        bodyContactCounts.Clear();
        currentWeight = 0.0f;
    }

    private void FixedUpdate()
    {
        RefreshCurrentWeight();
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterBody(other.attachedRigidbody);
    }

    private void OnTriggerExit(Collider other)
    {
        UnregisterBody(other.attachedRigidbody);
    }

    private void RegisterBody(Rigidbody body)
    {
        if (body == null)
        {
            return;
        }

        if (bodyContactCounts.TryGetValue(body, out int contactCount))
        {
            bodyContactCounts[body] = contactCount + 1;
            return;
        }

        bodyContactCounts.Add(body, 1);
    }

    private void UnregisterBody(Rigidbody body)
    {
        if (body == null || !bodyContactCounts.TryGetValue(body, out int contactCount))
        {
            return;
        }

        contactCount--;
        if (contactCount <= 0)
        {
            bodyContactCounts.Remove(body);
            return;
        }

        bodyContactCounts[body] = contactCount;
    }

    private void RefreshCurrentWeight()
    {
        float totalWeight = 0.0f;
        missingBodies.Clear();

        foreach (KeyValuePair<Rigidbody, int> entry in bodyContactCounts)
        {
            Rigidbody body = entry.Key;
            if (body == null)
            {
                missingBodies.Add(body);
                continue;
            }

            // 우산에 물이 담기면 Player Rigidbody.mass가 바뀌므로 매 FixedUpdate마다 다시 읽는다.
            totalWeight += body.mass;
        }

        for (int i = 0; i < missingBodies.Count; i++)
        {
            bodyContactCounts.Remove(missingBodies[i]);
        }

        currentWeight = totalWeight;
    }

    private void EnsureTriggerCollider()
    {
        Collider sensorCollider = GetComponent<Collider>();
        if (sensorCollider != null)
        {
            sensorCollider.isTrigger = true;
        }
    }
}
