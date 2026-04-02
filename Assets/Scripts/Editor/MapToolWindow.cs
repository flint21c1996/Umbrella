using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class MapToolWindow : EditorWindow
{
    private const string RootObjectName = "MapRoot";

    private enum NeighborSnapMode
    {
        Face,
        FaceExact,
        Edge,
        Vertex
    }

    private GameObject selectedPrefab;
    private GameObject floorPrefab;
    private GameObject wallPrefab;
    private GameObject stairPrefab;
    private Material selectedMaterial;
    private Transform placementParent;

    private float gridSize = 1.0f;
    private float heightStep = 1.0f;
    private float neighborSnapDistance = 0.5f;
    private float neighborAngleSnapThreshold = 10.0f;
    private float faceAssistSnapDistance = 0.2f;
    private int heightLevel;
    private float currentRotationDegrees;
    private bool placementEnabled = true;
    private bool showGrid = true;
    private bool snapToSurface = true;
    private bool snapToGrid = true;
    private bool snapToNeighbor;
    private bool topSurfaceOnly = true;
    private bool alignToSurfaceNormal;
    private bool autoAlignToNeighbor = true;
    private bool showMeshColliderBounds;
    private bool showAdvancedPlacementOptions;
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

        gridSize = Mathf.Max(0.1f, EditorGUILayout.FloatField(new GUIContent("Grid Size", "배치 위치를 맞출 grid 한 칸의 크기입니다."), gridSize));
        heightStep = Mathf.Max(0.1f, EditorGUILayout.FloatField(new GUIContent("Height Step", "Height Level이 1 증가할 때 올라가는 높이값입니다."), heightStep));
        heightLevel = EditorGUILayout.IntField(new GUIContent("Height Level", "현재 배치 높이 단계입니다. [ / ] 키로도 조절할 수 있습니다."), heightLevel);
        snapToSurface = EditorGUILayout.Toggle(new GUIContent("Snap To Surface", "클릭한 collider 표면에 prefab 바닥면이 닿도록 배치합니다."), snapToSurface);
        topSurfaceOnly = EditorGUILayout.Toggle(new GUIContent("Top Surface Only", "위를 향한 면만 배치 surface로 사용합니다. 옆면이나 아랫면은 무시합니다."), topSurfaceOnly);
        snapToNeighbor = EditorGUILayout.Toggle(new GUIContent("Snap To Neighbor", "주변 collider의 face, edge, vertex 기준으로 prefab을 붙입니다."), snapToNeighbor);
        neighborSnapMode = (NeighborSnapMode)EditorGUILayout.EnumPopup(
            new GUIContent("Neighbor Snap Mode", "Face, FaceExact, Edge, Vertex 중 어떤 기준으로 붙일지 정합니다."),
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
            "- [ / ]: 높이 단계 조절\n" +
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
                heightLevel--;
                Repaint();
            }

            if (GUILayout.Button("Height +"))
            {
                heightLevel++;
                Repaint();
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
        EditorGUILayout.LabelField("Height Offset", GetCurrentHeight().ToString("F2"));
        EditorGUILayout.LabelField("Current Rotation", $"{currentRotationDegrees:F1}도");
        EditorGUILayout.LabelField("Preview Occupied", lastPreviewOccupied ? "사용 중" : "비어 있음");
        EditorGUILayout.LabelField("Current Hit Collider", lastHitColliderName);
        EditorGUILayout.LabelField("Hovered Face", hasHoveredFaceAnchor && hoveredFaceCollider != null ? hoveredFaceCollider.name : "None");
        EditorGUILayout.LabelField("Recent Placements", recentPlacements.Count.ToString());
    }
}
