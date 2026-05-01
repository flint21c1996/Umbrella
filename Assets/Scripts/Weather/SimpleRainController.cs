using UnityEngine;

namespace UmbrellaPuzzle.Weather
{
    // 특정 위치 또는 대상을 따라다니는 박스형 비 영역을 런타임 Particle System으로 구성한다.
    // 이 컴포넌트는 "보이는 비"만 담당하고, 퍼즐 판정이나 우산 충돌은 별도 컴포넌트에서 처리한다.
    [DisallowMultipleComponent]
    public sealed class SimpleRainController : MonoBehaviour
    {
        [Header("Follow")]

        // 값이 있으면 비 영역의 중심이 매 프레임 이 대상을 따라간다.
        // 비를 특정 위치에 고정하고 싶다면 비워둔다.
        [SerializeField, Tooltip("지정하면 이 Transform을 따라 비 영역이 이동합니다. 고정형 Rain Zone에서는 비워두세요.")]
        private Transform followTarget;

        // followTarget을 사용할 때 대상 위치에서 얼마나 위/옆으로 떨어진 곳에 비를 생성할지 정한다.
        // 일반적으로 Y 값을 높여 플레이어 또는 카메라 위에서 비가 시작되게 한다.
        [SerializeField, Tooltip("Follow Target 기준 비 영역의 위치 오프셋입니다. 보통 Y 값을 높여 대상 위에서 비가 시작되게 합니다.")]
        private Vector3 followOffset = new Vector3(0f, 12f, 0f);

        [Header("Playback")]

        // 단독 비 오브젝트로 쓸 때는 켜두면 Start에서 바로 재생된다.
        // RainZoneVisual이 제어하는 퍼즐 구역에서는 false로 바뀌어 상태 제어가 우선된다.
        [SerializeField, Tooltip("켜면 Start에서 자동으로 비를 재생합니다. RainZoneVisual이 붙은 퍼즐 구역에서는 자동으로 꺼집니다.")]
        private bool playOnStart = true;

        [Header("Rain Shape")]

        // RainZoneVisual이 Collider 높이를 기준으로 이 값을 조절하면,
        // 판정 구역의 위쪽에서 비가 시작되도록 만들 수 있다.
        [SerializeField, Tooltip("자식 Rain Particles 오브젝트의 로컬 위치입니다. RainZoneVisual은 Collider 윗면으로 이 값을 자동 조정합니다.")]
        private Vector3 localEmitterOffset = Vector3.zero;

        // 비가 생성되는 박스 영역의 X/Z 크기다.
        // 퍼즐 구역 비라면 BoxCollider 크기와 비슷하게 맞추면 시각 범위와 판정 범위가 읽기 쉽다.
        [SerializeField, Tooltip("비가 생성되는 X/Z 영역 크기입니다. RainZoneVisual 사용 시 Collider 크기에서 자동으로 설정됩니다.")]
        private Vector2 areaSize = new Vector2(24f, 24f);

        // 0이면 비가 멈추고, 1이면 maxEmissionRate만큼 생성된다.
        // 퍼즐 상태에 따라 비의 강약을 바꿀 때 외부에서 조절하기 좋은 값이다.
        [SerializeField, Range(0f, 1f), Tooltip("현재 비 강도입니다. 0은 비 없음, 1은 Max Emission Rate 전체 사용입니다.")]
        private float intensity = 0.65f;

        // intensity가 1일 때 초당 생성되는 최대 빗방울 수다.
        // 여러 Rain Zone을 동시에 켤 예정이면 이 값을 낮게 잡는 편이 안전하다.
        [SerializeField, Tooltip("Intensity가 1일 때 초당 생성되는 최대 빗방울 수입니다. 여러 구역이 있으면 낮게 유지하는 것이 좋습니다.")]
        private float maxEmissionRate = 1200f;

        // 빗방울이 아래로 떨어지는 속도다.
        // 값이 높을수록 빗줄기가 빠르고 강하게 보인다.
        [SerializeField, Tooltip("빗방울이 아래로 떨어지는 속도입니다. 높을수록 강한 비처럼 보입니다.")]
        private float fallSpeed = 18f;

        // 빗방울에 적용할 수평 이동 속도다.
        // x는 월드 X축, y는 월드 Z축 방향 바람으로 사용한다.
        [SerializeField, Tooltip("빗방울의 수평 이동 속도입니다. X는 월드 X축, Y는 월드 Z축 방향으로 적용됩니다.")]
        private Vector2 wind = new Vector2(0.8f, 0f);

        [Header("Look")]

        // 여러 Rain Zone이 같은 머티리얼을 공유하게 하고 싶을 때 지정한다.
        // 비워두면 모든 SimpleRainController가 공유하는 런타임 머티리얼을 자동 생성한다.
        [SerializeField, Tooltip("비 파티클 렌더링에 사용할 머티리얼입니다. 비워두면 공유 런타임 머티리얼을 자동 생성합니다.")]
        private Material rainMaterial;

        // 빗방울의 색과 투명도다.
        // 알파가 높을수록 비가 선명하지만, 여러 투명 파티클이 겹치면 GPU 비용이 늘 수 있다.
        [SerializeField, Tooltip("빗방울 색과 투명도입니다. 알파가 높을수록 잘 보이지만 투명 파티클 비용이 늘 수 있습니다.")]
        private Color rainColor = new Color(0.68f, 0.78f, 0.9f, 0.55f);

        // 개별 빗방울의 가로 폭이다.
        // 너무 크면 비가 굵은 선처럼 보이고, 너무 작으면 멀리서 잘 보이지 않을 수 있다.
        [SerializeField, Tooltip("개별 빗방울의 폭입니다. 너무 크면 굵은 선처럼 보이고 너무 작으면 잘 보이지 않을 수 있습니다.")]
        private float dropWidth = 0.035f;

        // 개별 빗방울이 늘어난 세로 길이다.
        // Stretch 렌더링과 함께 사용되어 빠르게 떨어지는 빗줄기처럼 보이게 한다.
        [SerializeField, Tooltip("개별 빗줄기의 길이입니다. Stretch 렌더링과 함께 빠르게 떨어지는 비처럼 보이게 합니다.")]
        private float streakLength = 1.35f;

        // 자동 생성되는 자식 오브젝트 이름이다.
        // 이미 같은 이름의 자식이 있으면 새로 만들지 않고 재사용한다.
        private const string RainObjectName = "Rain Particles";

        // 실제 비 파티클을 시뮬레이션하는 Unity Particle System 참조다.
        private ParticleSystem rainParticles;

        // rainParticles를 화면에 그리는 렌더러 참조다.
        private ParticleSystemRenderer rainRenderer;

        // 전용 머티리얼 에셋이 없을 때 런타임에 생성해 사용하는 머티리얼이다.
        private static Material sharedRuntimeMaterial;

        // 외부 스크립트가 비 강도를 제어할 수 있는 진입점이다.
        // 값은 항상 0~1 사이로 제한되고, 변경 즉시 Particle System 설정에 반영된다.
        public float Intensity
        {
            get => intensity;
            set
            {
                intensity = Mathf.Clamp01(value);
                ApplyIntensity();
            }
        }

        // 런타임에 비가 따라갈 대상을 바꾼다.
        // 고정 위치 비로 되돌리고 싶으면 null을 넘긴다.
        // 현재 비 생성 영역의 X/Z 크기다.
        public Vector2 AreaSize => areaSize;

        // 현재 비 파티클이 생성되는 로컬 오프셋이다.
        public Vector3 LocalEmitterOffset => localEmitterOffset;

        // 자식 Particle System이 실제로 준비되었는지 확인할 때 사용한다.
        public bool IsReady => rainParticles != null && rainRenderer != null;

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
        }

        // RainZoneVisual처럼 외부 상태 관리자가 비 재생을 제어할 때 자동 재생을 끈다.
        public void SetPlayOnStart(bool shouldPlayOnStart)
        {
            playOnStart = shouldPlayOnStart;
        }

        // Rain Zone의 Collider 크기나 수동 설정값을 비 생성 영역에 반영한다.
        public void SetAreaSize(Vector2 size)
        {
            areaSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            ApplySettings();
        }

        // 비 파티클이 실제로 생성되는 자식 오브젝트 위치를 바꾼다.
        // Rain Zone에서는 보통 Collider의 윗면보다 조금 위로 올려서 사용한다.
        public void SetLocalEmitterOffset(Vector3 offset)
        {
            localEmitterOffset = offset;

            if (rainParticles != null)
            {
                rainParticles.transform.localPosition = localEmitterOffset;
            }
        }

        // 비활성화되었다가 다시 켜졌을 때 이미 준비된 Particle System만 다시 적용하고 재생한다.
        // 아직 Start 전이라 자식이 없다면 여기서는 생성하지 않는다.
        private void OnEnable()
        {
            if (rainParticles == null || rainRenderer == null)
            {
                return;
            }

            ApplySettings();

            if (playOnStart)
            {
                Play();
            }
        }

        // 모든 Awake가 끝난 뒤 자식 Particle System을 만들고 비를 재생한다.
        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        // followTarget이 있으면 모든 Update 이후에 비 영역 위치를 대상 주변으로 맞춘다.
        // LateUpdate를 사용해 플레이어/카메라 이동이 끝난 뒤 따라가도록 한다.
        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            transform.position = followTarget.position + followOffset;
        }

        // Inspector 값이 잘못된 범위로 들어가지 않도록 보정한다.
        // Play Mode 중에는 변경된 값을 즉시 Particle System에 다시 적용한다.
        private void OnValidate()
        {
            intensity = Mathf.Clamp01(intensity);
            areaSize.x = Mathf.Max(1f, areaSize.x);
            areaSize.y = Mathf.Max(1f, areaSize.y);
            maxEmissionRate = Mathf.Max(0f, maxEmissionRate);
            fallSpeed = Mathf.Max(0.1f, fallSpeed);
            dropWidth = Mathf.Max(0.001f, dropWidth);
            streakLength = Mathf.Max(0.01f, streakLength);

            if (!Application.isPlaying)
            {
                return;
            }

            if (rainParticles != null && rainRenderer != null)
            {
                ApplySettings();
            }
        }

        // 비 파티클 생성을 시작한다.
        // 이미 재생 중이면 중복 호출해도 아무 일도 하지 않는다.
        public void Play()
        {
            EnsureParticleSystem();
            ApplySettings();

            if (!rainParticles.isPlaying)
            {
                rainParticles.Play();
            }
        }

        // 새 빗방울 생성을 멈춘다.
        // 이미 생성된 파티클은 Particle System의 StopEmitting 동작에 따라 정리된다.
        public void Stop()
        {
            Stop(true);
        }

        // clearParticles가 true면 남아 있는 빗방울까지 즉시 지운다.
        // Rain Zone이 Inactive/Solved로 바뀔 때 시각적으로 바로 꺼지게 하기 위해 사용한다.
        public void Stop(bool clearParticles)
        {
            if (rainParticles == null)
            {
                return;
            }

            ParticleSystemStopBehavior stopBehavior = clearParticles
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            rainParticles.Stop(true, stopBehavior);
        }

        // 자식 Particle System과 Renderer를 확보한다.
        // 프리팹에 미리 만들어 둔 자식이 있으면 재사용하고, 없으면 런타임에 생성한다.
        private void EnsureParticleSystem()
        {
            if (rainParticles != null && rainRenderer != null)
            {
                return;
            }

            Transform child = transform.Find(RainObjectName);
            GameObject rainObject;

            if (child == null)
            {
                rainObject = new GameObject(RainObjectName);
                rainObject.transform.SetParent(transform, false);
            }
            else
            {
                rainObject = child.gameObject;
            }

            rainObject.transform.localPosition = localEmitterOffset;
            rainObject.transform.localRotation = Quaternion.identity;

            rainParticles = rainObject.GetComponent<ParticleSystem>();
            if (rainParticles == null)
            {
                rainParticles = rainObject.AddComponent<ParticleSystem>();
            }

            rainRenderer = rainObject.GetComponent<ParticleSystemRenderer>();
            if (rainRenderer == null)
            {
                rainRenderer = rainObject.AddComponent<ParticleSystemRenderer>();
            }
        }

        // Inspector 속성들을 Unity Particle System 모듈 설정으로 변환해 적용한다.
        // 이 메서드가 현재 비의 밀도, 영역, 속도, 색, 렌더링 방식을 실제로 결정한다.
        private void ApplySettings()
        {
            if (rainParticles == null || rainRenderer == null)
            {
                return;
            }

            rainParticles.transform.localPosition = localEmitterOffset;
            rainParticles.transform.localRotation = Quaternion.identity;

            ParticleSystem.MainModule main = rainParticles.main;
            main.loop = true;

            // Unity는 Particle System이 재생 중일 때 duration 변경을 허용하지 않는다.
            // duration은 고정값이므로 시스템이 완전히 멈춰 있을 때만 다시 적용한다.
            if (rainParticles.isStopped)
            {
                main.duration = 4f;
            }

            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.CeilToInt(maxEmissionRate * 2f);
            main.startLifetime = 1.6f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = dropWidth;
            main.startSizeY = streakLength;
            main.startSizeZ = dropWidth;
            main.startColor = rainColor;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = rainParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = maxEmissionRate * intensity;

            ParticleSystem.ShapeModule shape = rainParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(areaSize.x, 0.1f, areaSize.y);
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            ParticleSystem.VelocityOverLifetimeModule velocity = rainParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = wind.x;
            velocity.y = -fallSpeed;
            velocity.z = wind.y;

            ParticleSystem.ColorOverLifetimeModule color = rainParticles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(rainColor, 0f),
                    new GradientColorKey(rainColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(rainColor.a, 0.08f),
                    new GradientAlphaKey(rainColor.a, 0.86f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;

            ParticleSystem.NoiseModule noise = rainParticles.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.45f;

            ParticleSystem.CollisionModule collision = rainParticles.collision;
            collision.enabled = false;

            rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            rainRenderer.cameraVelocityScale = 0f;
            rainRenderer.velocityScale = 0.055f;
            rainRenderer.lengthScale = 1f;
            rainRenderer.normalDirection = 0f;
            rainRenderer.sortMode = ParticleSystemSortMode.None;
            rainRenderer.sharedMaterial = GetOrCreateRainMaterial();
        }

        // 비 강도만 바뀌었을 때는 전체 Particle System 설정을 다시 쓰지 않고,
        // 초당 방울 생성량만 갱신한다. 재생 중 duration 변경 경고와 불필요한 모듈 갱신을 피하기 위함이다.
        private void ApplyIntensity()
        {
            if (rainParticles == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = rainParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = maxEmissionRate * intensity;
        }

        // 비 렌더링에 사용할 머티리얼을 가져오거나 만든다.
        // URP 파티클 셰이더를 우선 사용하고, 없으면 기본 파티클/스프라이트 셰이더로 대체한다.
        private Material GetOrCreateRainMaterial()
        {
            if (rainMaterial != null)
            {
                return rainMaterial;
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
                name = "Shared Runtime Rain Material"
            };
            sharedRuntimeMaterial.SetColor("_BaseColor", Color.white);
            sharedRuntimeMaterial.SetColor("_Color", Color.white);

            return sharedRuntimeMaterial;
        }
    }
}
