using UnityEngine;

namespace UmbrellaPuzzle.Weather
{
    // Rain Zone 바닥에서 짧게 튀는 물방울을 담당하는 보조 비주얼이다.
    // 게임플레이 판정은 하지 않고, RainZoneVisual이 상태와 영역을 제어한다.
    [DisallowMultipleComponent]
    public sealed class SimpleRainSplashController : MonoBehaviour
    {
        [Header("Playback")]

        [SerializeField, Tooltip("켜면 Start에서 자동으로 물튐을 재생합니다. RainZoneVisual이 제어하는 구역에서는 자동으로 꺼집니다.")]
        private bool playOnStart;

        [Header("Splash Shape")]

        [SerializeField, Tooltip("자식 Splash Particles 오브젝트의 로컬 위치입니다. RainZoneVisual은 Collider 바닥면 근처로 이 값을 자동 조정합니다.")]
        private Vector3 localEmitterOffset = Vector3.zero;

        [SerializeField, Tooltip("물튐이 생성되는 X/Z 영역 크기입니다. RainZoneVisual 사용 시 Collider 크기에서 자동으로 설정됩니다.")]
        private Vector2 areaSize = new Vector2(6f, 6f);

        [SerializeField, Range(0f, 1f), Tooltip("현재 물튐 강도입니다. 0은 물튐 없음, 1은 Max Emission Rate 전체 사용입니다.")]
        private float intensity = 0.35f;

        [SerializeField, Tooltip("Intensity가 1일 때 초당 생성되는 최대 물튐 파티클 수입니다.")]
        private float maxEmissionRate = 180f;

        [Header("Motion")]

        [SerializeField, Tooltip("물방울이 위로 튀는 최소/최대 속도입니다.")]
        private Vector2 upwardSpeedRange = new Vector2(0.25f, 1.1f);

        [SerializeField, Tooltip("물방울이 옆으로 퍼지는 속도 범위입니다.")]
        private float horizontalSpread = 0.35f;

        [SerializeField, Tooltip("물튐 파티클이 유지되는 시간입니다.")]
        private float lifetime = 0.32f;

        [Header("Look")]

        [SerializeField, Tooltip("물튐 파티클 렌더링에 사용할 머티리얼입니다. 비워두면 공유 런타임 머티리얼을 자동 생성합니다.")]
        private Material splashMaterial;

        [SerializeField, Tooltip("물튐 색과 투명도입니다.")]
        private Color splashColor = new Color(0.72f, 0.84f, 0.95f, 0.55f);

        [SerializeField, Tooltip("물튐 파티클 크기입니다.")]
        private float particleSize = 0.08f;

        private const string SplashObjectName = "Ground Splash Particles";

        private static Material sharedRuntimeMaterial;

        private ParticleSystem splashParticles;
        private ParticleSystemRenderer splashRenderer;

        public float Intensity
        {
            get => intensity;
            set
            {
                intensity = Mathf.Clamp01(value);
                ApplyIntensity();
            }
        }

        public void SetPlayOnStart(bool shouldPlayOnStart)
        {
            playOnStart = shouldPlayOnStart;
        }

        public void SetAreaSize(Vector2 size)
        {
            areaSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            ApplySettings();
        }

        public void SetLocalEmitterOffset(Vector3 offset)
        {
            localEmitterOffset = offset;

            if (splashParticles != null)
            {
                splashParticles.transform.localPosition = localEmitterOffset;
            }
        }

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        private void OnEnable()
        {
            if (splashParticles == null || splashRenderer == null)
            {
                return;
            }

            ApplySettings();

            if (playOnStart)
            {
                Play();
            }
        }

        private void OnValidate()
        {
            intensity = Mathf.Clamp01(intensity);
            areaSize.x = Mathf.Max(1f, areaSize.x);
            areaSize.y = Mathf.Max(1f, areaSize.y);
            maxEmissionRate = Mathf.Max(0f, maxEmissionRate);
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

        public void Play()
        {
            EnsureParticleSystem();
            ApplySettings();

            if (!splashParticles.isPlaying)
            {
                splashParticles.Play();
            }
        }

        public void Stop()
        {
            Stop(true);
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

        private void EnsureParticleSystem()
        {
            if (splashParticles != null && splashRenderer != null)
            {
                return;
            }

            Transform child = transform.Find(SplashObjectName);
            GameObject splashObject;

            if (child == null)
            {
                splashObject = new GameObject(SplashObjectName);
                splashObject.transform.SetParent(transform, false);
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
            emission.rateOverTime = maxEmissionRate * intensity;

            ParticleSystem.ShapeModule shape = splashParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(areaSize.x, 0.05f, areaSize.y);
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
                    new GradientAlphaKey(splashColor.a, 0.15f),
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
            emission.rateOverTime = maxEmissionRate * intensity;
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
                name = "Shared Runtime Rain Splash Material"
            };
            sharedRuntimeMaterial.SetColor("_BaseColor", Color.white);
            sharedRuntimeMaterial.SetColor("_Color", Color.white);

            return sharedRuntimeMaterial;
        }
    }
}
