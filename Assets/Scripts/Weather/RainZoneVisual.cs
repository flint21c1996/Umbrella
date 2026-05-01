using UmbrellaPuzzle.Environmental;
using UnityEngine;

namespace UmbrellaPuzzle.Weather
{
    // Rain Zone 하나의 시각 상태를 관리한다.
    // 퍼즐 판정은 UmbrellaRainArea 같은 별도 컴포넌트가 맡고,
    // 이 컴포넌트는 "이 구역에 비가 내린다"는 시각 피드백만 제어한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SimpleRainController))]
    public sealed class RainZoneVisual : MonoBehaviour, IEnvironmentZoneVisual
    {
        // 이전 코드 호환용 상태 enum이다. 새 환경 요소 코드는 EnvironmentZoneState를 사용한다.
        public enum RainZoneState
        {
            Inactive,
            Active,
            Solved
        }

        [Header("References")]

        // 실제 비 파티클을 생성하는 하위 비주얼 컨트롤러다.
        // 비워두면 같은 GameObject의 SimpleRainController를 자동으로 찾는다.
        [SerializeField, Tooltip("실제 비 파티클을 생성하는 컨트롤러입니다. 비워두면 같은 GameObject에서 자동으로 찾습니다.")]
        private SimpleRainController rainController;

        [SerializeField, Tooltip("바닥 물튐 파티클을 생성하는 컨트롤러입니다. 비워두고 Auto Create Ground Splash를 켜면 런타임에 자동 추가됩니다.")]
        private SimpleRainSplashController groundSplashController;

        // Rain Zone의 판정 범위를 나타내는 Collider다.
        // 비워두면 같은 GameObject 또는 자식에서 Collider를 자동으로 찾는다.
        [SerializeField, Tooltip("비가 내려야 하는 퍼즐 구역의 Collider입니다. Sync Area From Collider가 켜져 있으면 이 크기로 비 영역을 맞춥니다.")]
        private Collider zoneCollider;

        [Header("Optional Visuals")]

        [SerializeField, Tooltip("비가 활성화될 때 켜지는 젖은 바닥 표시 오브젝트입니다. Decal, 얇은 Plane, Mesh 등을 연결할 수 있습니다.")]
        private GameObject wetAreaVisual;

        [SerializeField, Tooltip("Wet Area Visual을 Collider 바닥 중앙과 X/Z 크기에 맞춰 자동 배치합니다.")]
        private bool syncWetAreaFromCollider = true;

        [SerializeField, Tooltip("Wet Area Visual을 Collider 바닥면보다 살짝 위에 띄우는 높이입니다.")]
        private float wetAreaHeightOffset = 0.02f;

        [Header("Geometry")]

        // 켜두면 zoneCollider의 월드 크기를 읽어 비 생성 영역에 반영한다.
        // 퍼즐 판정 범위와 보이는 비 범위를 맞추기 위한 옵션이다.
        [SerializeField, Tooltip("켜면 Zone Collider의 월드 X/Z 크기와 윗면 위치를 사용해 비 생성 범위와 높이를 자동 설정합니다.")]
        private bool syncAreaFromCollider = true;

        // Collider가 없거나 자동 동기화를 끈 경우 사용할 수동 비 영역 크기다.
        [SerializeField, Tooltip("Collider 동기화를 끄거나 Collider가 없을 때 사용할 수동 비 영역 X/Z 크기입니다.")]
        private Vector2 manualAreaSize = new Vector2(6f, 6f);

        // 비 파티클 생성 위치를 Collider 윗면보다 얼마나 위에 둘지 정한다.
        // 너무 낮으면 비가 중간에서 시작하는 것처럼 보이고, 너무 높으면 구역 밖까지 퍼져 보일 수 있다.
        [SerializeField, Tooltip("비 생성 위치를 Collider 윗면보다 얼마나 위에 둘지 정합니다.")]
        private float emitterTopPadding = 0.25f;

        [SerializeField, Tooltip("바닥 물튐 생성 위치를 Collider 바닥면보다 얼마나 위에 둘지 정합니다.")]
        private float groundSplashHeightOffset = 0.05f;

        [Header("Splash")]

        [SerializeField, Tooltip("켜면 Ground Splash Controller가 없을 때 Play Mode에서 자동으로 추가합니다.")]
        private bool autoCreateGroundSplash = true;

        [SerializeField, Range(0f, 2f), Tooltip("비 강도 대비 바닥 물튐 강도 배율입니다. 0이면 물튐을 사용하지 않습니다.")]
        private float groundSplashIntensityMultiplier = 0.45f;

        [SerializeField, Tooltip("우산이 막고 있는 바닥 물튐 제외 영역을 RainBlocker 크기보다 약간 넓히는 값입니다.")]
        private float groundSplashBlockerPadding = 0.05f;

        [SerializeField, Tooltip("RainBlocked 신호가 잠깐 끊겨도 바닥 물튐 제외 영역을 유지하는 시간입니다.")]
        private float groundSplashBlockerGraceTime = 0.12f;

        [Header("State")]

        // Play Mode 시작 시 Rain Zone이 어떤 상태로 시작할지 정한다.
        [SerializeField, Tooltip("Play Mode 시작 시 이 Rain Zone이 가질 초기 시각 상태입니다.")]
        private EnvironmentZoneState startState = EnvironmentZoneState.Active;

        // Inactive 상태에서의 비 강도다.
        // 0이면 비가 보이지 않는 상태로 시작한다.
        [SerializeField, Range(0f, 1f), Tooltip("Inactive 상태의 비 강도입니다. 0이면 비가 완전히 꺼집니다.")]
        private float inactiveIntensity = 0f;

        // Active 상태에서의 비 강도다.
        // 퍼즐 입력 장치로 작동 중인 기본 상태에 해당한다.
        [SerializeField, Range(0f, 1f), Tooltip("Active 상태의 비 강도입니다. 퍼즐 구역이 작동 중일 때의 기본 강도입니다.")]
        private float activeIntensity = 0.65f;

        // Solved 상태에서의 비 강도다.
        // 퍼즐 해결 후 비가 멈추게 하려면 0으로 두고, 잔비를 남기려면 낮은 값을 준다.
        [SerializeField, Range(0f, 1f), Tooltip("Solved 상태의 비 강도입니다. 0이면 해결 후 비가 꺼지고, 낮은 값을 주면 잔비가 남습니다.")]
        private float solvedIntensity = 0f;

        // 상태가 바뀔 때 비 강도가 초당 얼마나 빠르게 목표값으로 이동할지 정한다.
        [SerializeField, Tooltip("상태 변경 시 현재 비 강도가 목표 강도로 이동하는 속도입니다. 값이 높을수록 빠르게 전환됩니다.")]
        private float intensityChangeSpeed = 2.5f;

        // 비 강도가 0에 도달했을 때 Particle System 생성을 완전히 멈출지 정한다.
        // 여러 Rain Zone을 둘 때 불필요한 상시 업데이트를 줄이는 데 도움이 된다.
        [SerializeField, Tooltip("비 강도가 0이 되었을 때 Particle System을 정지하고 남은 빗방울을 지웁니다.")]
        private bool stopWhenHidden = true;

        [Header("Runtime")]

        // 현재 Rain Zone 상태다. 디버깅을 위해 Inspector에 표시한다.
        [SerializeField, Tooltip("현재 Rain Zone의 런타임 상태입니다. Play Mode 중 변경하면 다음 Update에서 상태가 적용됩니다.")]
        private EnvironmentZoneState currentState;

        // 현재 SimpleRainController에 적용 중인 비 강도다.
        [SerializeField, Tooltip("현재 SimpleRainController에 적용 중인 비 강도입니다.")]
        private float currentIntensity;

        // 현재 상태가 목표로 하는 비 강도다.
        [SerializeField, Tooltip("현재 상태가 목표로 하는 비 강도입니다.")]
        private float targetIntensity;

        // Start 이후부터만 Particle System 재생/정지 같은 런타임 동작을 수행하기 위한 플래그다.
        private bool hasStarted;

        // OnValidate에서 바로 Play/Stop을 호출하지 않고, 다음 Update에서 안전하게 적용하기 위한 플래그다.
        private bool pendingImmediateStateApply;
        private Bounds groundSplashBlockerBounds;
        private float lastGroundSplashBlockerTime = float.NegativeInfinity;

        public EnvironmentZoneState CurrentState => currentState;
        public float CurrentIntensity => currentIntensity;
        public float TargetIntensity => targetIntensity;

        // 컴포넌트를 처음 붙였을 때 필요한 참조를 자동으로 채운다.
        private void Reset()
        {
            CacheReferences();
            RefreshGeometry();
        }

        // 런타임 시작 시 참조와 영역 정보만 확보한다.
        // 실제 Particle System 재생은 모든 Awake가 끝난 뒤 Start에서 처리한다.
        private void Awake()
        {
            CacheReferences();
            DisableRainControllerAutoPlay();
            RefreshGeometry();
            currentState = startState;
            targetIntensity = GetIntensityForState(currentState);
            currentIntensity = targetIntensity;

            if (rainController != null)
            {
                ApplyIntensityToVisuals(currentIntensity);
            }
        }

        // 비활성화되었다가 다시 켜질 때 현재 상태와 영역 정보를 다시 적용한다.
        // Start 전에는 계층 변경이 생길 수 있는 재생 처리를 하지 않는다.
        private void OnEnable()
        {
            CacheReferences();
            DisableRainControllerAutoPlay();
            RefreshGeometry();

            if (hasStarted)
            {
                SetState(currentState, true);
            }
        }

        // 모든 Awake가 끝난 뒤 시작 상태를 실제 비주얼에 반영한다.
        private void Start()
        {
            hasStarted = true;
            CacheReferences(true);
            DisableRainControllerAutoPlay();
            RefreshGeometry();
            SetState(currentState, true);
        }

        // Inspector 값이 바뀌었을 때 잘못된 범위를 보정하고, Play Mode 중이면 즉시 반영한다.
        private void OnValidate()
        {
            inactiveIntensity = Mathf.Clamp01(inactiveIntensity);
            activeIntensity = Mathf.Clamp01(activeIntensity);
            solvedIntensity = Mathf.Clamp01(solvedIntensity);
            manualAreaSize.x = Mathf.Max(1f, manualAreaSize.x);
            manualAreaSize.y = Mathf.Max(1f, manualAreaSize.y);
            emitterTopPadding = Mathf.Max(0f, emitterTopPadding);
            groundSplashHeightOffset = Mathf.Max(0f, groundSplashHeightOffset);
            wetAreaHeightOffset = Mathf.Max(0f, wetAreaHeightOffset);
            groundSplashIntensityMultiplier = Mathf.Max(0f, groundSplashIntensityMultiplier);
            groundSplashBlockerPadding = Mathf.Max(0f, groundSplashBlockerPadding);
            groundSplashBlockerGraceTime = Mathf.Max(0f, groundSplashBlockerGraceTime);
            intensityChangeSpeed = Mathf.Max(0.01f, intensityChangeSpeed);

            if (!Application.isPlaying)
            {
                return;
            }

            CacheReferences();
            DisableRainControllerAutoPlay();
            RefreshGeometry();

            targetIntensity = GetIntensityForState(currentState);
            currentIntensity = targetIntensity;

            if (rainController != null)
            {
                ApplyIntensityToVisuals(currentIntensity);
            }

            pendingImmediateStateApply = true;
        }

        // 매 프레임 현재 비 강도를 목표 강도로 부드럽게 이동시킨다.
        private void Update()
        {
            if (rainController == null)
            {
                ApplyGroundSplashBlocker();
                return;
            }

            if (pendingImmediateStateApply)
            {
                pendingImmediateStateApply = false;
                SetState(currentState, true);
                ApplyGroundSplashBlocker();
                return;
            }

            ApplyGroundSplashBlocker();

            if (Mathf.Approximately(currentIntensity, targetIntensity))
            {
                StopRainIfHidden();
                return;
            }

            currentIntensity = Mathf.MoveTowards(
                currentIntensity,
                targetIntensity,
                intensityChangeSpeed * Time.deltaTime
            );

            ApplyIntensityToVisuals(currentIntensity);
            StopRainIfHidden();
        }

        // 외부 퍼즐 로직에서 Rain Zone 상태를 바꿀 때 사용하는 진입점이다.
        public void SetState(EnvironmentZoneState nextState)
        {
            SetState(nextState, false);
        }

        // 이전 RainZoneVisual.RainZoneState API를 쓰는 코드가 있어도 동작하도록 남겨둔다.
        public void SetState(RainZoneState nextState)
        {
            SetState((EnvironmentZoneState)(int)nextState, false);
        }

        // 비 구역을 작동 상태로 만든다.
        public void SetActive()
        {
            SetState(EnvironmentZoneState.Active);
        }

        // 비 구역을 비활성 상태로 만든다.
        public void SetInactive()
        {
            SetState(EnvironmentZoneState.Inactive);
        }

        // 비 구역을 해결 완료 상태로 만든다.
        public void SetSolved()
        {
            SetState(EnvironmentZoneState.Solved);
        }

        // Collider 또는 수동 설정값을 다시 읽어 비 생성 범위에 반영한다.
        public void RefreshGeometry()
        {
            if (rainController == null)
            {
                return;
            }

            if (!syncAreaFromCollider || zoneCollider == null)
            {
                Vector3 manualGroundCenter = transform.position + Vector3.up * groundSplashHeightOffset;
                Vector3 manualGroundSplashOffset = groundSplashController != null
                    ? groundSplashController.transform.InverseTransformPoint(manualGroundCenter)
                    : Vector3.zero;

                rainController.SetAreaSize(manualAreaSize);
                rainController.SetLocalEmitterOffset(Vector3.zero);
                ApplyGroundSplashGeometry(manualAreaSize, manualGroundSplashOffset);
                RefreshWetAreaVisual(false, default);
                return;
            }

            Bounds bounds = zoneCollider.bounds;
            Vector2 areaSize = new Vector2(bounds.size.x, bounds.size.z);
            Vector3 topCenter = new Vector3(bounds.center.x, bounds.max.y + emitterTopPadding, bounds.center.z);
            Vector3 localEmitterOffset = rainController.transform.InverseTransformPoint(topCenter);
            Vector3 groundCenter = new Vector3(bounds.center.x, bounds.min.y + groundSplashHeightOffset, bounds.center.z);
            Vector3 localGroundSplashOffset = groundSplashController != null
                ? groundSplashController.transform.InverseTransformPoint(groundCenter)
                : Vector3.zero;

            rainController.SetAreaSize(areaSize);
            rainController.SetLocalEmitterOffset(localEmitterOffset);
            ApplyGroundSplashGeometry(areaSize, localGroundSplashOffset);
            RefreshWetAreaVisual(true, bounds);
        }

        public void SetGroundSplashBlocker(Bounds blockerBounds)
        {
            groundSplashBlockerBounds = blockerBounds;
            lastGroundSplashBlockerTime = Time.time;
            ApplyGroundSplashBlocker();
        }

        // 필요한 컴포넌트 참조를 자동으로 찾는다.
        private void CacheReferences()
        {
            CacheReferences(false);
        }

        private void CacheReferences(bool allowRuntimeCreation)
        {
            if (rainController == null)
            {
                rainController = GetComponent<SimpleRainController>();
            }

            if (groundSplashController == null)
            {
                groundSplashController = GetComponent<SimpleRainSplashController>();
            }

            if (groundSplashController == null && allowRuntimeCreation && autoCreateGroundSplash)
            {
                groundSplashController = gameObject.AddComponent<SimpleRainSplashController>();
            }

            if (zoneCollider == null)
            {
                zoneCollider = GetComponent<Collider>();
            }

            if (zoneCollider == null)
            {
                zoneCollider = GetComponentInChildren<Collider>();
            }
        }

        // Rain Zone에서는 SimpleRainController의 자동 재생을 끄고,
        // RainZoneVisual의 상태 변경만으로 비를 켜고 끄게 한다.
        private void DisableRainControllerAutoPlay()
        {
            if (rainController != null)
            {
                rainController.SetPlayOnStart(false);
            }

            if (groundSplashController != null)
            {
                groundSplashController.SetPlayOnStart(false);
            }
        }

        // 상태 변경을 실제 목표 강도로 변환한다.
        private void SetState(EnvironmentZoneState nextState, bool immediate)
        {
            CacheReferences(hasStarted);
            DisableRainControllerAutoPlay();

            currentState = nextState;
            targetIntensity = GetIntensityForState(nextState);

            if (rainController == null)
            {
                return;
            }

            if (targetIntensity > 0f)
            {
                rainController.Play();

                if (groundSplashController != null && groundSplashIntensityMultiplier > 0f)
                {
                    groundSplashController.Play();
                }
            }

            if (!immediate)
            {
                return;
            }

            currentIntensity = targetIntensity;
            ApplyIntensityToVisuals(currentIntensity);
            StopRainIfHidden();
        }

        // Rain Zone 상태별 Inspector 강도 값을 반환한다.
        private float GetIntensityForState(EnvironmentZoneState state)
        {
            switch (state)
            {
                case EnvironmentZoneState.Inactive:
                    return inactiveIntensity;

                case EnvironmentZoneState.Solved:
                    return solvedIntensity;

                default:
                    return activeIntensity;
            }
        }

        private void ApplyIntensityToVisuals(float normalizedIntensity)
        {
            if (rainController != null)
            {
                rainController.Intensity = normalizedIntensity;
            }

            if (groundSplashController != null)
            {
                groundSplashController.Intensity = Mathf.Clamp01(normalizedIntensity * groundSplashIntensityMultiplier);
            }

            RefreshWetAreaVisibility(normalizedIntensity > 0.001f || targetIntensity > 0.001f);
        }

        private void ApplyGroundSplashGeometry(Vector2 size, Vector3 localOffset)
        {
            if (groundSplashController == null)
            {
                return;
            }

            groundSplashController.SetAreaSize(size);
            groundSplashController.SetLocalEmitterOffset(localOffset);
            ApplyGroundSplashBlocker();
        }

        private void ApplyGroundSplashBlocker()
        {
            if (groundSplashController == null)
            {
                return;
            }

            bool hasRecentBlocker = Application.isPlaying
                && Time.time - lastGroundSplashBlockerTime <= groundSplashBlockerGraceTime
                && (currentIntensity > 0.001f || targetIntensity > 0.001f);

            if (!hasRecentBlocker)
            {
                groundSplashController.ClearWorldExclusionArea();
                return;
            }

            Vector2 blockerSize = new Vector2(
                groundSplashBlockerBounds.size.x + groundSplashBlockerPadding * 2f,
                groundSplashBlockerBounds.size.z + groundSplashBlockerPadding * 2f
            );

            groundSplashController.SetWorldExclusionArea(groundSplashBlockerBounds.center, blockerSize);
        }

        private void RefreshWetAreaVisual(bool hasBounds, Bounds bounds)
        {
            if (wetAreaVisual == null)
            {
                return;
            }

            if (syncWetAreaFromCollider && hasBounds)
            {
                Vector3 wetPosition = new Vector3(bounds.center.x, bounds.min.y + wetAreaHeightOffset, bounds.center.z);
                wetAreaVisual.transform.position = wetPosition;
                wetAreaVisual.transform.rotation = Quaternion.identity;

                Vector3 localScale = wetAreaVisual.transform.localScale;
                wetAreaVisual.transform.localScale = new Vector3(bounds.size.x, localScale.y, bounds.size.z);
            }

            RefreshWetAreaVisibility(currentIntensity > 0.001f || targetIntensity > 0.001f);
        }

        private void RefreshWetAreaVisibility(bool visible)
        {
            if (wetAreaVisual == null)
            {
                return;
            }

            if (wetAreaVisual.activeSelf != visible)
            {
                wetAreaVisual.SetActive(visible);
            }
        }

        // 비가 완전히 숨겨진 상태라면 Particle System을 멈춰 불필요한 생성을 줄인다.
        private void StopRainIfHidden()
        {
            if (!stopWhenHidden || rainController == null || targetIntensity > 0f || currentIntensity > 0.001f)
            {
                return;
            }

            rainController.Stop(true);

            if (groundSplashController != null)
            {
                groundSplashController.Stop(true);
                groundSplashController.ClearWorldExclusionArea();
            }

            RefreshWetAreaVisibility(false);
        }

        // Scene View에서 선택했을 때 Rain Zone의 시각 범위를 확인하기 위한 보조 표시다.
        private void OnDrawGizmosSelected()
        {
            CacheReferences();

            if (zoneCollider != null && syncAreaFromCollider)
            {
                Gizmos.color = new Color(0.35f, 0.65f, 1f, 0.25f);
                Gizmos.DrawCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
                Gizmos.color = new Color(0.35f, 0.65f, 1f, 0.95f);
                Gizmos.DrawWireCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
                return;
            }

            Gizmos.color = new Color(0.35f, 0.65f, 1f, 0.75f);
            Gizmos.DrawWireCube(transform.position, new Vector3(manualAreaSize.x, 1f, manualAreaSize.y));
        }
    }
}
