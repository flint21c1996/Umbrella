using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private void OnSceneGUI(SceneView sceneView)
    {
        // SceneView 안에서 preview, placement, delete를 모두 처리하는 메인 루프.
        // Update가 아니라 에디터 이벤트 기반이라 MouseMove / MouseDown 타이밍을 직접 챙긴다.
        if (!placementEnabled)
        {
            UpdatePreviewVisibility(false);
            return;
        }

        Event currentEvent = Event.current;

        if (currentEvent.type == EventType.MouseMove)
        {
            lastMousePosition = currentEvent.mousePosition;
            sceneView.Repaint();
        }

        DrawPlacementPreview(currentEvent);
        DrawPlacementScaleHandle(sceneView);
        DrawPlacementHeightHandle(sceneView);
        if (showMeshColliderBounds)
        {
            DrawMeshColliderBounds();
        }
        HandleShortcuts(currentEvent);

        // Size/Height 편집 중에는 Scene View handle이 좌클릭 드래그를 써야 한다.
        // 배치 클릭과 겹치면 손잡이를 잡다가 prefab이 찍힐 수 있어서 잠깐 배치를 막는다.
        bool isPlaceInput = currentEvent.button == 0 && !currentEvent.alt && !editPlacementScaleInScene && !editPlacementHeightInScene;

        if (currentEvent.type == EventType.MouseDown &&
            isPlaceInput)
        {
            if (currentEvent.control)
            {
                TryDeleteUnderCursor(currentEvent.mousePosition);
            }
            else
            {
                TryPlacePrefab(currentEvent.mousePosition);

                if (currentEvent.shift)
                {
                    isDragPlacing = true;
                    lastDragPlacementPosition = Vector3.positiveInfinity;
                }
            }

            currentEvent.Use();
        }

        if (currentEvent.type == EventType.MouseDrag &&
            isPlaceInput &&
            currentEvent.shift &&
            !currentEvent.control &&
            isDragPlacing)
        {
            TryPlacePrefab(currentEvent.mousePosition, true);
            currentEvent.Use();
        }

        if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
        {
            isDragPlacing = false;
        }
    }

    private void HandleShortcuts(Event currentEvent)
    {
        if (currentEvent.type != EventType.KeyDown)
        {
            return;
        }

        if (currentEvent.keyCode == KeyCode.R)
        {
            RotateRight();
            currentEvent.Use();
        }
        else if (currentEvent.keyCode == KeyCode.Q)
        {
            RotateFineLeft();
            currentEvent.Use();
        }
        else if (currentEvent.keyCode == KeyCode.E)
        {
            RotateFineRight();
            currentEvent.Use();
        }
        else if (currentEvent.keyCode == KeyCode.Alpha1 || currentEvent.keyCode == KeyCode.Keypad1)
        {
            ChangeHeightOffset(-heightStep);
            currentEvent.Use();
        }
        else if (currentEvent.keyCode == KeyCode.Alpha3 || currentEvent.keyCode == KeyCode.Keypad3)
        {
            ChangeHeightOffset(heightStep);
            currentEvent.Use();
        }
        else if (currentEvent.keyCode == KeyCode.Delete || currentEvent.keyCode == KeyCode.Backspace)
        {
            DeleteSelectedObjects();
            currentEvent.Use();
        }
    }

    private void RotateLeft()
    {
        currentRotationDegrees = Mathf.Repeat(currentRotationDegrees - 90.0f, 360.0f);
        Repaint();
    }

    private void RotateRight()
    {
        currentRotationDegrees = Mathf.Repeat(currentRotationDegrees + 90.0f, 360.0f);
        Repaint();
    }

    private void RotateFineLeft()
    {
        currentRotationDegrees = Mathf.Repeat(currentRotationDegrees - 1.0f, 360.0f);
        Repaint();
    }

    private void RotateFineRight()
    {
        currentRotationDegrees = Mathf.Repeat(currentRotationDegrees + 1.0f, 360.0f);
        Repaint();
    }

    private void DrawPlacementPreview(Event currentEvent)
    {
        if (selectedPrefab == null)
        {
            ClearHoveredFaceAnchor();
            UpdatePreviewVisibility(false);
            hasLastPreviewTransform = false;
            return;
        }

        if (editPlacementScaleInScene && hasScaleEditPose)
        {
            DrawLockedScaleEditPreview();
            return;
        }

        if (editPlacementHeightInScene && hasHeightEditPose)
        {
            DrawLockedHeightEditPreview();
            return;
        }

        Vector2 previewMousePosition = currentEvent.mousePosition;

        if (currentEvent.type == EventType.Repaint || currentEvent.type == EventType.Layout)
        {
            previewMousePosition = lastMousePosition;
        }
        else
        {
            lastMousePosition = currentEvent.mousePosition;
        }

        if (!TryGetPlacementPosition(previewMousePosition, out Vector3 surfacePosition, out Vector3 surfaceNormal))
        {
            lastPreviewOccupied = false;
            lastHitColliderName = "None";
            lastUsedNeighborSnap = false;
            hasLastPreviewTransform = false;
            ClearHoveredFaceAnchor();
            UpdatePreviewVisibility(false);
            return;
        }

        Quaternion placementRotation = GetPlacementRotation(surfaceNormal);
        bool useNeighborSnap = snapToNeighbor || currentEvent.alt;
        Vector3 snappedPosition = GetAlignedPreviewPosition(surfacePosition, placementRotation, surfaceNormal);
        if (useNeighborSnap)
        {
            // support surface 위에 올린 뒤, 필요하면 주변 메쉬 face 기준으로 한 번 더 붙인다.
            GetNeighborSnappedTransform(snappedPosition, placementRotation, out snappedPosition, out placementRotation);
        }
        else
        {
            ClearHoveredFaceAnchor();
        }

        lastPreviewOccupied = IsCellOccupied(snappedPosition);

        UpdatePreviewTransform(snappedPosition, placementRotation);
        UpdatePreviewMaterial(lastPreviewOccupied);
        lastPreviewPosition = snappedPosition;
        lastPreviewRotation = placementRotation;
        hasLastPreviewTransform = true;
        lastUsedNeighborSnap = useNeighborSnap;

        if (editPlacementScaleInScene && !hasScaleEditPose)
        {
            CaptureScaleEditPose();
        }

        if (editPlacementHeightInScene && !hasHeightEditPose)
        {
            CaptureHeightEditPose();
        }

        if (showGrid)
        {
            DrawGrid(snappedPosition, placementRotation);
        }

        Handles.color = lastPreviewOccupied
            ? new Color(1.0f, 0.35f, 0.35f, 0.9f)
            : new Color(0.2f, 0.9f, 1.0f, 0.9f);
        Handles.Label(
            snappedPosition + Vector3.up * 0.25f,
            lastPreviewOccupied
                ? $"{selectedPrefab.name} (Occupied)\nHit: {lastHitColliderName}"
                : $"{selectedPrefab.name}\nHit: {lastHitColliderName}"
        );
    }

    private void DrawLockedScaleEditPreview()
    {
        lastPreviewOccupied = IsCellOccupied(scaleEditPosition);

        UpdatePreviewTransform(scaleEditPosition, scaleEditRotation);
        UpdatePreviewMaterial(lastPreviewOccupied);
        lastPreviewPosition = scaleEditPosition;
        lastPreviewRotation = scaleEditRotation;
        hasLastPreviewTransform = true;

        if (showGrid)
        {
            DrawGrid(scaleEditPosition, scaleEditRotation);
        }

        Handles.color = new Color(0.2f, 0.9f, 1.0f, 0.9f);
        Handles.Label(
            scaleEditPosition + Vector3.up * 0.35f,
            $"{selectedPrefab.name} (Size Edit)\nMultiplier: {placementScale.x:F2}, {placementScale.y:F2}, {placementScale.z:F2}\nFinal: {GetPlacementLocalScale().ToString("F2")}"
        );
    }

    private void DrawPlacementScaleHandle(SceneView sceneView)
    {
        if (!editPlacementScaleInScene || selectedPrefab == null || !hasScaleEditPose)
        {
            return;
        }

        EditorGUI.BeginChangeCheck();
        float handleSize = HandleUtility.GetHandleSize(scaleEditPosition);
        Vector3 nextLocalScale = Handles.ScaleHandle(GetPlacementLocalScale(), scaleEditPosition, scaleEditRotation, handleSize);
        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        placementScale = ApplyPlacementScaleInput(GetPlacementScaleMultiplierFromLocalScale(nextLocalScale));
        Repaint();
        sceneView.Repaint();
    }

    private void DrawLockedHeightEditPreview()
    {
        lastPreviewOccupied = IsCellOccupied(heightEditPosition);

        UpdatePreviewTransform(heightEditPosition, heightEditRotation);
        UpdatePreviewMaterial(lastPreviewOccupied);
        lastPreviewPosition = heightEditPosition;
        lastPreviewRotation = heightEditRotation;
        hasLastPreviewTransform = true;

        if (showGrid)
        {
            DrawGrid(heightEditPosition, heightEditRotation);
        }

        Handles.color = new Color(0.2f, 0.9f, 1.0f, 0.9f);
        Handles.Label(
            heightEditPosition + Vector3.up * 0.45f,
            $"{selectedPrefab.name} (Height Edit)\nOffset: {GetCurrentHeight():F2}\nNudge: {heightStep:F2}"
        );
    }

    private void DrawPlacementHeightHandle(SceneView sceneView)
    {
        if (!editPlacementHeightInScene || selectedPrefab == null || !hasHeightEditPose)
        {
            return;
        }

        // 높이 편집은 X/Z 이동이 섞이면 안 된다.
        // PositionHandle은 카메라 방향 평면 이동까지 허용하므로 Y축 Slider만 사용한다.
        Handles.color = new Color(0.2f, 0.9f, 1.0f, 0.95f);
        float handleSize = HandleUtility.GetHandleSize(heightEditPosition);
        EditorGUI.BeginChangeCheck();
        Vector3 nextHandlePosition = Handles.Slider(
            heightEditPosition,
            Vector3.up,
            handleSize * 0.85f,
            Handles.ArrowHandleCap,
            0.0f);
        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        float yDelta = nextHandlePosition.y - heightEditBaseY;
        SetHeightOffset(heightEditBaseOffset + yDelta);
        sceneView.Repaint();
    }

    private void DrawGrid(Vector3 centerPosition, Quaternion placementRotation)
    {
        // 단순 월드 그리드가 아니라 현재 preview 회전에 맞춘 local grid를 그려서
        // 기울어진 배치에서도 "이번 셀" 감각이 유지되도록 했다.
        Vector3 rightAxis = placementRotation * Vector3.right;
        Vector3 forwardAxis = placementRotation * Vector3.forward;
        int radius = previewGridRadius;
        float extent = radius * gridSize;

        Handles.color = new Color(1.0f, 1.0f, 1.0f, 0.18f);

        for (int x = -radius; x <= radius; x++)
        {
            Vector3 lineOffset = rightAxis * (x * gridSize);
            Vector3 start = centerPosition + lineOffset - forwardAxis * extent;
            Vector3 end = centerPosition + lineOffset + forwardAxis * extent;
            Handles.DrawLine(start, end);
        }

        for (int z = -radius; z <= radius; z++)
        {
            Vector3 lineOffset = forwardAxis * (z * gridSize);
            Vector3 start = centerPosition - rightAxis * extent + lineOffset;
            Vector3 end = centerPosition + rightAxis * extent + lineOffset;
            Handles.DrawLine(start, end);
        }

        float halfSize = gridSize * 0.5f;
        Vector3 corner00 = centerPosition - rightAxis * halfSize - forwardAxis * halfSize;
        Vector3 corner01 = centerPosition - rightAxis * halfSize + forwardAxis * halfSize;
        Vector3 corner11 = centerPosition + rightAxis * halfSize + forwardAxis * halfSize;
        Vector3 corner10 = centerPosition + rightAxis * halfSize - forwardAxis * halfSize;

        Handles.color = new Color(0.2f, 0.9f, 1.0f, 0.35f);
        Handles.DrawSolidRectangleWithOutline(
            new Vector3[]
            {
                corner00,
                corner01,
                corner11,
                corner10,
            },
            new Color(0.2f, 0.9f, 1.0f, 0.08f),
            new Color(0.2f, 0.9f, 1.0f, 0.9f)
        );
    }
}
