using UnityEngine;

[DisallowMultipleComponent]
public class PuzzleMover : MonoBehaviour
{
    [Tooltip("실제로 움직일 오브젝트. 비워두면 이 스크립트가 붙은 오브젝트가 움직인다.")]
    [SerializeField] private Transform movingTarget;

    [Tooltip("비활성 상태의 위치. 예: 닫힌 문, 올라온 플랫폼.")]
    [SerializeField] private Transform inactivePoint;

    [Tooltip("활성 상태의 위치. 예: 열린 문, 내려간 플랫폼.")]
    [SerializeField] private Transform activePoint;

    [Tooltip("목표 위치까지 움직이는 속도.")]
    [SerializeField] private float moveSpeed = 2.0f;

    [Tooltip("F3 퍼즐 디버그 선이 연결될 기준점. 비워두면 Moving Target의 위쪽 중앙을 사용한다.")]
    [SerializeField] private Transform debugAnchor;

    [SerializeField] private bool activated;
    [SerializeField] private bool paused;

    public bool Activated => activated;
    public bool Paused => paused;
    public Vector3 DebugAnchorPosition => GetDebugAnchorPosition();

    private void Reset()
    {
        movingTarget = transform;
    }

    private void Awake()
    {
        if (movingTarget == null)
        {
            movingTarget = transform;
        }
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.0f, moveSpeed);
    }

    private void FixedUpdate()
    {
        if (paused)
        {
            return;
        }

        MoveTarget();
    }

    // UnityEvent에서 바로 연결하기 쉽도록 매개변수 없는 함수도 제공한다.
    public void Activate()
    {
        SetActivated(true);
    }

    public void Deactivate()
    {
        SetActivated(false);
    }

    // 현재 위치에서 멈춘다. 다시 Activate/Deactivate를 받으면 해당 방향으로 이어서 움직인다.
    public void Pause()
    {
        paused = true;
    }

    public void Resume()
    {
        paused = false;
    }

    public void Toggle()
    {
        SetActivated(!activated);
    }

    public void SetActivated(bool value)
    {
        activated = value;
        paused = false;
    }

    private void MoveTarget()
    {
        if (movingTarget == null)
        {
            return;
        }

        Transform targetPoint = activated ? activePoint : inactivePoint;
        if (targetPoint == null)
        {
            return;
        }

        movingTarget.position = Vector3.MoveTowards(
            movingTarget.position,
            targetPoint.position,
            moveSpeed * Time.fixedDeltaTime);
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !PuzzleDebugOverlay.OverlayEnabled)
        {
            return;
        }

        Camera targetCamera = PuzzleDebugOverlay.GetCamera();
        if (!PuzzleDebugOverlay.TryGetGuiPoint(targetCamera, GetDebugAnchorPosition(), out Vector2 labelPoint))
        {
            return;
        }

        string stateText = paused ? "Paused" : activated ? "Active" : "Inactive";
        PuzzleDebugOverlay.DrawLabel(labelPoint, $"PuzzleMover\n{stateText}", 140.0f, 42.0f);
    }

    private void OnDrawGizmos()
    {
        if (!PuzzleDebugOverlay.GizmosEnabled)
        {
            return;
        }

        Gizmos.color = paused ? Color.yellow : activated ? Color.green : Color.gray;
        Vector3 anchorPosition = GetDebugAnchorPosition();
        Gizmos.DrawSphere(anchorPosition, 0.07f);
        Gizmos.DrawWireSphere(anchorPosition, 0.28f);
    }

    private Vector3 GetDebugAnchorPosition()
    {
        if (debugAnchor != null)
        {
            return debugAnchor.position;
        }

        Transform anchor = movingTarget != null ? movingTarget : transform;
        Renderer targetRenderer = anchor.GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
        {
            Bounds bounds = targetRenderer.bounds;
            return bounds.center + Vector3.up * (bounds.extents.y + 0.25f);
        }

        Collider targetCollider = anchor.GetComponentInChildren<Collider>();
        if (targetCollider != null)
        {
            Bounds bounds = targetCollider.bounds;
            return bounds.center + Vector3.up * (bounds.extents.y + 0.25f);
        }

        return anchor.position + Vector3.up * 0.5f;
    }
}
