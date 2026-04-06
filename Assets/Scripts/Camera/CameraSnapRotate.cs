using UnityEngine;
using UnityEngine.InputSystem;

public class CameraSnapRotate : MonoBehaviour
{
    // 따라갈 대상
    public Transform target;

    // 왼쪽 회전 입력 액션
    public InputActionReference rotateLeftAction;

    // 오른쪽 회전 입력 액션
    public InputActionReference rotateRightAction;

    // 한 번 회전할 각도
    public float rotateStep = 45.0f;

    // 회전이 목표 각도까지 도달하는 시간
    public float smoothTime = 0.15f;

    // 현재 회전 속도
    private float rotationVelocity;

    // 목표 Y 회전값
    private float targetYaw;

    void Start()
    {
        // 시작할 때 현재 Y 회전을 목표값으로 저장
        targetYaw = transform.eulerAngles.y;
    }

    void OnEnable()
    {
        if (rotateLeftAction != null)
        {
            rotateLeftAction.action.Enable();
        }

        if (rotateRightAction != null)
        {
            rotateRightAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (rotateLeftAction != null)
        {
            rotateLeftAction.action.Disable();
        }

        if (rotateRightAction != null)
        {
            rotateRightAction.action.Disable();
        }
    }

    void LateUpdate()
    {
        // CameraRig가 플레이어 위치를 따라감
        if (target != null)
        {
            transform.position = target.position;
        }

        // Q를 누르면 목표 각도를 왼쪽으로 45도 변경
        if (rotateLeftAction != null && rotateLeftAction.action.WasPressedThisFrame())
        {
            targetYaw -= rotateStep;
        }

        // E를 누르면 목표 각도를 오른쪽으로 45도 변경
        if (rotateRightAction != null && rotateRightAction.action.WasPressedThisFrame())
        {
            targetYaw += rotateStep;
        }

        // 현재 Y 각도를 목표 각도로 부드럽게 보간
        float currentYaw = transform.eulerAngles.y;
        float smoothYaw = Mathf.SmoothDampAngle
        (
            currentYaw,
            targetYaw,
            ref rotationVelocity,
            smoothTime
        );

        transform.rotation = Quaternion.Euler(0.0f, smoothYaw, 0.0f);
    }
}
