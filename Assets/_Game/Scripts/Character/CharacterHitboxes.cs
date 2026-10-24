using System.Collections.Generic;
using UnityEngine;

namespace Game.Character
{
    // 캐릭터 본체에 붙여서 사용. 자세와 공격 모션에 맞춰 피격 상자(Hitbox)를 켜고 끄거나 더 만듦
    // - Top / Middle / Bottom: 평소 피격 상자. 앉아 있으면 Top을 끔
    //   (공격 중에는 CharacterMove가 앉기 상태가 아니므로, 앉아서 쓰는 공격은 AttackData의 Should Hide Top Hitbox로 끔)
    // - 공격 중에는 AttackData의 Extra Hitboxes(뻗은 팔, 다리 등)를 정한 프레임 구간에만 켬
    // - 완전 무적인 동안(CharacterAttack.IsInvincible)은 모든 피격 상자를 끔
    // 이동과 공격 처리가 모두 끝난 뒤에 맞추도록 실행 순서를 뒤로 미룸
    [DefaultExecutionOrder(30)]
    public class CharacterHitboxes : MonoBehaviour
    {
        [Header("평소 피격 상자")]
        [SerializeField] private Hitbox _topHitbox;
        [SerializeField] private Hitbox _middleHitbox;
        [SerializeField] private Hitbox _bottomHitbox;

        [Tooltip("공격 모션에 따라 더 생기는 피격 상자를 넣을 부모. 비워두면 Top 피격 상자의 부모")]
        [SerializeField] private Transform _extraHitboxParent;

        [Tooltip("앉기 상태와 바라보는 방향을 알려줄 CharacterMove. 비워두면 같은 오브젝트에서 찾음 (없으면 앉지 않음)")]
        [SerializeField] private CharacterMove CH_characterMove;

        [Tooltip("공격 모션을 알려줄 CharacterAttack. 비워두면 같은 오브젝트에서 찾음 (없으면 공격 피격 상자 없음)")]
        [SerializeField] private CharacterAttack CH_characterAttack;

        // 공격 모션에 따라 켜고 끄는 피격 상자 (필요한 만큼 만들어 두고 다시 씀)
        private readonly List<Hitbox> _extraHitboxPool = new List<Hitbox>();

        private void Awake()
        {
            if (CH_characterMove == null) CH_characterMove = GetComponent<CharacterMove>();
            if (CH_characterAttack == null) CH_characterAttack = GetComponent<CharacterAttack>();
            if (_extraHitboxParent == null && _topHitbox != null) _extraHitboxParent = _topHitbox.transform.parent;
            if (_extraHitboxParent == null) _extraHitboxParent = transform;
        }

        private void Update()
        {
            // 슬로우 모션 중에는 건너뛰는 프레임이 있음 (GameSpeed)
            if (!Game.Field.GameSpeed.IsTickFrame) return;

            AttackData attack = GetCurrentAttack();

            // 완전 무적인 동안(예: EX 승룡권 1~8프레임)은 피격 상자를 모두 끔
            bool isInvincible = attack != null && CH_characterAttack.IsInvincible;

            bool isCrouching = CH_characterMove != null && CH_characterMove.IsCrouching;
            bool shouldHideTop = isCrouching || (attack != null && attack.ShouldHideTopHitbox);
            if (_topHitbox != null) _topHitbox.gameObject.SetActive(!shouldHideTop && !isInvincible);
            if (_middleHitbox != null) _middleHitbox.gameObject.SetActive(!isInvincible);
            if (_bottomHitbox != null) _bottomHitbox.gameObject.SetActive(!isInvincible);

            int usedCount = 0;
            if (attack != null && attack.ExtraHitboxes != null && !isInvincible)
            {
                float facingSign = CH_characterMove == null || CH_characterMove.IsFacingRight ? 1f : -1f;
                foreach (var extraHitbox in attack.ExtraHitboxes)
                {
                    if (extraHitbox == null || !extraHitbox.IsActiveAt(CH_characterAttack.AttackFrame, CH_characterAttack.CurrentFrames)) continue;

                    Hitbox hitbox = GetExtraHitbox(usedCount++);
                    hitbox.gameObject.SetActive(true);
                    Vector2 center = (Vector2)transform.position + new Vector2(extraHitbox.Offset.x * facingSign, extraHitbox.Offset.y);
                    hitbox.SetWorldBox(center, extraHitbox.Size);
                }
            }

            for (int i = usedCount; i < _extraHitboxPool.Count; i++)
            {
                _extraHitboxPool[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 이 피격 상자에 맞았을 때의 피격 모션 (Top → 상단, Middle → 중단, Bottom → 하단)
        /// 공격 모션으로 더 생긴 피격 상자는 높이가 가장 가까운 평소 피격 상자를 따름
        /// </summary>
        public HitMotion GetMotionByHitbox(Hitbox hitbox)
        {
            if (hitbox == null) return HitMotion.Middle;
            if (hitbox == _topHitbox) return HitMotion.Top;
            if (hitbox == _middleHitbox) return HitMotion.Middle;
            if (hitbox == _bottomHitbox) return HitMotion.Low;

            float y = hitbox.WorldCenter.y;
            HitMotion closestMotion = HitMotion.Middle;
            float closestDistance = float.MaxValue;
            CheckCloser(_topHitbox, HitMotion.Top, y, ref closestMotion, ref closestDistance);
            CheckCloser(_middleHitbox, HitMotion.Middle, y, ref closestMotion, ref closestDistance);
            CheckCloser(_bottomHitbox, HitMotion.Low, y, ref closestMotion, ref closestDistance);
            return closestMotion;
        }

        private static void CheckCloser(Hitbox hitbox, HitMotion motion, float y, ref HitMotion closestMotion, ref float closestDistance)
        {
            if (hitbox == null) return;

            float distance = Mathf.Abs(hitbox.WorldCenter.y - y);
            if (distance >= closestDistance) return;
            closestDistance = distance;
            closestMotion = motion;
        }

        // 지금 피격 상자를 바꿔야 하는 공격 (착지 경직 중에는 평소 피격 상자)
        private AttackData GetCurrentAttack()
        {
            if (CH_characterAttack == null || !CH_characterAttack.IsAttacking || CH_characterAttack.IsLanding) return null;
            return CH_characterAttack.CurrentAttack;
        }

        private Hitbox GetExtraHitbox(int index)
        {
            while (_extraHitboxPool.Count <= index)
            {
                var extraObject = new GameObject("Extra" + _extraHitboxPool.Count);
                extraObject.layer = gameObject.layer;
                extraObject.transform.SetParent(_extraHitboxParent, false);

                var boxCollider = extraObject.AddComponent<BoxCollider>();
                boxCollider.isTrigger = true;
                _extraHitboxPool.Add(extraObject.AddComponent<Hitbox>()); // Hitbox가 z 두께를 0.2로 맞춤
            }
            return _extraHitboxPool[index];
        }
    }
}
