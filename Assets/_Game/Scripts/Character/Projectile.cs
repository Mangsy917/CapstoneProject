using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Character
{
    // 장풍 같은 투사체. CharacterAttack이 생성한 뒤 Launch로 날아갈 방향, 쏜 캐릭터, 맞힐 상대 태그를 정함
    // - 바라보는 쪽으로 일정한 속도로 날아감
    // - 화면(카메라에 보이는 범위) 밖으로 완전히 나가거나 스테이지 끝 벽(CharStageBoundary)에 닿으면 사라짐
    // - 상대 캐릭터(맞힐 태그)의 피격 상자(Hitbox)에 닿으면 데미지를 주고 사라짐
    //   (피격 상자가 없는 상대는 Pushbox, 없으면 Collider 범위로 판정)
    // - 개수(AttackData의 투사체 개수)를 가지고, 다른 캐릭터의 투사체와 부딪히면 서로 1씩 줄어 0이 되면 사라짐
    //   (같은 투사체끼리는 한 번만 부딪힘)
    // - 쏜 캐릭터의 투사체가 남아 있는 동안은 같은 캐릭터가 투사체 공격을 다시 쓸 수 없음 (HasActiveProjectile)
    public class Projectile : MonoBehaviour
    {
        [Tooltip("1프레임에 날아가는 거리. 공격(AttackData)의 버튼별 값이 있으면 그 값을 씀")]
        [SerializeField] private float _distancePerFrame = 0.07f;

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
        private HitInfo _hitInfo; // 맞혔을 때 줄 피격 경직 / 히트백 / 피격 모션 (쏜 공격의 AttackData 값)
        private int _remainingCount = 1; // 남은 개수 (0이 되면 사라짐)
        private bool _isDestroyed; // 사라지는 중인지 (Destroy는 프레임 끝에 지워지므로 그 전에 다시 처리하지 않도록)
        private readonly HashSet<Projectile> _clashedProjectiles = new HashSet<Projectile>(); // 이미 부딪힌 투사체
        private CharStageBoundary P_stageBoundary; // 스테이지 끝 위치 (없으면 벽 판정 안 함)

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
            P_stageBoundary = FindAnyObjectByType<CharStageBoundary>();
        }

        private void OnEnable()
        {
            _activeProjectiles.Add(this);
        }

        private void OnDisable()
        {
            _activeProjectiles.Remove(this);
        }

        /// <summary>distancePerFrame: 1프레임에 날아가는 거리 (0이면 프리팹의 값). count: 투사체의 개수 (다른 투사체와 부딪힐 때마다 1씩 줄어 0이면 사라짐)</summary>
        public void Launch(bool isFacingRight, GameObject owner, string targetTag, int damage, HitInfo hitInfo, float distancePerFrame = 0f, int count = 1)
        {
            P_direction = isFacingRight ? 1f : -1f;
            _owner = owner;
            _targetTag = targetTag;
            P_damage = damage;
            _hitInfo = hitInfo;
            _remainingCount = Mathf.Max(1, count);
            if (distancePerFrame > 0f) _distancePerFrame = distancePerFrame;
            if (P_spriteRenderer != null) P_spriteRenderer.flipX = !isFacingRight;
        }

        private void Update()
        {
            // 설정창이 열려 게임이 멈춰 있으면(timeScale 0) 날아가지 않고, 슬로우 모션 중에는 건너뛰는 프레임이 있음
            if (!Game.Field.GameSpeed.IsTickFrame) return;

            // 이번 프레임에 먼저 움직인 투사체와 부딪혀 사라지는 중이면 더 처리하지 않음
            if (_isDestroyed) return;

            transform.position += new Vector3(P_direction * _distancePerFrame, 0f, 0f);

            // 상대에게 맞거나 스테이지 끝 벽에 닿으면 개수와 상관없이 바로 사라짐
            GameObject hitTarget = FindHitTarget(out Hitbox hitbox);
            if (hitTarget != null)
            {
                // 장풍은 상대가 벽에 붙어 있어도 쏜 쪽을 밀어내지 않음
                CharacterAttack attacker = _owner != null ? _owner.GetComponent<CharacterAttack>() : null;
                CharacterHitReaction.HitTarget(hitTarget, P_damage, _hitInfo, hitbox, direction: P_direction, attacker: attacker, shouldPushAttacker: false, shouldHitStop: false);
                DestroyProjectile();
                return;
            }

            if (IsTouchingStageWall() || IsOutOfScreen())
            {
                DestroyProjectile();
                return;
            }

            ClashWithProjectiles();
        }

        // 다른 캐릭터가 쏜 투사체와 공격 판정 상자가 겹치면 서로 개수를 1씩 줄임 (같은 투사체끼리는 한 번만)
        private void ClashWithProjectiles()
        {
            GetAttackBox(out Vector2 center, out Vector2 halfSize);

            // 부딪혀 사라진 투사체는 목록에서 바로 빠지므로 뒤에서부터 돎
            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (i >= _activeProjectiles.Count) continue;
                Projectile other = _activeProjectiles[i];
                if (other == this || other._isDestroyed || other._owner == _owner || _clashedProjectiles.Contains(other)) continue;

                other.GetAttackBox(out Vector2 otherCenter, out Vector2 otherHalfSize);
                bool isOverlappingX = Mathf.Abs(otherCenter.x - center.x) < halfSize.x + otherHalfSize.x;
                bool isOverlappingY = Mathf.Abs(otherCenter.y - center.y) < halfSize.y + otherHalfSize.y;
                if (!isOverlappingX || !isOverlappingY) continue;

                _clashedProjectiles.Add(other);
                other._clashedProjectiles.Add(this);
                other.ReduceCount();
                ReduceCount();
                if (_isDestroyed) return;
            }
        }

        // 개수를 1 줄이고, 0이 되면 사라짐
        private void ReduceCount()
        {
            _remainingCount--;
            if (_remainingCount <= 0) DestroyProjectile();
        }

        private void DestroyProjectile()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;

            // Destroy는 프레임 끝에 지워지므로, 같은 프레임에 다시 쏠 수 있도록 목록에서는 바로 뺌
            _activeProjectiles.Remove(this);
            Destroy(gameObject);
        }

        // 공격 판정 상자의 중심과 크기의 절반 (월드 단위)
        private void GetAttackBox(out Vector2 center, out Vector2 halfSize)
        {
            center = (Vector2)transform.position + new Vector2(P_attackBoxOffset.x * P_direction, P_attackBoxOffset.y);
            halfSize = new Vector2(Mathf.Abs(P_attackBoxSize.x), Mathf.Abs(P_attackBoxSize.y)) * 0.5f;
        }

        // 날아가는 쪽 스테이지 끝 벽에 공격 판정 상자가 닿았는지 (스테이지 끝 설정이 없으면 false)
        private bool IsTouchingStageWall()
        {
            if (P_stageBoundary == null) return false;

            GetAttackBox(out Vector2 center, out Vector2 halfSize);
            if (P_direction > 0f) return center.x + halfSize.x >= P_stageBoundary.StageRightEnd;
            return center.x - halfSize.x <= P_stageBoundary.StageLeftEnd;
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
        // hitbox: 맞은 피격 상자 (피격 상자 없이 판정했으면 null)
        private GameObject FindHitTarget(out Hitbox hitbox)
        {
            hitbox = null;
            GetAttackBox(out Vector2 center, out Vector2 halfSize);

            foreach (GameObject target in GameObject.FindGameObjectsWithTag(_targetTag))
            {
                if (target == _owner) continue;

                if (Hitbox.HasHitbox(target))
                {
                    hitbox = Hitbox.FindHitHitbox(target, center, P_attackBoxSize);
                    if (hitbox != null) return target;
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
