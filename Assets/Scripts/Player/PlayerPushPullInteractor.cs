using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// 플레이어가 가까운 PushPullObject를 붙잡고 함께 움직이게 하는 컴포넌트.
// Interact Action을 비워두면 마우스 우클릭을 사용한다.
// 물체 배치는 직접 하고, 움직일 물체에는 PushPullObject와 Rigidbody를 붙이면 된다.
[DisallowMultipleComponent]
public class PlayerPushPullInteractor : MonoBehaviour
{
    // 입력이 거의 없는 상태를 0으로 취급하기 위한 기준값.
    private const float MoveInputThreshold = 0.0001f;

    // 주변 물체 검색 때 매 프레임 배열을 새로 만들지 않기 위한 고정 버퍼 크기.
    private const int MaxCandidateColliders = 32;

    // 플레이어와 상자가 잡은 순간의 간격에서 벌어졌을 때 되돌리는 보정 강도.
    private const float HoldOffsetCorrectionStrength = 8.0f;

    // 보정 속도가 너무 커지면 튀어 보이므로 최대 보정 속도를 제한한다.
    private const float MaxHoldOffsetCorrectionSpeed = 3.0f;

    // 상자가 벽에 막혔을 때 플레이어가 상자 안으로 파고드는 것을 줄이기 위한 영향도.
    private const float PlayerCorrectionInfluence = 0.5f;

    // 상자의 앞/뒤/좌/우 네 방향에 가까울 때만 잡을 수 있게 하는 기준값.
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

    // Physics.OverlapSphereNonAlloc에 넘기는 재사용 배열.
    // GC Alloc을 줄이려고 매번 new Collider[]를 만들지 않는다.
    private readonly Collider[] candidateHits = new Collider[MaxCandidateColliders];

    // 잡은 순간의 플레이어-상자 간격.
    // 오래 밀고 당길 때 서로 겹치거나 멀어지지 않게 보정하는 기준이 된다.
    private Vector3 grabbedOffsetFromPlayer;

    // 상자를 잡은 면 기준의 이동 축.
    // 대각선으로 붙었을 때 상자를 이상하게 끌지 않도록 네 방향 중 하나만 허용한다.
    private Vector3 grabbedMoveAxis;

    // 플레이어 이동/우산 상태를 읽기 위해 같은 오브젝트의 컴포넌트를 캐싱한다.
    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        umbrellaController = GetComponent<PlayerUmbrellaController>();
    }

    // Inspector에서 음수 값이 들어와도 런타임 계산이 깨지지 않도록 보정한다.
    private void OnValidate()
    {
        grabRange = Mathf.Max(0.0f, grabRange);
        grabbedMoveSpeed = Mathf.Max(0.0f, grabbedMoveSpeed);
    }

    // InputActionReference를 직접 넣은 경우에만 활성화한다.
    // 비어 있으면 아래 WasInteractPressed/IsInteractPressed에서 우클릭 fallback을 사용한다.
    private void OnEnable()
    {
        EnableAction(interactAction);
    }

    // 컴포넌트가 꺼질 때 잡기 상태를 반드시 해제한다.
    // 그래야 플레이어 이동 제한이나 상자 제약이 남아 있지 않는다.
    private void OnDisable()
    {
        EndGrab();
        DisableAction(interactAction);
    }

    // 입력과 후보 갱신은 프레임 단위로 처리한다.
    // 실제 Rigidbody 속도 적용은 FixedUpdate에서 처리해서 물리 업데이트와 맞춘다.
    private void Update()
    {
        RefreshCandidate();

        if (grabbedObject != null)
        {
            // 잡고 있는 동안 조건이 깨지면 즉시 놓는다.
            // 예: 우산을 펴거나, 공중에 뜨거나, 버튼을 떼거나, 너무 멀어지는 경우.
            if (!CanUseHands() || !IsInteractPressed() || IsGrabbedObjectTooFar())
            {
                EndGrab();
            }

            return;
        }

        // 아직 아무것도 잡지 않은 상태에서 상호작용 입력이 들어오면 잡기를 시도한다.
        if (WasInteractPressed())
        {
            TryBeginGrab();
        }
    }

    // 잡고 있는 동안 플레이어 입력을 상자 이동축으로 투영해 플레이어와 상자에 같은 방향의 속도를 준다.
    private void FixedUpdate()
    {
        if (grabbedObject == null || playerMovement == null)
        {
            return;
        }

        // 물리 프레임 사이에 손 사용 조건이 깨질 수도 있으므로 여기서 한 번 더 방어한다.
        if (!CanUseHands())
        {
            EndGrab();
            return;
        }

        ApplyGrabbedPlatformFrameMotion();

        Vector3 moveDirection = playerMovement.GetCameraRelativeMoveDirection(playerMovement.MoveInput);
        Vector3 grabbedVelocity = Vector3.zero;
        if (moveDirection.sqrMagnitude > MoveInputThreshold && grabbedMoveAxis.sqrMagnitude > MoveInputThreshold)
        {
            // 입력 방향을 잡기 축에 투영한다.
            // 예를 들어 상자 앞면을 잡았으면 앞/뒤 입력만 상자 이동으로 쓰고, 좌/우 입력은 무시된다.
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

    // 회전 발판 위에서 잡고 있을 때는 잡은 순간의 간격과 이동축도 발판 회전만큼 같이 돌린다.
    // 위치만 발판을 따라가고 이 기준값들이 월드 방향에 고정되면, 보정 속도가 대각선 힘처럼 섞일 수 있다.
    private void ApplyGrabbedPlatformFrameMotion()
    {
        if (grabbedObject == null)
        {
            return;
        }

        MovingPlatformSurface platform = grabbedObject.CurrentMovingPlatform;
        if (platform == null)
        {
            return;
        }

        float platformYawDelta = platform.GetDeltaYaw();
        if (Mathf.Approximately(platformYawDelta, 0.0f))
        {
            return;
        }

        Quaternion yawDelta = Quaternion.AngleAxis(platformYawDelta, Vector3.up);
        grabbedOffsetFromPlayer = yawDelta * grabbedOffsetFromPlayer;
        grabbedOffsetFromPlayer.y = 0.0f;

        grabbedMoveAxis = yawDelta * grabbedMoveAxis;
        grabbedMoveAxis.y = 0.0f;
        if (grabbedMoveAxis.sqrMagnitude > MoveInputThreshold)
        {
            grabbedMoveAxis.Normalize();
        }
    }

    // 현재 잡을 수 있는 후보를 갱신한다.
    // 이미 잡은 물체가 있으면 후보도 그 물체로 유지한다.
    private void RefreshCandidate()
    {
        if (grabbedObject != null)
        {
            currentCandidate = grabbedObject;
            return;
        }

        currentCandidate = FindCandidate();
    }

    // grabRange 안의 PushPullObject 중 플레이어가 네 방향에서 잡을 수 있는 가장 가까운 물체를 찾는다.
    private PushPullObject FindCandidate()
    {
        if (!CanUseHands())
        {
            return null;
        }

        Vector3 origin = GetCheckOrigin();
        // NonAlloc 버전으로 검색해서 매 프레임 후보를 찾더라도 GC 할당이 생기지 않게 한다.
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

            // 대각선에서 잡으면 밀고 당기는 축이 애매해지므로 네 방향에 가까운 경우만 후보로 인정한다.
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

    // 현재 후보를 실제로 잡기 시작한다.
    // 성공하면 플레이어의 일반 이동/회전은 잠시 막고 이 컴포넌트가 이동을 담당한다.
    private void TryBeginGrab()
    {
        if (currentCandidate == null || !CanUseHands())
        {
            return;
        }

        // 잡은 면을 기준으로 이동축을 고정한다.
        // 이 축을 저장해 두어 잡은 뒤에도 대각선 입력으로 상자가 옆으로 새지 않게 한다.
        if (!TryGetCardinalGrabAxis(currentCandidate, out Vector3 moveAxis))
        {
            return;
        }

        // 물체 쪽에서도 현재 다른 플레이어/시스템이 잡고 있지 않은지 확인한다.
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

    // 잡기 상태를 끝내고 플레이어/상자의 이동 제어를 원래대로 되돌린다.
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

    // 잡은 순간의 수평 간격을 저장한다.
    // 이후 보정 계산에서 이 간격을 유지하려고 시도한다.
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

    // 상자가 플레이어보다 늦거나 빠르게 움직여 간격이 변했을 때 이를 줄이는 보정 속도를 계산한다.
    private Vector3 GetHoldOffsetCorrectionVelocity()
    {
        if (grabbedObject == null || playerMovement == null)
        {
            return Vector3.zero;
        }

        Vector3 desiredObjectPosition = playerMovement.RigidbodyPosition + grabbedOffsetFromPlayer;
        Vector3 offsetError = desiredObjectPosition - grabbedObject.RigidbodyPosition;
        offsetError.y = 0.0f;

        // 잡기 이동은 처음 잡은 면의 축으로만 허용한다.
        // 간격 보정까지 X/Z 전체 방향으로 넣으면 상자가 대각선으로 밀릴 수 있어서 같은 축으로만 투영한다.
        Vector3 axisLimitedError = ProjectOntoGrabbedMoveAxis(offsetError);

        return Vector3.ClampMagnitude(
            axisLimitedError * HoldOffsetCorrectionStrength,
            MaxHoldOffsetCorrectionSpeed);
    }

    // 입력이나 보정 벡터를 현재 잡기 축 위로만 남긴다.
    // 이렇게 해야 상자를 앞뒤 또는 좌우 한 축으로만 안정적으로 밀고 당길 수 있다.
    private Vector3 ProjectOntoGrabbedMoveAxis(Vector3 vector)
    {
        vector.y = 0.0f;

        if (grabbedMoveAxis.sqrMagnitude <= MoveInputThreshold)
        {
            return Vector3.zero;
        }

        return grabbedMoveAxis * Vector3.Dot(vector, grabbedMoveAxis);
    }

    // 잡기 시작한 순간 플레이어가 상자를 바라보게 한다.
    // 잡은 상태에서는 회전을 잠그므로, 이때의 방향이 잡기 자세의 기준이 된다.
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

    // 플레이어가 상자의 앞/뒤/좌/우 중 어느 면에 가까운지 찾는다.
    // 충분히 가까운 면이 없으면 대각선 접근으로 보고 잡기를 거부한다.
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

        // Dot 값이 낮으면 플레이어가 면의 정면이 아니라 모서리/대각선에 있다고 판단한다.
        if (bestDot < MinCardinalGrabDot)
        {
            return false;
        }

        moveAxis = bestAxis;
        return true;
    }

    // 현재까지 찾은 축보다 플레이어 방향에 더 가까운 축이면 교체한다.
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

    // 상자 축이 기울어져 있거나 길이가 거의 0일 때를 대비해 수평 축으로 정리한다.
    private static Vector3 GetHorizontalAxis(Vector3 axis, Vector3 fallback)
    {
        axis.y = 0.0f;
        if (axis.sqrMagnitude <= MoveInputThreshold)
        {
            return fallback;
        }

        return axis.normalized;
    }

    // 현재 플레이어가 손 조작을 할 수 있는 상태인지 확인한다.
    // 우산 조작, 공중 상태, 활공과 손 조작이 겹치지 않게 하는 게 목적이다.
    private bool CanUseHands()
    {
        if (requireGrounded && playerMovement != null && !playerMovement.isGrounded)
        {
            return false;
        }

        // 우산 상태 제한을 끄거나, 아직 우산이 없다면 손 조작을 막지 않는다.
        if (!requireClosedUmbrella || umbrellaController == null || !umbrellaController.HasUmbrella)
        {
            return true;
        }

        return umbrellaController.CurrentState == PlayerUmbrellaController.UmbrellaState.Closed;
    }

    // 잡고 있는 도중 물리 충돌 등으로 물체가 너무 멀어지면 잡기를 끊는다.
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

    // 검색 중심점. 손 위치용 Transform을 넣지 않았으면 플레이어 몸 중앙보다 약간 위를 사용한다.
    private Vector3 GetCheckOrigin()
    {
        return checkOrigin != null ? checkOrigin.position : transform.position + Vector3.up * 0.6f;
    }

    // 상호작용을 새로 누른 프레임인지 확인한다.
    // InputAction이 비어 있으면 우클릭을 기본 입력으로 사용한다.
    private bool WasInteractPressed()
    {
        InputAction action = interactAction != null ? interactAction.action : null;
        if (action != null)
        {
            return action.WasPressedThisFrame();
        }

        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    }

    // 잡고 있는 동안 상호작용 버튼을 계속 누르고 있는지 확인한다.
    private bool IsInteractPressed()
    {
        InputAction action = interactAction != null ? interactAction.action : null;
        if (action != null)
        {
            return action.IsPressed();
        }

        return Mouse.current != null && Mouse.current.rightButton.isPressed;
    }

    // InputActionReference가 있을 때만 Enable한다.
    private void EnableAction(InputActionReference actionReference)
    {
        if (actionReference == null)
        {
            return;
        }

        actionReference.action.Enable();
    }

    // InputActionReference가 있을 때만 Disable한다.
    private void DisableAction(InputActionReference actionReference)
    {
        if (actionReference == null)
        {
            return;
        }

        actionReference.action.Disable();
    }

    // Scene View에서 선택했을 때 잡기 탐지 범위를 확인하기 위한 보조 기즈모.
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = GetCheckOrigin();

        Gizmos.color = grabbedObject != null ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(origin, grabRange);
    }
}
