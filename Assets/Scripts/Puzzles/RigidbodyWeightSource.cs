using UnityEngine;

// Rigidbody.mass를 그대로 무게 소스로 제공하는 컴포넌트.
// 플레이어 Rigidbody.mass가 우산 물 무게까지 반영하고 있으므로, 플레이어 자체를 저울 값으로 쓸 때도 사용할 수 있다.
[DisallowMultipleComponent]
public class RigidbodyWeightSource : PuzzleWeightSource
{
    [Tooltip("현재 무게 값으로 사용할 Rigidbody. 이 Rigidbody의 mass를 읽는다.")]
    [SerializeField] private Rigidbody targetRigidbody;

    // PlayerUmbrellaController가 플레이어 Rigidbody.mass에 물 무게를 더하므로,
    // 플레이어를 저울에 올리고 싶을 때는 이 값을 그대로 읽으면 된다.
    public override float CurrentWeight => targetRigidbody != null ? targetRigidbody.mass : 0.0f;

    // 컴포넌트를 처음 붙였을 때 부모 쪽 Rigidbody를 자동으로 찾아둔다.
    private void Reset()
    {
        CacheRigidbody();
    }

    // 런타임에서도 참조가 비어 있으면 한 번 더 보정한다.
    private void Awake()
    {
        CacheRigidbody();
    }

    // Inspector에서 컴포넌트 위치를 바꾸거나 값을 수정했을 때도 참조를 다시 채운다.
    private void OnValidate()
    {
        CacheRigidbody();
    }

    private void CacheRigidbody()
    {
        // 자기 오브젝트나 부모 중 가장 가까운 Rigidbody를 기본 대상으로 삼는다.
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponentInParent<Rigidbody>();
        }
    }
}
