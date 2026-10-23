using UnityEngine;

namespace Game.Character
{
    // 장풍 같은 투사체. CharacterAttack이 생성한 뒤 Launch로 날아갈 방향을 정함
    // - 바라보는 쪽으로 일정한 속도로 날아가고, 정해진 시간이 지나면 사라짐
    // - 아직 맞았을 때의 처리(데미지, 상쇄)는 없음
    public class Projectile : MonoBehaviour
    {
        [Tooltip("날아가는 속도 (초당 거리)")]
        [SerializeField] private float _speed = 6f;

        [Tooltip("생성 후 사라질 때까지의 시간(초)")]
        [SerializeField] private float _lifetime = 2f;

        [Tooltip("방향에 맞춰 좌우를 뒤집을 SpriteRenderer. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private SpriteRenderer P_spriteRenderer;

        private float P_direction = 1f; // 1이면 오른쪽, -1이면 왼쪽
        private float _elapsedTime;

        private void Awake()
        {
            if (P_spriteRenderer == null) P_spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void Launch(bool isFacingRight)
        {
            P_direction = isFacingRight ? 1f : -1f;
            if (P_spriteRenderer != null) P_spriteRenderer.flipX = !isFacingRight;
        }

        private void Update()
        {
            transform.position += new Vector3(P_direction * _speed * Time.deltaTime, 0f, 0f);

            _elapsedTime += Time.deltaTime;
            if (_elapsedTime >= _lifetime) Destroy(gameObject);
        }
    }
}
