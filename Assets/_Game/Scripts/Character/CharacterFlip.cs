using UnityEngine;

namespace Game.Character
{
    // 캐릭터(Player, Enemy)에 붙여서 사용
    // 상대 캐릭터가 있는 쪽을 바라보도록 스프라이트를 좌우로 뒤집음
    // - 스프라이트 기본 방향은 오른쪽을 바라봄 (왼쪽에 서서 오른쪽 상대를 바라보는 모습)
    // - 상대가 왼쪽에 있으면 flipX를 켬 (콜라이더 등 다른 것은 뒤집지 않음)
    // - CharacterMove가 있으면 그 좌우 판정(IsFacingRight)을 그대로 따름 (땅에 있을 때만 바뀜)
    // - CharacterMove가 없으면(Enemy 등) 상대와의 x 위치로 직접 판정함
    // - 피격 / 가드 스프라이트는 다른 스프라이트와 반대로(왼쪽을 바라보게) 그려져 있어서, 피격 / 가드 경직 중에는 반대로 뒤집음
    public class CharacterFlip : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;

        // 비워두면 같은 오브젝트에서 찾음 (없으면 x 위치로 판정)
        [SerializeField] private CharacterMove CF_characterMove;

        [Tooltip("피격 / 가드 경직 중인지 알려줄 CharacterHitReaction. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterHitReaction CF_hitReaction;

        [Tooltip("체크하면 피격 / 가드 경직 중에는 반대로 뒤집음 (피격 / 가드 스프라이트가 왼쪽을 바라보게 그려져 있을 때)")]
        [SerializeField] private bool _isStunSpriteFacingLeft = true;

        private bool CF_isFacingRight = true;

        // 바라볼 상대의 태그 (Player에는 Enemy, Enemy에는 Player)
        [SerializeField] private string _opponentTag = "Enemy";

        private Transform _opponent;

        void Awake()
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (CF_characterMove == null) CF_characterMove = GetComponent<CharacterMove>();
            if (CF_hitReaction == null) CF_hitReaction = GetComponent<CharacterHitReaction>();
        }

        // 이동이 모두 끝난 뒤 방향을 맞춤
        void LateUpdate()
        {
            if (_spriteRenderer == null) return;

            UpdateFacing();

            bool isStunSprite = _isStunSpriteFacingLeft && CF_hitReaction != null && CF_hitReaction.IsInStun;
            _spriteRenderer.flipX = isStunSprite ? CF_isFacingRight : !CF_isFacingRight;
        }

        private void UpdateFacing()
        {
            if (CF_characterMove != null)
            {
                CF_isFacingRight = CF_characterMove.IsFacingRight;
                return;
            }

            if (_opponent == null || !_opponent.gameObject.activeInHierarchy) FindOpponent();

            // 상대가 없거나 x가 같으면 이전 방향 유지
            if (_opponent == null) return;

            float distanceX = _opponent.position.x - transform.position.x;
            if (distanceX > 0f) CF_isFacingRight = true;
            else if (distanceX < 0f) CF_isFacingRight = false;
        }

        // 상대가 여러 명이면 가장 가까운 상대를 바라봄
        private void FindOpponent()
        {
            _opponent = null;
            float closestDistance = float.MaxValue;
            foreach (GameObject opponent in GameObject.FindGameObjectsWithTag(_opponentTag))
            {
                if (opponent == gameObject) continue;

                float distance = (opponent.transform.position - transform.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    _opponent = opponent.transform;
                }
            }
        }
    }
}
