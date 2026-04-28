using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private const string RootObjectName = "MapRoot";

    private enum NeighborSnapMode
    {
        Face,
        Edge,
        Vertex
    }

    private GameObject selectedPrefab;
    private GameObject floorPrefab;
    private GameObject wallPrefab;
    private GameObject stairPrefab;
    private Material selectedMaterial;
    private Material placementMaterial;
    private Transform placementParent;

    private const float MinimumPlacementScale = 0.01f;

    private float gridSize = 1.0f;
    private float heightStep = 0.1f;
    private float neighborSnapDistance = 0.5f;
    private float neighborAngleSnapThreshold = 10.0f;
    private float faceAssistSnapDistance = 0.2f;
    private Vector3 placementScale = Vector3.one;
    private float heightOffset;
    private float currentRotationDegrees;
    private bool placementEnabled = true;
    private bool showGrid = true;
    private bool snapToSurface = true;
    private bool snapToGrid = true;
    private bool snapToNeighbor;
    private bool alignToSurfaceNormal;
    private bool autoAlignToNeighbor = true;
    private bool showMeshColliderBounds;
    private bool showAdvancedPlacementOptions;
    private bool uniformPlacementScale = true;
    private NeighborSnapMode neighborSnapMode = NeighborSnapMode.Face;
    private int previewGridRadius = 12;
    private Vector2 lastMousePosition;
    private bool lastPreviewOccupied;
    private bool isDragPlacing;
    private GameObject previewInstance;
    private Material previewMaterial;
    private Vector3 lastDragPlacementPosition;
    private Collider lastSurfaceHitCollider;
    private RaycastHit lastSurfaceHit;
    private bool hasLastSurfaceHit;
    private Vector3 lastSurfaceHitPoint;
    private Vector3 lastSurfaceNormal = Vector3.up;
    private Vector3 lastPreviewPosition;
    private Quaternion lastPreviewRotation = Quaternion.identity;
    private bool hasLastPreviewTransform;
    private bool editPlacementScaleInScene;
    private bool hasScaleEditPose;
    private Vector3 scaleEditPosition;
    private Quaternion scaleEditRotation = Quaternion.identity;
    private bool editPlacementHeightInScene;
    private bool hasHeightEditPose;
    private Vector3 heightEditPosition;
    private Quaternion heightEditRotation = Quaternion.identity;
    private float heightEditBaseOffset;
    private float heightEditBaseY;
    private bool lastUsedNeighborSnap;
    private string lastHitColliderName = "None";
    private bool hasHoveredFaceAnchor;
    private Collider hoveredFaceCollider;
    private readonly List<Vector3> hoveredFacePoints = new();
    private Vector3 hoveredFaceCenter;
    private Vector3 hoveredFaceNormal = Vector3.up;
    private Vector3 hoveredFaceHitPoint;

    private readonly List<GameObject> recentPlacements = new();

    [MenuItem("Tools/Umbrella/Map Tool")]
    public static void OpenWindow()
    {
        GetWindow<MapToolWindow>("Map Tool");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += UpdatePreviewState;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= UpdatePreviewState;
        DestroyPreviewInstance();
    }

    private void OnGUI()
    {
        // 자주 쓰는 블록 prefab은 상단에서 바로 갈아끼우고,
        // 실제 배치 파라미터는 아래 섹션에서 조절하는 흐름으로 구성했다.
        EditorGUILayout.LabelField("프리팹 프리셋", EditorStyles.boldLabel);
        floorPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Floor", "자주 쓰는 바닥용 Floor prefab을 미리 등록합니다."),
            floorPrefab,
            typeof(GameObject),
            false
        );
        wallPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Wall", "자주 쓰는 벽용 Wall prefab을 미리 등록합니다."),
            wallPrefab,
            typeof(GameObject),
            false
        );
        stairPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Stair", "자주 쓰는 계단용 Stair prefab을 미리 등록합니다."),
            stairPrefab,
            typeof(GameObject),
            false
        );

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = floorPrefab != null;
            if (GUILayout.Button("Use Floor"))
            {
                SetSelectedPrefab(floorPrefab);
            }

            GUI.enabled = wallPrefab != null;
            if (GUILayout.Button("Use Wall"))
            {
                SetSelectedPrefab(wallPrefab);
            }

            GUI.enabled = stairPrefab != null;
            if (GUILayout.Button("Use Stair"))
            {
                SetSelectedPrefab(stairPrefab);
            }

            GUI.enabled = true;
        }

        EditorGUILayout.Space(6.0f);
        EditorGUILayout.LabelField("배치", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        GameObject prefabFieldValue = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Prefab", "현재 Scene에 배치할 prefab입니다."),
            selectedPrefab,
            typeof(GameObject),
            false
        );
        if (EditorGUI.EndChangeCheck())
        {
            SetSelectedPrefab(prefabFieldValue);
        }

        placementMaterial = (Material)EditorGUILayout.ObjectField(
            new GUIContent("Placement Material", "배치할 때 바로 적용할 material입니다. 비워두면 prefab의 기존 material을 유지합니다."),
            placementMaterial,
            typeof(Material),
            false
        );
        gridSize = Mathf.Max(0.1f, EditorGUILayout.FloatField(new GUIContent("Grid Size", "배치 위치를 맞출 grid 한 칸의 크기입니다."), gridSize));
        heightStep = Mathf.Max(0.01f, EditorGUILayout.FloatField(new GUIContent("Height Nudge", "1 / 3 키를 누를 때마다 움직이는 높이값입니다."), heightStep));

        EditorGUI.BeginChangeCheck();
        float nextHeightOffset = EditorGUILayout.FloatField(new GUIContent("Height Offset", "현재 배치 높이 오프셋입니다. 1 / 3 키로도 조금씩 조절할 수 있습니다."), heightOffset);
        if (EditorGUI.EndChangeCheck())
        {
            SetHeightOffset(nextHeightOffset);
        }

        bool nextEditPlacementHeight = GUILayout.Toggle(
            editPlacementHeightInScene,
            new GUIContent("Height 바꾸기", "Scene View에 transform handle을 띄워 현재 배치 높이를 눈으로 조절합니다."),
            "Button"
        );
        if (nextEditPlacementHeight != editPlacementHeightInScene)
        {
            SetHeightEditMode(nextEditPlacementHeight);
        }

        bool nextUniformPlacementScale = EditorGUILayout.Toggle(
            new GUIContent("Uniform Scale", "켜면 X/Y/Z 배율을 같은 값으로 유지합니다. 한 축을 바꾸면 나머지 축도 같이 맞춰집니다."),
            uniformPlacementScale
        );
        if (nextUniformPlacementScale != uniformPlacementScale)
        {
            uniformPlacementScale = nextUniformPlacementScale;
            if (uniformPlacementScale)
            {
                placementScale = MakeUniformPlacementScale(placementScale.x);
            }
        }

        Vector3 nextPlacementScale = EditorGUILayout.Vector3Field(new GUIContent("Scale Multiplier", "prefab의 기본 scale에 곱해지는 X/Y/Z 배율입니다. 1, 1, 1이면 prefab 원래 크기를 유지합니다."), placementScale);
        placementScale = ApplyPlacementScaleInput(nextPlacementScale);
        if (selectedPrefab != null)
        {
            EditorGUILayout.LabelField("Prefab Base Scale", GetSelectedPrefabBaseScale().ToString("F2"));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            bool nextEditPlacementScale = GUILayout.Toggle(
                editPlacementScaleInScene,
                new GUIContent("Size 바꾸기", "Scene View에 scale handle을 띄워 현재 배치 scale을 눈으로 조절합니다."),
                "Button"
            );
            if (nextEditPlacementScale != editPlacementScaleInScene)
            {
                SetScaleEditMode(nextEditPlacementScale);
            }

            if (GUILayout.Button(new GUIContent("Size Reset", "배치 배율을 1, 1, 1로 되돌립니다. prefab 원래 크기로 배치됩니다.")))
            {
                placementScale = MakeUniformPlacementScale(1.0f);
                Repaint();
                SceneView.RepaintAll();
            }
        }

        snapToSurface = EditorGUILayout.Toggle(new GUIContent("Snap To Surface", "클릭한 collider의 윗면(top surface)을 support로 사용해 prefab 바닥면이 닿도록 배치합니다."), snapToSurface);
        snapToNeighbor = EditorGUILayout.Toggle(new GUIContent("Snap To Neighbor", "주변 collider의 face, edge, vertex 기준으로 prefab을 붙입니다."), snapToNeighbor);
        neighborSnapMode = (NeighborSnapMode)EditorGUILayout.EnumPopup(
            new GUIContent("Neighbor Snap Mode", "Face, Edge, Vertex 중 어떤 기준으로 붙일지 정합니다."),
            neighborSnapMode
        );
        neighborSnapDistance = Mathf.Max(
            0.01f,
            EditorGUILayout.FloatField(
                new GUIContent("Neighbor Snap Distance", "주변 오브젝트와 얼마나 가까우면 snap할지 정합니다. Alt를 누르면 neighbor snap을 임시로 켤 수 있습니다."),
                neighborSnapDistance
            )
        );

        EditorGUILayout.Space(2.0f);
        showAdvancedPlacementOptions = EditorGUILayout.Foldout(showAdvancedPlacementOptions, "고급 옵션 (Advanced)", true);
        if (showAdvancedPlacementOptions)
        {
            // 기본 사용 흐름에는 꼭 필요하지 않은 옵션만 foldout 안으로 넣어
            // 맵을 찍을 때의 시각적 복잡도를 줄였다.
            EditorGUI.indentLevel++;
            alignToSurfaceNormal = EditorGUILayout.Toggle(new GUIContent("Align To Surface Normal", "surface normal 방향으로 prefab을 기울입니다. 끄면 회전은 월드 기준을 유지합니다."), alignToSurfaceNormal);
            snapToGrid = EditorGUILayout.Toggle(new GUIContent("Snap To Grid", "배치 위치의 X/Z 좌표를 grid에 맞춰 정렬합니다."), snapToGrid);
            autoAlignToNeighbor = EditorGUILayout.Toggle(
                new GUIContent("Auto Align To Neighbor", "neighbor와 각도 차이가 작으면 회전을 자동으로 맞춥니다."),
                autoAlignToNeighbor
            );
            neighborAngleSnapThreshold = Mathf.Clamp(
                EditorGUILayout.FloatField(
                    new GUIContent("Neighbor Angle Threshold", "자동 회전을 적용할 최대 angle 차이입니다. 이 값 이하면 neighbor 회전에 맞춰 정렬합니다."),
                    neighborAngleSnapThreshold
                ),
                0.0f,
                45.0f
            );
            faceAssistSnapDistance = Mathf.Max(
                0.0f,
                EditorGUILayout.FloatField(
                    new GUIContent("Face Assist Distance", "Face가 붙은 뒤 edge / vertex도 살짝 정렬해서 더 자연스럽게 보정합니다."),
                    faceAssistSnapDistance
                )
            );
            placementParent = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Parent", "배치된 오브젝트를 넣을 부모 Transform입니다. 비워두면 MapRoot를 자동 생성합니다."),
                placementParent,
                typeof(Transform),
                true
            );
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4.0f);
        EditorGUILayout.LabelField("빠른 조작", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scene View 조작:\n" +
            "- 좌클릭: prefab 배치\n" +
            "- Shift + 드래그: 연속 배치\n" +
            "- Ctrl + 좌클릭: 커서 아래 오브젝트 삭제\n" +
            "- Q / E: 1도 회전\n" +
            "- R: 90도 회전\n" +
            "- Delete: 선택 오브젝트 삭제\n" +
            "- 1 / 3: 높이 미세 조절\n" +
            "- Alt: 주변 오브젝트 붙이기 임시 활성화",
            MessageType.Info
        );

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Rotate Left"))
            {
                RotateLeft();
            }

            if (GUILayout.Button("Rotate Right"))
            {
                RotateRight();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Height -"))
            {
                ChangeHeightOffset(-heightStep);
            }

            if (GUILayout.Button("Height +"))
            {
                ChangeHeightOffset(heightStep);
            }
        }

        placementEnabled = EditorGUILayout.Toggle(new GUIContent("Enable Placement", "켜면 Scene View에서 preview와 배치를 사용할 수 있습니다."), placementEnabled);
        showGrid = EditorGUILayout.Toggle(new GUIContent("Show Grid Preview", "Scene View에 grid line과 현재 셀 preview를 표시합니다."), showGrid);
        showMeshColliderBounds = EditorGUILayout.Toggle(new GUIContent("Show MeshCollider Bounds", "Scene View에 MeshCollider wireframe과 face anchor debug를 표시합니다."), showMeshColliderBounds);
        previewGridRadius = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Grid Preview Radius", "마우스 주변으로 몇 칸까지 grid line을 그릴지 정합니다."), previewGridRadius));

        EditorGUILayout.Space(6.0f);
        EditorGUILayout.LabelField("작업", EditorStyles.boldLabel);

        selectedMaterial = (Material)EditorGUILayout.ObjectField(
            new GUIContent("Material Preset", "선택한 오브젝트들에 일괄 적용할 material preset입니다."),
            selectedMaterial,
            typeof(Material),
            false
        );

        if (GUILayout.Button("Apply Material To Selection"))
        {
            ApplyMaterialToSelection();
        }

        if (GUILayout.Button("Snap Selection To Grid"))
        {
            SnapSelectionToGrid();
        }

        if (GUILayout.Button("Add MeshCollider To Selection"))
        {
            AddMeshCollidersToSelection();
        }

        if (GUILayout.Button("Clear Recent Placement Cache"))
        {
            recentPlacements.Clear();
        }

        EditorGUILayout.Space(6.0f);
        EditorGUILayout.LabelField("상태", EditorStyles.boldLabel);
        // 디버그용 상태값은 지금 배치 계산이 무엇을 기준으로 굴러가는지
        // 빠르게 확인할 수 있게 최소 정보만 노출한다.
        EditorGUILayout.LabelField("Height Offset", GetCurrentHeight().ToString("F2"));
        EditorGUILayout.LabelField("Current Rotation", $"{currentRotationDegrees:F1}도");
        EditorGUILayout.LabelField("Preview Occupied", lastPreviewOccupied ? "사용 중" : "비어 있음");
        EditorGUILayout.LabelField("Current Hit Collider", lastHitColliderName);
        EditorGUILayout.LabelField("Hovered Face", hasHoveredFaceAnchor && hoveredFaceCollider != null ? hoveredFaceCollider.name : "None");
        EditorGUILayout.LabelField("Recent Placements", recentPlacements.Count.ToString());
    }

    private Vector3 SanitizePlacementScale(Vector3 scale)
    {
        // 0 이하 scale은 preview bounds 계산과 실제 배치가 뒤집히거나 사라지는 원인이 된다.
        // 맵툴에서는 "prefab 기본 scale에 곱하는 배율"로 쓰기 위해 아주 작은 양수까지로 제한한다.
        return new Vector3(
            Mathf.Max(MinimumPlacementScale, scale.x),
            Mathf.Max(MinimumPlacementScale, scale.y),
            Mathf.Max(MinimumPlacementScale, scale.z));
    }

    private Vector3 ApplyPlacementScaleInput(Vector3 nextScale)
    {
        nextScale = SanitizePlacementScale(nextScale);

        if (!uniformPlacementScale)
        {
            return nextScale;
        }

        // Uniform Scale이 켜져 있으면 가장 크게 바뀐 축의 값을 기준으로 세 축을 모두 맞춘다.
        // Inspector에서 X만 입력하거나 Scene View에서 한 축 손잡이를 잡아도 같은 크기로 유지하기 위함이다.
        float uniformValue = GetMostChangedScaleAxisValue(placementScale, nextScale);
        return MakeUniformPlacementScale(uniformValue);
    }

    private float GetMostChangedScaleAxisValue(Vector3 previousScale, Vector3 nextScale)
    {
        float xDelta = Mathf.Abs(nextScale.x - previousScale.x);
        float yDelta = Mathf.Abs(nextScale.y - previousScale.y);
        float zDelta = Mathf.Abs(nextScale.z - previousScale.z);

        if (yDelta > xDelta && yDelta >= zDelta)
        {
            return nextScale.y;
        }

        if (zDelta > xDelta && zDelta > yDelta)
        {
            return nextScale.z;
        }

        return nextScale.x;
    }

    private Vector3 MakeUniformPlacementScale(float scale)
    {
        float sanitizedScale = Mathf.Max(MinimumPlacementScale, scale);
        return new Vector3(sanitizedScale, sanitizedScale, sanitizedScale);
    }

    private Vector3 GetSelectedPrefabBaseScale()
    {
        return selectedPrefab != null ? selectedPrefab.transform.localScale : Vector3.one;
    }

    private Vector3 GetPlacementLocalScale()
    {
        // placementScale은 최종 localScale이 아니라 prefab 기본 scale에 곱하는 배율이다.
        // 그래야 Project 창에서 직접 끌어놓은 크기와 Map Tool로 찍은 기본 크기가 같아진다.
        return Vector3.Scale(GetSelectedPrefabBaseScale(), placementScale);
    }

    private Vector3 GetPlacementScaleMultiplierFromLocalScale(Vector3 localScale)
    {
        Vector3 baseScale = GetSelectedPrefabBaseScale();
        return SanitizePlacementScale(new Vector3(
            SafeDivideScale(localScale.x, baseScale.x),
            SafeDivideScale(localScale.y, baseScale.y),
            SafeDivideScale(localScale.z, baseScale.z)));
    }

    private float SafeDivideScale(float value, float divisor)
    {
        if (Mathf.Abs(divisor) <= 0.0001f)
        {
            return value;
        }

        return value / divisor;
    }

    private void SetScaleEditMode(bool enabled)
    {
        editPlacementScaleInScene = enabled;

        if (editPlacementScaleInScene)
        {
            editPlacementHeightInScene = false;
            hasHeightEditPose = false;
            CaptureScaleEditPose();
        }
        else
        {
            hasScaleEditPose = false;
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private void SetHeightEditMode(bool enabled)
    {
        editPlacementHeightInScene = enabled;

        if (editPlacementHeightInScene)
        {
            editPlacementScaleInScene = false;
            hasScaleEditPose = false;
            CaptureHeightEditPose();
        }
        else
        {
            hasHeightEditPose = false;
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private void CaptureScaleEditPose()
    {
        if (!hasLastPreviewTransform)
        {
            hasScaleEditPose = false;
            return;
        }

        scaleEditPosition = lastPreviewPosition;
        scaleEditRotation = lastPreviewRotation;
        hasScaleEditPose = true;
    }

    private void CaptureHeightEditPose()
    {
        if (!hasLastPreviewTransform)
        {
            hasHeightEditPose = false;
            return;
        }

        heightEditPosition = lastPreviewPosition;
        heightEditRotation = lastPreviewRotation;
        heightEditBaseOffset = heightOffset;
        heightEditBaseY = lastPreviewPosition.y;
        hasHeightEditPose = true;
    }

    private void ChangeHeightOffset(float delta)
    {
        SetHeightOffset(heightOffset + delta);
    }

    private void SetHeightOffset(float nextHeightOffset)
    {
        if (Mathf.Approximately(heightOffset, nextHeightOffset))
        {
            return;
        }

        float delta = nextHeightOffset - heightOffset;
        heightOffset = nextHeightOffset;

        if (hasHeightEditPose)
        {
            heightEditPosition += Vector3.up * delta;
        }

        Repaint();
        SceneView.RepaintAll();
    }
}
