#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WaterBasinTarget))]
[CanEditMultipleObjects]
public class WaterBasinTargetEditor : Editor
{
    private void OnSceneGUI()
    {
        WaterBasinTarget source = target as WaterBasinTarget;
        if (source == null)
        {
            return;
        }

        WaterBasinTarget[] allTargets = UnityEngine.Object.FindObjectsByType<WaterBasinTarget>();

        Handles.color = Color.yellow;
        for (int candidateIndex = 0; candidateIndex < allTargets.Length; candidateIndex++)
        {
            WaterBasinTarget candidate = allTargets[candidateIndex];
            if (candidate != null && AreTargetsAdjacent(source, candidate))
            {
                Handles.DrawLine(source.transform.position, candidate.transform.position);
            }
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8.0f);
        EditorGUILayout.LabelField("물 연결 제작 도구", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "인접 자동 연결은 에디터에서 Connected Targets를 채우는 작업입니다. 플레이 중에는 저장된 연결 목록만 사용합니다.",
            MessageType.Info);

        if (GUILayout.Button("검사 중인 대상에 인접 연결 추가"))
        {
            WaterBasinTargetEditorConnectionTool.AddAdjacentConnections(GetInspectorTargets(), true);
        }

        if (GUILayout.Button("씬 전체에 인접 연결 추가"))
        {
            WaterBasinTargetEditorConnectionTool.AddAdjacentConnections(
                UnityEngine.Object.FindObjectsByType<WaterBasinTarget>(),
                true);
        }

        EditorGUILayout.Space(4.0f);
        if (GUILayout.Button("검사 중인 대상의 연결 비우기"))
        {
            WaterBasinTargetEditorConnectionTool.ClearConnectionsWithConfirm(GetInspectorTargets());
        }
    }

    private WaterBasinTarget[] GetInspectorTargets()
    {
        List<WaterBasinTarget> result = new List<WaterBasinTarget>();
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] is WaterBasinTarget basinTarget)
            {
                result.Add(basinTarget);
            }
        }

        return result.ToArray();
    }

    private static bool AreTargetsAdjacent(WaterBasinTarget first, WaterBasinTarget second)
    {
        return WaterBasinTargetEditorConnectionTool.AreTargetsAdjacent(first, second);
    }
}

internal static class WaterBasinTargetEditorConnectionTool
{
    private const float Epsilon = 0.0001f;
    private const float AdjacentConnectionTolerance = 0.02f;
    private const string UndoName = "물 연결 편집";

    [MenuItem("Tools/Water Basin/선택 대상 인접 연결 추가")]
    private static void AddAdjacentConnectionsToSelection()
    {
        AddAdjacentConnections(GetSelectedTargets(), true);
    }

    [MenuItem("Tools/Water Basin/선택 대상 인접 연결 추가", true)]
    private static bool CanAddAdjacentConnectionsToSelection()
    {
        return GetSelectedTargets().Length > 0;
    }

    [MenuItem("Tools/Water Basin/씬 전체 인접 연결 추가")]
    private static void AddAdjacentConnectionsToScene()
    {
        AddAdjacentConnections(UnityEngine.Object.FindObjectsByType<WaterBasinTarget>(), true);
    }

    [MenuItem("Tools/Water Basin/선택 대상 연결 비우기")]
    private static void ClearSelectionConnections()
    {
        ClearConnectionsWithConfirm(GetSelectedTargets());
    }

    [MenuItem("Tools/Water Basin/선택 대상 연결 비우기", true)]
    private static bool CanClearSelectionConnections()
    {
        return GetSelectedTargets().Length > 0;
    }

    public static int AddAdjacentConnections(WaterBasinTarget[] sourceTargets, bool showLog)
    {
        WaterBasinTarget[] uniqueSources = GetUniqueTargets(sourceTargets);
        WaterBasinTarget[] allTargets = UnityEngine.Object.FindObjectsByType<WaterBasinTarget>();
        int addedCount = 0;

        for (int sourceIndex = 0; sourceIndex < uniqueSources.Length; sourceIndex++)
        {
            WaterBasinTarget source = uniqueSources[sourceIndex];
            if (source == null)
            {
                continue;
            }

            for (int candidateIndex = 0; candidateIndex < allTargets.Length; candidateIndex++)
            {
                WaterBasinTarget candidate = allTargets[candidateIndex];
                if (candidate == null
                    || candidate == source
                    || !AreTargetsAdjacent(source, candidate))
                {
                    continue;
                }

                if (AddOneWayConnection(source, candidate))
                {
                    addedCount++;
                }

                if (AddOneWayConnection(candidate, source))
                {
                    addedCount++;
                }
            }
        }

        if (showLog)
        {
            Debug.Log($"WaterBasinTarget 인접 연결 {addedCount}개를 Connected Targets에 추가했습니다.");
        }

        return addedCount;
    }

    public static bool AreTargetsAdjacent(WaterBasinTarget first, WaterBasinTarget second)
    {
        if (first == null || second == null || first == second)
        {
            return false;
        }

        Bounds firstBounds = new Bounds(first.VolumeWorldCenter, first.VolumeWorldSize);
        Bounds secondBounds = new Bounds(second.VolumeWorldCenter, second.VolumeWorldSize);
        return AreVolumeBoundsAdjacent(firstBounds, secondBounds, AdjacentConnectionTolerance);
    }

    public static void ClearConnectionsWithConfirm(WaterBasinTarget[] targets)
    {
        WaterBasinTarget[] uniqueTargets = GetUniqueTargets(targets);
        if (uniqueTargets.Length == 0)
        {
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "물 연결 비우기",
            "선택한 WaterBasinTarget의 Connected Targets를 모두 비웁니다. 떨어진 타겟을 직접 넣어둔 연결도 함께 제거됩니다.",
            "비우기",
            "취소");

        if (!confirmed)
        {
            return;
        }

        int removedCount = ClearConnections(uniqueTargets);
        Debug.Log($"WaterBasinTarget 연결 {removedCount}개를 제거했습니다.");
    }

    private static int ClearConnections(WaterBasinTarget[] targets)
    {
        int removedCount = 0;
        for (int i = 0; i < targets.Length; i++)
        {
            WaterBasinTarget target = targets[i];
            if (target == null || target.ConnectedTargets.Count == 0)
            {
                continue;
            }

            Undo.RecordObject(target, UndoName);
            removedCount += target.ClearConnectedTargets();
            EditorUtility.SetDirty(target);
        }

        return removedCount;
    }

    private static bool AddOneWayConnection(WaterBasinTarget source, WaterBasinTarget target)
    {
        if (source == null || !source.CanAddConnectedTarget(target))
        {
            return false;
        }

        Undo.RecordObject(source, UndoName);
        source.AddConnectedTarget(target);
        EditorUtility.SetDirty(source);
        return true;
    }

    private static bool AreVolumeBoundsAdjacent(Bounds first, Bounds second, float tolerance)
    {
        bool xTouching = AreRangesTouching(first.min.x, first.max.x, second.min.x, second.max.x, tolerance);
        bool zTouching = AreRangesTouching(first.min.z, first.max.z, second.min.z, second.max.z, tolerance);
        bool xOverlapping = GetRangeOverlap(first.min.x, first.max.x, second.min.x, second.max.x) > Epsilon;
        bool yOverlapping = GetRangeOverlap(first.min.y, first.max.y, second.min.y, second.max.y) > Epsilon;
        bool zOverlapping = GetRangeOverlap(first.min.z, first.max.z, second.min.z, second.max.z) > Epsilon;

        return yOverlapping
            && ((xTouching && zOverlapping)
                || (zTouching && xOverlapping));
    }

    private static bool AreRangesTouching(
        float firstMin,
        float firstMax,
        float secondMin,
        float secondMax,
        float tolerance)
    {
        return Mathf.Abs(firstMax - secondMin) <= tolerance
            || Mathf.Abs(secondMax - firstMin) <= tolerance;
    }

    private static float GetRangeOverlap(
        float firstMin,
        float firstMax,
        float secondMin,
        float secondMax)
    {
        return Mathf.Min(firstMax, secondMax) - Mathf.Max(firstMin, secondMin);
    }

    private static WaterBasinTarget[] GetSelectedTargets()
    {
        HashSet<WaterBasinTarget> result = new HashSet<WaterBasinTarget>();
        GameObject[] gameObjects = Selection.gameObjects;

        for (int i = 0; i < gameObjects.Length; i++)
        {
            WaterBasinTarget[] basinTargets = gameObjects[i].GetComponentsInChildren<WaterBasinTarget>(true);
            for (int targetIndex = 0; targetIndex < basinTargets.Length; targetIndex++)
            {
                result.Add(basinTargets[targetIndex]);
            }
        }

        return ToArray(result);
    }

    private static WaterBasinTarget[] GetUniqueTargets(WaterBasinTarget[] targets)
    {
        HashSet<WaterBasinTarget> result = new HashSet<WaterBasinTarget>();
        if (targets == null)
        {
            return ToArray(result);
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                result.Add(targets[i]);
            }
        }

        return ToArray(result);
    }

    private static WaterBasinTarget[] ToArray(HashSet<WaterBasinTarget> targets)
    {
        WaterBasinTarget[] result = new WaterBasinTarget[targets.Count];
        targets.CopyTo(result);
        return result;
    }
}
#endif
