using System;
using UnityEngine;

[DisallowMultipleComponent]
// NPC 조건에 재사용할 수 있는 간단한 bool 신호이다.
// 트리거 구역, 퍼즐 이벤트, 컷신 스크립트가 값을 바꾸고 Behavior Graph 조건이 이 값을 읽는다.
public class NPCBehaviorSignal : MonoBehaviour
{
    [SerializeField] private bool isSet;

    public bool IsSet => isSet;

    // 신호 변경에 즉시 반응해야 하는 시스템이 생길 경우를 대비해 열어둔다.
    public event Action Changed;

    public void Set(bool value)
    {
        if (isSet == value)
        {
            return;
        }

        isSet = value;
        Changed?.Invoke();
    }

    public void SetTrue()
    {
        Set(true);
    }

    public void SetFalse()
    {
        Set(false);
    }

    public void Toggle()
    {
        Set(!isSet);
    }
}
