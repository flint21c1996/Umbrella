using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerMovement : MonoBehaviour
{
    private const float MoveInputThreshold = 0.0001f;

    [Header("Move")]
    // 목표 최대 이동 속도
    public float moveSpeed = 5.0f;

    // 지상에서 목표 속도까지 붙는 가속도
    public float groundAcceleration = 30.0f;

    // 지상에서 입력을 놓았을 때 줄어드는 감속도
    public float groundDeceleration = 35.0f;

    // 공중에서의 가속도
    public float airAcceleration = 12.0f;

    // 공중에서 입력을 놓았을 때의 감속도
    public float airDeceleration = 4.0f;

    // 이동 방향을 바라보는 회전 속도
    public float turnSpeed = 720.0f;

    // 우산 붓기처럼 명확한 조준 상태일 때는 더 빠르게 시선을 맞춘다.
    public float facingOverrideTurnSpeedMultiplier = 2.5f;

    // 이 각도부터 회전 중 감속을 시작한다
    public float turnSlowdownStartAngle = 35.0f;

    // 이 각도 이상 벌어지면 이동을 거의 멈추고 회전에 집중한다
    public float turnLockAngle = 140.0f;

    [Header("References")]
    // 이동 기준이 되는 카메라 리그
    public Transform cameraRig;

    // 이동 입력 액션
    public InputActionReference moveAction;

    // Rigidbody 참조
    private Rigidbody rb;

    // 현재 이동 입력값 저장
    private Vector2 moveInput;

    // 다른 시스템이 잠깐 바라보는 방향을 강제로 지정할 때 사용한다.
    private bool hasFacingOverride;
    private Vector3 facingOverrideDirection;

    private bool isRotationLocked;

    // 바닥 체크용
    public bool isGrounded;

    // 밀기/당기기 같은 외부 시스템이 플레이어 이동 속도를 잠시 낮출 때 사용한다.
    private float externalMoveSpeedMultiplier = 1.0f;

    // 밀기/당기기처럼 다른 컴포넌트가 직접 수평 속도를 정할 때 사용한다.
    private bool hasExternalHorizontalVelocity;
    private Vector3 externalHorizontalVelocity;

    public Vector2 MoveInput => moveInput;
    public float ExternalMoveSpeedMultiplier => externalMoveSpeedMultiplier;
    public Vector3 RigidbodyPosition => rb != null ? rb.position : transform.position;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("Rigidbody is missing on the Player object.");
        }

        if (cameraRig == null)
        {
            Debug.LogError("CameraRig is not assigned in the Inspector.");
        }

        InitializeJumpReferences();
    }

    void OnValidate()
    {
        ValidateJumpSettings();
        externalMoveSpeedMultiplier = Mathf.Max(0.0f, externalMoveSpeedMultiplier);
    }

    void OnEnable()
    {
        EnableAction(moveAction);
        EnableJumpAction();
    }

    void OnDisable()
    {
        DisableAction(moveAction);
        DisableJumpAction();
    }

    void Update()
    {
        if (rb == null || cameraRig == null)
        {
            return;
        }

        // 입력은 Update에서 읽어서 저장
        InputAction moveInputAction = moveAction != null ? moveAction.action : null;
        moveInput = moveInputAction != null ? moveInputAction.ReadValue<Vector2>() : Vector2.zero;

        UpdateJumpInput();
    }

    void FixedUpdate()
    {
        if (rb == null || cameraRig == null)
        {
            return;
        }

        Vector3 moveDirection = GetCameraRelativeMoveDirection(moveInput);

        if (hasExternalHorizontalVelocity)
        {
            ApplyExternalHorizontalVelocity();
            ApplyGlideFallLimit();
            return;
        }

        Vector3 effectiveMoveDirection = GetEffectiveMoveDirection(moveDirection);

        ApplyMovementForce(effectiveMoveDirection);

        if (!isRotationLocked)
        {
            RotateTowardsMoveDirection(GetLookDirection(moveDirection));
        }

        ApplyGlideFallLimit();
    }

    public void SetExternalMoveSpeedMultiplier(float multiplier)
    {
        externalMoveSpeedMultiplier = Mathf.Max(0.0f, multiplier);
    }

    public void ResetExternalMoveSpeedMultiplier()
    {
        externalMoveSpeedMultiplier = 1.0f;
    }

    public void SetRotationLocked(bool locked)
    {
        isRotationLocked = locked;
    }

    public void SetHorizontalVelocity(Vector3 horizontalVelocity)
    {
        if (rb == null)
        {
            return;
        }

        horizontalVelocity.y = 0.0f;
        externalHorizontalVelocity = horizontalVelocity;
        hasExternalHorizontalVelocity = true;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
    }

    public void ClearHorizontalVelocityOverride()
    {
        hasExternalHorizontalVelocity = false;
        externalHorizontalVelocity = Vector3.zero;
    }

    public void StopHorizontalMovement()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = new Vector3(0.0f, rb.linearVelocity.y, 0.0f);
    }

    public void FaceDirectionImmediately(Vector3 worldDirection)
    {
        if (rb == null)
        {
            return;
        }

        worldDirection.y = 0.0f;
        if (worldDirection.sqrMagnitude < MoveInputThreshold)
        {
            return;
        }

        rb.rotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
    }

    public Vector3 GetCameraRelativeMoveDirection(Vector2 input)
    {
        if (cameraRig == null)
        {
            return Vector3.zero;
        }

        Vector3 forward = cameraRig.forward;
        Vector3 right = cameraRig.right;

        forward.y = 0.0f;
        right.y = 0.0f;

        forward.Normalize();
        right.Normalize();

        // 입력값을 카메라 기준 이동 방향으로 변환
        Vector3 moveDirection = forward * input.y + right * input.x;

        if (moveDirection.sqrMagnitude > 1.0f)
        {
            moveDirection.Normalize();
        }

        return moveDirection;
    }

    Vector3 GetLookDirection(Vector3 moveDirection)
    {
        if (hasFacingOverride && facingOverrideDirection.sqrMagnitude > MoveInputThreshold)
        {
            return facingOverrideDirection;
        }

        return moveDirection;
    }

    Vector3 GetEffectiveMoveDirection(Vector3 desiredMoveDirection)
    {
        if (desiredMoveDirection.sqrMagnitude < MoveInputThreshold)
        {
            return Vector3.zero;
        }

        Vector3 facingDirection = rb.rotation * Vector3.forward;
        facingDirection.y = 0.0f;
        facingDirection.Normalize();

        float angleToMoveDirection = Vector3.Angle(facingDirection, desiredMoveDirection);
        float moveAlignmentFactor = GetMoveAlignmentFactor(angleToMoveDirection);
        return desiredMoveDirection * moveAlignmentFactor;
    }

    float GetMoveAlignmentFactor(float angleToMoveDirection)
    {
        // 회전 중에도 완전히 멈추지 않게 하되,
        // 크게 반대 방향으로 꺾을 때는 속도를 줄여 발 미끄럼을 완화한다.
        if (angleToMoveDirection <= turnSlowdownStartAngle)
        {
            return 1.0f;
        }

        if (angleToMoveDirection >= turnLockAngle)
        {
            return 0.0f;
        }

        float t = Mathf.InverseLerp(turnLockAngle, turnSlowdownStartAngle, angleToMoveDirection);
        return Mathf.SmoothStep(0.0f, 1.0f, t);
    }

    void ApplyMovementForce(Vector3 moveDirection)
    {
        Vector3 targetVelocity = moveDirection * (moveSpeed * externalMoveSpeedMultiplier);
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 currentHorizontalVelocity = new Vector3(currentVelocity.x, 0.0f, currentVelocity.z);

        bool hasMoveInput = moveDirection.sqrMagnitude > MoveInputThreshold;
        float acceleration = GetCurrentAcceleration(hasMoveInput);

        Vector3 velocityDelta = targetVelocity - currentHorizontalVelocity;
        Vector3 requiredAcceleration = velocityDelta / Time.fixedDeltaTime;
        Vector3 clampedAcceleration = Vector3.ClampMagnitude(requiredAcceleration, acceleration);

        rb.AddForce(clampedAcceleration, ForceMode.Acceleration);

        Vector3 newHorizontalVelocity = new Vector3(rb.linearVelocity.x, 0.0f, rb.linearVelocity.z);
        float currentMoveSpeedLimit = moveSpeed * externalMoveSpeedMultiplier;
        if (newHorizontalVelocity.magnitude > currentMoveSpeedLimit)
        {
            newHorizontalVelocity = newHorizontalVelocity.normalized * currentMoveSpeedLimit;
            rb.linearVelocity = new Vector3(newHorizontalVelocity.x, rb.linearVelocity.y, newHorizontalVelocity.z);
        }
    }

    void ApplyExternalHorizontalVelocity()
    {
        rb.linearVelocity = new Vector3(externalHorizontalVelocity.x, rb.linearVelocity.y, externalHorizontalVelocity.z);
    }

    float GetCurrentAcceleration(bool hasMoveInput)
    {
        if (isGrounded)
        {
            return hasMoveInput ? groundAcceleration : groundDeceleration;
        }

        return hasMoveInput ? airAcceleration : airDeceleration;
    }

    void RotateTowardsMoveDirection(Vector3 moveDirection)
    {
        if (moveDirection.sqrMagnitude < MoveInputThreshold)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        float effectiveTurnSpeed = hasFacingOverride
            ? turnSpeed * facingOverrideTurnSpeedMultiplier
            : turnSpeed;
        Quaternion nextRotation = Quaternion.RotateTowards(
            rb.rotation,
            targetRotation,
            effectiveTurnSpeed * Time.fixedDeltaTime
        );

        rb.MoveRotation(nextRotation);
    }

    public void SetFacingOverride(Vector3 worldDirection)
    {
        worldDirection.y = 0.0f;

        if (worldDirection.sqrMagnitude < MoveInputThreshold)
        {
            return;
        }

        hasFacingOverride = true;
        facingOverrideDirection = worldDirection.normalized;
    }

    public void ClearFacingOverride()
    {
        hasFacingOverride = false;
        facingOverrideDirection = Vector3.zero;
    }

    void EnableAction(InputActionReference actionReference)
    {
        if (actionReference == null)
        {
            return;
        }

        actionReference.action.Enable();
    }

    void DisableAction(InputActionReference actionReference)
    {
        if (actionReference == null)
        {
            return;
        }

        actionReference.action.Disable();
    }
}
