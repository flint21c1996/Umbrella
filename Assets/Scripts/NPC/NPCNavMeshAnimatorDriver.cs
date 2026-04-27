using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
// NavMeshAgent의 이동 상태를 Animator 파라미터로 변환한다.
// Behavior Graph는 이동 목적지만 제어하고, Idle/Walk 전환은 이 컴포넌트가 자동으로 처리한다.
public class NPCNavMeshAnimatorDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private bool findAnimatorInChildren = true;

    [Header("Parameters")]
    [SerializeField] private string moveSpeedParameter = "MoveSpeed";
    [SerializeField] private string speedMagnitudeParameter = "SpeedMagnitude";
    [SerializeField] private string isMovingParameter = "IsMoving";

    [Header("Tuning")]
    [SerializeField] private bool normalizeMoveSpeed = true;
    [SerializeField, Min(0f)] private float movingThreshold = 0.05f;
    [SerializeField, Min(0f)] private float dampingTime = 0.1f;

    // Animator 파라미터를 매 프레임 검색하지 않도록 캐시한다.
    private RuntimeAnimatorController cachedController;
    private int moveSpeedHash;
    private int speedMagnitudeHash;
    private int isMovingHash;
    private bool hasMoveSpeed;
    private bool hasSpeedMagnitude;
    private bool hasIsMoving;

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        CacheReferences();
        RefreshParameterCache();
    }

    private void OnValidate()
    {
        CacheHashes();
    }

    private void Update()
    {
        CacheReferences();

        if (animator == null)
        {
            return;
        }

        if (cachedController != animator.runtimeAnimatorController)
        {
            RefreshParameterCache();
        }

        float rawSpeed = agent != null ? agent.velocity.magnitude : 0f;
        float moveSpeed = rawSpeed;

        // 정규화된 속도는 Blend Tree에서 0..1 범위로 Idle/Walk/Run을 다루기 좋다.
        if (normalizeMoveSpeed && agent != null && agent.speed > Mathf.Epsilon)
        {
            moveSpeed = Mathf.Clamp01(rawSpeed / agent.speed);
        }

        // 아주 작은 잔여 속도 때문에 Walk 상태가 유지되지 않도록 임계값 이하는 Idle로 본다.
        if (rawSpeed <= movingThreshold)
        {
            rawSpeed = 0f;
            moveSpeed = 0f;
        }

        if (hasMoveSpeed)
        {
            animator.SetFloat(moveSpeedHash, moveSpeed, dampingTime, Time.deltaTime);
        }

        if (hasSpeedMagnitude)
        {
            animator.SetFloat(speedMagnitudeHash, rawSpeed, dampingTime, Time.deltaTime);
        }

        if (hasIsMoving)
        {
            animator.SetBool(isMovingHash, rawSpeed > movingThreshold);
        }
    }

    private void CacheReferences()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (animator == null && findAnimatorInChildren)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    // Override Controller가 할당되는 등 Animator Controller가 바뀌면 파라미터 캐시를 다시 만든다.
    private void RefreshParameterCache()
    {
        cachedController = animator != null ? animator.runtimeAnimatorController : null;
        CacheHashes();

        hasMoveSpeed = HasParameter(moveSpeedHash, AnimatorControllerParameterType.Float);
        hasSpeedMagnitude = HasParameter(speedMagnitudeHash, AnimatorControllerParameterType.Float);
        hasIsMoving = HasParameter(isMovingHash, AnimatorControllerParameterType.Bool);
    }

    private void CacheHashes()
    {
        moveSpeedHash = Animator.StringToHash(moveSpeedParameter);
        speedMagnitudeHash = Animator.StringToHash(speedMagnitudeParameter);
        isMovingHash = Animator.StringToHash(isMovingParameter);
    }

    // 가벼운 NPC 컨트롤러는 사용하지 않는 파라미터를 생략할 수 있도록 누락을 허용한다.
    private bool HasParameter(int parameterHash, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }
}
