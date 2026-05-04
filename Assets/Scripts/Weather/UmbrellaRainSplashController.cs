using UnityEngine;

namespace UmbrellaPuzzle.Weather
{
    // 플레이어 우산이 비를 막고 있을 때 우산 위에서 물이 튀는 시각 효과를 담당한다.
    // PlayerUmbrellaController의 RainBlocked 이벤트를 받아 파티클 강도를 갱신한다.
    [DisallowMultipleComponent]
    public sealed class UmbrellaRainSplashController : MonoBehaviour
    {
        [Header("References")]

        [SerializeField, Tooltip("비 차단 이벤트를 제공하는 플레이어 우산 컨트롤러입니다. 비워두면 부모에서 자동으로 찾습니다.")]
        private PlayerUmbrellaController umbrellaController;

        [SerializeField, Tooltip("우산 빗물 차단 Collider를 만드는 컴포넌트입니다. 비워두면 부모에서 자동으로 찾고, 이 Collider 위치를 우산 물튐 기준으로 사용합니다.")]
        private UmbrellaRainBlocker rainBlocker;

        [SerializeField, Tooltip("켜면 Splash Origin 대신 UmbrellaRainBlocker가 만든 실제 차단 Collider 위치에 물튐 파티클을 붙입니다.")]
        private bool useRainBlockerAsOrigin = true;

        [SerializeField, Tooltip("켜면 우산 물튐 방출 영역 X/Z 크기를 Rain Blocker Collider 크기와 맞춥니다.")]
        private bool syncAreaFromRainBlocker = true;

        [SerializeField, Tooltip("물튐 파티클이 생성될 기준 Transform입니다. 비워두면 Open Visual, 없으면 이 GameObject를 사용합니다.")]
        private Transform splashOrigin;

        [Header("Shape")]

        [SerializeField, Tooltip("우산 위 물튐이 퍼지는 X/Z 영역 크기입니다.")]
        private Vector2 splashAreaSize = new Vector2(1.6f, 1.6f);

        [SerializeField, Tooltip("Splash Origin 기준 물튐 파티클 로컬 위치 오프셋입니다.")]
        private Vector3 localEmitterOffset = new Vector3(0f, 0.08f, 0f);

        [Header("Intensity")]

        [SerializeField, Range(0f, 1f), Tooltip("RainArea 안에서 열린 우산이 비를 막고 있을 때 적용할 우산 물튐 강도입니다.")]
        private float blockedRainSplashIntensity = 0.65f;

        [SerializeField, Tooltip("Intensity가 1일 때 초당 생성되는 최대 우산 물튐 파티클 수입니다.")]
        private float maxEmissionRate = 260f;

        [SerializeField, Tooltip("비 차단 이벤트가 잠깐 끊겨도 물튐을 유지하는 시간입니다.")]
        private float blockedRainGraceTime = 0.12f;

        [SerializeField, Tooltip("비 차단이 끝났을 때 물튐 강도가 줄어드는 속도입니다.")]
        private float fadeOutSpeed = 5f;

        [Header("Motion")]

        [SerializeField, Tooltip("물방울이 위로 튀는 최소/최대 속도입니다.")]
        private Vector2 upwardSpeedRange = new Vector2(0.4f, 1.4f);

        [SerializeField, Tooltip("물방울이 옆으로 퍼지는 속도 범위입니다.")]
        private float horizontalSpread = 0.75f;

        [SerializeField, Tooltip("물튐 파티클이 유지되는 시간입니다.")]
        private float lifetime = 0.35f;

        [SerializeField, Tooltip("우산 물튐 파티클에 적용할 중력 배율 범위입니다. 생성되는 Particle System의 Gravity Modifier를 Random Between Two Constants로 설정합니다.")]
        private Vector2 gravityModifierRange = new Vector2(0.3f, 1f);

        [Header("Look")]

        [SerializeField, Tooltip("우산 물튐 파티클 렌더링에 사용할 머티리얼입니다. 비워두면 공유 런타임 머티리얼을 자동 생성합니다.")]
        private Material splashMaterial;

        [SerializeField, Tooltip("물튐 색과 투명도입니다.")]
        private Color splashColor = new Color(0.72f, 0.86f, 1f, 0.62f);

        [SerializeField, Tooltip("개별 물튐 파티클 크기입니다.")]
        private float particleSize = 0.075f;

        [Header("Runtime")]

        [SerializeField, Tooltip("현재 우산 물튐 강도입니다.")]
        private float currentIntensity;

        private const string SplashObjectName = "Umbrella Splash Particles";
        private const float DefaultBlockedRainSplashIntensity = 0.65f;
        private const float MinGravityModifier = 0.3f;
        private const float MaxGravityModifier = 1f;

        private static Material sharedRuntimeMaterial;

        private ParticleSystem splashParticles;
        private ParticleSystemRenderer splashRenderer;
        private PlayerUmbrellaController subscribedUmbrellaController;
        private float lastBlockedRainTime = float.NegativeInfinity;
        private float targetIntensity;
        private bool pendingSettingsApply;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            SubscribeUmbrellaController();
        }

        private void OnDisable()
        {
            UnsubscribeUmbrellaController();
            Stop(true);
        }

        private void Start()
        {
            CacheReferences();
            SubscribeUmbrellaController();
            EnsureParticleSystem();
            ApplySettings();
            Stop(true);
        }

        private void OnValidate()
        {
            splashAreaSize.x = Mathf.Max(0.05f, splashAreaSize.x);
            splashAreaSize.y = Mathf.Max(0.05f, splashAreaSize.y);
            blockedRainSplashIntensity = Mathf.Clamp01(blockedRainSplashIntensity);
            maxEmissionRate = Mathf.Max(0f, maxEmissionRate);
            blockedRainGraceTime = Mathf.Max(0.01f, blockedRainGraceTime);
            fadeOutSpeed = Mathf.Max(0.01f, fadeOutSpeed);
            upwardSpeedRange.x = Mathf.Max(0f, upwardSpeedRange.x);
            upwardSpeedRange.y = Mathf.Max(upwardSpeedRange.x, upwardSpeedRange.y);
            horizontalSpread = Mathf.Max(0f, horizontalSpread);
            lifetime = Mathf.Max(0.05f, lifetime);
            gravityModifierRange = ClampGravityModifierRange(gravityModifierRange);
            particleSize = Mathf.Max(0.001f, particleSize);

            if (!Application.isPlaying)
            {
                return;
            }

            pendingSettingsApply = true;
        }

        private void Update()
        {
            if (pendingSettingsApply)
            {
                pendingSettingsApply = false;
                CacheReferences();
                SubscribeUmbrellaController();
                ApplySettings();
            }

            bool isStillBlockingRain = Time.time - lastBlockedRainTime <= blockedRainGraceTime;
            bool canShowSplash = umbrellaController == null || umbrellaController.IsOpen;

            if (!isStillBlockingRain || !canShowSplash)
            {
                targetIntensity = 0f;
            }

            currentIntensity = Mathf.MoveTowards(
                currentIntensity,
                targetIntensity,
                fadeOutSpeed * Time.deltaTime
            );

            ApplyIntensity();

            if (currentIntensity <= 0.001f && targetIntensity <= 0.001f)
            {
                Stop(true);
            }
        }

        public void SetSplashOrigin(Transform origin)
        {
            splashOrigin = origin;
            useRainBlockerAsOrigin = false;

            if (splashParticles != null)
            {
                RefreshParticleTransform();
            }
        }

        public void Play()
        {
            EnsureParticleSystem();
            ApplySettings();

            if (!splashParticles.isPlaying)
            {
                splashParticles.Play();
            }
        }

        public void Stop(bool clearParticles)
        {
            if (splashParticles == null)
            {
                return;
            }

            ParticleSystemStopBehavior stopBehavior = clearParticles
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            splashParticles.Stop(true, stopBehavior);
        }

        public void ShowBlockedRainSplash()
        {
            CacheReferences();
            ActivateSplash(GetBlockedRainSplashIntensity());
        }

        private void OnRainBlocked(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            ActivateSplash(GetBlockedRainSplashIntensity());
        }

        private float GetBlockedRainSplashIntensity()
        {
            return blockedRainSplashIntensity > 0.001f
                ? blockedRainSplashIntensity
                : DefaultBlockedRainSplashIntensity;
        }

        private void ActivateSplash(float normalizedIntensity)
        {
            targetIntensity = Mathf.Clamp01(Mathf.Max(targetIntensity, normalizedIntensity));
            currentIntensity = Mathf.Clamp01(Mathf.Max(currentIntensity, targetIntensity));
            lastBlockedRainTime = Time.time;

            if (targetIntensity > 0f)
            {
                Play();
            }
        }

        private void CacheReferences()
        {
            if (umbrellaController == null)
            {
                umbrellaController = GetComponentInParent<PlayerUmbrellaController>();
            }

            if (rainBlocker == null)
            {
                rainBlocker = GetComponentInParent<UmbrellaRainBlocker>();
            }

            if (rainBlocker == null && umbrellaController != null)
            {
                rainBlocker = umbrellaController.GetComponentInChildren<UmbrellaRainBlocker>();
            }

            if (rainBlocker == null)
            {
                rainBlocker = GetComponentInChildren<UmbrellaRainBlocker>();
            }

            if (umbrellaController == null && rainBlocker != null)
            {
                umbrellaController = rainBlocker.UmbrellaController;
            }

            if (splashOrigin == null && umbrellaController != null && umbrellaController.openVisual != null)
            {
                splashOrigin = umbrellaController.openVisual.transform;
            }
        }

        private void SubscribeUmbrellaController()
        {
            if (subscribedUmbrellaController == umbrellaController)
            {
                return;
            }

            UnsubscribeUmbrellaController();

            if (umbrellaController == null)
            {
                return;
            }

            subscribedUmbrellaController = umbrellaController;
            subscribedUmbrellaController.RainBlocked += OnRainBlocked;
        }

        private void UnsubscribeUmbrellaController()
        {
            if (subscribedUmbrellaController == null)
            {
                return;
            }

            subscribedUmbrellaController.RainBlocked -= OnRainBlocked;
            subscribedUmbrellaController = null;
        }

        private Transform GetEmitterParent()
        {
            if (useRainBlockerAsOrigin && rainBlocker != null && rainBlocker.BlockerTransform != null)
            {
                return rainBlocker.BlockerTransform;
            }

            if (splashOrigin != null)
            {
                return splashOrigin;
            }

            return transform;
        }

        private void EnsureParticleSystem()
        {
            if (splashParticles != null && splashRenderer != null)
            {
                return;
            }

            Transform parent = GetEmitterParent();
            Transform child = parent.Find(SplashObjectName);
            GameObject splashObject;

            if (child == null)
            {
                splashObject = new GameObject(SplashObjectName);
                splashObject.transform.SetParent(parent, false);
            }
            else
            {
                splashObject = child.gameObject;
            }

            splashParticles = splashObject.GetComponent<ParticleSystem>();
            if (splashParticles == null)
            {
                splashParticles = splashObject.AddComponent<ParticleSystem>();
            }

            splashRenderer = splashObject.GetComponent<ParticleSystemRenderer>();
            if (splashRenderer == null)
            {
                splashRenderer = splashObject.AddComponent<ParticleSystemRenderer>();
            }

            RefreshParticleTransform();
        }

        private void ApplySettings()
        {
            if (splashParticles == null || splashRenderer == null)
            {
                return;
            }

            RefreshParticleTransform();

            ParticleSystem.MainModule main = splashParticles.main;
            main.loop = true;

            if (splashParticles.isStopped)
            {
                main.duration = 4f;
            }

            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.CeilToInt(maxEmissionRate * Mathf.Max(1f, lifetime * 2f));
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.startSize = particleSize;
            main.startColor = splashColor;
            Vector2 resolvedGravityModifierRange = ClampGravityModifierRange(gravityModifierRange);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(
                resolvedGravityModifierRange.x,
                resolvedGravityModifierRange.y
            );
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = splashParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = maxEmissionRate * currentIntensity;

            ParticleSystem.ShapeModule shape = splashParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            Vector2 resolvedAreaSize = ResolveSplashAreaSize();
            shape.scale = new Vector3(resolvedAreaSize.x, 0.04f, resolvedAreaSize.y);
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            ParticleSystem.VelocityOverLifetimeModule velocity = splashParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-horizontalSpread, horizontalSpread);
            velocity.y = new ParticleSystem.MinMaxCurve(upwardSpeedRange.x, upwardSpeedRange.y);
            velocity.z = new ParticleSystem.MinMaxCurve(-horizontalSpread, horizontalSpread);

            ParticleSystem.ColorOverLifetimeModule color = splashParticles.colorOverLifetime;
            color.enabled = true;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(splashColor, 0f),
                    new GradientColorKey(splashColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(splashColor.a, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;

            splashRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            splashRenderer.normalDirection = 0f;
            splashRenderer.sortMode = ParticleSystemSortMode.None;
            splashRenderer.sharedMaterial = GetOrCreateSplashMaterial();
        }

        private Vector2 ResolveSplashAreaSize()
        {
            if (syncAreaFromRainBlocker && rainBlocker != null && rainBlocker.BlockerSize != Vector3.zero)
            {
                Vector3 blockerSize = rainBlocker.BlockerSize;
                return new Vector2(blockerSize.x, blockerSize.z);
            }

            return splashAreaSize;
        }

        private void RefreshParticleTransform()
        {
            if (splashParticles == null)
            {
                return;
            }

            Transform parent = GetEmitterParent();
            Transform particleTransform = splashParticles.transform;

            if (particleTransform.parent != parent)
            {
                particleTransform.SetParent(parent, false);
            }

            particleTransform.localPosition = useRainBlockerAsOrigin && rainBlocker != null && rainBlocker.BlockerTransform != null
                ? Vector3.zero
                : localEmitterOffset;
            particleTransform.localRotation = Quaternion.identity;
        }

        private void ApplyIntensity()
        {
            if (splashParticles == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = splashParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = maxEmissionRate * currentIntensity;
        }

        private Vector2 ClampGravityModifierRange(Vector2 range)
        {
            float min = Mathf.Clamp(range.x, MinGravityModifier, MaxGravityModifier);
            float max = Mathf.Clamp(range.y, MinGravityModifier, MaxGravityModifier);

            if (max < min)
            {
                max = min;
            }

            return new Vector2(min, max);
        }

        private Material GetOrCreateSplashMaterial()
        {
            if (splashMaterial != null)
            {
                return splashMaterial;
            }

            if (sharedRuntimeMaterial != null)
            {
                return sharedRuntimeMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            sharedRuntimeMaterial = new Material(shader)
            {
                name = "Shared Runtime Umbrella Splash Material"
            };
            sharedRuntimeMaterial.SetColor("_BaseColor", Color.white);
            sharedRuntimeMaterial.SetColor("_Color", Color.white);

            return sharedRuntimeMaterial;
        }
    }
}
