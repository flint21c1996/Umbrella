using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

using BehaviorAction = Unity.Behavior.Action;
using NodeStatus = Unity.Behavior.Node.Status;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "NPC Set Animator Trigger",
    description: "Sets an Animator trigger on an NPC or one of its children.",
    story: "[Agent] sets animator trigger [TriggerName]",
    category: "Action/NPC",
    id: "7b527d06bf474521a1a80248a60f0e36")]
// Interact, Talk처럼 한 번 실행하는 애니메이션 이벤트를 Behavior Graph에서 호출하기 위한 Action이다.
public partial class NPCSetAnimatorTriggerAction : BehaviorAction
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<string> TriggerName = new BlackboardVariable<string>("Interact");

    protected override NodeStatus OnStart()
    {
        Animator animator = ResolveAnimator(Agent?.Value);
        string triggerName = TriggerName?.Value;

        // 예외를 던지는 대신 노드를 실패 처리해서 Graph가 정상적으로 실패 흐름을 처리하게 한다.
        if (animator == null || string.IsNullOrEmpty(triggerName))
        {
            return NodeStatus.Failure;
        }

        int triggerHash = Animator.StringToHash(triggerName);
        if (!HasTrigger(animator, triggerHash))
        {
            return NodeStatus.Failure;
        }

        animator.SetTrigger(triggerHash);
        return NodeStatus.Success;
    }

    // 임시 NPC 모델 구조를 고려해 Animator가 VisualRoot 같은 자식 오브젝트에 있어도 찾는다.
    private static Animator ResolveAnimator(GameObject agentObject)
    {
        if (agentObject == null)
        {
            return null;
        }

        Animator animator = agentObject.GetComponent<Animator>();
        if (animator != null)
        {
            return animator;
        }

        return agentObject.GetComponentInChildren<Animator>();
    }

    // Graph의 TriggerName 오타로 잘못된 Trigger가 호출되지 않도록 확인한다.
    private static bool HasTrigger(Animator animator, int triggerHash)
    {
        if (animator.runtimeAnimatorController == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == triggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                return true;
            }
        }

        return false;
    }
}
