using System;
using UnityEngine;

// 퍼즐 조건으로 사용할 수 있는 컴포넌트의 공통 기반 클래스.
// 예: 눌린 발판, 켜진 레버, 충분한 물이 담긴 물체.
// PuzzleConditionGroup은 구체적인 조건 종류를 몰라도 IsSatisfied와 Changed만 보고 판단한다.
public abstract class PuzzleConditionSource : MonoBehaviour
{
    // 현재 조건이 만족되었는지 반환한다.
    // abstract라서 자식 클래스가 자기 방식대로 반드시 구현해야 한다.
    public abstract bool IsSatisfied { get; }

    // 조건 상태가 바뀐 순간에만 발생하는 이벤트.
    // 매 프레임 Update로 검사하지 않고, 변화가 있을 때만 ConditionGroup을 깨우기 위해 사용한다.
    public event Action Changed;

    // 자식 클래스가 만족/불만족 상태를 바꾼 뒤 호출한다.
    // 외부에서는 마음대로 호출하지 못하게 protected로 제한한다.
    protected void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
