using System.Collections.Generic;
using UnityEngine;

namespace Game.Character
{
    // 캐릭터의 자식 오브젝트(Hitbox/Top, Middle, Bottom 등)에 BoxCollider(Is Trigger)와 함께 붙여서 사용하는 피격 상자
    // - 통과 가능한 콜라이더라서 캐릭터를 막지 않음 (캐릭터끼리 막는 것은 본체의 Pushbox 콜라이더가 함)
    // - 상대의 공격 상자(AttackBox)와 겹치면 맞은 것으로 봄 (물리엔진 없이 상자 범위로 계산)
    // - 앉기 / 공격 모션에 따라 켜고 끄거나 위치를 바꾸는 것은 CharacterHitboxes가 함
    [RequireComponent(typeof(BoxCollider))]
    public class Hitbox : MonoBehaviour
    {
        [Tooltip("피격 범위로 쓸 BoxCollider. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private BoxCollider H_boxCollider;

        // 모든 피격 상자의 z 두께 (월드 단위). 판정은 x, y로만 하므로 보기 좋게 통일하는 값
        private const float Depth = 0.2f;

        // 피격 상자를 찾을 때 담아 둘 목록 (공격할 때마다 새 배열을 만들지 않도록 하나를 다시 씀)
        private static readonly List<Hitbox> _hitboxBuffer = new List<Hitbox>();

        private void Awake()
        {
            if (H_boxCollider == null) H_boxCollider = GetComponent<BoxCollider>();
            if (H_boxCollider == null) return;

            H_boxCollider.isTrigger = true;
            Vector3 size = H_boxCollider.size;
            size.z = Depth / Mathf.Abs(transform.lossyScale.z);
            H_boxCollider.size = size;
        }

        /// <summary>피격 상자 중심 (월드 단위)</summary>
        public Vector2 WorldCenter => H_boxCollider != null ? (Vector2)transform.TransformPoint(H_boxCollider.center) : (Vector2)transform.position;

        /// <summary>
        /// target(또는 그 자식)의 켜져 있는 피격 상자 중 공격 상자와 겹치는 것 (없으면 null)
        /// 여러 개가 겹치면 가장 많이 겹친 피격 상자 (피격 모션을 정할 때 사용)
        /// </summary>
        public static Hitbox FindHitHitbox(GameObject target, Vector2 attackBoxCenter, Vector2 attackBoxSize)
        {
            Hitbox bestHitbox = null;
            float bestArea = 0f;
            target.GetComponentsInChildren(_hitboxBuffer);
            foreach (var hitbox in _hitboxBuffer)
            {
                if (!hitbox.isActiveAndEnabled) continue;

                float area = hitbox.GetOverlapArea(attackBoxCenter, attackBoxSize);
                if (area > bestArea)
                {
                    bestArea = area;
                    bestHitbox = hitbox;
                }
            }
            return bestHitbox;
        }

        /// <summary>target(또는 그 자식)에 피격 상자가 하나라도 있는지 (꺼져 있는 것 포함)</summary>
        public static bool HasHitbox(GameObject target)
        {
            return target.GetComponentInChildren<Hitbox>(true) != null;
        }

        // 공격 상자와 이 피격 상자가 겹치는 넓이 (겹치지 않으면 0)
        private float GetOverlapArea(Vector2 attackBoxCenter, Vector2 attackBoxSize)
        {
            if (H_boxCollider == null) return 0f;

            Vector2 center = WorldCenter;
            Vector3 scale = transform.lossyScale;
            Vector2 halfSize = new Vector2(Mathf.Abs(H_boxCollider.size.x * scale.x), Mathf.Abs(H_boxCollider.size.y * scale.y)) * 0.5f;
            Vector2 attackBoxHalfSize = new Vector2(Mathf.Abs(attackBoxSize.x), Mathf.Abs(attackBoxSize.y)) * 0.5f;

            float overlapX = halfSize.x + attackBoxHalfSize.x - Mathf.Abs(center.x - attackBoxCenter.x);
            float overlapY = halfSize.y + attackBoxHalfSize.y - Mathf.Abs(center.y - attackBoxCenter.y);
            if (overlapX <= 0f || overlapY <= 0f) return 0f;

            // 한쪽 상자가 다른 쪽 안에 들어가 있으면 작은 쪽 크기까지만 겹침
            overlapX = Mathf.Min(overlapX, halfSize.x * 2f, attackBoxHalfSize.x * 2f);
            overlapY = Mathf.Min(overlapY, halfSize.y * 2f, attackBoxHalfSize.y * 2f);
            return overlapX * overlapY;
        }

        /// <summary>피격 상자를 캐릭터 기준 월드 단위 위치 / 크기로 맞춤 (공격 모션에 따라 바뀌는 상자에 사용)</summary>
        public void SetWorldBox(Vector2 worldCenter, Vector2 worldSize)
        {
            if (H_boxCollider == null) return;

            Vector3 scale = transform.lossyScale;
            H_boxCollider.center = transform.InverseTransformPoint(worldCenter);
            H_boxCollider.size = new Vector3(worldSize.x / Mathf.Abs(scale.x), worldSize.y / Mathf.Abs(scale.y), Depth / Mathf.Abs(scale.z));
        }
    }
}
