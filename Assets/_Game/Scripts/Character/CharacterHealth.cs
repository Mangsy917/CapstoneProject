using System;
using UnityEngine;

namespace Game.Character
{
    // 캐릭터(Player, Enemy)에 붙여서 사용하는 체력
    // - 공격(CharacterAttack)이나 투사체(Projectile)에 맞으면 TakeDamage로 줄어듦
    // - 0 아래로는 내려가지 않음 (KO 처리와 피격 모션은 아직 없음)
    // - 연습 재시작(PracticeRestart) 때 ResetHealth로 가득 참
    public class CharacterHealth : MonoBehaviour
    {
        [Tooltip("최대 체력")]
        [SerializeField] private int _maxHealth = 10000;

        public int MaxHealth => _maxHealth;
        public int CurrentHealth { get; private set; }

        /// <summary>체력이 바뀌었을 때 (체력바 등에서 구독)</summary>
        public event Action<CharacterHealth> OnHealthChanged;

        private void Awake()
        {
            CurrentHealth = _maxHealth;
        }

        public void TakeDamage(int damage)
        {
            if (damage <= 0 || CurrentHealth <= 0) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            Debug.Log($"[Health] {name} {damage} 데미지 (남은 체력 {CurrentHealth} / {_maxHealth})");
            OnHealthChanged?.Invoke(this);
        }

        public void ResetHealth()
        {
            CurrentHealth = _maxHealth;
            OnHealthChanged?.Invoke(this);
        }

        private void OnValidate()
        {
            if (_maxHealth < 1) _maxHealth = 1;
        }
    }
}
