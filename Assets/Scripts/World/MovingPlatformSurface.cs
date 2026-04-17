using UnityEngine;

// 플레이어를 실어 나를 수 있는 움직이는 표면임을 표시하는 컴포넌트.
// 회전/이동 전후 Transform 변화를 저장하고, 그 변화량을 플레이어가 따라갈 수 있게 제공한다.
public class MovingPlatformSurface : MonoBehaviour
{
    [Tooltip("Transform whose movement should carry the player. Empty uses this transform.")]
    [SerializeField] private Transform motionRoot;

    private Vector3 previousPosition;
    private Quaternion previousRotation = Quaternion.identity;
    private Vector3 currentPosition;
    private Quaternion currentRotation = Quaternion.identity;
    private float lastMotionTime = -1.0f;
    private bool initialized;

    private Transform MotionRoot => motionRoot != null ? motionRoot : transform;

    private void Awake()
    {
        ResetSnapshot();
    }

    private void OnEnable()
    {
        ResetSnapshot();
    }

    private void OnValidate()
    {
        // 같은 오브젝트에 붙여 쓰는 경우가 대부분이라 비워두면 자기 Transform을 기준으로 삼는다.
        if (motionRoot == null)
        {
            motionRoot = transform;
        }
    }

    public void CaptureBeforeMotion()
    {
        // 플랫폼을 움직이기 직전의 위치/회전을 저장한다.
        // AutoRotator처럼 실제 이동을 만드는 컴포넌트가 호출해준다.
        Transform root = MotionRoot;
        previousPosition = root.position;
        previousRotation = root.rotation;
        initialized = true;
    }

    public void CaptureAfterMotion()
    {
        // 플랫폼을 움직인 직후의 위치/회전을 저장한다.
        // 이전 값과 현재 값을 비교해서 이번 FixedUpdate의 이동량을 계산한다.
        Transform root = MotionRoot;
        currentPosition = root.position;
        currentRotation = root.rotation;
        lastMotionTime = Time.inFixedTimeStep ? Time.fixedTime : Time.time;

        if (!initialized)
        {
            // CaptureBeforeMotion 없이 먼저 호출된 경우에도 큰 순간 이동이 생기지 않게 현재값으로 맞춘다.
            previousPosition = currentPosition;
            previousRotation = currentRotation;
            initialized = true;
        }
    }

    public Vector3 GetDeltaPositionAt(Vector3 worldPosition)
    {
        if (!initialized || !HasMotionThisStep())
        {
            // 이번 물리 스텝에 기록된 움직임이 없으면 이전 프레임의 값을 재사용하지 않는다.
            return Vector3.zero;
        }

        // 이전 회전에서 현재 회전까지의 차이를 구한다.
        Quaternion rotationDelta = GetDeltaRotation();

        // 플레이어 위치를 플랫폼 기준점 주변의 한 점으로 보고, 플랫폼 회전/이동 후의 위치를 계산한다.
        Vector3 previousOffset = worldPosition - previousPosition;
        Vector3 nextPosition = currentPosition + rotationDelta * previousOffset;

        return nextPosition - worldPosition;
    }

    public Quaternion GetDeltaRotation()
    {
        if (!initialized || !HasMotionThisStep())
        {
            // 움직임이 기록되지 않은 프레임에서는 회전 변화가 없는 것으로 본다.
            return Quaternion.identity;
        }

        return currentRotation * Quaternion.Inverse(previousRotation);
    }

    public float GetDeltaYaw()
    {
        // 플레이어는 서 있는 캐릭터라 X/Z 기울기는 따라가지 않고, Y축 회전만 몸 방향에 반영한다.
        Quaternion rotationDelta = GetDeltaRotation();
        Vector3 previousForward = Vector3.forward;
        Vector3 currentForward = rotationDelta * previousForward;

        previousForward.y = 0.0f;
        currentForward.y = 0.0f;

        if (previousForward.sqrMagnitude <= 0.0001f || currentForward.sqrMagnitude <= 0.0001f)
        {
            return 0.0f;
        }

        return Vector3.SignedAngle(previousForward.normalized, currentForward.normalized, Vector3.up);
    }

    private bool HasMotionThisStep()
    {
        // 오래된 이동량이 다음 프레임까지 남아 있으면 플레이어가 한 번 더 끌려갈 수 있어서
        // 현재 Update/FixedUpdate에서 기록된 값인지 확인한다.
        float currentTime = Time.inFixedTimeStep ? Time.fixedTime : Time.time;
        return Mathf.Approximately(lastMotionTime, currentTime);
    }

    private void ResetSnapshot()
    {
        // 컴포넌트가 켜지는 순간의 위치를 기준점으로 삼아 첫 프레임 튐을 막는다.
        Transform root = MotionRoot;
        previousPosition = root.position;
        previousRotation = root.rotation;
        currentPosition = previousPosition;
        currentRotation = previousRotation;
        lastMotionTime = -1.0f;
        initialized = true;
    }
}
