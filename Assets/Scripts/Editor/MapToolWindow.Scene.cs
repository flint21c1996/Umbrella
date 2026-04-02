using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private void OnSceneGUI(SceneView sceneView)
    {
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
        if (showMeshColliderBounds)
        {
            DrawMeshColliderBounds();
        }
        HandleShortcuts(currentEvent);

        bool isPlaceInput = currentEvent.button == 0 && !currentEvent.alt;

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
        else if (currentEvent.keyCode == KeyCode.LeftBracket)
        {
            heightLevel--;
            Repaint();
            currentEvent.Use();
        }
        else if (currentEvent.keyCode == KeyCode.RightBracket)
        {
            heightLevel++;
            Repaint();
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
            ClearHoveredFaceAnchor();
            UpdatePreviewVisibility(false);
            return;
        }

        Quaternion placementRotation = GetPlacementRotation(surfaceNormal);
        bool useNeighborSnap = snapToNeighbor || currentEvent.alt;
        Vector3 snappedPosition = GetAlignedPreviewPosition(surfacePosition, placementRotation, surfaceNormal);
        if (useNeighborSnap)
        {
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
        lastUsedNeighborSnap = useNeighborSnap;

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

    private void DrawGrid(Vector3 centerPosition, Quaternion placementRotation)
    {
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
