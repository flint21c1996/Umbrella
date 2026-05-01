using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;

public class PlayerUmbrellaController : MonoBehaviour
{
    public enum UmbrellaState
    {
        Closed,
        Open,
        UpsideDown,
        Pouring
    }

    [Header("Ownership")]
    public bool startWithUmbrella;
    public Transform pickupAttachPoint;

    [Header("Input")]
    public InputActionReference toggleUmbrellaAction;
    public InputActionReference invertUmbrellaAction;
    public InputActionReference pourAction;
    public bool useKeyboardFallback = true;

    [Header("Visual")]
    public GameObject closedVisual;
    public GameObject openVisual;
    [FormerlySerializedAs("invertedVisual")]
    public GameObject upsideDownVisual;
    public Transform pourOrigin;

    [Header("Water")]
    public float maxStoredWater = 5.0f;
    public float pourRate = 1.5f;
    public float pourDistance = 3.0f;
    public LayerMask pourMask = ~0;

    [Header("Rain Response")]
    public float maxPlayerRainAmount = 5.0f;
    public float naturalDryRate = 0.35f;
    public float storedWaterWeightMultiplier = 1.0f;

    [Header("Aim")]
    public bool rotatePlayerTowardsMouseWhilePouring = true;
    public float mouseAimRayDistance = 100.0f;
    public LayerMask mouseAimMask = ~0;

    [Header("Debug Controls")]
    public bool enableDebugFillKey = true;
    public float debugFillAmount = 1.0f;
    [HideInInspector] public bool showDebugOverlay = true;
    [HideInInspector] public bool showDebugGizmos = true;
    public float debugFacingLineLength = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool hasUmbrella;
    [SerializeField] private UmbrellaState currentState = UmbrellaState.Closed;
    [SerializeField] private float currentStoredWater;
    [SerializeField] private float currentPlayerRainAmount;
    [SerializeField] private float currentWeightKg;
    [SerializeField] private GameObject runtimePickupVisual;
    [SerializeField] private string lastPourHitColliderName = "None";
    [SerializeField] private string lastPourTargetName = "None";
    [SerializeField] private bool hasLastPourRay;
    [SerializeField] private Vector3 lastPourRayOrigin;
    [SerializeField] private Vector3 lastPourRayDirection;
    [SerializeField] private bool hasMouseAimPoint;
    [SerializeField] private Vector3 lastMouseAimPoint;
    [SerializeField] private Vector3 lastMouseAimDirection;
    [SerializeField] private string lastMouseAimHitName = "None";

    private PlayerMovement playerMovement;
    private Rigidbody playerRigidbody;
    private Camera cachedMainCamera;
    private float baseWeightKg;
    private float lastRainExposureTime = float.NegativeInfinity;
    private const float RainExposureGraceTime = 0.12f;

    public bool HasUmbrella => hasUmbrella;
    public UmbrellaState CurrentState => currentState;
    public float CurrentStoredWater => currentStoredWater;
    public float CurrentPlayerRainAmount => currentPlayerRainAmount;
    public float CurrentWeightKg => currentWeightKg;
    public bool IsOpen => hasUmbrella && currentState == UmbrellaState.Open;
    public bool IsUpsideDown => hasUmbrella && currentState == UmbrellaState.UpsideDown;
    public bool IsPouring => hasUmbrella && currentState == UmbrellaState.Pouring;
    public bool CanCollectWater => hasUmbrella && currentState == UmbrellaState.UpsideDown;

    // 우산이 열린 상태에서 RainArea의 비를 막았을 때 발생한다.
    // amount는 RainArea가 이번 프레임에 전달한 비 노출량이며, 시각 효과 강도 계산에 사용할 수 있다.
    public event Action<float> RainBlocked;

    private void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
        playerRigidbody = GetComponent<Rigidbody>();
        cachedMainCamera = Camera.main;
        hasUmbrella = startWithUmbrella;
        currentState = UmbrellaState.Closed;
        currentStoredWater = Mathf.Clamp(currentStoredWater, 0.0f, maxStoredWater);
        currentPlayerRainAmount = Mathf.Clamp(currentPlayerRainAmount, 0.0f, maxPlayerRainAmount);
        baseWeightKg = playerRigidbody != null ? playerRigidbody.mass : 0.0f;
        RefreshWeight();
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
        ClearPourAimOverride();
    }

    private void Update()
    {
        if (!hasUmbrella)
        {
            ClearPourAimOverride();
            return;
        }

        HandleStateInput();
        UpdatePourAimFacing();
        UpdatePouring();
        UpdatePlayerRainDrying();

        if (currentState != UmbrellaState.Pouring)
        {
            hasLastPourRay = false;
        }
    }

    public void SetDebugVisible(bool showOverlay, bool showGizmos)
    {
        showDebugOverlay = showOverlay;
        showDebugGizmos = showGizmos;
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
        RefreshWeight();

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
        RefreshWeight();
        runtimePickupVisual = null;
        ClearPourAimOverride();
        RefreshVisuals();
    }

    public void OpenUmbrella()
    {
        if (!hasUmbrella)
        {
            return;
        }

        SetState(UmbrellaState.Open);
    }

    public void CloseUmbrella()
    {
        if (!hasUmbrella)
        {
            return;
        }

        SetState(UmbrellaState.Closed);
    }

    // Animation Debug에서 우산의 UpsideDown 상태와 관련 애니메이션 전환을 직접 테스트하기 위한 진입점.
    // 일반 플레이 입력은 HandleStateInput -> ToggleInvertState 흐름을 그대로 사용한다.
    public void TurnUmbrellaUpsideDown()
    {
        if (!hasUmbrella)
        {
            return;
        }

        SetState(UmbrellaState.UpsideDown);
    }

    // Animation Debug에서 PourWater 트리거와 물 붓기 상태 전환을 확인하기 위한 진입점.
    // 실제 시작 가능 여부는 BeginPour 내부 조건을 따르므로 게임플레이 규칙은 우회하지 않는다.
    public void StartPouring()
    {
        if (!hasUmbrella)
        {
            return;
        }

        BeginPour();
    }

    // Animation Debug 테스트 중 Pouring 상태를 즉시 종료하기 위한 진입점.
    // 실제 상태 복귀 처리는 기존 EndPour 로직을 그대로 사용한다.
    public void StopPouring()
    {
        EndPour();
    }

    public void AddWater(float amount)
    {
        if (!CanCollectWater || amount <= 0.0f)
        {
            return;
        }

        currentStoredWater = Mathf.Clamp(currentStoredWater + amount, 0.0f, maxStoredWater);
        RefreshWeight();
    }

    public void ApplyRainExposure(float amount)
    {
        if (amount <= 0.0f)
        {
            return;
        }

        lastRainExposureTime = Time.time;

        if (IsOpen)
        {
            RainBlocked?.Invoke(amount);
            return;
        }

        currentPlayerRainAmount = Mathf.Clamp(currentPlayerRainAmount + amount, 0.0f, maxPlayerRainAmount);

        if (CanCollectWater)
        {
            AddWater(amount);
        }
    }

    private void UpdatePlayerRainDrying()
    {
        if (currentPlayerRainAmount <= 0.0f)
        {
            return;
        }

        bool isStillReceivingRain = Time.time - lastRainExposureTime <= RainExposureGraceTime;
        if (isStillReceivingRain)
        {
            return;
        }

        currentPlayerRainAmount = Mathf.Max(0.0f, currentPlayerRainAmount - naturalDryRate * Time.deltaTime);
    }

    private void HandleStateInput()
    {
        bool togglePressed = WasUmbrellaActionPressed(toggleUmbrellaAction, Keyboard.current?.fKey);
        bool invertPressed = WasUmbrellaActionPressed(invertUmbrellaAction, Keyboard.current?.gKey);
        bool pourPressed = WasUmbrellaActionPressed(pourAction, Mouse.current?.rightButton);
        bool pourReleased = WasUmbrellaActionReleased(pourAction, Mouse.current?.rightButton);
        bool debugFillPressed = enableDebugFillKey && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;

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

        if (debugFillPressed)
        {
            currentStoredWater = Mathf.Clamp(currentStoredWater + debugFillAmount, 0.0f, maxStoredWater);
            RefreshWeight();
        }
    }

    private void ToggleOpenState()
    {
        switch (currentState)
        {
            case UmbrellaState.Closed:
                SetState(UmbrellaState.Open);
                break;

            case UmbrellaState.Open:
                SetState(UmbrellaState.Closed);
                break;

            case UmbrellaState.UpsideDown:
            case UmbrellaState.Pouring:
                SetState(UmbrellaState.Open);
                break;
        }
    }

    private void ToggleInvertState()
    {
        switch (currentState)
        {
            case UmbrellaState.UpsideDown:
                SetState(UmbrellaState.Closed);
                break;

            case UmbrellaState.Pouring:
                EndPour();
                break;

            default:
                SetState(UmbrellaState.UpsideDown);
                break;
        }
    }

    private void BeginPour()
    {
        if (currentStoredWater <= 0.0f)
        {
            return;
        }

        if (currentState != UmbrellaState.UpsideDown)
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

        SetState(UmbrellaState.UpsideDown);
    }

    private void UpdatePourAimFacing()
    {
        if (!rotatePlayerTowardsMouseWhilePouring || currentState != UmbrellaState.Pouring)
        {
            ClearPourAimOverride();
            return;
        }

        if (!TryGetMouseAimDirection(out Vector3 aimDirection, out Vector3 aimPoint, out string hitName))
        {
            ClearPourAimOverride();
            return;
        }

        playerMovement?.SetFacingOverride(aimDirection);
        hasMouseAimPoint = true;
        lastMouseAimPoint = aimPoint;
        lastMouseAimDirection = aimDirection;
        lastMouseAimHitName = hitName;
    }

    private void UpdatePouring()
    {
        if (currentState != UmbrellaState.Pouring)
        {
            return;
        }

        float pourAmount = pourRate * Time.deltaTime;
        currentStoredWater = Mathf.Max(0.0f, currentStoredWater - pourAmount);
        RefreshWeight();
        lastPourHitColliderName = "No Hit";
        lastPourTargetName = "None";

        if (TryGetPourRay(out Ray pourRay))
        {
            hasLastPourRay = true;
            lastPourRayOrigin = pourRay.origin;
            lastPourRayDirection = pourRay.direction;
            Debug.DrawRay(pourRay.origin, pourRay.direction * pourDistance, Color.cyan, 0.0f, false);

            RaycastHit[] hits = Physics.RaycastAll(
                pourRay,
                pourDistance,
                pourMask,
                QueryTriggerInteraction.Ignore
            );

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.GetComponentInParent<PlayerUmbrellaController>() == this)
                {
                    continue;
                }

                lastPourHitColliderName = hit.collider.name;

                UmbrellaWaterTarget waterTarget = hit.collider.GetComponentInParent<UmbrellaWaterTarget>();
                if (waterTarget != null)
                {
                    lastPourTargetName = waterTarget.name;
                    waterTarget.ReceiveWater(pourAmount);
                }

                break;
            }
        }
        else
        {
            hasLastPourRay = false;
            lastPourHitColliderName = "Invalid Ray";
            lastPourTargetName = "None";
        }

        if (currentStoredWater <= 0.0f)
        {
            EndPour();
        }
    }

    private bool TryGetMouseAimDirection(out Vector3 aimDirection, out Vector3 aimPoint, out string hitName)
    {
        aimDirection = Vector3.zero;
        aimPoint = Vector3.zero;
        hitName = "None";

        if (Mouse.current == null)
        {
            return false;
        }

        Camera targetCamera = cachedMainCamera != null ? cachedMainCamera : Camera.main;
        if (targetCamera == null)
        {
            return false;
        }

        cachedMainCamera = targetCamera;

        Ray mouseRay = targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(
            mouseRay,
            mouseAimRayDistance,
            mouseAimMask,
            QueryTriggerInteraction.Ignore
        );

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<PlayerUmbrellaController>() == this)
            {
                continue;
            }

            Vector3 hitDirection = hit.point - transform.position;
            hitDirection.y = 0.0f;
            if (hitDirection.sqrMagnitude <= 0.000001f)
            {
                continue;
            }

            aimPoint = hit.point;
            aimDirection = hitDirection.normalized;
            hitName = hit.collider.name;
            return true;
        }

        Plane groundPlane = new Plane(Vector3.up, transform.position);
        if (!groundPlane.Raycast(mouseRay, out float enter))
        {
            return false;
        }

        Vector3 planePoint = mouseRay.GetPoint(enter);
        Vector3 planeDirection = planePoint - transform.position;
        planeDirection.y = 0.0f;
        if (planeDirection.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        aimPoint = planePoint;
        aimDirection = planeDirection.normalized;
        hitName = "Ground Plane";
        return true;
    }

    private bool TryGetPourRay(out Ray pourRay)
    {
        Transform origin = pourOrigin != null ? pourOrigin : transform;
        Vector3 direction = origin.forward;

        if (hasMouseAimPoint)
        {
            Vector3 aimDirection = lastMouseAimPoint - origin.position;
            if (aimDirection.sqrMagnitude > 0.000001f)
            {
                direction = aimDirection.normalized;
            }
        }

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

        if (ShouldSpillStoredWater(nextState))
        {
            SpillStoredWater();
        }

        currentState = nextState;
        RefreshVisuals();
    }

    private bool ShouldSpillStoredWater(UmbrellaState nextState)
    {
        bool isHoldingWaterState = currentState == UmbrellaState.UpsideDown || currentState == UmbrellaState.Pouring;
        bool isNonHoldingState = nextState == UmbrellaState.Open || nextState == UmbrellaState.Closed;
        return isHoldingWaterState && isNonHoldingState && currentStoredWater > 0.0f;
    }

    private void SpillStoredWater()
    {
        currentStoredWater = 0.0f;
        RefreshWeight();
    }

    private void ClearPourAimOverride()
    {
        playerMovement?.ClearFacingOverride();
        hasMouseAimPoint = false;
        lastMouseAimDirection = Vector3.zero;
        lastMouseAimHitName = "None";
    }

    private void RefreshVisuals()
    {
        if (!hasUmbrella)
        {
            SetVisualActive(closedVisual, false);
            SetVisualActive(openVisual, false);
            SetVisualActive(upsideDownVisual, false);
            SetRuntimePickupRenderersEnabled(false);
            return;
        }

        bool hasDedicatedVisuals = closedVisual != null || openVisual != null || upsideDownVisual != null;

        if (hasDedicatedVisuals)
        {
            SetVisualActive(closedVisual, currentState == UmbrellaState.Closed);
            SetVisualActive(openVisual, currentState == UmbrellaState.Open || currentState == UmbrellaState.Pouring);
            SetVisualActive(upsideDownVisual, currentState == UmbrellaState.UpsideDown);
            SetRuntimePickupRenderersEnabled(false);
            return;
        }

        SetRuntimePickupRenderersEnabled(true);
    }

    private void RefreshWeight()
    {
        currentWeightKg = baseWeightKg + currentStoredWater * storedWaterWeightMultiplier;

        if (playerRigidbody != null)
        {
            playerRigidbody.mass = currentWeightKg;
        }
    }

    private void SetVisualActive(GameObject target, bool active)
    {
        if (target == null)
        {
            return;
        }

        target.SetActive(active);
    }

    private void AttachPickupVisual(GameObject pickedVisual)
    {
        runtimePickupVisual = pickedVisual;

        Transform targetParent = pickupAttachPoint != null ? pickupAttachPoint : transform;
        runtimePickupVisual.transform.SetParent(targetParent, false);
        runtimePickupVisual.transform.localPosition = Vector3.zero;
        runtimePickupVisual.transform.localRotation = Quaternion.identity;

        if (pourOrigin == null)
        {
            Transform runtimePourOrigin = FindPourOriginInPickupRoot(runtimePickupVisual.transform);
            if (runtimePourOrigin != null)
            {
                pourOrigin = runtimePourOrigin;
            }
        }

        foreach (Collider collider in runtimePickupVisual.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
        }

        foreach (Rigidbody rigidbodyComponent in runtimePickupVisual.GetComponentsInChildren<Rigidbody>(true))
        {
            if (!rigidbodyComponent.isKinematic)
            {
                rigidbodyComponent.linearVelocity = Vector3.zero;
                rigidbodyComponent.angularVelocity = Vector3.zero;
            }

            rigidbodyComponent.isKinematic = true;
        }
    }

    private void SetRuntimePickupRenderersEnabled(bool visible)
    {
        if (runtimePickupVisual == null)
        {
            return;
        }

        foreach (Renderer renderer in runtimePickupVisual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = visible;
        }
    }

    private Transform FindPourOriginInPickupRoot(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == "PourOrigin")
        {
            return root;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "PourOrigin")
            {
                return child;
            }
        }

        return null;
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

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showDebugGizmos)
        {
            return;
        }

        DrawPlayerColliderDebug();
        DrawFacingDirectionDebug();

        if (hasUmbrella && hasLastPourRay)
        {
            Gizmos.color = IsPouring ? Color.cyan : Color.yellow;
            Gizmos.DrawLine(lastPourRayOrigin, GetCurrentPourDebugEndPoint());
            Gizmos.DrawSphere(lastPourRayOrigin, 0.03f);
        }

        if (hasUmbrella && hasMouseAimPoint)
        {
            Gizmos.color = new Color(1.0f, 0.4f, 0.2f, 0.9f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.2f, lastMouseAimPoint);
            Gizmos.DrawSphere(lastMouseAimPoint, 0.06f);
        }
    }

    private void DrawPlayerColliderDebug()
    {
        foreach (Collider playerCollider in GetComponentsInChildren<Collider>(true))
        {
            if (!playerCollider.enabled)
            {
                continue;
            }

            Bounds bounds = playerCollider.bounds;
            Gizmos.color = new Color(0.4f, 1.0f, 0.4f, 0.85f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }

    private void DrawFacingDirectionDebug()
    {
        Vector3 start = transform.position + Vector3.up * 0.2f;
        Vector3 end = start + transform.forward * debugFacingLineLength;
        Gizmos.color = new Color(0.3f, 1.0f, 0.3f, 0.95f);
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, 0.04f);
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !showDebugOverlay)
        {
            return;
        }

        DrawGameViewDebugLines();

        Rect panelRect = new Rect(16.0f, 16.0f, 320.0f, 230.0f);
        GUI.Box(panelRect, "Umbrella Debug");

        float lineY = panelRect.y + 28.0f;
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"State: {currentState}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"Has Umbrella: {hasUmbrella}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"Stored Water: {currentStoredWater:F2}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"Player Rain: {currentPlayerRainAmount:F2}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"Weight: {currentWeightKg:F2} kg");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"Aim Hit: {lastMouseAimHitName}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"Pour Hit: {lastPourHitColliderName}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"Pour Target: {lastPourTargetName}");
        DrawDebugLabel(panelRect.x + 12.0f, ref lineY, $"Debug Toggle: F3");

        DrawPlayerStatusOverlay();
    }

    private void DrawDebugLabel(float x, ref float y, string text)
    {
        GUI.Label(new Rect(x, y, 300.0f, 18.0f), text);
        y += 20.0f;
    }

    private void DrawGameViewDebugLines()
    {
        Camera targetCamera = cachedMainCamera != null ? cachedMainCamera : Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        if (hasLastPourRay)
        {
            Vector3 pourEnd = GetCurrentPourDebugEndPoint();
            DrawWorldLine(targetCamera, lastPourRayOrigin, pourEnd, Color.cyan, 3.0f);
        }

        Vector3 facingStart = transform.position + Vector3.up * 0.2f;
        Vector3 facingEnd = facingStart + transform.forward * debugFacingLineLength;
        DrawWorldLine(targetCamera, facingStart, facingEnd, new Color(0.3f, 1.0f, 0.3f, 0.95f), 3.0f);

        if (hasMouseAimPoint)
        {
            Vector3 aimStart = transform.position + Vector3.up * 0.2f;
            DrawWorldLine(targetCamera, aimStart, lastMouseAimPoint, new Color(1.0f, 0.4f, 0.2f, 0.9f), 2.0f);
        }
    }

    private void DrawWorldLine(Camera targetCamera, Vector3 worldStart, Vector3 worldEnd, Color color, float thickness)
    {
        Vector3 screenStart = targetCamera.WorldToScreenPoint(worldStart);
        Vector3 screenEnd = targetCamera.WorldToScreenPoint(worldEnd);

        if (screenStart.z <= 0.0f || screenEnd.z <= 0.0f)
        {
            return;
        }

        screenStart.y = Screen.height - screenStart.y;
        screenEnd.y = Screen.height - screenEnd.y;

        DrawScreenLine(screenStart, screenEnd, color, thickness);
    }

    private void DrawScreenLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;

        Vector2 delta = end - start;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float length = delta.magnitude;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private Vector3 GetCurrentPourDebugEndPoint()
    {
        if (!hasLastPourRay)
        {
            return lastPourRayOrigin;
        }

        Vector3 fallbackEndPoint = lastPourRayOrigin + lastPourRayDirection * pourDistance;
        if (!hasMouseAimPoint)
        {
            return fallbackEndPoint;
        }

        Vector3 toAimPoint = lastMouseAimPoint - lastPourRayOrigin;
        if (toAimPoint.sqrMagnitude <= 0.000001f)
        {
            return fallbackEndPoint;
        }

        float clampedDistance = Mathf.Min(toAimPoint.magnitude, pourDistance);
        return lastPourRayOrigin + toAimPoint.normalized * clampedDistance;
    }

    private void DrawPlayerStatusOverlay()
    {
        Camera targetCamera = cachedMainCamera != null ? cachedMainCamera : Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        Vector3 worldLabelPosition = transform.position + Vector3.up * 1.6f;
        Vector3 screenPoint = targetCamera.WorldToScreenPoint(worldLabelPosition);
        if (screenPoint.z <= 0.0f)
        {
            return;
        }

        float width = 180.0f;
        float height = 78.0f;
        float x = screenPoint.x - width * 0.5f;
        float y = Screen.height - screenPoint.y - height;

        GUI.Box(new Rect(x, y, width, height), "Player Rain");
        GUI.Label(new Rect(x + 8.0f, y + 20.0f, width - 16.0f, 16.0f), $"Rain: {currentPlayerRainAmount:F2}");
        GUI.Label(new Rect(x + 8.0f, y + 36.0f, width - 16.0f, 16.0f), $"Stored: {currentStoredWater:F2}");
        GUI.Label(new Rect(x + 8.0f, y + 52.0f, width - 16.0f, 16.0f), $"Weight: {currentWeightKg:F2} kg");
    }
}
