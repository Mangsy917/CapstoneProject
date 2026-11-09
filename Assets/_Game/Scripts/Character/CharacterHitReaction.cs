using UnityEngine;

namespace Game.Character
{
    // 맞았을 때 맞은 쪽이 무엇을 하고 있었는지에 따른 히트 종류
    public enum HitType
    {
        Normal,        // 노말 히트: 공격하지 않을 때 (대기, 이동, 경직 중 등)
        Counter,       // 카운터: 공격의 발생 / 지속 중
        PunishCounter  // 퍼니시 카운터: 공격의 회수 / 착지 경직 중
    }

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
        public int HitStopFrames { get; }

        // 히트 종류마다 맞은 쪽이 받는 결과 (없으면 노말 히트와 같음)
        private readonly AttackData.HitTypeResult _normalHitResult;
        private readonly AttackData.HitTypeResult _counterHitResult;
        private readonly AttackData.HitTypeResult _punishCounterResult;

        public HitInfo(int hitStunFrames, float hitBackDistance, int hitBackFrames,
                       int blockStunFrames, float guardBackDistance, int guardBackFrames,
                       AttackHeight height, HitMotion motion, bool isThrow,
                       int hitStopFrames = 0,
                       AttackData.HitTypeResult normalHitResult = null,
                       AttackData.HitTypeResult counterHitResult = null,
                       AttackData.HitTypeResult punishCounterResult = null)
        {
            HitStopFrames = hitStopFrames;
            _normalHitResult = normalHitResult;
            _counterHitResult = counterHitResult;
            _punishCounterResult = punishCounterResult;
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

        private AttackData.HitTypeResult GetResult(HitType hitType)
        {
            switch (hitType)
            {
                case HitType.Counter: return _counterHitResult ?? _normalHitResult;
                case HitType.PunishCounter: return _punishCounterResult ?? _normalHitResult;
                default: return _normalHitResult;
            }
        }

        /// <summary>이 히트 종류로 맞았을 때 데미지에 곱할 배율</summary>
        public float GetDamageRate(HitType hitType)
        {
            AttackData.HitTypeResult result = GetResult(hitType);
            return result != null ? result.DamageRate : 1f;
        }

        /// <summary>이 히트 종류로 맞았을 때 때린 쪽과 맞은 쪽이 멈추는 프레임 수 (히트 종류에 정해 두었으면 그 값, 아니면 기술의 히트스톱)</summary>
        public int GetHitStopFrames(HitType hitType)
        {
            AttackData.HitTypeResult result = GetResult(hitType);
            return result != null && result.ShouldOverrideHitStop ? result.AD_HitStopFrames : HitStopFrames;
        }

        // 맞은 뒤 다시 움직일 수 있을 때까지의 전체 프레임 수 (피격 경직 + 다운)
        // 히트 종류에 피격 경직을 직접 정해 두었으면 땅 / 공중에 따라 그 값, 아니면 히트 시 유리 프레임 기준 경직 + 추가 경직
        private int GetTotalStunFrames(HitType hitType, bool isAirborne)
        {
            AttackData.HitTypeResult result = GetResult(hitType);
            if (result != null && result.ShouldOverrideHitStun) return isAirborne ? result.AirHitStunFrames : result.StandHitStunFrames;
            return HitStunFrames + (result != null ? result.ExtraHitStunFrames : 0);
        }

        /// <summary>이 히트 종류로 맞았을 때의 피격 경직 프레임 수 (다운되면 쓰러지는 동안만). isAirborne: 맞은 쪽이 공중에 있는지 (띄워지는 경우 포함)</summary>
        public int GetHitStunFrames(HitType hitType, bool isAirborne = false)
        {
            int totalFrames = GetTotalStunFrames(hitType, isAirborne);
            AttackData.HitTypeResult result = GetResult(hitType);
            if (result == null || !result.ShouldKnockDown) return totalFrames;
            return Mathf.Min(result.FallFrames, totalFrames);
        }

        /// <summary>
        /// 이 히트 종류로 맞았을 때 피격 경직이 끝난 뒤 기상할 때까지 다운되어 있는 프레임 수 (다운되지 않으면 0)
        /// 다운 프레임을 정해 두었으면 그 값, 0이면 기상이 히트 시 유리 프레임(또는 직접 정한 피격 경직)에 맞도록 남은 경직
        /// </summary>
        public int GetKnockDownFrames(HitType hitType, bool isAirborne = false)
        {
            AttackData.HitTypeResult result = GetResult(hitType);
            if (result == null || !result.ShouldKnockDown) return 0;
            if (result.DownFrames > 0) return result.DownFrames;
            return GetTotalStunFrames(hitType, isAirborne) - GetHitStunFrames(hitType, isAirborne);
        }

        /// <summary>이 히트 종류로 맞았을 때 밀려나는 거리 (히트 종류에 정해 두었으면 그 값, 아니면 기술의 히트백)</summary>
        public float GetHitBackDistance(HitType hitType)
        {
            AttackData.HitTypeResult result = GetResult(hitType);
            return result != null && result.ShouldOverrideHitBack ? result.HitBackDistance : HitBackDistance;
        }

        /// <summary>이 히트 종류로 맞았을 때 히트백 프레임 수 (히트 종류에 정해 두었으면 그 값, 아니면 기술의 히트백)</summary>
        public int GetHitBackFrames(HitType hitType)
        {
            AttackData.HitTypeResult result = GetResult(hitType);
            return result != null && result.ShouldOverrideHitBack ? result.HitBackFrames : HitBackFrames;
        }

        /// <summary>이 히트 종류로 맞았을 때 공중으로 띄울 높이 (0이면 띄우지 않음)</summary>
        public float GetAirborneHeight(HitType hitType)
        {
            AttackData.HitTypeResult result = GetResult(hitType);
            return result != null ? result.AirborneHeight : 0f;
        }

        /// <summary>이 히트 종류로 맞았을 때의 피격 모션 (히트 종류에 정한 모션이 자동이면 기술의 피격 모션)</summary>
        public HitMotion GetMotion(HitType hitType)
        {
            AttackData.HitTypeResult result = GetResult(hitType);
            return result != null && result.HitMotion != HitMotion.Auto ? result.HitMotion : Motion;
        }
    }

    // 캐릭터(Player, Enemy)에 붙여서 사용하는 피격 / 가드 처리
    // - 공격이 닿으면 가드할 수 있는지 먼저 봄
    //   플레이어: 땅에서 뒤(4)를 누르면 서서 가드, 뒤아래(1)를 누르면 앉아 가드 (CharacterMove)
    //   더미: 설정창의 가드 설정대로 (DummyGuard)
    //   서서 가드는 하단을, 앉아 가드는 중단을 막지 못함. 잡기, 공중, 공격 중, 피격 경직 중에는 가드할 수 없음
    // - 맞으면: 하던 공격을 취소하고 피격 경직 + 히트백 + 피격 모션, 데미지는 CharacterHealth로
    //   콤보의 첫 타가 맞은 쪽의 공격 발생 / 지속 중에 맞으면 카운터, 회수 / 착지 경직 중이면 퍼니시 카운터, 그 외(콤보 2타부터 포함)는 노말 히트
    //   히트 종류마다 데미지 배율, 추가 경직, 다운 여부, 피격 모션을 공격(AttackData)의 히트 종류별 결과에서 정함
    //   피격 모션은 히트 종류에 정한 모션 → 기술의 모션 순서로 쓰고, 자동이면 상단 공격은 MiddleHit, 중단 공격은 Top2Low, 하단 공격은 맞은 피격 상자 위치에 따라 정함
    // - 가드하면: 가드 경직 + 가드백 + 가드 모션 (가드한 피격 상자 위치에 따라 Top / Middle / Bottom), 데미지 없음
    // - 히트백 / 가드백: 정해진 프레임 동안 뒤로 밀려남. 벽에 붙어 있으면 대신 때린 쪽이 밀려남 (장풍은 제외)
    // - 때린 쪽 움직임 따라가기: 띄우는 공격에 맞으면 때린 쪽이 움직이던 속도(앞으로, 위로)를 따라 같이 떠오름
    //   뛰어오르는 속도가 빠르거나 데미지가 클수록 높이 뜸. 공중에서 맞으면 앞으로 가던 공격의 가로 속도를 같이 받음
    //   땅에서 띄우지 않는 공격에 맞으면 따라가지 않음 (히트백만 받음)
    // - 다운: 다운되는 히트 종류로 맞으면, 쓰러지는 프레임 동안 피격 경직(추가타 가능) 뒤 기상할 때까지 다운됨
    //   다운 프레임을 정해 두면 그만큼 다운되고, 0이면 기상 시점이 히트 시 유리 프레임(위키의 KD 값)이 되도록 계산함
    //   다운 중에는 피격 상자가 모두 꺼져서 무적이고(CharacterHitboxes), 움직이거나 공격할 수 없음
    // - 경직 / 다운 중에는 움직이거나 공격할 수 없음 (CharacterMove, CharacterAttack이 IsInStun을 확인)
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

        [Tooltip("더미가 점프 중(공중)인지 알려주고, 띄우는 공격에 맞으면 띄울 DummyAction. 비워두면 같은 오브젝트에서 찾음 (없어도 됨)")]
        [SerializeField] private DummyAction _dummyAction;

        [Tooltip("맞은 피격 상자로 피격 / 가드 모션을 정할 CharacterHitboxes. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterHitboxes _characterHitboxes;

        [Tooltip("히트백 / 가드백 중 벽에 붙었는지 확인할 Pushbox. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private Pushbox _pushbox;

        [Header("때린 쪽 움직임 따라가기")]
        [Tooltip("띄우는 공격에 맞거나 공중에서 맞았을 때, 때린 쪽이 움직이던 속도를 얼마나 따라가는지 (1이면 같은 속도, 0이면 따라가지 않음).\n" +
                 "예) 승룡권에 맞으면 위로 뛰어오르는 승룡권과 같은 속도로 같이 떠오름")]
        [SerializeField] private float _attackerVelocityFollowRatio = 1f;

        [Tooltip("띄우는 공격의 데미지가 이 값일 때 뜨는 높이가 1배. 데미지가 클수록 더 높이 뜸 (예: 1200이면 1600 데미지는 1.33배)")]
        [SerializeField] private float _launchReferenceDamage = 1200f;

        [Tooltip("데미지로 정하는 뜨는 높이 배율의 최소 / 최대")]
        [SerializeField] private Vector2 _launchDamageScaleRange = new Vector2(0.5f, 2f);

        /// <summary>남은 피격 경직 프레임</summary>
        public int HitStunFramesLeft { get; private set; }

        /// <summary>남은 가드 경직 프레임</summary>
        public int GuardStunFramesLeft { get; private set; }

        public bool IsInHitStun => HitStunFramesLeft > 0;
        public bool IsInGuardStun => GuardStunFramesLeft > 0;

        /// <summary>남은 다운 프레임</summary>
        public int DownFramesLeft { get; private set; }

        /// <summary>다운 중인지 (무적이고 움직이거나 공격할 수 없음)</summary>
        public bool IsDown => DownFramesLeft > 0;

        /// <summary>피격 경직, 가드 경직, 다운 중인지 (움직이거나 공격할 수 없음)</summary>
        public bool IsInStun => IsInHitStun || IsInGuardStun || IsDown;

        /// <summary>마지막으로 맞았을 때의 피격 모션 (자동은 들어가지 않음)</summary>
        public HitMotion CurrentMotion { get; private set; } = HitMotion.Middle;

        /// <summary>마지막으로 가드했을 때 가드한 위치 (Top / Middle / Low만 들어감)</summary>
        public HitMotion CurrentGuardMotion { get; private set; } = HitMotion.Middle;

        /// <summary>맞거나 가드할 때마다 1씩 늘어남 (같은 모션이 연달아 나와도 처음부터 다시 재생하는 데 사용)</summary>
        public int HitCount { get; private set; }

        /// <summary>이번 콤보에서 맞은 횟수 (피격 경직 중에 또 맞으면 1씩 늘고, 경직이 풀린 뒤에 맞으면 1부터 다시 셈)</summary>
        public int ComboCount { get; private set; }

        /// <summary>마지막으로 맞았을 때의 히트 종류 (노말 히트 / 카운터 / 퍼니시 카운터)</summary>
        public HitType LastHitType { get; private set; } = HitType.Normal;

        private int _pendingDownFrames; // 피격 경직이 끝나면 시작할 다운 프레임 (0이면 다운 안 함)
        private int _reactionStartFrame = -1; // 마지막으로 맞거나 가드한 프레임 (그 프레임에는 경직 / 밀림을 진행하지 않음)
        private int _pushBackFramesLeft;
        private float _pushBackDistancePerFrame;
        private float _pushBackDirection;    // 밀려나는 방향 (1: 오른쪽, -1: 왼쪽)
        private Transform _pushBackAttacker; // 벽에 붙었을 때 대신 밀려날 때린 쪽 (장풍이면 null)

        /// <summary>
        /// target에게 공격이 닿았을 때 호출. 가드하면 가드 경직 / 가드백, 슈퍼아머로 버티면 데미지만, 아니면 데미지와 피격 경직 / 히트백을 줌
        /// direction: 밀려나는 방향 (때린 쪽에서 맞은 쪽으로, 1: 오른쪽, -1: 왼쪽)
        /// shouldPushAttacker: 맞은 쪽이 벽에 붙어 있을 때 때린 쪽이 대신 밀려날지 (장풍은 false)
        /// shouldHitStop: 맞혔을 때 때린 쪽과 맞은 쪽을 히트스톱으로 멈출지 (장풍은 false)
        /// </summary>
        public static void HitTarget(GameObject target, int damage, HitInfo info, Hitbox hitbox, float direction, CharacterAttack attacker, bool shouldPushAttacker, bool shouldHitStop = true)
        {
            var reaction = target.GetComponent<CharacterHitReaction>();

            // 다운 중에는 무적 (피격 상자를 끄기 전 프레임에 닿은 공격도 맞지 않도록 여기서도 막음)
            if (reaction != null && reaction.IsDown) return;

            Transform pushedAttacker = shouldPushAttacker && attacker != null ? attacker.transform : null;
            if (reaction != null && reaction.CanGuard(info))
            {
                reaction.ReceiveGuard(info, hitbox, direction, pushedAttacker);
                return;
            }

            var targetAttack = target.GetComponent<CharacterAttack>();
            bool isAbsorbed = targetAttack != null && targetAttack.TryAbsorbHit(info.Height);

            // 맞은 쪽이 하던 공격은 ReceiveHit에서 취소되므로 그 전에 히트 종류를 정함
            // 카운터 / 퍼니시 카운터는 콤보의 첫 타만 (피격 경직 중에 맞은 2타부터와 슈퍼아머로 버틴 공격은 노말 히트)
            bool isComboHit = reaction != null && reaction.IsInHitStun;
            HitType hitType = !isAbsorbed && !isComboHit && targetAttack != null ? targetAttack.GetHitTypeOnHit() : HitType.Normal;
            int finalDamage = Mathf.RoundToInt(damage * info.GetDamageRate(hitType));

            // 맞은 쪽이 따라 움직일 때린 쪽의 속도 (장풍은 때린 쪽 몸이 아니므로 따라가지 않음)
            Vector2 attackerVelocity = shouldPushAttacker && attacker != null ? GetVelocity(attacker.gameObject) : Vector2.zero;
            if (!isAbsorbed && reaction != null) reaction.ReceiveHit(info, hitbox, direction, pushedAttacker, hitType, attackerVelocity, finalDamage);

            // 맞은 쪽의 판정 표시(StateUI)에 노말 히트 / 카운터 / 퍼니시 카운터를 넣음 (다음에 맞을 때까지 유지)
            var targetState = target.GetComponent<CharactorState>();
            if (targetState != null) SetCounterState(targetState, hitType);

            // 타격감: 맞은 쪽과 때린 쪽을 잠깐 멈춤 (게임 시간은 그대로 흐름)
            if (shouldHitStop)
            {
                int hitStopFrames = info.GetHitStopFrames(hitType);
                HitStop.Stop(target, hitStopFrames);
                if (attacker != null) HitStop.Stop(attacker.gameObject, hitStopFrames);
            }

            var health = target.GetComponent<CharacterHealth>();
            if (health != null) health.TakeDamage(finalDamage, attacker: attacker);
        }

        // 캐릭터가 지금 움직이는 속도 (플레이어는 CharacterMove, 더미는 DummyAction. 둘 다 없으면 0)
        private static Vector2 GetVelocity(GameObject character)
        {
            var move = character.GetComponent<CharacterMove>();
            if (move != null) return move.Velocity;
            var dummyAction = character.GetComponent<DummyAction>();
            return dummyAction != null ? dummyAction.DA_Velocity : Vector2.zero;
        }

        /// <summary>
        /// 피격 경직 중이면 경직이 최소 minFrames 남도록 늘림 (가드 중이거나 경직이 아니면 그대로)
        /// 맞은 기술을 캔슬해서 낸 기술이 이어서 맞도록 CharacterAttack이 호출
        /// </summary>
        public void ExtendHitStun(int minFrames)
        {
            if (HitStunFramesLeft > 0) HitStunFramesLeft = Mathf.Max(HitStunFramesLeft, minFrames);
        }

        private static void SetCounterState(CharactorState state, HitType hitType)
        {
            switch (hitType)
            {
                case HitType.Counter: state.CounterHit = true; break;
                case HitType.PunishCounter: state.PunishCounter = true; break;
                default: state.NormalHit = true; break;
            }
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

            // 히트스톱 중에는 경직이 줄지 않고 밀려나지도 않음 (풀린 프레임부터 경직 / 히트백 진행)
            if (HitStop.IsStoppedNow(this)) return;

            if (_pushBackFramesLeft > 0)
            {
                _pushBackFramesLeft--;
                MovePushBack();
            }

            if (DownFramesLeft > 0) DownFramesLeft--;

            if (HitStunFramesLeft > 0)
            {
                HitStunFramesLeft--;

                // 다운되는 공격에 맞았으면 피격 경직이 끝난 프레임부터 다운
                if (HitStunFramesLeft == 0 && _pendingDownFrames > 0)
                {
                    DownFramesLeft = _pendingDownFrames;
                    _pendingDownFrames = 0;
                }
            }
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
        /// attackerVelocity: 때린 쪽이 움직이던 속도 (1프레임 거리, 오른쪽 / 위가 +). damage: 히트 종류 배율까지 곱한 데미지
        public void ReceiveHit(HitInfo info, Hitbox hitbox, float direction, Transform pushedAttacker, HitType hitType = HitType.Normal,
                               Vector2 attackerVelocity = default, int damage = 0)
        {
            // 경직 마지막 프레임에 맞아도 아직 줄기 전이라 경직 중으로 봄
            ComboCount = IsInHitStun ? ComboCount + 1 : 1;
            LastHitType = hitType;

            // 이미 공중에 있거나 띄우는 공격에 맞으면 공중 피격 경직을 씀 (히트 종류에 피격 경직을 직접 정해 두었을 때)
            float airborneHeight = info.GetAirborneHeight(hitType);
            bool isAirborne = airborneHeight > 0f || !IsGrounded();
            HitStunFramesLeft = info.GetHitStunFrames(hitType, isAirborne);
            _pendingDownFrames = info.GetKnockDownFrames(hitType, isAirborne); // 콤보 중이면 마지막으로 맞은 공격이 다운을 정함

            // 띄우는 공격이면 피격 경직(다운되면 쓰러지는 프레임) 동안 공중에 떴다가, 끝나는 프레임에 착지
            // 공중에서 맞으면 앞으로 가던 공격(점프 공격 등)의 가로 속도를 같이 받아 밀려남
            if (airborneHeight > 0f)
                LaunchByHit(GetLaunchVelocity(airborneHeight, HitStunFramesLeft, direction, attackerVelocity, damage), HitStunFramesLeft);
            else if (!IsGrounded())
                AddAirVelocityX(GetFollowVelocityX(direction, attackerVelocity));
            GuardStunFramesLeft = 0;
            CurrentMotion = GetHitMotion(info, hitbox, hitType);
            HitCount++;
            StartPushBack(info.GetHitBackDistance(hitType), info.GetHitBackFrames(hitType), direction, pushedAttacker);

            // 맞으면 하던 공격은 취소됨
            if (CHR_characterAttack != null) CHR_characterAttack.CancelAttack();
            if (_dummyGuard != null) _dummyGuard.NotifyHit();
        }

        /// <summary>
        /// 띄우는 공격에 맞았을 때 떠오르는 첫 프레임 속도
        /// - 가로: 때린 쪽이 맞은 쪽으로 다가오던 속도를 따라감 (승룡권처럼 앞으로 나가는 공격이면 같이 앞으로 날아감)
        /// - 세로: 때린 쪽이 뛰어오르던 속도를 따라감 + 기술의 띄우는 높이만큼 더 뜨는 속도
        ///   빠르게 뛰어오르는 기술일수록 높이 뜨고, 그 전체에 데미지 배율을 곱해서 데미지가 클수록 높이 뜸
        /// frames 프레임째에 바닥에 닿도록 중력이 정해지므로, 처음 속도가 빠를수록 더 높이 떴다가 같은 프레임에 착지함
        /// </summary>
        private Vector2 GetLaunchVelocity(float airborneHeight, int frames, float direction, Vector2 attackerVelocity, int damage)
        {
            int safeFrames = Mathf.Max(1, frames);
            float followY = Mathf.Max(0f, attackerVelocity.y) * _attackerVelocityFollowRatio;
            float extraY = 4f * airborneHeight / safeFrames; // 바닥에서 이 속도로 뜨면 가장 높은 곳이 airborneHeight
            return new Vector2(GetFollowVelocityX(direction, attackerVelocity), (followY + extraY) * GetDamageScale(damage));
        }

        // 때린 쪽이 맞은 쪽으로 다가오던 가로 속도 (멀어지던 중이면 따라가지 않음)
        private float GetFollowVelocityX(float direction, Vector2 attackerVelocity)
        {
            bool isMovingToward = attackerVelocity.x * direction > 0f;
            return isMovingToward ? attackerVelocity.x * _attackerVelocityFollowRatio : 0f;
        }

        // 데미지가 기준 데미지의 몇 배인지 (최소 / 최대 안에서). 데미지를 모르면 1배
        private float GetDamageScale(int damage)
        {
            if (damage <= 0 || _launchReferenceDamage <= 0f) return 1f;
            return Mathf.Clamp(damage / _launchReferenceDamage, _launchDamageScaleRange.x, _launchDamageScaleRange.y);
        }

        private void LaunchByHit(Vector2 velocity, int frames)
        {
            if (CHR_characterMove != null) CHR_characterMove.LaunchIntoAirWithVelocity(velocity, airFrames: frames);
            else if (_dummyAction != null) _dummyAction.LaunchIntoAir(velocity, frames);
        }

        private void AddAirVelocityX(float x)
        {
            if (x == 0f) return;
            if (CHR_characterMove != null) CHR_characterMove.AddAirVelocityX(x);
            else if (_dummyAction != null) _dummyAction.AddAirVelocityX(x);
        }

        private void OnValidate()
        {
            if (_attackerVelocityFollowRatio < 0f) _attackerVelocityFollowRatio = 0f;
            if (_launchReferenceDamage < 0f) _launchReferenceDamage = 0f;
            if (_launchDamageScaleRange.x < 0f) _launchDamageScaleRange.x = 0f;
            if (_launchDamageScaleRange.y < _launchDamageScaleRange.x) _launchDamageScaleRange.y = _launchDamageScaleRange.x;
        }

        // 플레이어는 CharacterMove, 더미는 DummyAction으로 땅에 있는지 봄 (둘 다 없으면 땅에 있다고 봄)
        private bool IsGrounded()
        {
            if (CHR_characterMove != null) return CHR_characterMove.IsGrounded;
            return _dummyAction == null || _dummyAction.DA_IsGrounded;
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
            DownFramesLeft = 0;
            _pendingDownFrames = 0;
            GuardStunFramesLeft = 0;
            ComboCount = 0;
            _reactionStartFrame = -1;
            _pushBackFramesLeft = 0;
            _pushBackAttacker = null;
            if (_dummyGuard != null) _dummyGuard.ResetRecord();
        }

        // 히트 종류에 정한 모션 → 기술의 모션 순서로 쓰고, 둘 다 자동이면
        // 상단 공격은 MiddleHit, 중단 공격은 Top2Low, 하단 공격은 맞은 피격 상자 위치에 따라 정함
        private HitMotion GetHitMotion(HitInfo info, Hitbox hitbox, HitType hitType)
        {
            HitMotion motion = info.GetMotion(hitType);
            if (motion != HitMotion.Auto) return motion;
            if (info.Height == AttackHeight.High) return HitMotion.Middle;
            if (info.Height == AttackHeight.Middle) return HitMotion.Top2Low;
            return _characterHitboxes != null ? _characterHitboxes.GetMotionByHitbox(hitbox) : HitMotion.Low;
        }
    }
}
