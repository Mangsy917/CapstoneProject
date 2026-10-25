using System.Collections.Generic;
using UnityEngine;

namespace Game.Character
{
    // 캐릭터 상태를 Animator 파라미터로 넘겨줌
    // 어느 상태로 갈지는 Animator Controller의 전이가 정함 (Animator 창에서 조건을 바꿀 수 있음)
    // 파라미터와 기본 전이는 Tools > Animator > 선택한 Animator 전이 다시 만들기 메뉴로 만듦
    // 이동과 공격 처리가 모두 끝난 뒤(LateUpdate)에 넘김
    public class CharacterAnimationPlayer : MonoBehaviour
    {
        // Animator 파라미터 이름 (에디터의 전이 만들기 메뉴도 이 이름을 씀)
        public const string IsGroundedParameter = "IsGrounded";
        public const string IsCrouchingParameter = "IsCrouching";
        public const string MoveStateParameter = "MoveState";       // CharacterMove.MoveState 값
        public const string IsAttackingParameter = "IsAttacking";   // 애니메이션이 있는 공격 중
        public const string IsLandingParameter = "IsLanding";       // 공격 착지 경직 중
        public const string IsHitStunParameter = "IsHitStun";
        public const string IsGuardStunParameter = "IsGuardStun";
        public const string HitParameter = "Hit";                   // 트리거: 새로 맞음
        public const string GuardParameter = "Guard";               // 트리거: 새로 가드함
        public const string HitMotionParameter = "HitMotion";       // HitMotion 값
        public const string GuardMotionParameter = "GuardMotion";   // HitMotion 값
        // 공격 시작 트리거는 AttackData의 AnimationState와 같은 이름

        private static readonly int _isGroundedHash = Animator.StringToHash(IsGroundedParameter);
        private static readonly int _isCrouchingHash = Animator.StringToHash(IsCrouchingParameter);
        private static readonly int _moveStateHash = Animator.StringToHash(MoveStateParameter);
        private static readonly int _isAttackingHash = Animator.StringToHash(IsAttackingParameter);
        private static readonly int _isLandingHash = Animator.StringToHash(IsLandingParameter);
        private static readonly int _isHitStunHash = Animator.StringToHash(IsHitStunParameter);
        private static readonly int _isGuardStunHash = Animator.StringToHash(IsGuardStunParameter);
        private static readonly int _hitHash = Animator.StringToHash(HitParameter);
        private static readonly int _guardHash = Animator.StringToHash(GuardParameter);
        private static readonly int _hitMotionHash = Animator.StringToHash(HitMotionParameter);
        private static readonly int _guardMotionHash = Animator.StringToHash(GuardMotionParameter);

        [Tooltip("재생할 Animator. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private Animator _animator;
        [Tooltip("비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterMove CAP_characterMove;
        [Tooltip("비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterAttack CAP_characterAttack;
        [Tooltip("비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterHitReaction CAP_hitReaction;
        [Tooltip("더미의 서기 / 앉기 / 점프. 비워두면 같은 오브젝트에서 찾음 (CharacterMove가 없을 때 사용)")]
        [SerializeField] private DummyAction CAP_dummyAction;

        private int _playedHitCount; // 마지막으로 트리거를 보낸 피격 / 가드 (새로 맞으면 같은 모션이어도 처음부터 다시 재생)
        private readonly List<int> _triggersToReset = new List<int>();

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (CAP_characterMove == null) CAP_characterMove = GetComponent<CharacterMove>();
            if (CAP_characterAttack == null) CAP_characterAttack = GetComponent<CharacterAttack>();
            if (CAP_hitReaction == null) CAP_hitReaction = GetComponent<CharacterHitReaction>();
            if (CAP_dummyAction == null) CAP_dummyAction = GetComponent<DummyAction>();
        }

        private void LateUpdate()
        {
            if (_animator == null) return;

            SetMoveParameters();
            SetAttackParameters();
            SetHitParameters();

            _animator.Update(0f); // 다음 프레임까지 기다리지 않고 바로 전이해서 스프라이트를 바꿈

            // 이번 프레임에 전이를 못 탄 트리거가 남아 있다가 나중에 엉뚱하게 쓰이지 않도록 지움
            foreach (int trigger in _triggersToReset) _animator.ResetTrigger(trigger);
            _triggersToReset.Clear();
        }

        private void SetMoveParameters()
        {
            bool isGrounded;
            bool isCrouching;
            CharacterMove.MoveState moveState = CharacterMove.MoveState.Neutral;
            if (CAP_characterMove != null)
            {
                isGrounded = CAP_characterMove.IsGrounded;
                isCrouching = CAP_characterMove.IsCrouching;
                moveState = CAP_characterMove.CurrentState;
            }
            else
            {
                // 더미: 설정창의 더미 행동에 따라
                isGrounded = CAP_dummyAction == null || CAP_dummyAction.DA_IsGrounded;
                isCrouching = CAP_dummyAction != null && CAP_dummyAction.DA_IsCrouching;
            }

            _animator.SetBool(_isGroundedHash, isGrounded);
            _animator.SetBool(_isCrouchingHash, isCrouching);
            _animator.SetInteger(_moveStateHash, (int)moveState);
        }

        private void SetAttackParameters()
        {
            AttackData attack = CAP_characterAttack != null && CAP_characterAttack.IsAttacking ? CAP_characterAttack.CurrentAttack : null;
            // 애니메이션이 없는 공격은 이동 애니메이션을 그대로 재생
            bool isAttackAnimated = attack != null && !string.IsNullOrEmpty(attack.AnimationState);

            _animator.SetBool(_isAttackingHash, isAttackAnimated);
            _animator.SetBool(_isLandingHash, isAttackAnimated && CAP_characterAttack.IsLanding);

            // 같은 공격을 연달아 써도 처음부터 다시 재생되도록, 공격 첫 프레임마다 트리거를 보냄
            if (isAttackAnimated && CAP_characterAttack.AttackFrame == 1)
                SetTrigger(Animator.StringToHash(attack.AnimationState));
        }

        private void SetHitParameters()
        {
            if (CAP_hitReaction == null) return;

            bool isHitStun = CAP_hitReaction.IsInHitStun;
            bool isGuardStun = CAP_hitReaction.IsInGuardStun;
            _animator.SetBool(_isHitStunHash, isHitStun);
            _animator.SetBool(_isGuardStunHash, isGuardStun);
            _animator.SetInteger(_hitMotionHash, (int)CAP_hitReaction.CurrentMotion);
            _animator.SetInteger(_guardMotionHash, (int)CAP_hitReaction.CurrentGuardMotion);

            if (CAP_hitReaction.HitCount == _playedHitCount) return;
            _playedHitCount = CAP_hitReaction.HitCount;
            if (isHitStun) SetTrigger(_hitHash);
            else if (isGuardStun) SetTrigger(_guardHash);
        }

        private void SetTrigger(int trigger)
        {
            _animator.SetTrigger(trigger);
            _triggersToReset.Add(trigger);
        }
    }
}
