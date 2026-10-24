using UnityEngine;

namespace Game.Character
{
    // 공격이 맞았을 때(또는 가드됐을 때) 맞은 쪽에 전하는 정보 (AttackData.GetHitInfo로 만듦)
    public readonly struct HitInfo
    {
        public int HitStunFrames { get; }
        public float HitBackDistance { get; }
        public int HitBackFrames { get; }
        public int BlockStunFrames { get; }
        public float GuardBackDistance { get; }
        public int GuardBackFrames { get; }
        public AttackHeight Height { get; }
        public HitMotion Motion { get; }
        public bool IsThrow { get; }

        public HitInfo(int hitStunFrames, float hitBackDistance, int hitBackFrames,
                       int blockStunFrames, float guardBackDistance, int guardBackFrames,
                       AttackHeight height, HitMotion motion, bool isThrow)
        {
            HitStunFrames = hitStunFrames;
            HitBackDistance = hitBackDistance;
            HitBackFrames = hitBackFrames;
            BlockStunFrames = blockStunFrames;
            GuardBackDistance = guardBackDistance;
            GuardBackFrames = guardBackFrames;
            Height = height;
            Motion = motion;
            IsThrow = isThrow;
        }
    }

    // 캐릭터(Player, Enemy)에 붙여서 사용하는 피격 / 가드 처리
    // - 공격이 닿으면 가드할 수 있는지 먼저 봄
    //   플레이어: 땅에서 뒤(4)를 누르면 서서 가드, 뒤아래(1)를 누르면 앉아 가드 (CharacterMove)
    //   더미: 설정창의 가드 설정대로 (DummyGuard)
    //   서서 가드는 하단을, 앉아 가드는 중단을 막지 못함. 잡기, 공중, 공격 중, 피격 경직 중에는 가드할 수 없음
    // - 맞으면: 하던 공격을 취소하고 피격 경직 + 히트백 + 피격 모션, 데미지는 CharacterHealth로
    //   피격 모션은 공격(AttackData)에 정한 모션, 자동이면 상단 공격은 MiddleHit, 중단 공격은 Top2Low, 하단 공격은 맞은 피격 상자 위치에 따라 정함
    // - 가드하면: 가드 경직 + 가드백 + 가드 모션 (가드한 피격 상자 위치에 따라 Top / Middle / Bottom), 데미지 없음
    // - 히트백 / 가드백: 정해진 프레임 동안 뒤로 밀려남. 벽에 붙어 있으면 대신 때린 쪽이 밀려남 (장풍은 제외)
    // - 경직 중에는 움직이거나 공격할 수 없음 (CharacterMove, CharacterAttack이 IsInStun을 확인)
    // - 슈퍼아머로 버틴 공격은 데미지만 받고 경직 / 히트백이 없음
    // - 모션 재생은 CharacterAnimationPlayer가 함
    // 경직은 공격(CharacterAttack, 실행 순서 20)과 투사체가 처리된 뒤에 1프레임씩 줄임 (밀림 판정 Pushbox(50)보다는 먼저)
    // - 경직 마지막 프레임에 들어온 공격도 경직 중에 맞은 것(콤보)으로 봄
    // - 맞은 프레임에는 줄이지 않아서, 다음 프레임부터 경직 프레임 수만큼 움직일 수 없음
    [DefaultExecutionOrder(40)]
    public class CharacterHitReaction : MonoBehaviour
    {
        [Tooltip("맞으면 하던 공격을 취소할 CharacterAttack. 비워두면 같은 오브젝트에서 찾음 (없어도 됨)")]
        [SerializeField] private CharacterAttack CHR_characterAttack;

        [Tooltip("플레이어의 가드 입력(뒤 / 뒤아래)과 땅에 있는지 알려줄 CharacterMove. 비워두면 같은 오브젝트에서 찾음 (없어도 됨)")]
        [SerializeField] private CharacterMove CHR_characterMove;

        [Tooltip("더미의 가드 설정. 비워두면 같은 오브젝트에서 찾음 (없어도 됨)")]
        [SerializeField] private DummyGuard _dummyGuard;

        [Tooltip("더미가 점프 중(공중)인지 알려줄 DummyAction. 비워두면 같은 오브젝트에서 찾음 (없어도 됨)")]
        [SerializeField] private DummyAction _dummyAction;

        [Tooltip("맞은 피격 상자로 피격 / 가드 모션을 정할 CharacterHitboxes. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterHitboxes _characterHitboxes;

        [Tooltip("히트백 / 가드백 중 벽에 붙었는지 확인할 Pushbox. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private Pushbox _pushbox;

        /// <summary>남은 피격 경직 프레임</summary>
        public int HitStunFramesLeft { get; private set; }

        /// <summary>남은 가드 경직 프레임</summary>
        public int GuardStunFramesLeft { get; private set; }

        public bool IsInHitStun => HitStunFramesLeft > 0;
        public bool IsInGuardStun => GuardStunFramesLeft > 0;

        /// <summary>피격 경직이나 가드 경직 중인지 (움직이거나 공격할 수 없음)</summary>
        public bool IsInStun => IsInHitStun || IsInGuardStun;

        /// <summary>마지막으로 맞았을 때의 피격 모션 (자동은 들어가지 않음)</summary>
        public HitMotion CurrentMotion { get; private set; } = HitMotion.Middle;

        /// <summary>마지막으로 가드했을 때 가드한 위치 (Top / Middle / Low만 들어감)</summary>
        public HitMotion CurrentGuardMotion { get; private set; } = HitMotion.Middle;

        /// <summary>맞거나 가드할 때마다 1씩 늘어남 (같은 모션이 연달아 나와도 처음부터 다시 재생하는 데 사용)</summary>
        public int HitCount { get; private set; }

        private int _reactionStartFrame = -1; // 마지막으로 맞거나 가드한 프레임 (그 프레임에는 경직 / 밀림을 진행하지 않음)
        private int _pushBackFramesLeft;
        private float _pushBackDistancePerFrame;
        private float _pushBackDirection;    // 밀려나는 방향 (1: 오른쪽, -1: 왼쪽)
        private Transform _pushBackAttacker; // 벽에 붙었을 때 대신 밀려날 때린 쪽 (장풍이면 null)

        /// <summary>
        /// target에게 공격이 닿았을 때 호출. 가드하면 가드 경직 / 가드백, 슈퍼아머로 버티면 데미지만, 아니면 데미지와 피격 경직 / 히트백을 줌
        /// direction: 밀려나는 방향 (때린 쪽에서 맞은 쪽으로, 1: 오른쪽, -1: 왼쪽)
        /// shouldPushAttacker: 맞은 쪽이 벽에 붙어 있을 때 때린 쪽이 대신 밀려날지 (장풍은 false)
        /// </summary>
        public static void HitTarget(GameObject target, int damage, HitInfo info, Hitbox hitbox, float direction, CharacterAttack attacker, bool shouldPushAttacker)
        {
            var reaction = target.GetComponent<CharacterHitReaction>();
            Transform pushedAttacker = shouldPushAttacker && attacker != null ? attacker.transform : null;
            if (reaction != null && reaction.CanGuard(info))
            {
                reaction.ReceiveGuard(info, hitbox, direction, pushedAttacker);
                return;
            }

            var targetAttack = target.GetComponent<CharacterAttack>();
            bool isAbsorbed = targetAttack != null && targetAttack.TryAbsorbHit(info.Height);
            if (!isAbsorbed && reaction != null) reaction.ReceiveHit(info, hitbox, direction, pushedAttacker);

            var health = target.GetComponent<CharacterHealth>();
            if (health != null) health.TakeDamage(damage, attacker: attacker);
        }

        private void Awake()
        {
            if (CHR_characterAttack == null) CHR_characterAttack = GetComponent<CharacterAttack>();
            if (CHR_characterMove == null) CHR_characterMove = GetComponent<CharacterMove>();
            if (_dummyGuard == null) _dummyGuard = GetComponent<DummyGuard>();
            if (_dummyAction == null) _dummyAction = GetComponent<DummyAction>();
            if (_characterHitboxes == null) _characterHitboxes = GetComponent<CharacterHitboxes>();
            if (_pushbox == null) _pushbox = GetComponent<Pushbox>();
        }

        private void Update()
        {
            // 슬로우 모션 중에는 건너뛰는 프레임이 있음 (GameSpeed)
            if (!Game.Field.GameSpeed.IsTickFrame) return;
            if (_reactionStartFrame == Time.frameCount) return;

            if (_pushBackFramesLeft > 0)
            {
                _pushBackFramesLeft--;
                MovePushBack();
            }

            if (HitStunFramesLeft > 0) HitStunFramesLeft--;
            if (GuardStunFramesLeft > 0) GuardStunFramesLeft--;
        }

        // 이번 프레임만큼 뒤로 밀려남. 벽에 붙어 있으면 때린 쪽을 반대로 밀어냄
        private void MovePushBack()
        {
            Vector3 step = new Vector3(_pushBackDirection * _pushBackDistancePerFrame, 0f, 0f);
            bool isAtWall = _pushbox != null && _pushbox.IsTouchingWall(_pushBackDirection);
            if (!isAtWall)
                transform.position += step;
            else if (_pushBackAttacker != null)
                _pushBackAttacker.position -= step;
        }

        private void StartPushBack(float distance, int frames, float direction, Transform pushedAttacker)
        {
            _reactionStartFrame = Time.frameCount;
            _pushBackFramesLeft = Mathf.Max(1, frames);
            _pushBackDistancePerFrame = distance / _pushBackFramesLeft;
            _pushBackDirection = direction < 0f ? -1f : 1f;
            _pushBackAttacker = pushedAttacker;
        }

        /// <summary>이 공격을 가드할 수 있는지 (가드하려는 자세가 공격 높이에 맞는지까지 봄)</summary>
        public bool CanGuard(HitInfo info)
        {
            if (info.IsThrow || IsInHitStun) return false;
            if (CHR_characterAttack != null && CHR_characterAttack.IsAttacking) return false;

            bool isCrouching;
            bool isGuarding = CHR_characterMove != null ? TryGetInputGuard(out isCrouching) : TryGetDummyGuard(info.Height, out isCrouching);
            if (!isGuarding) return false;

            // 서서 가드는 하단을, 앉아 가드는 중단을 막지 못함
            return isCrouching ? info.Height != AttackHeight.Middle : info.Height != AttackHeight.Low;
        }

        // 플레이어: 땅에서 뒤(4) = 서서 가드, 뒤아래(1) = 앉아 가드
        // 가드 경직 중에는 이동 상태가 중립이 되므로 레버 방향으로 봄
        private bool TryGetInputGuard(out bool isCrouching)
        {
            isCrouching = false;
            if (!CHR_characterMove.IsGrounded) return false;

            if (IsInGuardStun)
            {
                int direction = CHR_characterMove.RelativeDirection;
                isCrouching = direction == 1;
                return direction == 1 || direction == 4;
            }

            isCrouching = CHR_characterMove.CurrentState == CharacterMove.MoveState.CrouchGuard;
            return CHR_characterMove.IsGuarding;
        }

        // 더미: 설정창의 가드 설정대로. 점프 중(공중)에는 가드하지 못함
        private bool TryGetDummyGuard(AttackHeight height, out bool isCrouching)
        {
            isCrouching = false;
            if (_dummyAction != null && !_dummyAction.DA_IsGrounded) return false;
            return _dummyGuard != null && _dummyGuard.TryGetGuard(height, out isCrouching);
        }

        /// <summary>맞았을 때 피격 경직, 히트백, 피격 모션을 정함 (보통은 HitTarget을 통해 호출)</summary>
        public void ReceiveHit(HitInfo info, Hitbox hitbox, float direction, Transform pushedAttacker)
        {
            HitStunFramesLeft = info.HitStunFrames;
            GuardStunFramesLeft = 0;
            CurrentMotion = GetHitMotion(info, hitbox);
            HitCount++;
            StartPushBack(info.HitBackDistance, info.HitBackFrames, direction, pushedAttacker);

            // 맞으면 하던 공격은 취소됨
            if (CHR_characterAttack != null) CHR_characterAttack.CancelAttack();
            if (_dummyGuard != null) _dummyGuard.NotifyHit();
        }

        /// <summary>가드했을 때 가드 경직, 가드백, 가드 모션을 정함 (보통은 HitTarget을 통해 호출)</summary>
        public void ReceiveGuard(HitInfo info, Hitbox hitbox, float direction, Transform pushedAttacker)
        {
            GuardStunFramesLeft = info.BlockStunFrames;
            CurrentGuardMotion = _characterHitboxes != null ? _characterHitboxes.GetMotionByHitbox(hitbox) : HitMotion.Middle;
            HitCount++;
            StartPushBack(info.GuardBackDistance, info.GuardBackFrames, direction, pushedAttacker);

            if (_dummyGuard != null) _dummyGuard.NotifyGuard();
        }

        /// <summary>재시작할 때 호출. 경직과 히트백 / 가드백을 모두 취소</summary>
        public void ResetReaction()
        {
            HitStunFramesLeft = 0;
            GuardStunFramesLeft = 0;
            _reactionStartFrame = -1;
            _pushBackFramesLeft = 0;
            _pushBackAttacker = null;
            if (_dummyGuard != null) _dummyGuard.ResetRecord();
        }

        // 자동: 상단 공격은 MiddleHit, 중단 공격은 Top2Low, 하단 공격은 맞은 피격 상자 위치에 따라 정함
        private HitMotion GetHitMotion(HitInfo info, Hitbox hitbox)
        {
            if (info.Motion != HitMotion.Auto) return info.Motion;
            if (info.Height == AttackHeight.High) return HitMotion.Middle;
            if (info.Height == AttackHeight.Middle) return HitMotion.Top2Low;
            return _characterHitboxes != null ? _characterHitboxes.GetMotionByHitbox(hitbox) : HitMotion.Low;
        }
    }
}
