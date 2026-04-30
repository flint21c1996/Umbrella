using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

[Serializable, GeneratePropertyBag]
[Condition(
    name: "NPC Signal Is Set",
    description: "Checks whether an NPCBehaviorSignal is set.",
    story: "[SignalObject] signal is set with invert [Invert]",
    category: "Conditions/NPC",
    id: "4e1019cccba84d7e9eaa0f363f9eb3c4")]
// 씬에 배치된 NPCBehaviorSignal 값을 Behavior Graph 조건으로 읽기 위한 노드이다.
public partial class NPCBehaviorSignalCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> SignalObject;
    [SerializeReference] public BlackboardVariable<bool> Invert = new BlackboardVariable<bool>(false);

    public override bool IsTrue()
    {
        NPCBehaviorSignal signal = ResolveSignal(SignalObject?.Value);
        bool isSatisfied = signal != null && signal.IsSet;
        return Invert != null && Invert.Value ? !isSatisfied : isSatisfied;
    }

    // 신호 오브젝트 자체뿐 아니라 부모/자식에 붙은 신호도 찾아서 씬 연결이 덜 까다롭도록 한다.
    private static NPCBehaviorSignal ResolveSignal(GameObject signalObject)
    {
        if (signalObject == null)
        {
            return null;
        }

        NPCBehaviorSignal signal = signalObject.GetComponent<NPCBehaviorSignal>();
        if (signal != null)
        {
            return signal;
        }

        signal = signalObject.GetComponentInParent<NPCBehaviorSignal>();
        if (signal != null)
        {
            return signal;
        }

        return signalObject.GetComponentInChildren<NPCBehaviorSignal>();
    }
}
