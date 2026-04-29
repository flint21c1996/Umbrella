using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

using BehaviorAction = Unity.Behavior.Action;
using NodeStatus = Unity.Behavior.Node.Status;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "NPC Set Signal",
    description: "Sets an NPCBehaviorSignal value.",
    story: "[SignalObject] sets NPC signal to [Value]",
    category: "Action/NPC",
    id: "0e7400e2c2c9474a873b58d6945671d8")]
// Behavior Graph에서 완료 플래그나 이벤트 플래그를 true/false로 바꾸기 위한 Action이다.
public partial class NPCSetSignalAction : BehaviorAction
{
    [SerializeReference] public BlackboardVariable<GameObject> SignalObject;
    [SerializeReference] public BlackboardVariable<bool> Value = new BlackboardVariable<bool>(true);

    protected override NodeStatus OnStart()
    {
        NPCBehaviorSignal signal = ResolveSignal(SignalObject?.Value);
        if (signal == null)
        {
            return NodeStatus.Failure;
        }

        signal.Set(Value == null || Value.Value);
        return NodeStatus.Success;
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
