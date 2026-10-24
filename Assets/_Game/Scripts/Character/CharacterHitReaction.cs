using UnityEngine;

namespace Game.Character
{
    // 공격이 맞았을 때 맞은 쪽에 전하는 정보 (AttackData.GetHitInfo로 만듦)
    public readonly struct HitInfo
    {
        public int HitStunFrames { get; }
        public float HitBackDistance { get; }
        public int HitBackFrames { get; }
        public AttackHeight Height { get; }
        public HitMotion Motion { get; }

        public HitInfo(int hitStunFrames, float hitBackDistance, int hitBackFrames, AttackHeight height, HitMotion motion)
        {
            HitStunFrames = hitStunFrames;
            HitBackDistance = hitBackDistance;
            HitBackFrames = hitBackFrames;
            Height = height;
            Motion = motion;
        }
    }

    // 캐릭터(Player, Enemy)에 붙여서 사용하는 피격 경직 / 히트백 / 피격 모션
    // - 맞으면 하던 공격을 취소하고, 피격 경직 동안은 움직이거나 공격할 수 없음 (CharacterMove, CharacterAttack이 확인)
    // - 히트백: 맞은 쪽이 정해진 프레임 동안 뒤로 밀려남. 벽에 붙어 있으면 대신 때린 쪽이 밀려남 (장풍은 제외)
    // - 피격 모션: 공격(AttackData)에 정한 모션, 자동이면 중단 공격은 Top2Low, 그 외는 맞은 피격 상자 위치에 따라 정함
    //   재생은 CharacterAnimationPlayer가 함
    // - 슈퍼아머로 버틴 공격은 데미지만 받고 경직 / 히트백이 없음 (HitTarget)
    // 공격(CharacterAttack, 실행 순서 20)이 맞힌 다음 프레임부터 세도록, 공격보다 먼저 실행
    [DefaultExecutionOrder(10)]
    public class CharacterHitReaction : MonoBehaviour
    {
        [Tooltip("맞으면 하던 공격을 취소할 CharacterAttack. 비워두면 같은 오브젝트에서 찾음 (없어도 됨)")]
        [SerializeField] private CharacterAttack CHR_characterAttack;

        [Tooltip("맞은 피격 상자로 피격 모션을 정할 CharacterHitboxes. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterHitboxes _characterHitboxes;

        [Tooltip("히트백 중 벽에 붙었는지 확인할 Pushbox. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private Pushbox _pushbox;

        /// <summary>남은 피격 경직 프레임</summary>
        public int HitStunFramesLeft { get; private set; }

        public bool IsInHitStun => HitStunFramesLeft > 0;

        /// <summary>마지막으로 맞았을 때의 피격 모션 (자동은 들어가지 않음)</summary>
        public HitMotion CurrentMotion { get; private set; } = HitMotion.Middle;

        /// <summary>맞을 때마다 1씩 늘어남 (같은 모션으로 연달아 맞아도 처음부터 다시 재생하는 데 사용)</summary>
        public int HitCount { get; private set; }

        private int _hitBackFramesLeft;
        private float _hitBackDistancePerFrame;
        private float _hitBackDirection;    // 밀려나는 방향 (1: 오른쪽, -1: 왼쪽)
        private Transform _hitBackAttacker; // 벽에 붙었을 때 대신 밀려날 때린 쪽 (장풍이면 null)

        /// <summary>
        /// target에게 공격이 맞았을 때 호출. 슈퍼아머로 버티면 데미지만, 아니면 데미지와 피격 경직 / 히트백을 줌
        /// direction: 밀려나는 방향 (때린 쪽에서 맞은 쪽으로, 1: 오른쪽, -1: 왼쪽)
        /// shouldPushAttacker: 맞은 쪽이 벽에 붙어 있을 때 때린 쪽이 대신 밀려날지 (장풍은 false)
        /// </summary>
        public static void HitTarget(GameObject target, int damage, HitInfo info, Hitbox hitbox, float direction, CharacterAttack attacker, bool shouldPushAttacker)
        {
            var targetAttack = target.GetComponent<CharacterAttack>();
            bool isAbsorbed = targetAttack != null && targetAttack.TryAbsorbHit(info.Height);
            if (!isAbsorbed)
            {
                var reaction = target.GetComponent<CharacterHitReaction>();
                Transform pushedAttacker = shouldPushAttacker && attacker != null ? attacker.transform : null;
                if (reaction != null) reaction.ReceiveHit(info, hitbox, direction, pushedAttacker);
            }

            var health = target.GetComponent<CharacterHealth>();
            if (health != null) health.TakeDamage(damage, attacker: attacker);
        }

        private void Awake()
        {
            if (CHR_characterAttack == null) CHR_characterAttack = GetComponent<CharacterAttack>();
            if (_characterHitboxes == null) _characterHitboxes = GetComponent<CharacterHitboxes>();
            if (_pushbox == null) _pushbox = GetComponent<Pushbox>();
        }

        private void Update()
        {
            // 슬로우 모션 중에는 건너뛰는 프레임이 있음 (GameSpeed)
            if (!Game.Field.GameSpeed.IsTickFrame) return;

            if (_hitBackFramesLeft > 0)
            {
                _hitBackFramesLeft--;
                MoveHitBack();
            }

            if (HitStunFramesLeft > 0) HitStunFramesLeft--;
        }

        // 이번 프레임만큼 뒤로 밀려남. 벽에 붙어 있으면 때린 쪽을 반대로 밀어냄
        private void MoveHitBack()
        {
            Vector3 step = new Vector3(_hitBackDirection * _hitBackDistancePerFrame, 0f, 0f);
            bool isAtWall = _pushbox != null && _pushbox.IsTouchingWall(_hitBackDirection);
            if (!isAtWall)
                transform.position += step;
            else if (_hitBackAttacker != null)
                _hitBackAttacker.position -= step;
        }

        /// <summary>맞았을 때 피격 경직, 히트백, 피격 모션을 정함 (보통은 HitTarget을 통해 호출)</summary>
        public void ReceiveHit(HitInfo info, Hitbox hitbox, float direction, Transform pushedAttacker)
        {
            HitStunFramesLeft = info.HitStunFrames;
            CurrentMotion = GetMotion(info, hitbox);
            HitCount++;

            _hitBackFramesLeft = Mathf.Max(1, info.HitBackFrames);
            _hitBackDistancePerFrame = info.HitBackDistance / _hitBackFramesLeft;
            _hitBackDirection = direction < 0f ? -1f : 1f;
            _hitBackAttacker = pushedAttacker;

            // 맞으면 하던 공격은 취소됨
            if (CHR_characterAttack != null) CHR_characterAttack.CancelAttack();
        }

        /// <summary>재시작할 때 호출. 피격 경직과 히트백을 모두 취소</summary>
        public void ResetReaction()
        {
            HitStunFramesLeft = 0;
            _hitBackFramesLeft = 0;
            _hitBackAttacker = null;
        }

        private HitMotion GetMotion(HitInfo info, Hitbox hitbox)
        {
            if (info.Motion != HitMotion.Auto) return info.Motion;
            if (info.Height == AttackHeight.Middle) return HitMotion.Top2Low;
            return _characterHitboxes != null ? _characterHitboxes.GetMotionByHitbox(hitbox) : HitMotion.Middle;
        }
    }
}
