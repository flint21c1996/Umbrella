using UnityEngine;

// 오브젝트를 자동으로 회전시키는 범용 컴포넌트.
// X/Y/Z축 회전 속도를 각각 지정할 수 있어서 맵, 장치, 장식 오브젝트에 재사용할 수 있다.
// MovingPlatformSurface보다 먼저 움직여야 플레이어가 같은 FixedUpdate에서 이동량을 따라갈 수 있다.
[DefaultExecutionOrder(-50)]
public class AutoRotator : MonoBehaviour
{
    [Tooltip("Rotation speed for each axis in degrees per second.")]
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0.0f, 5.0f, 0.0f);

    [Tooltip("If true, rotate around world axes. If false, rotate around this object's local axes.")]
    [SerializeField] private bool useWorldSpace = true;

    [Tooltip("Use FixedUpdate when this object should carry physics objects such as the player.")]
    [SerializeField] private bool useFixedUpdate = true;

    private MovingPlatformSurface movingPlatformSurface;

    private void Awake()
    {
        movingPlatformSurface = GetComponent<MovingPlatformSurface>();
    }

    private void FixedUpdate()
    {
        if (!useFixedUpdate)
        {
            return;
        }

        RotateBy(Time.fixedDeltaTime);
    }

    private void Update()
    {
        if (useFixedUpdate)
        {
            return;
        }

        RotateBy(Time.deltaTime);
    }

    private void RotateBy(float deltaTime)
    {
        // 세 축 모두 0이면 회전시킬 필요가 없으므로 바로 빠져나간다.
        if (rotationSpeed.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        // MovingPlatformSurface가 있으면 회전 전후를 기록해서 플레이어가 같은 이동량을 따라갈 수 있게 한다.
        movingPlatformSurface?.CaptureBeforeMotion();

        Space rotationSpace = useWorldSpace ? Space.World : Space.Self;
        Vector3 deltaRotation = rotationSpeed * deltaTime;

        transform.Rotate(deltaRotation, rotationSpace);

        movingPlatformSurface?.CaptureAfterMotion();
    }

    private void OnValidate()
    {
        // 너무 큰 값을 실수로 넣어도 다루기 쉬운 범위 안에서 테스트할 수 있게 제한한다.
        rotationSpeed.x = Mathf.Clamp(rotationSpeed.x, -360.0f, 360.0f);
        rotationSpeed.y = Mathf.Clamp(rotationSpeed.y, -360.0f, 360.0f);
        rotationSpeed.z = Mathf.Clamp(rotationSpeed.z, -360.0f, 360.0f);
    }
}
