using Unity.Behavior;
using UnityEngine;

[DisallowMultipleComponent]
// Behavior Graph에서 공통으로 사용하는 블랙보드 변수를 씬 참조와 맞춰준다.
// NPC 프리팹마다 Agent/Player를 직접 연결하지 않아도 같은 Graph를 재사용하기 위한 컴포넌트이다.
public class NPCBehaviorBlackboardBinder : MonoBehaviour
{
    [Header("Behavior")]
    [SerializeField] private BehaviorGraphAgent behaviorAgent;

    [Header("Agent")]
    [SerializeField] private GameObject agentObject;
    [SerializeField] private string agentVariableName = "Agent";

    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private bool findPlayerByTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string playerGameObjectVariableName = "Player";
    [SerializeField] private string playerTransformVariableName = "PlayerTransform";

    [Header("Runtime")]
    [SerializeField] private bool bindOnAwake = true;
    [SerializeField] private bool bindOnStart = true;
    [SerializeField] private bool logBindingFailures;

    private void Reset()
    {
        behaviorAgent = GetComponent<BehaviorGraphAgent>();
        agentObject = gameObject;
    }

    private void Awake()
    {
        CacheReferences();

        if (bindOnAwake)
        {
            BindNow();
        }
    }

    private void Start()
    {
        if (bindOnStart)
        {
            BindNow();
        }
    }

    // 런타임에 플레이어를 지정하거나 Graph가 바뀐 뒤에도 다시 바인딩할 수 있게 열어둔다.
    public void BindNow()
    {
        CacheReferences();

        if (behaviorAgent == null)
        {
            return;
        }

        GameObject resolvedAgent = agentObject != null ? agentObject : gameObject;
        TrySetGameObject(agentVariableName, resolvedAgent);

        Transform resolvedPlayer = ResolvePlayer();
        if (resolvedPlayer == null)
        {
            return;
        }

        TrySetGameObject(playerGameObjectVariableName, resolvedPlayer.gameObject);
        TrySetTransform(playerTransformVariableName, resolvedPlayer);
    }

    public void SetPlayer(Transform targetPlayer)
    {
        player = targetPlayer;
        BindNow();
    }

    private void CacheReferences()
    {
        if (behaviorAgent == null)
        {
            behaviorAgent = GetComponent<BehaviorGraphAgent>();
        }

        if (agentObject == null)
        {
            agentObject = gameObject;
        }
    }

    // 직접 지정된 플레이어가 있으면 우선 사용하고, 없으면 설정된 태그로 플레이어를 찾는다.
    private Transform ResolvePlayer()
    {
        if (player != null)
        {
            return player;
        }

        if (!findPlayerByTag || string.IsNullOrEmpty(playerTag))
        {
            return null;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject == null)
        {
            return null;
        }

        player = playerObject.transform;
        return player;
    }

    // 변수 이름이 없거나 타입이 맞지 않으면 BehaviorGraphAgent.SetVariableValue가 false를 반환한다.
    private void TrySetGameObject(string variableName, GameObject value)
    {
        if (string.IsNullOrEmpty(variableName) || value == null)
        {
            return;
        }

        bool success = behaviorAgent.SetVariableValue(variableName, value);
        LogBindingFailure(variableName, success);
    }

    private void TrySetTransform(string variableName, Transform value)
    {
        if (string.IsNullOrEmpty(variableName) || value == null)
        {
            return;
        }

        bool success = behaviorAgent.SetVariableValue(variableName, value);
        LogBindingFailure(variableName, success);
    }

    private void LogBindingFailure(string variableName, bool success)
    {
        if (success || !logBindingFailures)
        {
            return;
        }

        Debug.LogWarning($"{name}: Behavior blackboard variable '{variableName}' was not found or had a different type.", this);
    }
}
