using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Character
{
    // 장풍 같은 투사체. CharacterAttack이 생성한 뒤 Launch로 날아갈 방향, 쏜 캐릭터, 맞힐 상대 태그를 정함
    // - 바라보는 쪽으로 일정한 속도로 날아감
    // - 화면(카메라에 보이는 범위) 밖으로 완전히 나가면 사라짐
    // - 상대 캐릭터(맞힐 태그)의 피격 상자(Hitbox)에 닿으면 데미지를 주고 사라짐
    //   (피격 상자가 없는 상대는 Pushbox, 없으면 Collider 범위로 판정)
    // - 쏜 캐릭터의 투사체가 남아 있는 동안은 같은 캐릭터가 투사체 공격을 다시 쓸 수 없음 (HasActiveProjectile)
    public class Projectile : MonoBehaviour
    {
        [Tooltip("날아가는 속도 (초당 거리)")]
        [SerializeField] private float _speed = 6f;

        [Tooltip("방향에 맞춰 좌우를 뒤집을 SpriteRenderer. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private SpriteRenderer P_spriteRenderer;

        [Header("공격 판정")]
        [Tooltip("상대에게 닿았는지 볼 상자 크기 (월드 단위)")]
        [FormerlySerializedAs("_hitboxSize")]
        [SerializeField] private Vector2 P_attackBoxSize = new Vector2(0.6f, 0.6f);
        [Tooltip("상자 중심 위치 (투사체 위치 기준, x는 날아가는 쪽이 +)")]
        [FormerlySerializedAs("_hitboxOffset")]
        [SerializeField] private Vector2 P_attackBoxOffset = new Vector2(0.45f, 0f);

        // 지금 화면에 있는 모든 투사체 (쏜 캐릭터별로 남아 있는지 확인할 때 사용)
        private static readonly List<Projectile> _activeProjectiles = new List<Projectile>();

        private float P_direction = 1f; // 1이면 오른쪽, -1이면 왼쪽
        private GameObject _owner;
        private string _targetTag = "Enemy";
        private int P_damage; // 맞혔을 때 줄 데미지 (쏜 공격의 AttackData 값)

        /// <summary>owner가 쏜 투사체가 아직 남아 있는지</summary>
        public static bool HasActiveProjectile(GameObject owner)
        {
            foreach (var projectile in _activeProjectiles)
            {
                if (projectile._owner == owner) return true;
            }
            return false;
        }

        private void Awake()
        {
            if (P_spriteRenderer == null) P_spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            _activeProjectiles.Add(this);
        }

        private void OnDisable()
        {
            _activeProjectiles.Remove(this);
        }

        public void Launch(bool isFacingRight, GameObject owner, string targetTag, int damage)
        {
            P_direction = isFacingRight ? 1f : -1f;
            _owner = owner;
            _targetTag = targetTag;
            P_damage = damage;
            if (P_spriteRenderer != null) P_spriteRenderer.flipX = !isFacingRight;
        }

        private void Update()
        {
            transform.position += new Vector3(P_direction * _speed * Time.deltaTime, 0f, 0f);

            GameObject hitTarget = FindHitTarget();
            if (hitTarget != null)
            {
                var health = hitTarget.GetComponent<CharacterHealth>();
                if (health != null) health.TakeDamage(P_damage);
                DestroyProjectile();
            }
            else if (IsOutOfScreen())
            {
                DestroyProjectile();
            }
        }

        private void DestroyProjectile()
        {
            // Destroy는 프레임 끝에 지워지므로, 같은 프레임에 다시 쏠 수 있도록 목록에서는 바로 뺌
            _activeProjectiles.Remove(this);
            Destroy(gameObject);
        }

        // 날아가는 쪽으로 화면 밖에 완전히 나갔는지 (투사체 그림 전체가 카메라 범위를 벗어남)
        private bool IsOutOfScreen()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null || P_spriteRenderer == null) return false;

            Bounds bounds = P_spriteRenderer.bounds;
            if (P_direction > 0f)
                return mainCamera.WorldToViewportPoint(new Vector3(bounds.min.x, bounds.center.y, bounds.center.z)).x > 1f;
            return mainCamera.WorldToViewportPoint(new Vector3(bounds.max.x, bounds.center.y, bounds.center.z)).x < 0f;
        }

        // 맞힐 태그의 캐릭터 중 공격 판정 상자와 겹치는 캐릭터 (없으면 null)
        // 상대의 피격 상자(Hitbox)로 판정하고, 피격 상자가 없으면 Pushbox, 그것도 없으면 Collider 범위로 판정
        private GameObject FindHitTarget()
        {
            Vector2 center = (Vector2)transform.position + new Vector2(P_attackBoxOffset.x * P_direction, P_attackBoxOffset.y);
            Vector2 halfSize = new Vector2(Mathf.Abs(P_attackBoxSize.x), Mathf.Abs(P_attackBoxSize.y)) * 0.5f;

            foreach (GameObject target in GameObject.FindGameObjectsWithTag(_targetTag))
            {
                if (target == _owner) continue;

                if (Hitbox.HasHitbox(target))
                {
                    if (Hitbox.IsHitting(target, center, P_attackBoxSize)) return target;
                    continue;
                }

                float targetCenterX, targetHalfWidth, targetBottom, targetTop;
                var pushbox = target.GetComponent<Pushbox>();
                if (pushbox != null)
                {
                    targetCenterX = pushbox.CenterX;
                    targetHalfWidth = pushbox.HalfWidth;
                    targetBottom = pushbox.Bottom;
                    targetTop = pushbox.Top;
                }
                else
                {
                    var targetCollider = target.GetComponent<Collider>();
                    if (targetCollider == null) continue;
                    Bounds bounds = targetCollider.bounds;
                    targetCenterX = bounds.center.x;
                    targetHalfWidth = bounds.extents.x;
                    targetBottom = bounds.min.y;
                    targetTop = bounds.max.y;
                }

                bool isOverlappingX = Mathf.Abs(targetCenterX - center.x) < targetHalfWidth + halfSize.x;
                bool isOverlappingY = center.y - halfSize.y < targetTop && targetBottom < center.y + halfSize.y;
                if (isOverlappingX && isOverlappingY) return target;
            }
            return null;
        }

        // 씬 뷰에서 공격 판정 상자를 빨간 사각형으로 보여줌
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            float direction = Application.isPlaying ? P_direction : 1f;
            Vector3 center = transform.position + new Vector3(P_attackBoxOffset.x * direction, P_attackBoxOffset.y, 0f);
            Gizmos.DrawWireCube(center, new Vector3(P_attackBoxSize.x, P_attackBoxSize.y, 0.1f));
        }
    }
}
