using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    // 이동 속도
    public float moveSpeed = 5.0f;

    // 점프 힘
    public float jumpForce = 5.0f;

    // 이동 기준이 되는 카메라 리그
    public Transform cameraRig;

    // 이동 입력 액션
    public InputActionReference moveAction;

    // 점프 입력 액션
    public InputActionReference jumpAction;

    // Rigidbody 참조
    private Rigidbody rb;

    // 현재 이동 입력값 저장
    private Vector2 moveInput;

    // 바닥 체크용
    public bool isGrounded;



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
    }

    void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.action.Enable();
        }

        if (jumpAction != null)
        {
            jumpAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.action.Disable();
        }

        if (jumpAction != null)
        {
            jumpAction.action.Disable();
        }
    }

    void Update()
    {
        if (moveAction == null || jumpAction == null || rb == null || cameraRig == null)
        {
            return;
        }

        // 입력은 Update에서 읽어서 저장
        moveInput = moveAction.action.ReadValue<Vector2>();

        // 점프는 바닥에 있을 때만
        if (isGrounded && jumpAction.action.WasPressedThisFrame())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void FixedUpdate()
    {
        if (rb == null || cameraRig == null)
        {
            return;
        }

        // 카메라 기준 forward/right 계산
        Vector3 forward = cameraRig.forward;
        Vector3 right = cameraRig.right;

        forward.y = 0.0f;
        right.y = 0.0f;

        forward.Normalize();
        right.Normalize();

        // 입력값을 카메라 기준 이동 방향으로 변환
        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;

        if (moveDirection.magnitude > 1.0f)
        {
            moveDirection.Normalize();
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.x = moveDirection.x * moveSpeed;
        velocity.z = moveDirection.z * moveSpeed;
        rb.linearVelocity = velocity;

    }

    void OnCollisionStay(Collision collision)
    {
        isGrounded = true;
    }

    void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}