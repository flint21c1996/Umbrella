using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

[Serializable, GeneratePropertyBag]
[Condition(
    name: "NPC Puzzle Condition Is Satisfied",
    description: "Checks a PuzzleConditionSource or PuzzleConditionGroup from the current project.",
    story: "[ConditionObject] puzzle condition is satisfied with invert [Invert]",
    category: "Conditions/NPC",
    id: "790e2f13b2db4cb1bcd6e6ca87bf4e9e")]
// 기존 퍼즐 조건 컴포넌트를 Behavior Graph 조건으로 재사용하기 위한 노드이다.
// NPC 행동을 위해 퍼즐 판정 로직을 새로 만들지 않도록 한다.
public partial class NPCPuzzleConditionIsSatisfiedCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> ConditionObject;
    [SerializeReference] public BlackboardVariable<bool> Invert = new BlackboardVariable<bool>(false);

    public override bool IsTrue()
    {
        bool isSatisfied = TryReadCondition(ConditionObject?.Value, out bool value) && value;
        return Invert != null && Invert.Value ? !isSatisfied : isSatisfied;
    }

    private static bool TryReadCondition(GameObject conditionObject, out bool value)
    {
        value = false;

        if (conditionObject == null)
        {
            return false;
        }

        // 여러 조건을 묶은 퍼즐일 수 있으므로 Group을 먼저 확인한다.
        PuzzleConditionGroup group = FindConditionGroup(conditionObject);
        if (group != null)
        {
            value = group.IsSatisfied;
            return true;
        }

        // 버튼 하나, 트리거 하나처럼 단일 조건인 경우 Source를 확인한다.
        PuzzleConditionSource source = FindConditionSource(conditionObject);
        if (source != null)
        {
            value = source.IsSatisfied;
            return true;
        }

        return false;
    }

    // 정확한 조건 오브젝트가 아니라 주변 부모/루트 오브젝트를 넣어도 동작하도록 탐색한다.
    private static PuzzleConditionGroup FindConditionGroup(GameObject conditionObject)
    {
        PuzzleConditionGroup group = conditionObject.GetComponent<PuzzleConditionGroup>();
        if (group != null)
        {
            return group;
        }

        group = conditionObject.GetComponentInParent<PuzzleConditionGroup>();
        if (group != null)
        {
            return group;
        }

        return conditionObject.GetComponentInChildren<PuzzleConditionGroup>();
    }

    // Group과 동일하게 단일 조건 Source도 부모/자식 범위에서 느슨하게 탐색한다.
    private static PuzzleConditionSource FindConditionSource(GameObject conditionObject)
    {
        PuzzleConditionSource source = conditionObject.GetComponent<PuzzleConditionSource>();
        if (source != null)
        {
            return source;
        }

        source = conditionObject.GetComponentInParent<PuzzleConditionSource>();
        if (source != null)
        {
            return source;
        }

        return conditionObject.GetComponentInChildren<PuzzleConditionSource>();
    }
}
