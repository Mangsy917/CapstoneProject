using UnityEngine;

namespace Game.Character
{
    // 캐릭터의 자식 오브젝트에 BoxCollider(Is Trigger)와 함께 붙여서 사용하는 피격 판정 상자
    // - 통과 가능한 콜라이더라서 캐릭터를 막지 않음 (캐릭터끼리 막는 것은 본체의 Pushbox 콜라이더가 함)
    // - 공격 판정 상자와 겹치면 맞은 것으로 봄 (물리엔진 없이 상자 범위로 계산)
    // - 좌우가 뒤집혀도 같도록 가운데(x = 0)에 두는 것을 권장
    [RequireComponent(typeof(BoxCollider))]
    public class Hurtbox : MonoBehaviour
    {
        [Tooltip("피격 범위로 쓸 BoxCollider. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private BoxCollider H_boxCollider;

        private void Awake()
        {
            if (H_boxCollider == null) H_boxCollider = GetComponent<BoxCollider>();
            if (H_boxCollider != null) H_boxCollider.isTrigger = true;
        }

        /// <summary>target(또는 그 자식)의 켜져 있는 피격 상자 중 하나라도 공격 상자와 겹치는지</summary>
        public static bool IsHitting(GameObject target, Vector2 hitboxCenter, Vector2 hitboxSize)
        {
            foreach (var hurtbox in target.GetComponentsInChildren<Hurtbox>())
            {
                if (hurtbox.isActiveAndEnabled && hurtbox.IsOverlapping(hitboxCenter, hitboxSize)) return true;
            }
            return false;
        }

        /// <summary>target(또는 그 자식)에 피격 상자가 하나라도 있는지</summary>
        public static bool HasHurtbox(GameObject target)
        {
            return target.GetComponentInChildren<Hurtbox>() != null;
        }

        // 공격 상자(월드 단위 중심, 크기)와 이 피격 상자가 x, y 모두 겹치는지
        public bool IsOverlapping(Vector2 hitboxCenter, Vector2 hitboxSize)
        {
            if (H_boxCollider == null) return false;

            Vector2 center = transform.TransformPoint(H_boxCollider.center);
            Vector3 scale = transform.lossyScale;
            Vector2 halfSize = new Vector2(Mathf.Abs(H_boxCollider.size.x * scale.x), Mathf.Abs(H_boxCollider.size.y * scale.y)) * 0.5f;
            Vector2 hitboxHalfSize = new Vector2(Mathf.Abs(hitboxSize.x), Mathf.Abs(hitboxSize.y)) * 0.5f;

            bool isOverlappingX = Mathf.Abs(center.x - hitboxCenter.x) < halfSize.x + hitboxHalfSize.x;
            bool isOverlappingY = Mathf.Abs(center.y - hitboxCenter.y) < halfSize.y + hitboxHalfSize.y;
            return isOverlappingX && isOverlappingY;
        }
    }
}
