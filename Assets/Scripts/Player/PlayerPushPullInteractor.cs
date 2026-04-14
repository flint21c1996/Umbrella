using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// 플레이어가 가까운 PushPullObject를 붙잡고 함께 움직이게 하는 컴포넌트.
// Interact Action을 비워두면 마우스 우클릭을 사용한다.
// 물체 배치는 직접 하고, 움직일 물체에는 PushPullObject와 Rigidbody를 붙이면 된다.
[DisallowMultipleComponent]
public class PlayerPushPullInteractor : MonoBehaviour
{
    private const float MoveInputThreshold = 0.0001f;
    private const int MaxCandidateColliders = 32;
    private const float HoldOffsetCorrectionStrength = 8.0f;
    private const float MaxHoldOffsetCorrectionSpeed = 3.0f;
    private const float PlayerCorrectionInfluence = 0.5f;
    private const float MinCardinalGrabDot = 0.82f;

    [Header("Input")]
    public InputActionReference interactAction;

    [Header("Detection")]
    // 비워두면 플레이어 transform 위치를 기준으로 가까운 물체를 찾는다.
    public Transform checkOrigin;

    // 이 거리 안에 있는 PushPullObject 중 가장 가까운 것을 잡는다.
    [FormerlySerializedAs("checkDistance")]
    public float grabRange = 1.4f;

    public LayerMask pushPullMask = ~0;

    [Header("Rules")]
    // 우산이 닫힌 상태일 때만 물체를 잡을 수 있게 해서 우산 조작과 손 조작을 분리한다.
    public bool requireClosedUmbrella = true;

    // 공중에서 물체를 끌어당기는 이상한 상황을 막는다.
    public bool requireGrounded = true;

    [Header("Movement")]
    // 잡고 있을 때 플레이어와 물체가 함께 움직이는 속도.
    [FormerlySerializedAs("pushPullForce")]
    public float grabbedMoveSpeed = 2.0f;

    [Header("Runtime")]
    [SerializeField] private PushPullObject currentCandidate;
    [SerializeField] private PushPullObject grabbedObject;

    private PlayerMovement playerMovement;
    private PlayerUmbrellaController umbrellaController;
    private readonly Collider[] candidateHits = new Collider[MaxCandidateColliders];
    private Vector3 grabbedOffsetFromPlayer;
    private Vector3 grabbedMoveAxis;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        umbrellaController = GetComponent<PlayerUmbrellaController>();
    }

    private void OnValidate()
    {
        grabRange = Mathf.Max(0.0f, grabRange);
        grabbedMoveSpeed = Mathf.Max(0.0f, grabbedMoveSpeed);
    }

    private void OnEnable()
    {
        EnableAction(interactAction);
    }

    private void OnDisable()
    {
        EndGrab();
        DisableAction(interactAction);
    }

    private void Update()
    {
        RefreshCandidate();

        if (grabbedObject != null)
        {
            if (!CanUseHands() || !IsInteractPressed() || IsGrabbedObjectTooFar())
            {
                EndGrab();
            }

            return;
        }

        if (WasInteractPressed())
        {
            TryBeginGrab();
        }
    }

    private void FixedUpdate()
    {
        if (grabbedObject == null || playerMovement == null)
        {
            return;
        }

        if (!CanUseHands())
        {
            EndGrab();
            return;
        }

        Vector3 moveDirection = playerMovement.GetCameraRelativeMoveDirection(playerMovement.MoveInput);
        Vector3 grabbedVelocity = Vector3.zero;
        if (moveDirection.sqrMagnitude > MoveInputThreshold && grabbedMoveAxis.sqrMagnitude > MoveInputThreshold)
        {
            float axisInput = Vector3.Dot(moveDirection, grabbedMoveAxis);
            if (Mathf.Abs(axisInput) > MoveInputThreshold)
            {
                grabbedVelocity = grabbedMoveAxis * axisInput * grabbedMoveSpeed;
            }
        }

        // Rigidbody 속도 기반 이동은 유지하되, 잡은 순간의 간격에서 너무 벗어나면 살짝 되돌린다.
        // 완전 고정보다 물리감은 남기고, 오래 밀고 당길 때 겹치거나 멀어지는 현상은 줄인다.
        Vector3 correctionVelocity = GetHoldOffsetCorrectionVelocity();
        Vector3 objectVelocity = grabbedVelocity + correctionVelocity;
        Vector3 appliedObjectVelocity = grabbedObject.SetHorizontalVelocity(objectVelocity);
        Vector3 appliedCorrectionVelocity = appliedObjectVelocity - grabbedVelocity;
        Vector3 playerVelocity = grabbedVelocity - appliedCorrectionVelocity * PlayerCorrectionInfluence;

        playerMovement.SetHorizontalVelocity(playerVelocity);
    }

    private void RefreshCandidate()
    {
        if (grabbedObject != null)
        {
            currentCandidate = grabbedObject;
            return;
        }

        currentCandidate = FindCandidate();
    }

    private PushPullObject FindCandidate()
    {
        if (!CanUseHands())
        {
            return null;
        }

        Vector3 origin = GetCheckOrigin();
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            grabRange,
            candidateHits,
            pushPullMask,
            QueryTriggerInteraction.Ignore);

        PushPullObject bestCandidate = null;
        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = candidateHits[i];
            if (hit == null)
            {
                continue;
            }

            PushPullObject candidate = hit.GetComponentInParent<PushPullObject>();
            if (candidate == null || !candidate.CanGrab(this))
            {
                continue;
            }

            if (!TryGetCardinalGrabAxis(candidate, out _))
            {
                continue;
            }

            float distance = Vector3.Distance(origin, candidate.transform.position);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestCandidate = candidate;
        }

        return bestCandidate;
    }

    private void TryBeginGrab()
    {
        if (currentCandidate == null || !CanUseHands())
        {
            return;
        }

        if (!TryGetCardinalGrabAxis(currentCandidate, out Vector3 moveAxis))
        {
            return;
        }

        if (!currentCandidate.TryBeginGrab(this))
        {
            return;
        }

        grabbedObject = currentCandidate;
        grabbedMoveAxis = moveAxis;
        StoreGrabbedOffset();
        FaceGrabbedObject();

        // 잡고 있는 동안 기본 이동 힘은 막고, 이 컴포넌트가 플레이어와 물체를 같이 움직인다.
        playerMovement?.SetExternalMoveSpeedMultiplier(0.0f);
        playerMovement?.SetRotationLocked(true);
    }

    private void EndGrab()
    {
        if (grabbedObject != null)
        {
            grabbedObject.SetHorizontalVelocity(Vector3.zero);
            grabbedObject.EndGrab(this);
            grabbedObject = null;
        }

        playerMovement?.StopHorizontalMovement();
        playerMovement?.ClearHorizontalVelocityOverride();
        playerMovement?.ResetExternalMoveSpeedMultiplier();
        playerMovement?.SetRotationLocked(false);
        grabbedOffsetFromPlayer = Vector3.zero;
        grabbedMoveAxis = Vector3.zero;
    }

    private void StoreGrabbedOffset()
    {
        if (grabbedObject == null || playerMovement == null)
        {
            grabbedOffsetFromPlayer = Vector3.zero;
            return;
        }

        grabbedOffsetFromPlayer = grabbedObject.RigidbodyPosition - playerMovement.RigidbodyPosition;
        grabbedOffsetFromPlayer.y = 0.0f;
    }

    private Vector3 GetHoldOffsetCorrectionVelocity()
    {
        if (grabbedObject == null || playerMovement == null)
        {
            return Vector3.zero;
        }

        Vector3 desiredObjectPosition = playerMovement.RigidbodyPosition + grabbedOffsetFromPlayer;
        Vector3 offsetError = desiredObjectPosition - grabbedObject.RigidbodyPosition;
        offsetError.y = 0.0f;
        return Vector3.ClampMagnitude(
            offsetError * HoldOffsetCorrectionStrength,
            MaxHoldOffsetCorrectionSpeed);
    }

    private void FaceGrabbedObject()
    {
        if (grabbedObject == null || playerMovement == null)
        {
            return;
        }

        Vector3 lookDirection = grabbedMoveAxis.sqrMagnitude > MoveInputThreshold
            ? -grabbedMoveAxis
            : grabbedObject.transform.position - transform.position;
        playerMovement.FaceDirectionImmediately(lookDirection);
    }

    private bool TryGetCardinalGrabAxis(PushPullObject targetObject, out Vector3 moveAxis)
    {
        moveAxis = Vector3.zero;

        if (targetObject == null)
        {
            return false;
        }

        Vector3 toPlayer = transform.position - targetObject.transform.position;
        toPlayer.y = 0.0f;
        if (toPlayer.sqrMagnitude <= MoveInputThreshold)
        {
            return false;
        }

        toPlayer.Normalize();

        Vector3 right = GetHorizontalAxis(targetObject.transform.right, Vector3.right);
        Vector3 forward = GetHorizontalAxis(targetObject.transform.forward, Vector3.forward);

        Vector3 bestAxis = right;
        float bestDot = Vector3.Dot(toPlayer, right);
        CheckBetterAxis(-right, toPlayer, ref bestAxis, ref bestDot);
        CheckBetterAxis(forward, toPlayer, ref bestAxis, ref bestDot);
        CheckBetterAxis(-forward, toPlayer, ref bestAxis, ref bestDot);

        if (bestDot < MinCardinalGrabDot)
        {
            return false;
        }

        moveAxis = bestAxis;
        return true;
    }

    private static void CheckBetterAxis(Vector3 axis, Vector3 toPlayer, ref Vector3 bestAxis, ref float bestDot)
    {
        float dot = Vector3.Dot(toPlayer, axis);
        if (dot <= bestDot)
        {
            return;
        }

        bestAxis = axis;
        bestDot = dot;
    }

    private static Vector3 GetHorizontalAxis(Vector3 axis, Vector3 fallback)
    {
        axis.y = 0.0f;
        if (axis.sqrMagnitude <= MoveInputThreshold)
        {
            return fallback;
        }

        return axis.normalized;
    }

    private bool CanUseHands()
    {
        if (requireGrounded && playerMovement != null && !playerMovement.isGrounded)
        {
            return false;
        }

        if (!requireClosedUmbrella || umbrellaController == null || !umbrellaController.HasUmbrella)
        {
            return true;
        }

        return umbrellaController.CurrentState == PlayerUmbrellaController.UmbrellaState.Closed;
    }

    private bool IsGrabbedObjectTooFar()
    {
        if (grabbedObject == null)
        {
            return false;
        }

        Vector3 toObject = grabbedObject.transform.position - GetCheckOrigin();
        toObject.y = 0.0f;
        return toObject.magnitude > grabRange + 0.6f;
    }

    private Vector3 GetCheckOrigin()
    {
        return checkOrigin != null ? checkOrigin.position : transform.position + Vector3.up * 0.6f;
    }

    private bool WasInteractPressed()
    {
        InputAction action = interactAction != null ? interactAction.action : null;
        if (action != null)
        {
            return action.WasPressedThisFrame();
        }

        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    }

    private bool IsInteractPressed()
    {
        InputAction action = interactAction != null ? interactAction.action : null;
        if (action != null)
        {
            return action.IsPressed();
        }

        return Mouse.current != null && Mouse.current.rightButton.isPressed;
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
        Vector3 origin = GetCheckOrigin();

        Gizmos.color = grabbedObject != null ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(origin, grabRange);
    }
}
