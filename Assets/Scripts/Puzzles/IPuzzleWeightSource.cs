using UnityEngine;

// 양팔저울처럼 무게 값만 필요한 퍼즐에서 공통으로 읽을 수 있는 인터페이스.
// WeightSensor, WeightedButton, 고정 추, Rigidbody, 물 저장량 등 서로 다른 구현을 같은 방식으로 다루기 위해 사용한다.
public interface IPuzzleWeightSource
{
    // 양팔저울은 이 값만 읽는다.
    // 실제 무게가 센서에서 오든, 물 저장량에서 오든, Rigidbody.mass에서 오든 신경 쓰지 않게 하기 위함이다.
    float CurrentWeight { get; }
}

// Inspector에 붙일 수 있는 무게 소스 컴포넌트의 공통 기반 클래스.
// WeightedButton처럼 이미 다른 기반 클래스를 상속 중인 컴포넌트는 이 클래스 대신 IPuzzleWeightSource만 직접 구현하면 된다.
public abstract class PuzzleWeightSource : MonoBehaviour, IPuzzleWeightSource
{
    // MonoBehaviour로 붙일 수 있는 무게 소스들은 이 값을 자기 방식대로 계산해서 반환한다.
    public abstract float CurrentWeight { get; }
}
