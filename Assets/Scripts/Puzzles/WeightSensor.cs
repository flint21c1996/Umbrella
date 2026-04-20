using System.Collections.Generic;
using UnityEngine;

// Trigger 영역 안에 들어온 Rigidbody들의 질량을 합산하는 무게 센서.
// 발판, 양팔저울 접시처럼 "위에 올라온 것들의 총 무게"가 필요한 곳에 붙인다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class WeightSensor : PuzzleWeightSource
{
    [SerializeField] private float currentWeight;

    // 같은 Rigidbody가 여러 Collider로 센서에 닿을 수 있어서 접촉 횟수를 함께 기록한다.
    private readonly Dictionary<Rigidbody, int> bodyContactCounts = new Dictionary<Rigidbody, int>();
    private readonly List<Rigidbody> missingBodies = new List<Rigidbody>();

    public override float CurrentWeight => currentWeight;
    public int BodyCount => bodyContactCounts.Count;

    // 컴포넌트를 처음 붙였을 때 Collider를 센서용 Trigger로 맞춘다.
    private void Reset()
    {
        EnsureTriggerCollider();
    }

    // 런타임에서도 Collider가 Trigger가 아니면 센서가 막는 벽처럼 동작하므로 한 번 더 보정한다.
    private void Awake()
    {
        EnsureTriggerCollider();
    }

    // 비활성화될 때 남아 있던 접촉 정보를 지워, 다시 켰을 때 이전 무게가 남지 않게 한다.
    private void OnDisable()
    {
        bodyContactCounts.Clear();
        currentWeight = 0.0f;
    }

    // Rigidbody.mass가 런타임에 바뀔 수 있으므로 물리 주기마다 합산값을 다시 계산한다.
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
        // Rigidbody가 없는 Trigger/Collider는 무게를 낼 수 없으므로 무시한다.
        if (body == null)
        {
            return;
        }

        // 하나의 Rigidbody가 여러 Collider로 들어올 수 있어 접촉 횟수를 누적한다.
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

        // 같은 Rigidbody의 마지막 Collider가 빠져나갔을 때만 센서 목록에서 제거한다.
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
            // 센서는 충돌을 막는 용도가 아니라 겹침을 감지하는 용도라 Trigger로 사용한다.
            sensorCollider.isTrigger = true;
        }
    }
}
