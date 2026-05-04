#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WaterBasinTarget))]
[CanEditMultipleObjects]
public class WaterBasinTargetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8.0f);
        EditorGUILayout.LabelField("물 연결 제작 도구", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "자동 연결은 에디터에서 탐색 거리 안의 WaterBasinTarget을 찾아 Connected Targets를 채우는 작업입니다. 플레이 중에는 저장된 연결 목록만 사용합니다.",
            MessageType.Info);

        if (GUILayout.Button("검사 중인 대상에 자동 연결 추가"))
        {
            WaterBasinTargetEditorConnectionTool.AddAutoConnections(GetInspectorTargets(), true);
        }

        if (GUILayout.Button("씬 전체에 자동 연결 추가"))
        {
            WaterBasinTargetEditorConnectionTool.AddAutoConnections(
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

}

[InitializeOnLoad]
internal static class WaterBasinTargetSceneDebugDrawer
{
    private static readonly Color SavedConnectionColor = new Color(0.1f, 0.45f, 1.0f, 1.0f);
    private static readonly Color AutoConnectionColor = Color.yellow;

    static WaterBasinTargetSceneDebugDrawer()
    {
        SceneView.duringSceneGui += DrawWaterBasinConnections;
    }

    private static void DrawWaterBasinConnections(SceneView sceneView)
    {
        if (Application.isPlaying)
        {
            return;
        }

        WaterBasinTarget[] sources = GetPreviewSources();
        if (sources.Length == 0)
        {
            return;
        }

        DrawAutoConnectionPreview(sources);
        DrawSavedConnections(sources);
    }

    private static void DrawAutoConnectionPreview(WaterBasinTarget[] sources)
    {
        WaterBasinTarget[] allTargets = UnityEngine.Object.FindObjectsByType<WaterBasinTarget>();

        Handles.color = AutoConnectionColor;
        for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            WaterBasinTarget source = sources[sourceIndex];
            if (source == null)
            {
                continue;
            }

            for (int candidateIndex = 0; candidateIndex < allTargets.Length; candidateIndex++)
            {
                WaterBasinTarget candidate = allTargets[candidateIndex];
                if (candidate != null && WaterBasinTargetEditorConnectionTool.AreTargetsInAutoConnectionRange(source, candidate))
                {
                    Handles.DrawLine(source.transform.position, candidate.transform.position);
                }
            }
        }
    }

    private static void DrawSavedConnections(WaterBasinTarget[] sources)
    {
        Handles.color = SavedConnectionColor;
        for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            WaterBasinTarget source = sources[sourceIndex];
            if (source == null)
            {
                continue;
            }

            IReadOnlyList<WaterBasinTarget> connectedTargets = source.ConnectedTargets;
            for (int connectedIndex = 0; connectedIndex < connectedTargets.Count; connectedIndex++)
            {
                WaterBasinTarget connected = connectedTargets[connectedIndex];
                if (connected == null)
                {
                    continue;
                }

                Handles.DrawLine(source.transform.position, connected.transform.position);
            }
        }
    }

    private static WaterBasinTarget[] GetPreviewSources()
    {
        return GetSelectedTargets();
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

    private static WaterBasinTarget[] ToArray(HashSet<WaterBasinTarget> targets)
    {
        WaterBasinTarget[] result = new WaterBasinTarget[targets.Count];
        targets.CopyTo(result);
        return result;
    }
}

internal static class WaterBasinTargetEditorConnectionTool
{
    public const float AutoConnectionSearchDistance = 0.1f;
    private const string UndoName = "물 연결 편집";

    [MenuItem("Tools/Water Basin/선택 대상 자동 연결 추가")]
    private static void AddAutoConnectionsToSelection()
    {
        AddAutoConnections(GetSelectedTargets(), true);
    }

    [MenuItem("Tools/Water Basin/선택 대상 자동 연결 추가", true)]
    private static bool CanAddAutoConnectionsToSelection()
    {
        return GetSelectedTargets().Length > 0;
    }

    [MenuItem("Tools/Water Basin/씬 전체 자동 연결 추가")]
    private static void AddAutoConnectionsToScene()
    {
        AddAutoConnections(UnityEngine.Object.FindObjectsByType<WaterBasinTarget>(), true);
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

    public static int AddAutoConnections(WaterBasinTarget[] sourceTargets, bool showLog)
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
                    || !AreTargetsInAutoConnectionRange(source, candidate))
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
            Debug.Log($"WaterBasinTarget 자동 연결 {addedCount}개를 Connected Targets에 추가했습니다.");
        }

        return addedCount;
    }

    public static bool AreTargetsInAutoConnectionRange(WaterBasinTarget first, WaterBasinTarget second)
    {
        if (first == null || second == null || first == second)
        {
            return false;
        }

        Bounds firstBounds = new Bounds(first.VolumeWorldCenter, first.VolumeWorldSize);
        Bounds secondBounds = new Bounds(second.VolumeWorldCenter, second.VolumeWorldSize);
        return GetBoundsDistance(firstBounds, secondBounds)
            <= AutoConnectionSearchDistance;
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

    private static float GetBoundsDistance(Bounds first, Bounds second)
    {
        float xDistance = GetRangeDistance(first.min.x, first.max.x, second.min.x, second.max.x);
        float yDistance = GetRangeDistance(first.min.y, first.max.y, second.min.y, second.max.y);
        float zDistance = GetRangeDistance(first.min.z, first.max.z, second.min.z, second.max.z);

        return Mathf.Sqrt(
            xDistance * xDistance
            + yDistance * yDistance
            + zDistance * zDistance);
    }

    private static float GetRangeDistance(
        float firstMin,
        float firstMax,
        float secondMin,
        float secondMax)
    {
        if (firstMax < secondMin)
        {
            return secondMin - firstMax;
        }

        if (secondMax < firstMin)
        {
            return firstMin - secondMax;
        }

        return 0.0f;
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
