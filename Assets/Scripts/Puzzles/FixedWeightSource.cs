using UnityEngine;

// 변하지 않는 고정 무게를 제공하는 소스.
// 저울 한쪽에 "3kg짜리 추" 같은 기준값을 놓고 싶을 때 사용한다.
[DisallowMultipleComponent]
public class FixedWeightSource : PuzzleWeightSource
{
    [Tooltip("양팔저울 퍼즐에서 사용할 고정 무게 값.")]
    [SerializeField] private float weight = 1.0f;

    // 고정 추처럼 항상 같은 무게를 제공한다.
    public override float CurrentWeight => weight;

    // Inspector에서 실수로 음수를 넣어도 저울 계산에는 0kg 이상만 들어가게 한다.
    private void OnValidate()
    {
        weight = Mathf.Max(0.0f, weight);
    }
}
