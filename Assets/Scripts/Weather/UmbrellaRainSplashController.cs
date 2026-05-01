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

        [SerializeField, Tooltip("물튐 파티클이 생성될 기준 Transform입니다. 비워두면 Open Visual, 없으면 이 GameObject를 사용합니다.")]
        private Transform splashOrigin;

        [Header("Shape")]

        [SerializeField, Tooltip("우산 위 물튐이 퍼지는 X/Z 영역 크기입니다.")]
        private Vector2 splashAreaSize = new Vector2(1.6f, 1.6f);

        [SerializeField, Tooltip("Splash Origin 기준 물튐 파티클 로컬 위치 오프셋입니다.")]
        private Vector3 localEmitterOffset = new Vector3(0f, 0.08f, 0f);

        [Header("Intensity")]

        [SerializeField, Tooltip("RainArea가 전달한 초당 비 노출량을 파티클 강도로 바꾸는 배율입니다.")]
        private float rainRateToIntensity = 0.75f;

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

        private static Material sharedRuntimeMaterial;

        private ParticleSystem splashParticles;
        private ParticleSystemRenderer splashRenderer;
        private float lastBlockedRainTime = float.NegativeInfinity;
        private float targetIntensity;

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

            if (umbrellaController != null)
            {
                umbrellaController.RainBlocked -= OnRainBlocked;
                umbrellaController.RainBlocked += OnRainBlocked;
            }
        }

        private void OnDisable()
        {
            if (umbrellaController != null)
            {
                umbrellaController.RainBlocked -= OnRainBlocked;
            }

            Stop(true);
        }

        private void Start()
        {
            EnsureParticleSystem();
            ApplySettings();
            Stop(true);
        }

        private void OnValidate()
        {
            splashAreaSize.x = Mathf.Max(0.05f, splashAreaSize.x);
            splashAreaSize.y = Mathf.Max(0.05f, splashAreaSize.y);
            rainRateToIntensity = Mathf.Max(0f, rainRateToIntensity);
            maxEmissionRate = Mathf.Max(0f, maxEmissionRate);
            blockedRainGraceTime = Mathf.Max(0.01f, blockedRainGraceTime);
            fadeOutSpeed = Mathf.Max(0.01f, fadeOutSpeed);
            upwardSpeedRange.x = Mathf.Max(0f, upwardSpeedRange.x);
            upwardSpeedRange.y = Mathf.Max(upwardSpeedRange.x, upwardSpeedRange.y);
            horizontalSpread = Mathf.Max(0f, horizontalSpread);
            lifetime = Mathf.Max(0.05f, lifetime);
            particleSize = Mathf.Max(0.001f, particleSize);

            if (!Application.isPlaying || splashParticles == null || splashRenderer == null)
            {
                return;
            }

            ApplySettings();
        }

        private void Update()
        {
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

            if (splashParticles != null)
            {
                splashParticles.transform.SetParent(GetEmitterParent(), false);
                splashParticles.transform.localPosition = localEmitterOffset;
                splashParticles.transform.localRotation = Quaternion.identity;
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

        private void OnRainBlocked(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float rainRate = amount / deltaTime;
            targetIntensity = Mathf.Clamp01(rainRate * rainRateToIntensity);
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

            if (splashOrigin == null && umbrellaController != null && umbrellaController.openVisual != null)
            {
                splashOrigin = umbrellaController.openVisual.transform;
            }
        }

        private Transform GetEmitterParent()
        {
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

            splashObject.transform.localPosition = localEmitterOffset;
            splashObject.transform.localRotation = Quaternion.identity;

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
        }

        private void ApplySettings()
        {
            if (splashParticles == null || splashRenderer == null)
            {
                return;
            }

            splashParticles.transform.localPosition = localEmitterOffset;
            splashParticles.transform.localRotation = Quaternion.identity;

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
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = splashParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = maxEmissionRate * currentIntensity;

            ParticleSystem.ShapeModule shape = splashParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(splashAreaSize.x, 0.04f, splashAreaSize.y);
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
