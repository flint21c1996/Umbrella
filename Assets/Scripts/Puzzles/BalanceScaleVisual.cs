using UnityEngine;

// 양팔저울의 조건 판정과 별개로, 접시와 빔이 실제 저울처럼 움직이게 하는 시각 연출 컴포넌트.
// BalanceScaleCondition이 있으면 그 조건의 왼쪽/오른쪽 무게를 읽고,
// 없으면 직접 지정한 IPuzzleWeightSource 컴포넌트에서 무게를 읽는다.
// 접시에 MovingPlatformSurface를 붙여두면 플레이어와 PushPullObject도 접시 이동량을 따라간다.
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class BalanceScaleVisual : MonoBehaviour
{
    private enum BeamTiltAxis
    {
        LocalX,
        LocalNegativeX,
        LocalY,
        LocalNegativeY,
        LocalZ,
        LocalNegativeZ
    }

    [Header("Weight")]
    [Tooltip("왼쪽/오른쪽 무게를 읽어올 조건 컴포넌트. 지정하면 아래의 직접 무게 소스는 사용하지 않는다.")]
    [SerializeField] private BalanceScaleCondition condition;

    [Tooltip("Condition이 비어 있을 때만 사용하는 왼쪽 무게 소스. IPuzzleWeightSource를 구현한 컴포넌트를 넣어야 한다.")]
    [SerializeField] private MonoBehaviour leftWeightSource;

    [Tooltip("Condition이 비어 있을 때만 사용하는 오른쪽 무게 소스. IPuzzleWeightSource를 구현한 컴포넌트를 넣어야 한다.")]
    [SerializeField] private MonoBehaviour rightWeightSource;

    [Header("Visuals")]
    [Tooltip("양쪽 접시 사이에서 기울어질 빔 또는 막대.")]
    [SerializeField] private Transform beam;

    [Tooltip("위아래로 움직일 왼쪽 접시 Transform.")]
    [SerializeField] private Transform leftPlate;

    [Tooltip("위아래로 움직일 오른쪽 접시 Transform.")]
    [SerializeField] private Transform rightPlate;

    [Header("Motion")]
    [Tooltip("최대 기울기와 최대 접시 이동량에 도달하기 위해 필요한 무게 차이.")]
    [SerializeField] private float maxWeightDifference = 5.0f;

    [Tooltip("각 접시가 기준 위치에서 위아래로 움직일 수 있는 최대 거리.")]
    [SerializeField] private float maxPlateOffset = 0.5f;

    [Tooltip("빔이 최대로 기울어질 각도. 단위는 도(degree).")]
    [SerializeField] private float maxBeamTiltAngle = 12.0f;

    [Tooltip("빔을 기울일 때 사용할 로컬 축. 같은 축인데 방향이 반대로 보이면 Negative 축을 사용한다.")]
    [SerializeField] private BeamTiltAxis beamTiltAxis = BeamTiltAxis.LocalX;

    [Tooltip("접시와 빔이 목표 자세를 따라가는 속도.")]
    [SerializeField] private float followSpeed = 8.0f;

    [Tooltip("접시와 빔의 움직임 방향이 씬에서 반대로 보일 때 켠다.")]
    [SerializeField] private bool invertDirection;

    [Header("Runtime")]
    [SerializeField] private float leftWeight;
    [SerializeField] private float rightWeight;
    [SerializeField] private float visualBalance;

    private Vector3 initialLeftPlateLocalPosition;
    private Vector3 initialRightPlateLocalPosition;
    private Quaternion initialBeamLocalRotation;
    private MovingPlatformSurface leftPlateSurface;
    private MovingPlatformSurface rightPlateSurface;
    private bool hasInitialPose;

    // 컴포넌트를 붙였을 때 같은 오브젝트의 BalanceScaleCondition을 자동으로 연결한다.
    private void Reset()
    {
        condition = GetComponent<BalanceScaleCondition>();
    }

    // 처음 시작할 때 기준 위치를 저장한다.
    // 이후 저울은 이 위치를 기준으로 위/아래로만 움직인다.
    private void Awake()
    {
        CacheInitialPose();
        CacheMovingPlatformSurfaces();
    }

    private void OnEnable()
    {
        CacheInitialPose();
        CacheMovingPlatformSurfaces();
    }

    // Inspector에서 음수 값이 들어가면 움직임 계산이 뒤집히거나 0으로 나눌 수 있어서 보정한다.
    private void OnValidate()
    {
        maxWeightDifference = Mathf.Max(0.01f, maxWeightDifference);
        maxPlateOffset = Mathf.Max(0.0f, maxPlateOffset);
        maxBeamTiltAngle = Mathf.Max(0.0f, maxBeamTiltAngle);
        followSpeed = Mathf.Max(0.0f, followSpeed);
    }

    // 접시가 물리 오브젝트를 태우는 발판 역할도 하므로 FixedUpdate에서 움직인다.
    private void FixedUpdate()
    {
        RefreshWeights();
        ApplyVisualMotion(Time.fixedDeltaTime);
    }

    private void CacheInitialPose()
    {
        if (hasInitialPose)
        {
            return;
        }

        if (leftPlate != null)
        {
            initialLeftPlateLocalPosition = leftPlate.localPosition;
        }

        if (rightPlate != null)
        {
            initialRightPlateLocalPosition = rightPlate.localPosition;
        }

        if (beam != null)
        {
            initialBeamLocalRotation = beam.localRotation;
        }

        hasInitialPose = true;
    }

    private void CacheMovingPlatformSurfaces()
    {
        leftPlateSurface = FindSurfaceFor(leftPlate);
        rightPlateSurface = FindSurfaceFor(rightPlate);
    }

    private void RefreshWeights()
    {
        // 조건 컴포넌트가 있으면 조건 판정에 쓰는 값과 시각 연출 값이 항상 같아진다.
        if (condition != null)
        {
            leftWeight = condition.LeftWeight;
            rightWeight = condition.RightWeight;
            return;
        }

        leftWeight = GetCurrentWeight(leftWeightSource);
        rightWeight = GetCurrentWeight(rightWeightSource);
    }

    private void ApplyVisualMotion(float deltaTime)
    {
        if (!hasInitialPose)
        {
            CacheInitialPose();
        }

        // 접시를 움직이기 전 위치를 기록해, 위에 올라간 플레이어/상자가 같은 이동량을 받을 수 있게 한다.
        CapturePlateMotionBefore();

        // 오른쪽이 더 무거우면 양수, 왼쪽이 더 무거우면 음수로 본다.
        float weightDelta = rightWeight - leftWeight;
        visualBalance = Mathf.Clamp(weightDelta / maxWeightDifference, -1.0f, 1.0f);

        if (invertDirection)
        {
            visualBalance = -visualBalance;
        }

        float followFactor = GetFollowFactor(deltaTime);
        ApplyPlateMotion(followFactor);
        ApplyBeamTilt(followFactor);

        // 접시를 움직인 뒤 위치를 기록한다.
        // MovingPlatformSurface는 이 전후 차이를 플레이어/상자에게 전달한다.
        CapturePlateMotionAfter();
    }

    private void ApplyPlateMotion(float followFactor)
    {
        // 오른쪽이 무거우면 오른쪽 접시는 내려가고, 왼쪽 접시는 올라간다.
        float leftOffset = visualBalance * maxPlateOffset;
        float rightOffset = -visualBalance * maxPlateOffset;

        if (leftPlate != null)
        {
            Vector3 targetPosition = initialLeftPlateLocalPosition + Vector3.up * leftOffset;
            leftPlate.localPosition = Vector3.Lerp(leftPlate.localPosition, targetPosition, followFactor);
        }

        if (rightPlate != null)
        {
            Vector3 targetPosition = initialRightPlateLocalPosition + Vector3.up * rightOffset;
            rightPlate.localPosition = Vector3.Lerp(rightPlate.localPosition, targetPosition, followFactor);
        }
    }

    private void ApplyBeamTilt(float followFactor)
    {
        if (beam == null)
        {
            return;
        }

        // 씬에서 빔을 어떤 축 기준으로 배치했는지에 따라 기울이는 로컬 축을 선택한다.
        // 예를 들어 빔이 X축 기준으로 기울어져야 자연스럽다면 Beam Tilt Axis를 LocalX로 둔다.
        Quaternion targetRotation = initialBeamLocalRotation * Quaternion.AngleAxis(
            visualBalance * maxBeamTiltAngle,
            GetBeamTiltAxis());

        beam.localRotation = Quaternion.Slerp(beam.localRotation, targetRotation, followFactor);
    }

    private Vector3 GetBeamTiltAxis()
    {
        switch (beamTiltAxis)
        {
            case BeamTiltAxis.LocalX:
                return Vector3.right;
            case BeamTiltAxis.LocalNegativeX:
                return Vector3.left;
            case BeamTiltAxis.LocalY:
                return Vector3.up;
            case BeamTiltAxis.LocalNegativeY:
                return Vector3.down;
            case BeamTiltAxis.LocalZ:
                return Vector3.forward;
            case BeamTiltAxis.LocalNegativeZ:
                return Vector3.back;
            default:
                return Vector3.right;
        }
    }

    private float GetFollowFactor(float deltaTime)
    {
        if (followSpeed <= 0.0f)
        {
            return 1.0f;
        }

        return 1.0f - Mathf.Exp(-followSpeed * deltaTime);
    }

    private void CapturePlateMotionBefore()
    {
        leftPlateSurface?.CaptureBeforeMotion();
        rightPlateSurface?.CaptureBeforeMotion();
    }

    private void CapturePlateMotionAfter()
    {
        leftPlateSurface?.CaptureAfterMotion();
        rightPlateSurface?.CaptureAfterMotion();
    }

    private MovingPlatformSurface FindSurfaceFor(Transform plate)
    {
        if (plate == null)
        {
            return null;
        }

        MovingPlatformSurface surface = plate.GetComponent<MovingPlatformSurface>();
        if (surface != null)
        {
            return surface;
        }

        return plate.GetComponentInParent<MovingPlatformSurface>();
    }

    private float GetCurrentWeight(MonoBehaviour source)
    {
        if (source is not IPuzzleWeightSource weightSource)
        {
            return 0.0f;
        }

        return Mathf.Max(0.0f, weightSource.CurrentWeight);
    }
}
