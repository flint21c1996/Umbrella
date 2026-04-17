using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어를 따라가는 카메라 리그.
// Q/E 스냅 회전과 마우스 휠 줌만 담당한다.
// 회전 플랫폼 위에 올라가도 카메라 각도는 자동으로 따라 돌리지 않는다.
public class CameraSnapRotate : MonoBehaviour
{
    private const float MouseWheelNotchValue = 120.0f;

    // 카메라 리그가 따라갈 대상. 보통 Player나 CameraTarget을 연결한다.
    public Transform target;

    [Header("Camera")]
    // 실제 거리를 조절할 Camera Transform. 비워두면 자식 Camera를 자동으로 찾는다.
    [SerializeField] private Transform cameraTransform;

    [Header("Rotate")]
    // 카메라를 왼쪽으로 돌리는 입력 액션.
    public InputActionReference rotateLeftAction;

    // 카메라를 오른쪽으로 돌리는 입력 액션.
    public InputActionReference rotateRightAction;

    // Q/E를 한 번 눌렀을 때 회전할 각도.
    public float rotateStep = 45.0f;

    // 목표 회전 각도까지 부드럽게 도달하는 시간.
    public float smoothTime = 0.15f;

    [Header("Zoom")]
    // 마우스 휠 한 칸에 카메라 거리가 얼마나 변할지 정한다.
    [SerializeField] private float zoomStep = 1.0f;

    // 카메라가 플레이어에게 가장 가까워질 수 있는 거리.
    [SerializeField] private float minDistance = 4.0f;

    // 카메라가 플레이어에게 가장 멀어질 수 있는 거리.
    [SerializeField] private float maxDistance = 14.0f;

    // 목표 거리까지 부드럽게 따라가는 시간.
    [SerializeField] private float zoomSmoothTime = 0.08f;

    private float rotationVelocity;
    private float zoomVelocity;

    // Q/E 입력으로 도달하려는 목표 Y 회전값.
    private float targetYaw;

    // 마우스 휠 입력으로 도달하려는 카메라 거리.
    private float targetCameraDistance;

    // 카메라가 리그 기준 어느 방향으로 떨어져 있는지 저장한다.
    private Vector3 cameraLocalDirection = Vector3.back;

    void Start()
    {
        CacheCameraTransform();

        // 시작할 때 현재 Y 회전을 목표값으로 저장한다.
        targetYaw = transform.eulerAngles.y;

        InitializeZoomDistance();
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
        // 카메라 리그는 매 프레임 target 위치를 따라간다.
        if (target != null)
        {
            transform.position = target.position;
        }

        // Q/E 입력은 카메라의 목표 Y 각도만 바꾼다.
        // 회전 플랫폼 같은 외부 오브젝트는 카메라 각도를 직접 바꾸지 않는다.
        if (rotateLeftAction != null && rotateLeftAction.action.WasPressedThisFrame())
        {
            targetYaw -= rotateStep;
        }

        if (rotateRightAction != null && rotateRightAction.action.WasPressedThisFrame())
        {
            targetYaw += rotateStep;
        }

        HandleZoomInput();
        ApplyYawRotation();
        ApplyZoomDistance();
    }

    private void OnValidate()
    {
        // Inspector에서 잘못된 값이 들어와도 정상 범위로 보정한다.
        minDistance = Mathf.Max(0.1f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        zoomStep = Mathf.Max(0.0f, zoomStep);
        zoomSmoothTime = Mathf.Max(0.0f, zoomSmoothTime);
    }

    private void CacheCameraTransform()
    {
        if (cameraTransform != null)
        {
            return;
        }

        // 직접 연결하지 않았다면 CameraRig 아래의 Camera를 자동으로 사용한다.
        Camera childCamera = GetComponentInChildren<Camera>();
        if (childCamera != null)
        {
            cameraTransform = childCamera.transform;
        }
    }

    private void InitializeZoomDistance()
    {
        if (cameraTransform == null)
        {
            targetCameraDistance = Mathf.Clamp(maxDistance, minDistance, maxDistance);
            return;
        }

        Vector3 localPosition = cameraTransform.localPosition;

        // 카메라가 리그 중심에서 떨어져 있는 방향을 저장한다.
        // 현재 프로젝트처럼 z = -10 형태면 Vector3.back 방향이 된다.
        if (localPosition.sqrMagnitude > 0.0001f)
        {
            cameraLocalDirection = localPosition.normalized;
        }

        targetCameraDistance = Mathf.Clamp(localPosition.magnitude, minDistance, maxDistance);
        cameraTransform.localPosition = cameraLocalDirection * targetCameraDistance;
    }

    private void HandleZoomInput()
    {
        if (Mouse.current == null)
        {
            return;
        }

        float scrollDelta = Mouse.current.scroll.ReadValue().y;

        // 휠을 움직이지 않았다면 카메라 거리를 바꾸지 않는다.
        if (Mathf.Approximately(scrollDelta, 0.0f))
        {
            return;
        }

        float scrollNotches = NormalizeScrollDelta(scrollDelta);

        targetCameraDistance = Mathf.Clamp(
            targetCameraDistance - scrollNotches * zoomStep,
            minDistance,
            maxDistance
        );
    }

    private float NormalizeScrollDelta(float scrollDelta)
    {
        // 일반 마우스 휠은 한 칸이 보통 120으로 들어온다.
        // 이미 작은 값으로 들어오는 장치도 있어서 큰 값일 때만 보정한다.
        if (Mathf.Abs(scrollDelta) > 1.0f)
        {
            return scrollDelta / MouseWheelNotchValue;
        }

        return scrollDelta;
    }

    private void ApplyYawRotation()
    {
        // 카메라 회전은 Q/E 입력으로 정한 targetYaw만 따라간다.
        // 움직이는 발판의 회전값은 플레이어 몸 방향에만 적용하고 카메라에는 섞지 않는다.
        float currentYaw = transform.eulerAngles.y;
        float smoothYaw = Mathf.SmoothDampAngle(
            currentYaw,
            targetYaw,
            ref rotationVelocity,
            smoothTime
        );

        transform.rotation = Quaternion.Euler(0.0f, smoothYaw, 0.0f);
    }

    private void ApplyZoomDistance()
    {
        if (cameraTransform == null)
        {
            return;
        }

        float currentDistance = cameraTransform.localPosition.magnitude;
        float nextDistance = Mathf.SmoothDamp(
            currentDistance,
            targetCameraDistance,
            ref zoomVelocity,
            zoomSmoothTime
        );

        cameraTransform.localPosition = cameraLocalDirection * nextDistance;
    }
}
