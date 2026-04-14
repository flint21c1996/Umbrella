using UnityEngine;

// 플레이어가 손으로 밀고 당길 수 있는 퍼즐 오브젝트.
// Rigidbody.mass를 그대로 사용하므로 물을 담아 무거워진 오브젝트는 더 둔하게 움직이고,
// WeightSensor 위에 올리면 현재 질량이 버튼 무게로 계산된다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PushPullObject : MonoBehaviour
{
    [Header("Movement")]
    // 너무 빨리 미끄러지지 않게 수평 속도를 제한한다.
    public float maxHorizontalSpeed = 2.5f;

    [Header("Runtime")]
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private bool isGrabbed;

    private PlayerPushPullInteractor currentGrabber;
    private RigidbodyConstraints baseConstraints;

    public Vector3 RigidbodyPosition => targetRigidbody != null ? targetRigidbody.position : transform.position;

    private void Reset()
    {
        CacheRigidbody();

        if (targetRigidbody != null)
        {
            // 상자형 퍼즐 오브젝트는 보통 넘어지면 다루기 어려우므로 기본 회전을 잠근다.
            targetRigidbody.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void Awake()
    {
        CacheRigidbody();
        CacheBaseConstraints();
        ApplyGrabStateConstraints(false);
    }

    private void OnValidate()
    {
        CacheRigidbody();
        maxHorizontalSpeed = Mathf.Max(0.0f, maxHorizontalSpeed);
    }

    public bool CanGrab(PlayerPushPullInteractor interactor)
    {
        if (!isActiveAndEnabled || targetRigidbody == null)
        {
            return false;
        }

        return currentGrabber == null || currentGrabber == interactor;
    }

    public bool TryBeginGrab(PlayerPushPullInteractor interactor)
    {
        if (!CanGrab(interactor))
        {
            return false;
        }

        currentGrabber = interactor;
        isGrabbed = true;
        ApplyGrabStateConstraints(true);
        return true;
    }

    public void EndGrab(PlayerPushPullInteractor interactor)
    {
        if (currentGrabber != interactor)
        {
            return;
        }

        currentGrabber = null;
        isGrabbed = false;
        StopHorizontalMotion();
        ApplyGrabStateConstraints(false);
    }

    public Vector3 SetHorizontalVelocity(Vector3 horizontalVelocity)
    {
        if (!isGrabbed || targetRigidbody == null)
        {
            return Vector3.zero;
        }

        horizontalVelocity.y = 0.0f;
        horizontalVelocity = ClampHorizontalVelocity(horizontalVelocity);
        targetRigidbody.linearVelocity = new Vector3(horizontalVelocity.x, targetRigidbody.linearVelocity.y, horizontalVelocity.z);
        return horizontalVelocity;
    }

    private void CacheRigidbody()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }
    }

    private void CacheBaseConstraints()
    {
        if (targetRigidbody == null)
        {
            baseConstraints = RigidbodyConstraints.None;
            return;
        }

        baseConstraints = targetRigidbody.constraints;
    }

    private void ApplyGrabStateConstraints(bool canMoveHorizontally)
    {
        if (targetRigidbody == null)
        {
            return;
        }

        RigidbodyConstraints constraints = baseConstraints;
        if (canMoveHorizontally)
        {
            constraints &= ~RigidbodyConstraints.FreezePositionX;
            constraints &= ~RigidbodyConstraints.FreezePositionZ;
        }
        else
        {
            constraints |= RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
        }

        targetRigidbody.constraints = constraints;
    }

    private void StopHorizontalMotion()
    {
        if (targetRigidbody == null)
        {
            return;
        }

        targetRigidbody.linearVelocity = new Vector3(0.0f, targetRigidbody.linearVelocity.y, 0.0f);
        targetRigidbody.angularVelocity = Vector3.zero;
    }

    private Vector3 ClampHorizontalVelocity(Vector3 horizontalVelocity)
    {
        if (maxHorizontalSpeed <= 0.0f)
        {
            return horizontalVelocity;
        }

        if (horizontalVelocity.magnitude <= maxHorizontalSpeed)
        {
            return horizontalVelocity;
        }

        return horizontalVelocity.normalized * maxHorizontalSpeed;
    }
}
