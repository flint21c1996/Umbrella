using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PlayerUmbrellaController : MonoBehaviour
{
    // 우산 시스템은 이동과 별도로 관리한다.
    // 이동 입력과 우산 상태가 섞이기 시작하면 이후 활공, 차단, 상호작용 확장이 어려워지기 때문이다.
    public enum UmbrellaState
    {
        Closed,
        Open,
        Inverted,
        Pouring
    }

    [Header("Ownership")]
    // 테스트를 빠르게 하고 싶을 때를 대비해 시작 시 우산 소유 여부를 선택할 수 있게 둔다.
    public bool startWithUmbrella;
    // 바닥에서 주운 우산을 플레이어 쪽에 붙일 기준점이다.
    public Transform pickupAttachPoint;

    [Header("Input")]
    public InputActionReference toggleUmbrellaAction;
    public InputActionReference invertUmbrellaAction;
    public InputActionReference pourAction;
    // Input Action을 아직 연결하지 않아도 바로 테스트할 수 있도록 임시 키 입력을 둔다.
    public bool useKeyboardFallback = true;

    [Header("Visual")]
    // 초기 단계에서는 애니메이션 대신 상태별 오브젝트를 켜고 끄는 방식으로 흐름을 검증한다.
    public GameObject closedVisual;
    public GameObject openVisual;
    public GameObject invertedVisual;
    public Transform pourOrigin;

    [Header("Water")]
    // 비를 받아 저장하고, 이후 특정 대상에게 붓는 가장 작은 루프를 먼저 만든다.
    public float maxStoredWater = 5.0f;
    public float pourRate = 1.5f;
    public float pourDistance = 3.0f;
    public LayerMask pourMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool hasUmbrella;
    [SerializeField] private UmbrellaState currentState = UmbrellaState.Closed;
    [SerializeField] private float currentStoredWater;
    [SerializeField] private GameObject runtimePickupVisual;

    public bool HasUmbrella => hasUmbrella;
    public UmbrellaState CurrentState => currentState;
    public float CurrentStoredWater => currentStoredWater;
    public bool IsOpen => hasUmbrella && currentState == UmbrellaState.Open;
    public bool IsInverted => hasUmbrella && currentState == UmbrellaState.Inverted;
    public bool IsPouring => hasUmbrella && currentState == UmbrellaState.Pouring;
    public bool CanCollectWater => hasUmbrella && currentState == UmbrellaState.Inverted;

    private void Start()
    {
        hasUmbrella = startWithUmbrella;
        currentState = UmbrellaState.Closed;
        currentStoredWater = Mathf.Clamp(currentStoredWater, 0.0f, maxStoredWater);
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
        // 우산을 얻기 전에는 시스템은 붙어 있어도 입력과 상호작용을 잠가둔다.
        if (!hasUmbrella)
        {
            return;
        }

        HandleStateInput();
        UpdatePouring();
    }

    public void AcquireUmbrella()
    {
        AcquireUmbrella(null);
    }

    public void AcquireUmbrella(GameObject pickedVisual)
    {
        if (hasUmbrella)
        {
            return;
        }

        hasUmbrella = true;
        currentState = UmbrellaState.Closed;
        currentStoredWater = 0.0f;

        if (pickedVisual != null)
        {
            AttachPickupVisual(pickedVisual);
        }

        RefreshVisuals();
    }

    public void RemoveUmbrella()
    {
        if (!hasUmbrella)
        {
            return;
        }

        hasUmbrella = false;
        currentState = UmbrellaState.Closed;
        currentStoredWater = 0.0f;
        runtimePickupVisual = null;
        RefreshVisuals();
    }

    public void AddWater(float amount)
    {
        // 뒤집힌 상태에서만 물을 받게 만들어, 상태 선택 자체가 곧 플레이 문법이 되도록 한다.
        if (!CanCollectWater || amount <= 0.0f)
        {
            return;
        }

        currentStoredWater = Mathf.Clamp(currentStoredWater + amount, 0.0f, maxStoredWater);
    }

    private void HandleStateInput()
    {
        bool togglePressed = WasUmbrellaActionPressed(toggleUmbrellaAction, Keyboard.current?.fKey);
        bool invertPressed = WasUmbrellaActionPressed(invertUmbrellaAction, Keyboard.current?.gKey);
        bool pourPressed = WasUmbrellaActionPressed(pourAction, Mouse.current?.rightButton);
        bool pourReleased = WasUmbrellaActionReleased(pourAction, Mouse.current?.rightButton);

        if (togglePressed)
        {
            ToggleOpenState();
        }

        if (invertPressed)
        {
            ToggleInvertState();
        }

        if (pourPressed)
        {
            BeginPour();
        }

        if (pourReleased)
        {
            EndPour();
        }
    }

    private void ToggleOpenState()
    {
        // 열린 상태와 닫힌 상태는 가장 자주 오가는 기본 상태다.
        // 붓기나 뒤집기 도중에도 한 번에 열린 상태로 복귀할 수 있게 둔다.
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
        // 뒤집기는 저장용 상태로 두고, 다시 누르면 닫힌 상태로 돌아가게 한다.
        // 붓는 중이라면 먼저 붓기를 종료하고 뒤집힌 상태를 유지한다.
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
        // 지금 단계에서는 저장한 물이 있고, 뒤집힌 상태일 때만 붓기를 허용한다.
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

        // 붓는 동안에는 저장량을 줄이고, 앞쪽에 있는 간단한 대상에게만 물을 전달한다.
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
        // 우산을 얻기 전에는 플레이어 손에 어떤 우산도 보이지 않게 한다.
        if (!hasUmbrella)
        {
            SetVisualActive(closedVisual, false);
            SetVisualActive(openVisual, false);
            SetVisualActive(invertedVisual, false);
            SetVisualActive(runtimePickupVisual, false);
            return;
        }

        bool hasDedicatedVisuals = closedVisual != null || openVisual != null || invertedVisual != null;

        if (hasDedicatedVisuals)
        {
            SetVisualActive(closedVisual, currentState == UmbrellaState.Closed);
            SetVisualActive(openVisual, currentState == UmbrellaState.Open || currentState == UmbrellaState.Pouring);
            SetVisualActive(invertedVisual, currentState == UmbrellaState.Inverted);
            SetVisualActive(runtimePickupVisual, false);
            return;
        }

        // 전용 상태 비주얼을 아직 만들지 않았다면, 주운 우산 오브젝트 하나를 계속 보여준다.
        SetVisualActive(runtimePickupVisual, true);
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
        if (!hasUmbrella)
        {
            return;
        }

        if (!TryGetPourRay(out Ray pourRay))
        {
            return;
        }

        Gizmos.color = IsPouring ? Color.cyan : Color.yellow;
        Gizmos.DrawLine(pourRay.origin, pourRay.origin + pourRay.direction * pourDistance);
    }

    private void AttachPickupVisual(GameObject pickedVisual)
    {
        runtimePickupVisual = pickedVisual;

        Transform targetParent = pickupAttachPoint != null ? pickupAttachPoint : transform;
        runtimePickupVisual.transform.SetParent(targetParent, false);
        runtimePickupVisual.transform.localPosition = Vector3.zero;
        runtimePickupVisual.transform.localRotation = Quaternion.identity;

        foreach (Collider collider in runtimePickupVisual.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (Rigidbody rigidbodyComponent in runtimePickupVisual.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbodyComponent.isKinematic = true;
            rigidbodyComponent.linearVelocity = Vector3.zero;
            rigidbodyComponent.angularVelocity = Vector3.zero;
        }
    }

    private bool WasUmbrellaActionPressed(InputActionReference actionReference, ButtonControl fallbackButton)
    {
        if (actionReference != null && actionReference.action.WasPressedThisFrame())
        {
            return true;
        }

        return useKeyboardFallback && fallbackButton != null && fallbackButton.wasPressedThisFrame;
    }

    private bool WasUmbrellaActionReleased(InputActionReference actionReference, ButtonControl fallbackButton)
    {
        if (actionReference != null && actionReference.action.WasReleasedThisFrame())
        {
            return true;
        }

        return useKeyboardFallback && fallbackButton != null && fallbackButton.wasReleasedThisFrame;
    }
}
