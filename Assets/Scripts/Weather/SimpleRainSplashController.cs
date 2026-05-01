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
        private const string SegmentObjectPrefix = "Ground Splash Segment ";
        private const int SegmentCount = 4;
        private const float MinSegmentSize = 0.05f;

        private static Material sharedRuntimeMaterial;

        private ParticleSystem splashParticles;
        private ParticleSystemRenderer splashRenderer;
        private ParticleSystem[] segmentParticles;
        private ParticleSystemRenderer[] segmentRenderers;
        private bool hasWorldExclusionArea;
        private Vector3 worldExclusionCenter;
        private Vector2 worldExclusionSize;
        private bool pendingSettingsApply;
        private readonly Vector3[] segmentLocalPositions = new Vector3[SegmentCount];
        private readonly Vector2[] segmentSizes = new Vector2[SegmentCount];
        private readonly float[] segmentEmissionWeights = new float[SegmentCount];

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

            if (hasWorldExclusionArea)
            {
                ApplySettings();
                return;
            }

            if (splashParticles != null)
            {
                splashParticles.transform.localPosition = localEmitterOffset;
            }
        }

        public void SetWorldExclusionArea(Vector3 worldCenter, Vector2 worldSize)
        {
            hasWorldExclusionArea = worldSize.x > 0.01f && worldSize.y > 0.01f;
            worldExclusionCenter = worldCenter;
            worldExclusionSize = new Vector2(Mathf.Max(0f, worldSize.x), Mathf.Max(0f, worldSize.y));
            ApplySettings();
        }

        public void ClearWorldExclusionArea()
        {
            if (!hasWorldExclusionArea)
            {
                return;
            }

            hasWorldExclusionArea = false;
            ApplySettings();

            if (Application.isPlaying && intensity > 0.001f && splashParticles != null && !splashParticles.isPlaying)
            {
                splashParticles.Play();
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

            pendingSettingsApply = true;
            ApplyIntensity();
        }

        private void Update()
        {
            if (!pendingSettingsApply)
            {
                return;
            }

            pendingSettingsApply = false;
            ApplySettings();
        }

        public void Play()
        {
            EnsureParticleSystem();
            ApplySettings();

            if (hasWorldExclusionArea)
            {
                PlaySegmentParticles();
                return;
            }

            StopSegmentParticles(true);

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
            StopSegmentParticles(clearParticles);
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

            if (hasWorldExclusionArea)
            {
                int segmentCount = BuildVisibleSegments();
                ConfigureParticleSystem(splashParticles, splashRenderer, localEmitterOffset, areaSize, 0f);
                splashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                EnsureSegmentSystems();
                for (int i = 0; i < SegmentCount; i++)
                {
                    if (i >= segmentCount)
                    {
                        StopSegmentParticle(i, true);
                        continue;
                    }

                    ConfigureParticleSystem(
                        segmentParticles[i],
                        segmentRenderers[i],
                        segmentLocalPositions[i],
                        segmentSizes[i],
                        segmentEmissionWeights[i]
                    );

                    if (intensity > 0.001f && !segmentParticles[i].isPlaying)
                    {
                        segmentParticles[i].Play();
                    }
                }

                return;
            }

            StopSegmentParticles(true);
            ConfigureParticleSystem(splashParticles, splashRenderer, localEmitterOffset, areaSize, 1f);
        }

        private int BuildVisibleSegments()
        {
            Vector3 areaCenter = transform.TransformPoint(localEmitterOffset);
            float areaMinX = areaCenter.x - areaSize.x * 0.5f;
            float areaMaxX = areaCenter.x + areaSize.x * 0.5f;
            float areaMinZ = areaCenter.z - areaSize.y * 0.5f;
            float areaMaxZ = areaCenter.z + areaSize.y * 0.5f;

            float exclusionMinX = worldExclusionCenter.x - worldExclusionSize.x * 0.5f;
            float exclusionMaxX = worldExclusionCenter.x + worldExclusionSize.x * 0.5f;
            float exclusionMinZ = worldExclusionCenter.z - worldExclusionSize.y * 0.5f;
            float exclusionMaxZ = worldExclusionCenter.z + worldExclusionSize.y * 0.5f;

            float overlapMinX = Mathf.Max(areaMinX, exclusionMinX);
            float overlapMaxX = Mathf.Min(areaMaxX, exclusionMaxX);
            float overlapMinZ = Mathf.Max(areaMinZ, exclusionMinZ);
            float overlapMaxZ = Mathf.Min(areaMaxZ, exclusionMaxZ);

            if (overlapMinX >= overlapMaxX || overlapMinZ >= overlapMaxZ)
            {
                segmentLocalPositions[0] = localEmitterOffset;
                segmentSizes[0] = areaSize;
                segmentEmissionWeights[0] = 1f;
                return 1;
            }

            float totalArea = Mathf.Max(0.01f, areaSize.x * areaSize.y);
            int segmentIndex = 0;

            TryAddSegment(areaMinX, overlapMinX, areaMinZ, areaMaxZ, areaCenter.y, totalArea, ref segmentIndex);
            TryAddSegment(overlapMaxX, areaMaxX, areaMinZ, areaMaxZ, areaCenter.y, totalArea, ref segmentIndex);
            TryAddSegment(overlapMinX, overlapMaxX, areaMinZ, overlapMinZ, areaCenter.y, totalArea, ref segmentIndex);
            TryAddSegment(overlapMinX, overlapMaxX, overlapMaxZ, areaMaxZ, areaCenter.y, totalArea, ref segmentIndex);

            return segmentIndex;
        }

        private void TryAddSegment(
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            float worldY,
            float totalArea,
            ref int segmentIndex
        )
        {
            if (segmentIndex >= SegmentCount)
            {
                return;
            }

            float width = maxX - minX;
            float depth = maxZ - minZ;

            if (width < MinSegmentSize || depth < MinSegmentSize)
            {
                return;
            }

            Vector3 worldCenter = new Vector3((minX + maxX) * 0.5f, worldY, (minZ + maxZ) * 0.5f);
            segmentLocalPositions[segmentIndex] = transform.InverseTransformPoint(worldCenter);
            segmentSizes[segmentIndex] = new Vector2(width, depth);
            segmentEmissionWeights[segmentIndex] = Mathf.Clamp01(width * depth / totalArea);
            segmentIndex++;
        }

        private void EnsureSegmentSystems()
        {
            if (segmentParticles == null || segmentParticles.Length != SegmentCount)
            {
                segmentParticles = new ParticleSystem[SegmentCount];
                segmentRenderers = new ParticleSystemRenderer[SegmentCount];
            }

            for (int i = 0; i < SegmentCount; i++)
            {
                if (segmentParticles[i] != null && segmentRenderers[i] != null)
                {
                    continue;
                }

                string segmentName = SegmentObjectPrefix + (i + 1);
                Transform child = transform.Find(segmentName);
                GameObject segmentObject;

                if (child == null)
                {
                    segmentObject = new GameObject(segmentName);
                    segmentObject.transform.SetParent(transform, false);
                }
                else
                {
                    segmentObject = child.gameObject;
                }

                segmentParticles[i] = segmentObject.GetComponent<ParticleSystem>();
                if (segmentParticles[i] == null)
                {
                    segmentParticles[i] = segmentObject.AddComponent<ParticleSystem>();
                }

                segmentRenderers[i] = segmentObject.GetComponent<ParticleSystemRenderer>();
                if (segmentRenderers[i] == null)
                {
                    segmentRenderers[i] = segmentObject.AddComponent<ParticleSystemRenderer>();
                }
            }
        }

        private void PlaySegmentParticles()
        {
            if (segmentParticles == null)
            {
                return;
            }

            for (int i = 0; i < SegmentCount; i++)
            {
                if (segmentParticles[i] == null || segmentEmissionWeights[i] <= 0f)
                {
                    continue;
                }

                if (!segmentParticles[i].isPlaying)
                {
                    segmentParticles[i].Play();
                }
            }
        }

        private void StopSegmentParticles(bool clearParticles)
        {
            if (segmentParticles == null)
            {
                return;
            }

            for (int i = 0; i < SegmentCount; i++)
            {
                StopSegmentParticle(i, clearParticles);
            }
        }

        private void StopSegmentParticle(int index, bool clearParticles)
        {
            if (segmentParticles == null || index < 0 || index >= segmentParticles.Length || segmentParticles[index] == null)
            {
                return;
            }

            ParticleSystemStopBehavior stopBehavior = clearParticles
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            segmentParticles[index].Stop(true, stopBehavior);
            segmentEmissionWeights[index] = 0f;
        }

        private void ConfigureParticleSystem(
            ParticleSystem particles,
            ParticleSystemRenderer particleRenderer,
            Vector3 localPosition,
            Vector2 shapeSize,
            float emissionWeight
        )
        {
            if (particles == null || particleRenderer == null)
            {
                return;
            }

            particles.transform.localPosition = localPosition;
            particles.transform.localRotation = Quaternion.identity;

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;

            if (particles.isStopped)
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

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = maxEmissionRate * intensity * Mathf.Clamp01(emissionWeight);

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(shapeSize.x, 0.05f, shapeSize.y);
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-horizontalSpread, horizontalSpread);
            velocity.y = new ParticleSystem.MinMaxCurve(upwardSpeedRange.x, upwardSpeedRange.y);
            velocity.z = new ParticleSystem.MinMaxCurve(-horizontalSpread, horizontalSpread);

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
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

            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.normalDirection = 0f;
            particleRenderer.sortMode = ParticleSystemSortMode.None;
            particleRenderer.sharedMaterial = GetOrCreateSplashMaterial();
        }

        private void ApplyIntensity()
        {
            if (splashParticles == null)
            {
                return;
            }

            if (hasWorldExclusionArea)
            {
                ParticleSystem.EmissionModule mainEmission = splashParticles.emission;
                mainEmission.enabled = true;
                mainEmission.rateOverTime = 0f;

                if (segmentParticles == null)
                {
                    return;
                }

                for (int i = 0; i < SegmentCount; i++)
                {
                    if (segmentParticles[i] == null)
                    {
                        continue;
                    }

                    ParticleSystem.EmissionModule segmentEmission = segmentParticles[i].emission;
                    segmentEmission.enabled = true;
                    segmentEmission.rateOverTime = maxEmissionRate * intensity * segmentEmissionWeights[i];
                }

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
