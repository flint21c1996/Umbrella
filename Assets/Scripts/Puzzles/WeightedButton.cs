using UnityEngine;

// WeightSensor가 읽은 무게를 바탕으로 눌림 상태를 만드는 퍼즐 조건 소스.
// 문이나 플랫폼을 직접 호출하지 않고, PuzzleConditionGroup이 IsSatisfied를 읽도록 설계했다.
[DisallowMultipleComponent]
public class WeightedButton : PuzzleConditionSource
{
    private const float ButtonLabelWidth = 170.0f;
    private const float ButtonLabelHeight = 58.0f;

    [Header("Weight")]
    [Tooltip("Sensor that measures the weight currently on this button.")]
    [SerializeField] private WeightSensor sensor;

    [Tooltip("The button becomes pressed when the current weight is equal to or higher than this value.")]
    [SerializeField] private float pressWeight = 5.0f;

    [Tooltip("The button releases when the current weight falls to or below this value. Keep this lower than Press Weight to prevent flickering.")]
    [SerializeField] private float releaseWeight = 4.5f;

    [Header("Motion")]
    [Tooltip("The visible part of the button that moves up and down.")]
    [SerializeField] private Transform buttonVisual;

    [Tooltip("Position used when the button is not pressed.")]
    [SerializeField] private Transform releasedPoint;

    [Tooltip("Position used when the button is pressed.")]
    [SerializeField] private Transform pressedPoint;

    [Tooltip("How fast the button visual moves toward the target point.")]
    [SerializeField] private float moveSpeed = 1.5f;

    [Header("Debug")]
    [Tooltip("Optional F3 debug label anchor. If empty, Button Visual is used.")]
    [SerializeField] private Transform debugAnchor;

    [SerializeField] private bool isPressed;
    [SerializeField] private float currentWeight;

    public bool IsPressed => isPressed;
    public override bool IsSatisfied => IsPressed;
    public float CurrentWeight => currentWeight;
    public Vector3 DebugAnchorPosition => GetDebugAnchorPosition();

    // 컴포넌트를 처음 붙였을 때 자주 쓰는 참조를 자동으로 채워준다.
    private void Reset()
    {
        sensor = GetComponentInChildren<WeightSensor>();
        buttonVisual = transform;
    }

    // 런타임에서 참조가 비어 있어도 기본 구조에서는 동작하도록 한 번 더 보정한다.
    private void Awake()
    {
        if (sensor == null)
        {
            sensor = GetComponentInChildren<WeightSensor>();
        }

        if (buttonVisual == null)
        {
            buttonVisual = transform;
        }
    }

    // 눌림/해제 기준이 뒤집히지 않도록 Inspector 값을 보정한다.
    // releaseWeight는 pressWeight보다 낮게 두어 무게가 경계에서 흔들릴 때 덜덜거림을 줄인다.
    private void OnValidate()
    {
        pressWeight = Mathf.Max(0.0f, pressWeight);
        releaseWeight = Mathf.Clamp(releaseWeight, 0.0f, pressWeight);
        moveSpeed = Mathf.Max(0.0f, moveSpeed);
    }

    // 물리 무게는 FixedUpdate 주기에 맞춰 읽고, 버튼 시각 이동도 같은 주기로 갱신한다.
    private void FixedUpdate()
    {
        RefreshPressedState();
        MoveButtonVisual();
    }

    // 센서의 현재 무게를 읽어 눌림/해제 상태 전환이 필요한지 판단한다.
    private void RefreshPressedState()
    {
        currentWeight = sensor != null ? sensor.CurrentWeight : 0.0f;

        // 아직 눌리지 않았을 때 pressWeight 이상이면 눌림 상태로 전환한다.
        if (!isPressed && currentWeight >= pressWeight)
        {
            SetPressed(true);
            return;
        }

        // 이미 눌린 상태에서는 releaseWeight 이하로 내려가야 해제한다.
        // pressWeight와 releaseWeight 사이에서는 현재 상태를 유지해 흔들림을 막는다.
        if (isPressed && currentWeight <= releaseWeight)
        {
            SetPressed(false);
        }
    }

    // 눌림 상태가 실제로 바뀐 경우에만 Changed 이벤트를 보낸다.
    // ConditionGroup이 같은 상태를 반복 처리하지 않게 하기 위함이다.
    private void SetPressed(bool pressed)
    {
        if (isPressed == pressed)
        {
            return;
        }

        isPressed = pressed;
        NotifyChanged();
    }

    // 버튼의 보이는 부분을 눌림/해제 위치로 부드럽게 이동한다.
    // 조건 판정과 시각 표현을 분리해, 상태는 즉시 바뀌고 모양만 따라오게 한다.
    private void MoveButtonVisual()
    {
        if (buttonVisual == null)
        {
            return;
        }

        Transform targetPoint = isPressed ? pressedPoint : releasedPoint;
        if (targetPoint == null)
        {
            return;
        }

        buttonVisual.position = Vector3.MoveTowards(
            buttonVisual.position,
            targetPoint.position,
            moveSpeed * Time.fixedDeltaTime);
    }

    private void OnGUI()
    {
        // F3 디버그 오버레이가 켜진 Play 모드에서만 상태 라벨을 그린다.
        if (!Application.isPlaying || !PuzzleDebugOverlay.OverlayEnabled)
        {
            return;
        }

        Camera targetCamera = PuzzleDebugOverlay.GetCamera();
        if (targetCamera == null)
        {
            return;
        }

        DrawButtonLabel(targetCamera, GetDebugAnchorPosition());
    }

    private void OnDrawGizmos()
    {
        // Scene View에서는 버튼 앵커 위치만 표시한다.
        // 조건/결과 연결선은 PuzzleConditionGroup 쪽에서 그린다.
        if (!PuzzleDebugOverlay.GizmosEnabled)
        {
            return;
        }

        DrawSceneAnchor(GetDebugAnchorPosition(), isPressed ? Color.green : Color.white, 0.22f);
    }

    // Game View에 버튼 이름, 눌림 상태, 현재 무게를 표시한다.
    private void DrawButtonLabel(Camera targetCamera, Vector3 sourcePosition)
    {
        if (!PuzzleDebugOverlay.TryGetGuiPoint(targetCamera, sourcePosition, out Vector2 labelPoint))
        {
            return;
        }

        string stateText = isPressed ? "Pressed" : "Released";
        string label = $"{name}\n{stateText}  {currentWeight:F1}/{pressWeight:F1}";
        PuzzleDebugOverlay.DrawLabel(labelPoint, label, ButtonLabelWidth, ButtonLabelHeight);
    }

    // Scene View에서 버튼 기준점을 눈으로 확인하기 위한 작은 마커.
    private void DrawSceneAnchor(Vector3 position, Color color, float radius)
    {
        color.a = 0.95f;
        Gizmos.color = color;
        Gizmos.DrawSphere(position, radius * 0.35f);
        Gizmos.DrawWireSphere(position, radius);
    }

    // 디버그 라벨과 연결선이 시작될 위치를 계산한다.
    // 별도 앵커가 없으면 버튼 시각 오브젝트의 위쪽을 사용해 글자가 발판을 덜 가리게 한다.
    private Vector3 GetDebugAnchorPosition()
    {
        if (debugAnchor != null)
        {
            return debugAnchor.position;
        }

        Transform anchor = buttonVisual != null ? buttonVisual : transform;
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
