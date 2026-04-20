using UnityEngine;

// UmbrellaWaterTarget에 저장된 물의 양을 무게 소스로 제공하는 컴포넌트.
// 물 저장 오브젝트 자체를 저울 한쪽의 가상 무게로 쓰고 싶을 때 사용한다.
[DisallowMultipleComponent]
public class WaterAmountWeightSource : PuzzleWeightSource
{
    [Tooltip("저장된 물의 양을 무게로 변환할 물 저장 대상.")]
    [SerializeField] private UmbrellaWaterTarget waterTarget;

    [Tooltip("UmbrellaWaterTarget의 AddedWeight 값을 사용한다. 끄면 ReceivedWater * Weight Multiplier를 사용한다.")]
    [SerializeField] private bool useTargetAddedWeight = true;

    [Tooltip("Use Target Added Weight를 끈 경우 물 저장량에 곱할 무게 배율.")]
    [SerializeField] private float weightMultiplier = 1.0f;

    // 물 저장량을 저울에서 읽을 수 있는 무게 값으로 변환한다.
    public override float CurrentWeight
    {
        get
        {
            // 물을 받을 대상이 없으면 이 소스는 무게를 제공하지 않는 것으로 본다.
            if (waterTarget == null)
            {
                return 0.0f;
            }

            // UmbrellaWaterTarget이 Rigidbody에 더하는 무게와 같은 값을 쓰고 싶을 때의 기본 경로.
            if (useTargetAddedWeight)
            {
                return waterTarget.AddedWeight;
            }

            // 퍼즐에 따라 "물 1만큼 = n kg"처럼 별도 비율을 쓰고 싶을 때 사용한다.
            return waterTarget.ReceivedWater * weightMultiplier;
        }
    }

    // 컴포넌트를 물 저장 오브젝트에 붙였을 때 같은 오브젝트/부모의 UmbrellaWaterTarget을 자동으로 찾는다.
    private void Reset()
    {
        CacheWaterTarget();
    }

    // 런타임 시작 시 참조가 비어 있으면 한 번 더 자동 연결한다.
    private void Awake()
    {
        CacheWaterTarget();
    }

    // Inspector에서 multiplier를 음수로 넣지 못하게 하고, 물 대상 참조도 보정한다.
    private void OnValidate()
    {
        weightMultiplier = Mathf.Max(0.0f, weightMultiplier);
        CacheWaterTarget();
    }

    private void CacheWaterTarget()
    {
        // 자기 오브젝트 또는 부모에 물 저장 컴포넌트가 있으면 기본 대상으로 사용한다.
        if (waterTarget == null)
        {
            waterTarget = GetComponentInParent<UmbrellaWaterTarget>();
        }
    }
}
