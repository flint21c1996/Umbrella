using System.Collections.Generic;
using UnityEngine;

public class CameraOcclusionFade : MonoBehaviour
{
    // 카메라가 바라볼 플레이어 기준점 (Player의 몸통 높이에 세팅하면 좋음)
    public Transform target;

    // 가림 처리 대상 레이어 - 예: FadeObstacle 레이어를 지정하면 그 레이어만 검사
    public LayerMask obstacleMask;

    // 카메라와 플레이어 사이를 검사할 때 사용할 구의 반지름, 단순 Raycast보다 약간 두께 있는 검사라서 더 안정적으로 장애물을 찾을 수 있음
    public float sphereRadius = 0.3f;

    // 가려질 때 알파값
    public float hiddenAlpha = 0.2f;

    // 원래 알파값
    public float visibleAlpha = 1.0f;

    // 알파가 변하는 속도
    public float fadeSpeed = 10.0f;

    // 이번 프레임에서 실제로 플레이어를 가리고 있는 Renderer 목록
    // HashSet을 쓰는 이유는 중복 추가를 막기 위해서
    private readonly HashSet<Renderer> currentOccluders = new();

    // 한 번이라도 감지한 Renderer의 머티리얼을 캐시해두는 딕셔너리
    // Renderer를 key로 쓰고, 그 Renderer가 가진 Material 배열을 value로 저장
    // 나중에 가리지 않을 때 원래 알파값으로 복구할 때도 사용
    private readonly Dictionary<Renderer, Material[]> materialCache = new();

    void LateUpdate()
    {
        // 목표 지점이 연결되지 않았다면 더 이상 처리하지 않음
        if (target == null)
        {
            return;
        }

        // 이번 프레임에 새로 검사할 것이므로 목록을 먼저 비움
        currentOccluders.Clear();

        
        Vector3 start = transform.position;         // 카메라 위치에서 시작
        Vector3 end = target.position;              // 플레이어의 CameraTarget 위치를 끝점으로 사용
        Vector3 direction = end - start;            // 카메라에서 플레이어 쪽으로 향하는 방향 벡터
        float distance = direction.magnitude;       // 카메라와 목표 지점 사이의 거리

        // 카메라와 플레이어 사이를 구 형태로 검사, 너무 가까운 경우를 제외하고 검사를 수행할 수 있도록
        if (distance > 0.001f)
        {

            // SphereCastAll:
            // 카메라에서 플레이어까지 구 형태로 훑으면서 obstacleMask에 해당하는 충돌체를 모두 찾음
            RaycastHit[] hits = Physics.SphereCastAll(
                start,
                sphereRadius,
                direction.normalized,
                distance,
                obstacleMask,
                QueryTriggerInteraction.Ignore
            );

            foreach (RaycastHit hit in hits)
            {
                // 충돌한 오브젝트에서 Renderer를 직접 찾음
                Renderer renderer = hit.collider.GetComponent<Renderer>();

                // Collider는 자식에 있고 Renderer는 부모에 있는 경우가 많아서
                // 직접 못 찾으면 부모 쪽에서도 다시 찾음
                if (renderer == null)
                {
                    renderer = hit.collider.GetComponentInParent<Renderer>();
                }

                // Renderer를 끝내 찾지 못하면 처리 대상이 아니므로 넘어감
                if (renderer == null)
                {
                    continue;
                }

                // 아직 캐시에 등록되지 않은 Renderer라면 머티리얼 인스턴스를 저장
                CacheMaterials(renderer);

                // 이번 프레임에 플레이어를 가리고 있는 오브젝트로 등록
                currentOccluders.Add(renderer);
            }
        }

        // 지금까지 캐시된 모든 Renderer를 순회
        // 현재 가리고 있으면 hiddenAlpha,
        // 아니면 visibleAlpha를 목표값으로 삼아 보간
        foreach (var pair in materialCache)
        {
            Renderer renderer = pair.Key;
            Material[] materials = pair.Value;

            float targetAlpha = currentOccluders.Contains(renderer) ? hiddenAlpha : visibleAlpha;
            FadeMaterials(materials, targetAlpha);
        }
    }

    void CacheMaterials(Renderer renderer)
    {
        // 이미 캐시된 Renderer라면 다시 저장할 필요 없음
        if (materialCache.ContainsKey(renderer))
        {
            return;
        }

        // renderer.materials를 사용하면 이 Renderer 전용 머티리얼 인스턴스가 생성됨
        // 공유 머티리얼 전체를 바꾸지 않고, 감지된 오브젝트만 개별적으로 알파 조절하기 위함
        materialCache[renderer] = renderer.materials;
    }

    void FadeMaterials(Material[] materials, float targetAlpha)
    {
        foreach (Material material in materials)
        {
            if (material == null)
            {
                continue;
            }

            // URP Lit 머티리얼은 보통 _BaseColor를 사용
            if (material.HasProperty("_BaseColor"))
            {
                Color color = material.GetColor("_BaseColor");
                color.a = Mathf.Lerp(color.a, targetAlpha, fadeSpeed * Time.deltaTime);     // 현재 알파값을 목표 알파값으로 부드럽게 보간
                material.SetColor("_BaseColor", color);                                     // 변경된 색을 다시 머티리얼에 적용
            }

            // 일부 셰이더는 _Color를 사용할 수 있으므로 예외 처리
            else if (material.HasProperty("_Color"))
            {
                Color color = material.color;
                color.a = Mathf.Lerp(color.a, targetAlpha, fadeSpeed * Time.deltaTime);     // 현재 알파값을 목표 알파값으로 부드럽게 보간
                material.color = color;                                                     // 변경된 색을 다시 머티리얼에 적용
            }
        }
    }
}