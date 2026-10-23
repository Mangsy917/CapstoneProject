using UnityEngine;

namespace Game.Character
{
    // 캐릭터 상태에 맞는 Animator 상태를 재생함
    // - 공격 중이면 그 공격(AttackData)의 애니메이션
    // - 아니면 이동 상태에 따라 Idle / Sit / Jump / 걷기
    // 이동과 공격 처리가 모두 끝난 뒤(LateUpdate)에 정함
    public class CharacterAnimationPlayer : MonoBehaviour
    {
        [Tooltip("재생할 Animator. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private Animator _animator;
        [Tooltip("비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterMove CAP_characterMove;
        [Tooltip("비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterAttack CAP_characterAttack;

        [Header("Animator 상태 이름")]
        [SerializeField] private string _idleState = "Idle";
        [SerializeField] private string _sitState = "Sit";
        [SerializeField] private string _jumpState = "Jump";
        [SerializeField] private string _walkForwardState = "WalkForward";
        [SerializeField] private string _walkBackState = "WalkBack";

        private string CAP_currentState;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (CAP_characterMove == null) CAP_characterMove = GetComponent<CharacterMove>();
            if (CAP_characterAttack == null) CAP_characterAttack = GetComponent<CharacterAttack>();
        }

        private void LateUpdate()
        {
            if (_animator == null) return;

            string state = GetStateName();
            // 같은 공격을 연달아 써도 처음부터 다시 재생되도록, 공격 첫 프레임에는 항상 재생
            bool isAttackStarted = CAP_characterAttack != null && CAP_characterAttack.IsAttacking && CAP_characterAttack.AttackFrame == 1;
            if (state == CAP_currentState && !isAttackStarted) return;

            CAP_currentState = state;
            _animator.Play(state, 0, 0f);
            _animator.Update(0f); // 다음 프레임까지 기다리지 않고 바로 스프라이트를 바꿈
        }

        private string GetStateName()
        {
            if (CAP_characterAttack != null && CAP_characterAttack.IsAttacking &&
                !string.IsNullOrEmpty(CAP_characterAttack.CurrentAttack.AnimationState))
                return CAP_characterAttack.CurrentAttack.AnimationState;

            if (CAP_characterMove == null) return _idleState;
            if (!CAP_characterMove.IsGrounded) return _jumpState;
            if (CAP_characterMove.IsCrouching) return _sitState;

            switch (CAP_characterMove.CurrentState)
            {
                case CharacterMove.MoveState.WalkForward:
                case CharacterMove.MoveState.ForwardDash:
                case CharacterMove.MoveState.Run:
                    return _walkForwardState;
                case CharacterMove.MoveState.WalkBackGuard:
                case CharacterMove.MoveState.BackDash:
                    return _walkBackState;
                default:
                    return _idleState;
            }
        }
    }
}
