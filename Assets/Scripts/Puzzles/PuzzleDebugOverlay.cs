using UnityEngine;

public static class PuzzleDebugOverlay
{
    private static bool overlayEnabled;
    private static bool gizmosEnabled;

    public static bool OverlayEnabled => overlayEnabled;
    public static bool GizmosEnabled => gizmosEnabled;

    public static void SetEnabled(bool value)
    {
        SetVisible(value, value);
    }

    public static void SetVisible(bool showOverlay, bool showGizmos)
    {
        overlayEnabled = showOverlay;
        gizmosEnabled = showGizmos;
    }

    public static Camera GetCamera()
    {
        return Camera.main;
    }

    public static void DrawWorldLine(Camera targetCamera, Vector3 worldStart, Vector3 worldEnd, Color color, float thickness)
    {
        if (!TryGetGuiPoint(targetCamera, worldStart, out Vector2 start) ||
            !TryGetGuiPoint(targetCamera, worldEnd, out Vector2 end))
        {
            return;
        }

        DrawScreenLine(start, end, color, thickness);
    }

    public static void DrawDashedWorldLine(
        Camera targetCamera,
        Vector3 worldStart,
        Vector3 worldEnd,
        Color color,
        float thickness,
        float dashLength,
        float gapLength,
        float screenOffset = 0.0f,
        float dashOffset = 0.0f)
    {
        if (!TryGetGuiPoint(targetCamera, worldStart, out Vector2 start) ||
            !TryGetGuiPoint(targetCamera, worldEnd, out Vector2 end))
        {
            return;
        }

        ApplyScreenLineOffset(ref start, ref end, screenOffset);
        DrawDashedScreenLine(start, end, color, thickness, dashLength, gapLength, dashOffset);
    }

    public static void DrawDashedGuiToWorldLine(
        Camera targetCamera,
        Vector2 guiStart,
        Vector3 worldEnd,
        Color color,
        float thickness,
        float dashLength,
        float gapLength,
        float screenOffset = 0.0f,
        float dashOffset = 0.0f)
    {
        if (!TryGetGuiPoint(targetCamera, worldEnd, out Vector2 end))
        {
            return;
        }

        Vector2 start = guiStart;
        ApplyScreenLineOffset(ref start, ref end, screenOffset);
        DrawDashedScreenLine(start, end, color, thickness, dashLength, gapLength, dashOffset);
    }

    public static void DrawDashedGizmoLine(
        Vector3 worldStart,
        Vector3 worldEnd,
        Color color,
        float dashLength,
        float gapLength,
        float dashOffset = 0.0f)
    {
        Vector3 delta = worldEnd - worldStart;
        float length = delta.magnitude;
        if (length <= 0.001f)
        {
            return;
        }

        Vector3 direction = delta / length;
        float step = Mathf.Max(0.001f, dashLength + gapLength);
        float startDistance = -Mathf.Repeat(dashOffset, step);

        Gizmos.color = color;

        for (float distance = startDistance; distance < length; distance += step)
        {
            float dashStart = Mathf.Max(distance, 0.0f);
            float dashEnd = Mathf.Min(distance + dashLength, length);
            if (dashEnd <= dashStart)
            {
                continue;
            }

            Gizmos.DrawLine(
                worldStart + direction * dashStart,
                worldStart + direction * dashEnd);
        }
    }

    public static bool TryGetGuiPoint(Camera targetCamera, Vector3 worldPosition, out Vector2 guiPoint)
    {
        guiPoint = Vector2.zero;

        if (targetCamera == null)
        {
            return false;
        }

        Vector3 screenPoint = targetCamera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z <= 0.0f)
        {
            return false;
        }

        guiPoint = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
        return true;
    }

    public static void DrawLabel(Vector2 guiPoint, string text, float width, float height)
    {
        GUI.Box(new Rect(guiPoint.x - width * 0.5f, guiPoint.y - height, width, height), text);
    }

    private static void DrawScreenLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;

        Vector2 delta = end - start;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float length = delta.magnitude;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private static void ApplyScreenLineOffset(ref Vector2 start, ref Vector2 end, float offset)
    {
        if (Mathf.Abs(offset) <= 0.001f)
        {
            return;
        }

        Vector2 delta = end - start;
        if (delta.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Vector2 perpendicular = new Vector2(-delta.y, delta.x).normalized;
        start += perpendicular * offset;
        end += perpendicular * offset;
    }

    private static void DrawDashedScreenLine(
        Vector2 start,
        Vector2 end,
        Color color,
        float thickness,
        float dashLength,
        float gapLength,
        float dashOffset)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.001f)
        {
            return;
        }

        Vector2 direction = delta / length;
        float step = Mathf.Max(0.001f, dashLength + gapLength);
        float startDistance = -Mathf.Repeat(dashOffset, step);

        for (float distance = startDistance; distance < length; distance += step)
        {
            float dashStart = Mathf.Max(distance, 0.0f);
            float dashEnd = Mathf.Min(distance + dashLength, length);
            if (dashEnd <= dashStart)
            {
                continue;
            }

            DrawScreenLine(
                start + direction * dashStart,
                start + direction * dashEnd,
                color,
                thickness);
        }
    }
}
