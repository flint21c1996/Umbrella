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

    // 컴포넌트를 처음 붙였을 때 기본 Rigidbody 세팅을 잡아준다.
    // 손으로 미는 물체는 넘어지면 조작감이 망가지기 쉬워서 X/Z 회전을 기본으로 잠근다.
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

    // 런타임 시작 시 Rigidbody와 원래 Constraints를 저장한다.
    // 잡기 시작/끝마다 원래 제약으로 되돌리기 위해 baseConstraints를 기억해 둔다.
    private void Awake()
    {
        CacheRigidbody();
        CacheBaseConstraints();
        ApplyGrabStateConstraints(false);
    }

    // Inspector에서 값이 바뀔 때도 Rigidbody 참조와 속도 제한을 안전하게 보정한다.
    private void OnValidate()
    {
        CacheRigidbody();
        maxHorizontalSpeed = Mathf.Max(0.0f, maxHorizontalSpeed);
    }

    // 현재 이 물체를 잡을 수 있는지 확인한다.
    // 이미 다른 Interactor가 잡고 있으면 동시에 두 곳에서 제어하지 못하게 막는다.
    public bool CanGrab(PlayerPushPullInteractor interactor)
    {
        if (!isActiveAndEnabled || targetRigidbody == null)
        {
            return false;
        }

        return currentGrabber == null || currentGrabber == interactor;
    }

    // 플레이어가 잡기 시작할 때 호출된다.
    // 성공하면 수평 위치 잠금을 풀어 Interactor가 Rigidbody 속도를 넣을 수 있게 한다.
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

    // 잡기를 끝낼 때 호출된다.
    // 자신을 잡고 있던 Interactor가 맞을 때만 해제해서 잘못된 해제를 막는다.
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

    // 잡힌 상태에서만 수평 속도를 적용한다.
    // 실제 이동은 Rigidbody가 처리하므로 벽, 바닥, 버튼과의 물리 충돌을 그대로 이용할 수 있다.
    public Vector3 SetHorizontalVelocity(Vector3 horizontalVelocity)
    {
        if (!isGrabbed || targetRigidbody == null)
        {
            return Vector3.zero;
        }

        // 이 시스템은 수평으로 밀고 당기는 용도이므로 Y 속도는 건드리지 않는다.
        horizontalVelocity.y = 0.0f;
        horizontalVelocity = ClampHorizontalVelocity(horizontalVelocity);
        targetRigidbody.linearVelocity = new Vector3(horizontalVelocity.x, targetRigidbody.linearVelocity.y, horizontalVelocity.z);
        return horizontalVelocity;
    }

    // Rigidbody를 직접 넣지 않아도 같은 오브젝트에서 자동으로 찾는다.
    private void CacheRigidbody()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }
    }

    // 원래 제약을 저장해 둔다.
    // 예를 들어 디자이너가 Y 위치나 회전을 잠가 둔 설정은 잡기 후에도 유지되어야 한다.
    private void CacheBaseConstraints()
    {
        if (targetRigidbody == null)
        {
            baseConstraints = RigidbodyConstraints.None;
            return;
        }

        baseConstraints = targetRigidbody.constraints;
    }

    // 잡지 않은 상태에서는 X/Z 위치를 잠가서 플레이어가 몸으로 밀어버리는 상황을 막는다.
    // 잡은 상태에서만 X/Z 잠금을 풀어 의도한 손 조작으로 움직이게 한다.
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

    // 놓는 순간 수평 속도와 회전을 멈춰 상자가 미끄러지거나 빙글도는 잔여 움직임을 줄인다.
    private void StopHorizontalMotion()
    {
        if (targetRigidbody == null)
        {
            return;
        }

        targetRigidbody.linearVelocity = new Vector3(0.0f, targetRigidbody.linearVelocity.y, 0.0f);
        targetRigidbody.angularVelocity = Vector3.zero;
    }

    // 조작 속도가 너무 커져 버튼/벽 충돌이 불안정해지는 것을 막는다.
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
