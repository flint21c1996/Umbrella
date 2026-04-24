using UnityEngine;

// Player 애니메이션의 로코모션 모드.
public enum PlayerLocomotionMode
{
    Empty = 0,
    UmbrellaClosed = 1,
    UmbrellaOpen = 2,
    UmbrellaInvertedHook = 3,
    UmbrellaInvertedWater = 4,
    UmbrellaPouring = 5,
    CarryObject = 6,
    PushPull = 7
}

// PlayerMovement와 PlayerUmbrellaController의 상태를 읽어 Animator 파라미터로 전달한다.
// 이동, 우산, 들기/밀기 같은 게임플레이 로직은 각 전용 컴포넌트가 담당하고,
// 이 스크립트는 시각 모델의 애니메이션 상태 동기화만 담당한다.
public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    // 플레이어 상태를 시각 모델 애니메이션에 반영할 Animator.
    [SerializeField] private Animator ModelAnimator;

    // 실제 이동 속도와 수직 속도를 읽기 위한 Rigidbody.
    [SerializeField] private Rigidbody PlayerRigidbody;

    // 지면 상태처럼 이동 컴포넌트가 이미 판단한 값을 읽는다.
    [SerializeField] private PlayerMovement PlayerMovement;

    // 우산 보유 여부와 현재 우산 상태를 애니메이션 모드로 변환할 때 사용한다.
    [SerializeField] private PlayerUmbrellaController UmbrellaController;

    [Header("Locomotion Override")]
    // 임시 애니메이션 테스트용 수동 모드. 실제 플레이에서는 꺼두는 것을 기본으로 한다.
    [SerializeField] private bool UseLocomotionModeOverride;
    [SerializeField] private PlayerLocomotionMode LocomotionModeOverride = PlayerLocomotionMode.Empty;

    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int LocomotionModeHash = Animator.StringToHash("LocomotionMode");

    void Awake()
    {
        ResolveReferences();
    }

    void Reset()
    {
        ResolveReferences();
    }

    void LateUpdate()
    {
        // 이동/우산 상태 갱신이 끝난 뒤 같은 프레임의 최종 상태를 Animator에 반영한다.
        UpdateAnimatorParameters();
    }

    void ResolveReferences()
    {
        if (PlayerRigidbody == null)
        {
            PlayerRigidbody = GetComponent<Rigidbody>();
        }

        if (PlayerMovement == null)
        {
            PlayerMovement = GetComponent<PlayerMovement>();
        }

        if (UmbrellaController == null)
        {
            UmbrellaController = GetComponent<PlayerUmbrellaController>();
        }

        if (ModelAnimator == null)
        {
            // 기본 구조는 Player 아래 VisualRoot에 Animator가 붙는 형태를 가정한다.
            ModelAnimator = GetComponentInChildren<Animator>();
        }
    }

    // 현재 플레이어 상태를 Animator 파라미터로 전달한다.
    void UpdateAnimatorParameters()
    {
        if (ModelAnimator == null || PlayerRigidbody == null || PlayerMovement == null)
        {
            return;
        }

        Vector3 velocity = PlayerRigidbody.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0.0f, velocity.z);

        ModelAnimator.SetFloat(MoveSpeedHash, horizontalVelocity.magnitude);
        ModelAnimator.SetFloat(VerticalSpeedHash, velocity.y);
        ModelAnimator.SetBool(IsGroundedHash, PlayerMovement.isGrounded);
        ModelAnimator.SetInteger(LocomotionModeHash, (int)GetCurrentLocomotionMode());
    }

    PlayerLocomotionMode GetCurrentLocomotionMode()
    {
        if (UseLocomotionModeOverride)
        {
            return LocomotionModeOverride;
        }

        if (UmbrellaController == null || !UmbrellaController.HasUmbrella)
        {
            return PlayerLocomotionMode.Empty;
        }

        // 현재 우산 상태를 애니메이션용 locomotion 모드로 변환한다.
        // 세부 기획이 확정되면 갈고리, 물건 들기, 밀기/당기기 상태를 여기서 확장한다.
        switch (UmbrellaController.CurrentState)
        {
            case PlayerUmbrellaController.UmbrellaState.Open:
                return PlayerLocomotionMode.UmbrellaOpen;
            case PlayerUmbrellaController.UmbrellaState.UpsideDown:
                return PlayerLocomotionMode.UmbrellaInvertedWater;
            case PlayerUmbrellaController.UmbrellaState.Pouring:
                return PlayerLocomotionMode.UmbrellaPouring;
            default:
                return PlayerLocomotionMode.UmbrellaClosed;
        }
    }
}
