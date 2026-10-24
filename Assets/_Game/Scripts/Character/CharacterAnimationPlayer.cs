using UnityEngine;

namespace Game.Character
{
    // 캐릭터 상태에 맞는 Animator 상태를 재생함
    // - 피격 경직 중이면 피격 모션 (CharacterHitReaction.CurrentMotion)
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
        [Tooltip("비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterHitReaction CAP_hitReaction;

        [Header("Animator 상태 이름")]
        [SerializeField] private string _idleState = "Idle";
        [SerializeField] private string _sitState = "Sit";
        [SerializeField] private string _jumpState = "Jump";
        [SerializeField] private string _walkForwardState = "WalkForward";
        [SerializeField] private string _walkBackState = "WalkBack";

        [Header("피격 모션 Animator 상태 이름")]
        [SerializeField] private string _topHitState = "TopHit";
        [SerializeField] private string _middleHitState = "MiddleHit";
        [SerializeField] private string _lowHitState = "LowHit";
        [SerializeField] private string _top2LowHitState = "Top2LowHit";
        [SerializeField] private string _low2TopHitState = "Low2TopHit";

        private string CAP_currentState;
        private int _playedHitCount; // 마지막으로 재생한 피격 (새로 맞으면 같은 모션이어도 처음부터 다시 재생)

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (CAP_characterMove == null) CAP_characterMove = GetComponent<CharacterMove>();
            if (CAP_characterAttack == null) CAP_characterAttack = GetComponent<CharacterAttack>();
            if (CAP_hitReaction == null) CAP_hitReaction = GetComponent<CharacterHitReaction>();
        }

        private void LateUpdate()
        {
            if (_animator == null) return;

            string state = GetStateName();
            // 같은 공격을 연달아 써도 처음부터 다시 재생되도록, 공격 첫 프레임에는 항상 재생
            bool isAttackStarted = CAP_characterAttack != null && CAP_characterAttack.IsAttacking && CAP_characterAttack.AttackFrame == 1;
            bool isNewHit = CAP_hitReaction != null && CAP_hitReaction.HitCount != _playedHitCount;
            if (state == CAP_currentState && !isAttackStarted && !isNewHit) return;

            if (CAP_hitReaction != null) _playedHitCount = CAP_hitReaction.HitCount;
            CAP_currentState = state;
            _animator.Play(state, 0, 0f);
            _animator.Update(0f); // 다음 프레임까지 기다리지 않고 바로 스프라이트를 바꿈
        }

        private string GetStateName()
        {
            if (CAP_hitReaction != null && CAP_hitReaction.IsInHitStun)
                return GetHitStateName(CAP_hitReaction.CurrentMotion);

            if (CAP_characterAttack != null && CAP_characterAttack.IsAttacking)
            {
                AttackData attack = CAP_characterAttack.CurrentAttack;
                // 착지 경직 중이면 착지 애니메이션 (비어 있으면 공격 애니메이션 유지)
                if (CAP_characterAttack.IsLanding && !string.IsNullOrEmpty(attack.LandingAnimationState))
                    return attack.LandingAnimationState;
                if (!string.IsNullOrEmpty(attack.AnimationState))
                    return attack.AnimationState;
            }

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

        private string GetHitStateName(HitMotion motion)
        {
            switch (motion)
            {
                case HitMotion.Top: return _topHitState;
                case HitMotion.Low: return _lowHitState;
                case HitMotion.Top2Low: return _top2LowHitState;
                case HitMotion.Low2Top: return _low2TopHitState;
                default: return _middleHitState;
            }
        }
    }
}
