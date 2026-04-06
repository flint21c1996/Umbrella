using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerUmbrellaController : MonoBehaviour
{
    // 우산은 단일 도구이지만 상태에 따라 전혀 다른 환경 문법으로 확장된다.
    // 처음 버전에서는 기획서의 핵심 루프인 펼치기, 뒤집기, 붓기만 검증할 수 있게
    // 최소 상태만 enum으로 고정했다.
    public enum UmbrellaState
    {
        Closed,
        Open,
        Inverted,
        Pouring
    }

    [Header("Input")]
    // 이동과 분리된 별도 입력으로 두어, 우산 로직이 PlayerMovement와 섞이지 않게 한다.
    public InputActionReference toggleUmbrellaAction;
    public InputActionReference invertUmbrellaAction;
    public InputActionReference pourAction;

    [Header("Visual")]
    // 초기에 애니메이션 없이도 상태를 바로 확인할 수 있도록
    // 열림, 닫힘, 뒤집힘 비주얼을 각각 따로 두고 활성화만 전환한다.
    public GameObject closedVisual;
    public GameObject openVisual;
    public GameObject invertedVisual;
    public Transform pourOrigin;

    [Header("Water")]
    // 비 시스템은 이후 빛, 바람, 생장으로 확장될 공통 구조를 염두에 두고
    // 저장량과 방출량을 먼저 분리했다.
    public float maxStoredWater = 5.0f;
    public float pourRate = 1.5f;
    public float pourDistance = 3.0f;
    public LayerMask pourMask = ~0;

    [Header("Debug")]
    [SerializeField] private UmbrellaState currentState = UmbrellaState.Closed;
    [SerializeField] private float currentStoredWater;

    public UmbrellaState CurrentState => currentState;
    public float CurrentStoredWater => currentStoredWater;
    public bool IsOpen => currentState == UmbrellaState.Open;
    public bool IsInverted => currentState == UmbrellaState.Inverted;
    public bool IsPouring => currentState == UmbrellaState.Pouring;
    public bool CanCollectWater => currentState == UmbrellaState.Inverted;

    private void Start()
    {
        RefreshVisuals();
    }

    private void OnEnable()
    {
        EnableAction(toggleUmbrellaAction);
        EnableAction(invertUmbrellaAction);
        EnableAction(pourAction);
    }

    private void OnDisable()
    {
        DisableAction(toggleUmbrellaAction);
        DisableAction(invertUmbrellaAction);
        DisableAction(pourAction);
    }

    private void Update()
    {
        HandleStateInput();
        UpdatePouring();
    }

    public void AddWater(float amount)
    {
        // 현재는 뒤집힌 상태일 때만 물을 받을 수 있게 제한해
        // 우산 상태 선택 자체가 퍼즐 입력으로 기능하게 한다.
        if (!CanCollectWater || amount <= 0.0f)
        {
            return;
        }

        currentStoredWater = Mathf.Clamp(currentStoredWater + amount, 0.0f, maxStoredWater);
    }

    private void HandleStateInput()
    {
        if (toggleUmbrellaAction != null && toggleUmbrellaAction.action.WasPressedThisFrame())
        {
            ToggleOpenState();
        }

        if (invertUmbrellaAction != null && invertUmbrellaAction.action.WasPressedThisFrame())
        {
            ToggleInvertState();
        }

        if (pourAction != null && pourAction.action.WasPressedThisFrame())
        {
            BeginPour();
        }

        if (pourAction != null && pourAction.action.WasReleasedThisFrame())
        {
            EndPour();
        }
    }

    private void ToggleOpenState()
    {
        // 펼치기 입력은 현재 상태를 기준으로 가장 자연스러운 기본 상태로 되돌린다.
        // 뒤집기나 붓기 중에도 다시 일반 우산 상태로 복귀할 수 있게 한다.
        switch (currentState)
        {
            case UmbrellaState.Closed:
                SetState(UmbrellaState.Open);
                break;

            case UmbrellaState.Open:
                SetState(UmbrellaState.Closed);
                break;

            case UmbrellaState.Inverted:
            case UmbrellaState.Pouring:
                SetState(UmbrellaState.Open);
                break;
        }
    }

    private void ToggleInvertState()
    {
        // 뒤집기는 물을 저장하는 전용 상태로 본다.
        // 따라서 붓는 중에 다시 누르면 먼저 붓기를 종료하고 저장 상태로 되돌린다.
        switch (currentState)
        {
            case UmbrellaState.Inverted:
                SetState(UmbrellaState.Closed);
                break;

            case UmbrellaState.Pouring:
                EndPour();
                break;

            default:
                SetState(UmbrellaState.Inverted);
                break;
        }
    }

    private void BeginPour()
    {
        // 지금 버전에서는 저장한 물이 있을 때만 붓기를 허용한다.
        // 물의 유무 자체가 퍼즐 진행 조건으로 보이게 하기 위한 처리다.
        if (currentStoredWater <= 0.0f)
        {
            return;
        }

        if (currentState != UmbrellaState.Inverted)
        {
            return;
        }

        SetState(UmbrellaState.Pouring);
    }

    private void EndPour()
    {
        if (currentState != UmbrellaState.Pouring)
        {
            return;
        }

        SetState(UmbrellaState.Inverted);
    }

    private void UpdatePouring()
    {
        if (currentState != UmbrellaState.Pouring)
        {
            return;
        }

        // 붓는 동안은 저장량을 계속 줄이고,
        // 정면의 간단한 타깃 오브젝트에만 물을 전달하는 최소 루프부터 검증한다.
        float pourAmount = pourRate * Time.deltaTime;
        currentStoredWater = Mathf.Max(0.0f, currentStoredWater - pourAmount);

        if (TryGetPourRay(out Ray pourRay) &&
            Physics.Raycast(pourRay, out RaycastHit hit, pourDistance, pourMask, QueryTriggerInteraction.Ignore) &&
            hit.collider.TryGetComponent(out UmbrellaWaterTarget waterTarget))
        {
            waterTarget.ReceiveWater(pourAmount);
        }

        if (currentStoredWater <= 0.0f)
        {
            EndPour();
        }
    }

    private bool TryGetPourRay(out Ray pourRay)
    {
        Transform origin = pourOrigin != null ? pourOrigin : transform;
        Vector3 direction = origin.forward;

        if (direction.sqrMagnitude <= 0.000001f)
        {
            pourRay = default;
            return false;
        }

        pourRay = new Ray(origin.position, direction.normalized);
        return true;
    }

    private void SetState(UmbrellaState nextState)
    {
        if (currentState == nextState)
        {
            return;
        }

        currentState = nextState;
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        // 초기 프로토타입에서는 상태 확인이 가장 중요하므로
        // 실제 애니메이션 대신 활성화 전환으로 즉시 피드백을 준다.
        SetVisualActive(closedVisual, currentState == UmbrellaState.Closed);
        SetVisualActive(openVisual, currentState == UmbrellaState.Open || currentState == UmbrellaState.Pouring);
        SetVisualActive(invertedVisual, currentState == UmbrellaState.Inverted);
    }

    private void SetVisualActive(GameObject target, bool active)
    {
        if (target == null)
        {
            return;
        }

        target.SetActive(active);
    }

    private void EnableAction(InputActionReference actionReference)
    {
        if (actionReference == null)
        {
            return;
        }

        actionReference.action.Enable();
    }

    private void DisableAction(InputActionReference actionReference)
    {
        if (actionReference == null)
        {
            return;
        }

        actionReference.action.Disable();
    }

    private void OnDrawGizmosSelected()
    {
        if (!TryGetPourRay(out Ray pourRay))
        {
            return;
        }

        Gizmos.color = IsPouring ? Color.cyan : Color.yellow;
        Gizmos.DrawLine(pourRay.origin, pourRay.origin + pourRay.direction * pourDistance);
    }
}
