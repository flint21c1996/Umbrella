using UnityEngine;

[AddComponentMenu("")]
public sealed class LightZonePrototypeArea : MonoBehaviour
{
    [SerializeField] private LightZonePrototypeMode mode;
    [SerializeField] private float radius = 2.2f;
    [SerializeField] private float triggerHeight = 2f;
    [SerializeField] private float heatPerSecond = 1f;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.78f, 0.22f, 0.35f);

    public LightZonePrototypeMode Mode => mode;
    public float Radius => radius;
    public float HeatPerSecond => heatPerSecond;

    public void Configure(LightZonePrototypeMode newMode, float newRadius, float newTriggerHeight, float newHeatPerSecond, Color newColor)
    {
        mode = newMode;
        radius = Mathf.Max(0.1f, newRadius);
        triggerHeight = Mathf.Max(0.1f, newTriggerHeight);
        heatPerSecond = newHeatPerSecond;
        gizmoColor = new Color(newColor.r, newColor.g, newColor.b, 0.35f);
    }

    public bool TryEvaluate(Vector3 worldPoint, out float normalizedLight, out float heatRate)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        if (localPoint.y < 0f || localPoint.y > triggerHeight)
        {
            normalizedLight = 0f;
            heatRate = 0f;
            return false;
        }

        float distance = new Vector2(localPoint.x, localPoint.z).magnitude;
        if (distance > radius)
        {
            normalizedLight = 0f;
            heatRate = 0f;
            return false;
        }

        normalizedLight = 1f - Mathf.SmoothStep(radius * 0.65f, radius, distance);
        heatRate = heatPerSecond * normalizedLight;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Color previousColor = Gizmos.color;
        Gizmos.color = gizmoColor;
        DrawCircle(0f);
        DrawCircle(triggerHeight);
        DrawVerticalEdges();
        Gizmos.color = previousColor;
    }

    private void DrawCircle(float y)
    {
        const int segments = 48;
        Vector3 previous = transform.TransformPoint(new Vector3(radius, y, 0f));
        for (int i = 1; i <= segments; i++)
        {
            float radians = (float)i / segments * Mathf.PI * 2f;
            Vector3 next = transform.TransformPoint(new Vector3(Mathf.Cos(radians) * radius, y, Mathf.Sin(radians) * radius));
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    private void DrawVerticalEdges()
    {
        for (int i = 0; i < 4; i++)
        {
            float radians = i / 4f * Mathf.PI * 2f;
            Vector3 bottom = transform.TransformPoint(new Vector3(Mathf.Cos(radians) * radius, 0f, Mathf.Sin(radians) * radius));
            Vector3 top = transform.TransformPoint(new Vector3(Mathf.Cos(radians) * radius, triggerHeight, Mathf.Sin(radians) * radius));
            Gizmos.DrawLine(bottom, top);
        }
    }
}
