using UnityEngine;

namespace UmbrellaPuzzle.Weather
{
    // 열린 우산 위에 비 파티클을 막는 얇은 Collider를 만든다.
    // 실제 게임플레이 차단 판정은 PlayerUmbrellaController가 처리하고,
    // 이 컴포넌트는 Particle System Collision을 위한 비주얼용 Collider만 담당한다.
    [DisallowMultipleComponent]
    public sealed class UmbrellaRainBlocker : MonoBehaviour
    {
        [Header("References")]

        [SerializeField, Tooltip("우산 상태를 제공하는 컨트롤러입니다. 비워두면 부모에서 자동으로 찾습니다.")]
        private PlayerUmbrellaController umbrellaController;

        [SerializeField, Tooltip("차단 Collider를 붙일 기준 Transform입니다. 비워두면 PlayerUmbrellaController.openVisual을 사용합니다.")]
        private Transform blockerParent;

        [Header("Layer")]

        [SerializeField, Tooltip("비 파티클 Collision Mask와 맞출 레이어 이름입니다. 기본값은 RainBlocker입니다.")]
        private string blockerLayerName = "RainBlocker";

        [SerializeField, Tooltip("RainBlocker 레이어를 찾지 못했을 때 사용할 fallback 레이어 번호입니다.")]
        private int fallbackLayer = 0;

        [Header("Shape")]

        [SerializeField, Tooltip("우산 위 차단 Collider의 로컬 위치입니다. 우산 윗면보다 살짝 위에 두면 자연스럽습니다.")]
        private Vector3 localOffset = new Vector3(0f, 0.08f, 0f);

        [SerializeField, Tooltip("우산 위 차단 Collider의 크기입니다. X/Z는 우산 폭, Y는 두께입니다.")]
        private Vector3 blockerSize = new Vector3(1.8f, 0.08f, 1.8f);

        [SerializeField, Tooltip("Scene View에서 차단 Collider 범위를 표시합니다.")]
        private bool drawGizmo = true;

        private const string BlockerObjectName = "Umbrella Rain Blocker";

        private BoxCollider blockerCollider;
        private Transform blockerTransform;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void Start()
        {
            EnsureBlocker();
            RefreshBlockerTransform();
            RefreshEnabledState();
        }

        private void OnEnable()
        {
            CacheReferences();

            if (blockerCollider != null)
            {
                RefreshBlockerTransform();
                RefreshEnabledState();
            }
        }

        private void Update()
        {
            if (umbrellaController == null)
            {
                CacheReferences();
            }

            if (blockerCollider == null)
            {
                EnsureBlocker();
            }

            RefreshBlockerTransform();
            RefreshEnabledState();
        }

        private void OnValidate()
        {
            fallbackLayer = Mathf.Clamp(fallbackLayer, 0, 31);
            blockerSize.x = Mathf.Max(0.05f, blockerSize.x);
            blockerSize.y = Mathf.Max(0.01f, blockerSize.y);
            blockerSize.z = Mathf.Max(0.05f, blockerSize.z);

            if (!Application.isPlaying || blockerCollider == null)
            {
                return;
            }

            RefreshBlockerTransform();
            RefreshEnabledState();
        }

        private void CacheReferences()
        {
            if (umbrellaController == null)
            {
                umbrellaController = GetComponentInParent<PlayerUmbrellaController>();
            }

            if (blockerParent == null && umbrellaController != null && umbrellaController.openVisual != null)
            {
                blockerParent = umbrellaController.openVisual.transform;
            }
        }

        private Transform GetBlockerParent()
        {
            if (blockerParent != null)
            {
                return blockerParent;
            }

            return transform;
        }

        private void EnsureBlocker()
        {
            Transform parent = GetBlockerParent();
            Transform child = parent.Find(BlockerObjectName);
            GameObject blockerObject;

            if (child == null)
            {
                blockerObject = new GameObject(BlockerObjectName);
                blockerObject.transform.SetParent(parent, false);
            }
            else
            {
                blockerObject = child.gameObject;
            }

            blockerTransform = blockerObject.transform;
            blockerCollider = blockerObject.GetComponent<BoxCollider>();

            if (blockerCollider == null)
            {
                blockerCollider = blockerObject.AddComponent<BoxCollider>();
            }

            blockerCollider.isTrigger = false;
            blockerObject.layer = ResolveBlockerLayer();
        }

        private int ResolveBlockerLayer()
        {
            if (!string.IsNullOrWhiteSpace(blockerLayerName))
            {
                int namedLayer = LayerMask.NameToLayer(blockerLayerName);
                if (namedLayer >= 0)
                {
                    return namedLayer;
                }
            }

            return fallbackLayer;
        }

        private void RefreshBlockerTransform()
        {
            if (blockerTransform == null || blockerCollider == null)
            {
                return;
            }

            Transform targetParent = GetBlockerParent();
            if (blockerTransform.parent != targetParent)
            {
                blockerTransform.SetParent(targetParent, false);
            }

            blockerTransform.localPosition = localOffset;
            blockerTransform.localRotation = Quaternion.identity;
            blockerTransform.localScale = Vector3.one;
            blockerCollider.size = blockerSize;
            blockerCollider.center = Vector3.zero;
            blockerCollider.gameObject.layer = ResolveBlockerLayer();
        }

        private void RefreshEnabledState()
        {
            if (blockerCollider == null)
            {
                return;
            }

            blockerCollider.enabled = umbrellaController != null && umbrellaController.IsOpen;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo)
            {
                return;
            }

            CacheReferences();

            Transform parent = GetBlockerParent();
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(parent.TransformPoint(localOffset), parent.rotation, parent.lossyScale);
            Gizmos.color = new Color(0.25f, 0.7f, 1f, 0.25f);
            Gizmos.DrawCube(Vector3.zero, blockerSize);
            Gizmos.color = new Color(0.25f, 0.7f, 1f, 0.95f);
            Gizmos.DrawWireCube(Vector3.zero, blockerSize);
            Gizmos.matrix = previousMatrix;
        }
    }
}
