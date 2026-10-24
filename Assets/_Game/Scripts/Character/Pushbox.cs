using System.Collections.Generic;
using UnityEngine;

namespace Game.Character
{
    // 물리엔진 없이 캐릭터끼리, 캐릭터와 벽이 서로 겹치지 않게 밀어내는 상자 (격투게임의 밀림 판정)
    // - 캐릭터(Player, Enemy)와 벽(스테이지 끝 벽, Boundary 벽)에 붙여서 사용
    // - 모든 이동(Update)이 끝난 뒤 한 번에 겹침을 풀어줌
    //   1) 캐릭터끼리 겹치면 반씩 밀어냄. 한쪽이 벽에 붙어 있으면 다른 쪽이 전부 밀려남
    //   2) 캐릭터가 벽과 겹치면 벽 바깥(원래 있던 쪽)으로 되돌림
    // - 어느 쪽에 있었는지는 이전 프레임 위치로 정하기 때문에, 한 프레임에 많이 움직여도 벽을 통과하지 않음
    // - x축(좌우)으로만 밀어냄. 점프로 상대 위를 넘어가는 것은 높이가 겹치지 않으므로 허용
    // 캐릭터 이동(CharacterMove, CharacterAttack)이 끝난 뒤 실행되도록 순서를 뒤로 미룸
    [DefaultExecutionOrder(50)]
    public class Pushbox : MonoBehaviour
    {
        [Tooltip("체크하면 다른 상자에 밀려서 움직임 (캐릭터). 끄면 움직이지 않는 벽")]
        [SerializeField] private bool _canBePushed = true;

        [Tooltip("체크하면 높이와 상관없이 막음 (점프로 넘어갈 수 없는 벽)")]
        [SerializeField] private bool _ignoreHeight = false;

        [Tooltip("상자 크기로 쓸 BoxCollider. 비워두면 같은 오브젝트에서 찾고, 없으면 아래 Size / Offset을 사용")]
        [SerializeField] private BoxCollider _boxCollider;

        [Tooltip("BoxCollider가 없을 때 쓰는 상자 크기 (월드 단위)")]
        [SerializeField] private Vector2 _size = new Vector2(1f, 2f);

        [Tooltip("BoxCollider가 없을 때 쓰는 상자 중심 위치 (오브젝트 위치 기준, 월드 단위)")]
        [SerializeField] private Vector2 _offset = Vector2.zero;

        // 겹침을 풀 때 반복하는 횟수 (캐릭터 → 벽 → 다시 캐릭터 순으로 밀림이 이어질 때를 위해)
        private const int SolveIterations = 3;
        // 이 거리 이하로 붙어 있으면 맞닿은 것으로 봄
        private const float TouchDistance = 0.01f;

        private static readonly List<Pushbox> _allPushboxes = new List<Pushbox>();
        private static int _lastSolvedFrame = -1;

        private float _previousX; // 이전 프레임의 상자 중심 x (어느 쪽에 있었는지 판단용)

        public bool CanBePushed => _canBePushed;

        private void Awake()
        {
            if (_boxCollider == null) _boxCollider = GetComponent<BoxCollider>();

            // 물리엔진이 위치를 바꾸지 않도록 Rigidbody가 있으면 끔 (중력, 충돌 밀어내기 모두 사용 안 함)
            var body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        private void OnEnable()
        {
            _previousX = CenterX;
            _allPushboxes.Add(this);
        }

        private void OnDisable()
        {
            _allPushboxes.Remove(this);
        }

        private void Update()
        {
            // 상자가 여러 개라도 한 프레임에 한 번만 계산
            if (_lastSolvedFrame == Time.frameCount) return;
            _lastSolvedFrame = Time.frameCount;
            SolveAll();
        }

        // =====================================================================
        // 상자 위치와 크기 (월드 단위)
        // =====================================================================

        public float CenterX => GetCenter().x;
        public float HalfWidth => GetHalfSize().x;
        public float Bottom => GetCenter().y - GetHalfSize().y;
        public float Top => GetCenter().y + GetHalfSize().y;

        private Vector2 GetCenter()
        {
            if (_boxCollider != null) return transform.TransformPoint(_boxCollider.center);
            return (Vector2)transform.position + _offset;
        }

        private Vector2 GetHalfSize()
        {
            if (_boxCollider != null)
            {
                Vector3 scale = transform.lossyScale;
                return new Vector2(Mathf.Abs(_boxCollider.size.x * scale.x), Mathf.Abs(_boxCollider.size.y * scale.y)) * 0.5f;
            }
            return new Vector2(Mathf.Abs(_size.x), Mathf.Abs(_size.y)) * 0.5f;
        }

        private void MoveX(float distance)
        {
            if (distance == 0f) return;
            transform.position += new Vector3(distance, 0f, 0f);
        }

        // =====================================================================
        // 다른 스크립트에서 쓰는 기능
        // =====================================================================

        /// <summary>
        /// 캐릭터를 순간이동시킨 뒤(시작 위치로 되돌리기, 좌우 바꾸기 등) 호출.
        /// 지금 위치를 이전 위치로 기억해서, 벽을 넘어간 것으로 보고 원래 쪽으로 되돌리지 않도록 함
        /// </summary>
        public static void SyncAfterTeleport()
        {
            foreach (var box in _allPushboxes)
            {
                box._previousX = box.CenterX;
            }
        }

        /// <summary>이 상자를 direction(1: 오른쪽, -1: 왼쪽)으로 밀면 바로 벽에 막히는지 (예: 히트백 중 벽에 붙었는지)</summary>
        public bool IsTouchingWall(float direction)
        {
            return IsBlockedByWall(this, direction);
        }

        /// <summary>두 상자가 맞닿아 있거나 겹쳐 있는지 (예: 달리다가 상대에게 붙었는지)</summary>
        public static bool IsTouching(Pushbox a, Pushbox b)
        {
            if (a == null || b == null) return false;
            if (!IsOverlappingY(a, b)) return false;
            float gap = Mathf.Abs(b.CenterX - a.CenterX) - (a.HalfWidth + b.HalfWidth);
            return gap <= TouchDistance;
        }

        // =====================================================================
        // 겹침 풀기
        // =====================================================================

        private static void SolveAll()
        {
            for (int iteration = 0; iteration < SolveIterations; iteration++)
            {
                // 1) 캐릭터끼리
                for (int i = 0; i < _allPushboxes.Count; i++)
                {
                    if (!_allPushboxes[i]._canBePushed) continue;
                    for (int j = i + 1; j < _allPushboxes.Count; j++)
                    {
                        if (!_allPushboxes[j]._canBePushed) continue;
                        SeparateCharacters(_allPushboxes[i], _allPushboxes[j]);
                    }
                }

                // 2) 캐릭터와 벽
                foreach (var character in _allPushboxes)
                {
                    if (!character._canBePushed) continue;
                    foreach (var wall in _allPushboxes)
                    {
                        if (wall._canBePushed) continue;
                        PushOutOfWall(character, wall);
                    }
                }
            }

            foreach (var box in _allPushboxes)
            {
                box._previousX = box.CenterX;
            }
        }

        private static bool IsOverlappingY(Pushbox a, Pushbox b)
        {
            if (a._ignoreHeight || b._ignoreHeight) return true;
            return a.Bottom < b.Top && b.Bottom < a.Top;
        }

        // b가 a의 오른쪽이면 1, 왼쪽이면 -1
        private static float GetSide(Pushbox a, Pushbox b)
        {
            float distanceX = b.CenterX - a.CenterX;
            if (Mathf.Abs(distanceX) > TouchDistance) return Mathf.Sign(distanceX);

            // 거의 같은 위치면 이전 프레임 위치로 판단 (그것도 같으면 b를 오른쪽으로)
            float previousDistanceX = b._previousX - a._previousX;
            return previousDistanceX < 0f ? -1f : 1f;
        }

        private static void SeparateCharacters(Pushbox a, Pushbox b)
        {
            if (!IsOverlappingY(a, b)) return;

            float overlap = a.HalfWidth + b.HalfWidth - Mathf.Abs(b.CenterX - a.CenterX);
            if (overlap <= 0f) return;

            float side = GetSide(a, b);

            // 벽에 붙어 있어서 더 밀려날 수 없는 쪽은 그대로 두고, 반대쪽이 전부 밀려남
            bool isABlocked = IsBlockedByWall(a, direction: -side);
            bool isBBlocked = IsBlockedByWall(b, direction: side);
            float aShare = 0.5f;
            if (isABlocked && !isBBlocked) aShare = 0f;
            else if (isBBlocked && !isABlocked) aShare = 1f;

            a.MoveX(-side * overlap * aShare);
            b.MoveX(side * overlap * (1f - aShare));
        }

        // character를 direction(1: 오른쪽, -1: 왼쪽)으로 밀었을 때 바로 벽에 막히는지
        private static bool IsBlockedByWall(Pushbox character, float direction)
        {
            foreach (var wall in _allPushboxes)
            {
                if (wall._canBePushed || !IsOverlappingY(character, wall)) continue;

                float wallSide = GetWallSide(character, wall);
                if (wallSide != direction) continue; // 반대쪽 벽

                float gap = (wall.CenterX - character.CenterX) * direction - (character.HalfWidth + wall.HalfWidth);
                if (gap <= TouchDistance) return true;
            }
            return false;
        }

        // 벽이 캐릭터의 오른쪽이면 1, 왼쪽이면 -1. 이전 프레임 위치로 정해서 벽을 뚫고 지나가도 원래 쪽으로 판단함
        private static float GetWallSide(Pushbox character, Pushbox wall)
        {
            float previousDistanceX = wall._previousX - character._previousX;
            if (previousDistanceX != 0f) return Mathf.Sign(previousDistanceX);
            return wall.CenterX >= character.CenterX ? 1f : -1f;
        }

        private static void PushOutOfWall(Pushbox character, Pushbox wall)
        {
            if (!IsOverlappingY(character, wall)) return;

            float wallSide = GetWallSide(character, wall);
            // 캐릭터가 있어야 하는 가장 벽 쪽 위치
            float limitX = wall.CenterX - wallSide * (wall.HalfWidth + character.HalfWidth);

            float pushDistance = limitX - character.CenterX;
            if (pushDistance * wallSide < 0f) // 벽을 넘어간 만큼 되돌림
                character.MoveX(pushDistance);
        }

        // 씬 뷰에서 상자를 초록(캐릭터) / 빨강(벽) 사각형으로 보여줌
        private void OnDrawGizmosSelected()
        {
            if (_boxCollider == null) _boxCollider = GetComponent<BoxCollider>();
            Gizmos.color = _canBePushed ? Color.green : Color.red;
            Vector2 center = GetCenter();
            Vector2 halfSize = GetHalfSize();
            Gizmos.DrawWireCube(new Vector3(center.x, center.y, transform.position.z), new Vector3(halfSize.x * 2f, halfSize.y * 2f, 0.1f));
        }
    }
}
