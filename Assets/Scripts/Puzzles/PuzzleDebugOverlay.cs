using UnityEngine;

// 퍼즐 디버그 표시를 위한 공통 유틸리티.
// 각 퍼즐 컴포넌트가 직접 선/라벨 그리기 코드를 중복해서 갖지 않도록 모아둔다.
public static class PuzzleDebugOverlay
{
    private static bool overlayEnabled;
    private static bool gizmosEnabled;

    public static bool OverlayEnabled => overlayEnabled;
    public static bool GizmosEnabled => gizmosEnabled;

    // Overlay와 Gizmo를 같은 값으로 한 번에 켜고 끈다.
    public static void SetEnabled(bool value)
    {
        SetVisible(value, value);
    }

    // Game View 오버레이와 Scene View 기즈모를 따로 제어할 때 사용한다.
    public static void SetVisible(bool showOverlay, bool showGizmos)
    {
        overlayEnabled = showOverlay;
        gizmosEnabled = showGizmos;
    }

    // 현재 디버그 오버레이에서 사용할 카메라.
    // 지금은 플레이 카메라 하나를 기준으로 하므로 Camera.main을 사용한다.
    public static Camera GetCamera()
    {
        return Camera.main;
    }

    // 두 월드 좌표를 Game View 위의 실선으로 연결한다.
    public static void DrawWorldLine(Camera targetCamera, Vector3 worldStart, Vector3 worldEnd, Color color, float thickness)
    {
        if (!TryGetGuiPoint(targetCamera, worldStart, out Vector2 start) ||
            !TryGetGuiPoint(targetCamera, worldEnd, out Vector2 end))
        {
            return;
        }

        DrawScreenLine(start, end, color, thickness);
    }

    // 두 월드 좌표를 Game View 위의 점선으로 연결한다.
    // 현재는 성능 문제 때문에 주 사용 경로에서는 실선을 쓰지만, 비교/테스트용으로 남겨둔다.
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

    // GUI 좌표에서 월드 좌표로 이어지는 점선을 그린다.
    // 라벨 가장자리에서 오브젝트로 선을 연결할 때 사용한다.
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

    // GUI 좌표에서 월드 좌표로 이어지는 실선을 그린다.
    // ConditionGroup의 이벤트 연결 표시처럼 라벨에서 대상 오브젝트로 이어야 할 때 사용한다.
    public static void DrawGuiToWorldLine(
        Camera targetCamera,
        Vector2 guiStart,
        Vector3 worldEnd,
        Color color,
        float thickness,
        float screenOffset = 0.0f)
    {
        if (!TryGetGuiPoint(targetCamera, worldEnd, out Vector2 end))
        {
            return;
        }

        Vector2 start = guiStart;
        ApplyScreenLineOffset(ref start, ref end, screenOffset);
        DrawScreenLine(start, end, color, thickness);
    }

    // Scene View에서 점선 기즈모를 그린다.
    // Game View의 GUI 점선과 달리 Gizmos.DrawLine을 여러 번 호출한다.
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

    // 월드 좌표를 IMGUI에서 쓰는 GUI 좌표로 변환한다.
    // 카메라 뒤에 있는 점은 그릴 수 없으므로 false를 반환한다.
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

    // 지정한 GUI 좌표를 기준으로 박스 라벨을 그린다.
    // guiPoint는 라벨의 아래쪽 중앙처럼 쓰이도록 x는 중앙 정렬, y는 위로 올린다.
    public static void DrawLabel(Vector2 guiPoint, string text, float width, float height)
    {
        GUI.Box(new Rect(guiPoint.x - width * 0.5f, guiPoint.y - height, width, height), text);
    }

    // IMGUI에는 선 그리기 API가 없으므로 흰 텍스처를 회전시켜 선처럼 보이게 한다.
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

    // 서로 겹치는 연결선을 조금 옆으로 밀어 글자나 다른 선과 덜 겹치게 한다.
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

    // 실선을 여러 짧은 구간으로 나누어 점선처럼 그린다.
    // dashOffset을 바꾸면 점선이 흐르는 것처럼 보이지만, 선이 많을수록 비용이 커진다.
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
