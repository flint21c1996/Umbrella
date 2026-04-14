using UnityEngine;

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

    private void Reset()
    {
        sensor = GetComponentInChildren<WeightSensor>();
        buttonVisual = transform;
    }

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

    private void OnValidate()
    {
        pressWeight = Mathf.Max(0.0f, pressWeight);
        releaseWeight = Mathf.Clamp(releaseWeight, 0.0f, pressWeight);
        moveSpeed = Mathf.Max(0.0f, moveSpeed);
    }

    private void FixedUpdate()
    {
        RefreshPressedState();
        MoveButtonVisual();
    }

    private void RefreshPressedState()
    {
        currentWeight = sensor != null ? sensor.CurrentWeight : 0.0f;

        if (!isPressed && currentWeight >= pressWeight)
        {
            SetPressed(true);
            return;
        }

        if (isPressed && currentWeight <= releaseWeight)
        {
            SetPressed(false);
        }
    }

    private void SetPressed(bool pressed)
    {
        if (isPressed == pressed)
        {
            return;
        }

        isPressed = pressed;
        NotifyChanged();
    }

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
        if (!PuzzleDebugOverlay.GizmosEnabled)
        {
            return;
        }

        DrawSceneAnchor(GetDebugAnchorPosition(), isPressed ? Color.green : Color.white, 0.22f);
    }

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

    private void DrawSceneAnchor(Vector3 position, Color color, float radius)
    {
        color.a = 0.95f;
        Gizmos.color = color;
        Gizmos.DrawSphere(position, radius * 0.35f);
        Gizmos.DrawWireSphere(position, radius);
    }

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
