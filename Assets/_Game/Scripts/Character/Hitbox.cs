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

        private void Awake()
        {
            if (H_boxCollider == null) H_boxCollider = GetComponent<BoxCollider>();
            if (H_boxCollider != null) H_boxCollider.isTrigger = true;
        }

        /// <summary>target(또는 그 자식)의 켜져 있는 피격 상자 중 하나라도 공격 상자와 겹치는지</summary>
        public static bool IsHitting(GameObject target, Vector2 attackBoxCenter, Vector2 attackBoxSize)
        {
            foreach (var hitbox in target.GetComponentsInChildren<Hitbox>())
            {
                if (hitbox.isActiveAndEnabled && hitbox.IsOverlapping(attackBoxCenter, attackBoxSize)) return true;
            }
            return false;
        }

        /// <summary>target(또는 그 자식)에 피격 상자가 하나라도 있는지 (꺼져 있는 것 포함)</summary>
        public static bool HasHitbox(GameObject target)
        {
            return target.GetComponentInChildren<Hitbox>(true) != null;
        }

        // 공격 상자(월드 단위 중심, 크기)와 이 피격 상자가 x, y 모두 겹치는지
        public bool IsOverlapping(Vector2 attackBoxCenter, Vector2 attackBoxSize)
        {
            if (H_boxCollider == null) return false;

            Vector2 center = transform.TransformPoint(H_boxCollider.center);
            Vector3 scale = transform.lossyScale;
            Vector2 halfSize = new Vector2(Mathf.Abs(H_boxCollider.size.x * scale.x), Mathf.Abs(H_boxCollider.size.y * scale.y)) * 0.5f;
            Vector2 attackBoxHalfSize = new Vector2(Mathf.Abs(attackBoxSize.x), Mathf.Abs(attackBoxSize.y)) * 0.5f;

            bool isOverlappingX = Mathf.Abs(center.x - attackBoxCenter.x) < halfSize.x + attackBoxHalfSize.x;
            bool isOverlappingY = Mathf.Abs(center.y - attackBoxCenter.y) < halfSize.y + attackBoxHalfSize.y;
            return isOverlappingX && isOverlappingY;
        }

        /// <summary>피격 상자를 캐릭터 기준 월드 단위 위치 / 크기로 맞춤 (공격 모션에 따라 바뀌는 상자에 사용)</summary>
        public void SetWorldBox(Vector2 worldCenter, Vector2 worldSize)
        {
            if (H_boxCollider == null) return;

            Vector3 scale = transform.lossyScale;
            H_boxCollider.center = transform.InverseTransformPoint(worldCenter);
            H_boxCollider.size = new Vector3(worldSize.x / Mathf.Abs(scale.x), worldSize.y / Mathf.Abs(scale.y), H_boxCollider.size.z);
        }
    }
}
