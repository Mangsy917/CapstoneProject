using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Character
{
    // 캐릭터(Player, Enemy)에 붙여서 사용하는 체력
    // - 공격(CharacterAttack)이나 투사체(Projectile)에 맞으면 TakeDamage로 줄어듦 (0 아래로는 내려가지 않음)
    // - 설정창(파라미터 탭)의 체력 % / 체력 방식을 따름 (HealthSetting이 태그별로 넣어줌)
    //   자동 회복: 맞은 뒤 한동안 맞지 않으면 설정한 체력까지 회복. 체력이 1 아래로 내려가지 않음 (KO 없음)
    //   항상 고정: 맞아도 줄어들지 않음
    //   일반: 맞은 만큼 계속 줄어듦
    // - 연습 재시작(PracticeRestart) 때 ResetHealth로 설정한 체력이 됨
    // - 때린 공격(CharacterAttack)이 끝날 때까지는 데미지가 "확정되지 않은" 상태 (IsDamageSettled)
    //   체력바는 그동안 깎인 만큼을 반투명 막대로 남겨 두고, 확정되면 줄임
    // - 피격 모션은 아직 없음 (체력이 0이 됐을 때의 KO는 KnockOutSequence가 처리)
    public class CharacterHealth : MonoBehaviour
    {
        // 설정창 체력 방식 스테퍼의 선택지 순서와 같음 (자동 회복, 항상 고정, 일반)
        public enum HealthMode
        {
            AutoRecover, // 자동 회복
            Fixed,       // 항상 고정
            Normal       // 일반
        }

        [Tooltip("최대 체력 (설정창의 체력 100%)")]
        [SerializeField] private int _maxHealth = 10000;

        [Tooltip("설정창에서 바꾸기 전의 체력 방식 (설정창의 처음 값과 같게 둠)")]
        [SerializeField] private HealthMode _healthMode = HealthMode.AutoRecover;

        [Header("자동 회복")]
        [Tooltip("마지막으로 맞은 뒤 이 프레임 수 동안 맞지 않으면 회복을 시작함")]
        [SerializeField] private int _recoveryDelayFrames = 60;
        [Tooltip("체력 0에서 최대 체력까지 다시 차는 데 걸리는 프레임 수 (회복 속도)")]
        [SerializeField] private int _refillFrames = 30;

        // 태그별 설정 (씬에 새로 생긴 캐릭터도 같은 설정을 쓰도록 기억해 둠. 예: 다시 생성된 Enemy)
        private static readonly Dictionary<string, KeyValuePair<HealthMode, float>> _settingsByTag = new Dictionary<string, KeyValuePair<HealthMode, float>>();

        public int MaxHealth => _maxHealth;
        public int CurrentHealth { get; private set; }

        /// <summary>시작 체력 (설정창의 체력 %). 재시작과 자동 회복은 이 값까지 채움</summary>
        public int StartHealth { get; private set; }

        public HealthMode Mode => _healthMode;

        /// <summary>
        /// 마지막으로 받은 데미지가 확정됐는지 (때린 공격이 회수, 착지 경직까지 끝남)
        /// 피격 경직이 생기면 그 경직이 끝날 때도 함께 봐야 함
        /// </summary>
        public bool IsDamageSettled => _lastAttacker == null || !_lastAttacker.IsAttacking;

        /// <summary>체력이 바뀌었을 때 (체력바 등에서 구독)</summary>
        public event Action<CharacterHealth> OnHealthChanged;

        private int _framesSinceDamage;
        private CharacterAttack _lastAttacker; // 마지막으로 데미지를 준 공격 (투사체면 쏜 캐릭터의 공격)

        // Domain Reload를 꺼둔 상태로 플레이해도 이전 설정이 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSettings()
        {
            _settingsByTag.Clear();
        }

        /// <summary>tag를 가진 캐릭터들의 체력 방식과 체력 %를 바꾸고 바로 적용함 (나중에 생기는 캐릭터에도 적용)</summary>
        public static void ApplySetting(string tag, HealthMode mode, float percent)
        {
            _settingsByTag[tag] = new KeyValuePair<HealthMode, float>(mode, percent);

            foreach (var health in FindObjectsByType<CharacterHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (health.CompareTag(tag)) health.ApplySetting(mode, percent);
            }
        }

        private void Awake()
        {
            StartHealth = _maxHealth;
            CurrentHealth = _maxHealth;
        }

        private void Start()
        {
            if (_settingsByTag.TryGetValue(tag, out var setting)) ApplySetting(setting.Key, setting.Value);
        }

        private void Update()
        {
            // 설정창이 열려 멈춰 있거나 슬로우 모션 중에 건너뛰는 프레임에는 회복하지 않음
            if (!Game.Field.GameSpeed.IsTickFrame) return;
            if (_healthMode != HealthMode.AutoRecover) return;

            // 체력이 0이면 KO 연출 중이므로 회복하지 않음
            if (CurrentHealth <= 0 || CurrentHealth >= StartHealth) return;

            // 때린 공격이 끝나서 데미지가 확정된 뒤부터 회복 대기 프레임을 셈
            if (!IsDamageSettled) return;

            _framesSinceDamage++;
            if (_framesSinceDamage < _recoveryDelayFrames) return;

            int amountPerFrame = Mathf.CeilToInt((float)_maxHealth / Mathf.Max(1, _refillFrames));
            CurrentHealth = Mathf.Min(StartHealth, CurrentHealth + amountPerFrame);
            OnHealthChanged?.Invoke(this);
        }

        public void ApplySetting(HealthMode mode, float percent)
        {
            _healthMode = mode;
            // 0%여도 1은 남겨 둠 (0이면 재시작하자마자 다시 KO가 되므로)
            StartHealth = Mathf.Clamp(Mathf.RoundToInt(_maxHealth * percent / 100f), 1, _maxHealth);
            CurrentHealth = StartHealth;
            _framesSinceDamage = 0;
            OnHealthChanged?.Invoke(this);
        }

        /// <summary>attacker: 데미지를 준 공격 (그 공격이 끝날 때 데미지가 확정됨). 없으면 바로 확정</summary>
        public void TakeDamage(int damage, CharacterAttack attacker = null)
        {
            if (damage <= 0 || CurrentHealth <= 0) return;

            _framesSinceDamage = 0;
            _lastAttacker = attacker;
            if (_healthMode == HealthMode.Fixed)
            {
                Debug.Log($"[Health] {name} {damage} 데미지 (항상 고정이라 체력 그대로 {CurrentHealth} / {_maxHealth})");
                return;
            }

            // 자동 회복일 때는 1 아래로 내려가지 않음 (KO 되지 않고 계속 연습할 수 있게)
            int minHealth = _healthMode == HealthMode.AutoRecover ? 1 : 0;
            CurrentHealth = Mathf.Max(minHealth, CurrentHealth - damage);
            Debug.Log($"[Health] {name} {damage} 데미지 (남은 체력 {CurrentHealth} / {_maxHealth})");
            OnHealthChanged?.Invoke(this);
        }

        public void ResetHealth()
        {
            CurrentHealth = StartHealth;
            _framesSinceDamage = 0;
            _lastAttacker = null;
            OnHealthChanged?.Invoke(this);
        }

        private void OnValidate()
        {
            if (_maxHealth < 1) _maxHealth = 1;
            if (_recoveryDelayFrames < 0) _recoveryDelayFrames = 0;
            if (_refillFrames < 1) _refillFrames = 1;
        }
    }
}
