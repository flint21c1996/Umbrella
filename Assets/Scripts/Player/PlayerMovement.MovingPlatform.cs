using UnityEngine;

// PlayerMovement의 움직이는 발판 처리 파트.
// 회전/이동하는 플랫폼 위에서 플레이어의 위치와 바라보는 방향을 함께 운반한다.
public partial class PlayerMovement
{
    // 현재 밟고 있는 움직이는 표면. null이면 일반 바닥으로 본다.
    private MovingPlatformSurface currentMovingPlatform;

    // 움직이는 발판에서 점프한 뒤, 착지 전까지 같은 발판 좌표계를 따라가기 위한 참조.
    private MovingPlatformSurface airborneMovingPlatform;

    // 플레이어의 몸 방향은 이동 입력, 조준, 회전 발판 보정처럼 코드에서 직접 정한다.
    // 회전하는 바닥과의 마찰/충돌이 만든 물리 각속도는 남겨두면 멈춘 뒤에도 몸이 빙글빙글 돌 수 있다.
    void ClearPhysicsAngularVelocity()
    {
        if (rb == null || rb.isKinematic)
        {
            return;
        }

        rb.angularVelocity = Vector3.zero;
    }

    // 움직이는 발판 위에 있거나, 그 발판에서 점프한 직후라면 발판의 이동량만큼 플레이어를 옮긴다.
    void ApplyMovingPlatformMotion()
    {
        MovingPlatformSurface activePlatform = GetActiveMovingPlatform();
        if (activePlatform == null)
        {
            return;
        }

        Vector3 platformDelta = activePlatform.GetDeltaPositionAt(rb.position);
        float platformYawDelta = activePlatform.GetDeltaYaw();

        // 위치 변화가 거의 없으면 MovePosition을 생략해서 불필요한 물리 갱신을 줄인다.
        if (platformDelta.sqrMagnitude > 0.000001f)
        {
            rb.MovePosition(rb.position + platformDelta);
        }

        // 발판이 Y축으로 회전하면 플레이어가 바라보는 방향도 같은 각도만큼 더한다.
        // 잡기 중에는 일반 이동 회전은 막지만, 발판 위에 실려 가는 회전은 계속 허용한다.
        if (!Mathf.Approximately(platformYawDelta, 0.0f))
        {
            Quaternion yawDelta = Quaternion.AngleAxis(platformYawDelta, Vector3.up);
            rb.MoveRotation(yawDelta * rb.rotation);
        }
    }

    // 땅에 붙어 있을 때는 현재 밟는 발판을, 공중에서는 점프를 시작한 발판을 기준으로 삼는다.
    MovingPlatformSurface GetActiveMovingPlatform()
    {
        if (isGrounded)
        {
            return currentMovingPlatform;
        }

        return airborneMovingPlatform;
    }

    // 움직이는 발판에서 점프할 때 호출한다.
    // 달리는 지하철 안에서 뛰어도 지하철과 함께 이동하는 것처럼, 착지 전까지 같은 발판 기준 이동을 유지한다.
    void BeginMovingPlatformAirCarry()
    {
        if (!isGrounded || currentMovingPlatform == null)
        {
            return;
        }

        airborneMovingPlatform = currentMovingPlatform;
    }

    // 새 바닥에 착지하면 이전 발판 기준의 공중 보정을 끝낸다.
    void ClearMovingPlatformAirCarry()
    {
        airborneMovingPlatform = null;
    }
}
