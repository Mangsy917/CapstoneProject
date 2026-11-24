using System.Collections.Generic;
using UnityEngine;

namespace Game.Character
{
    // 캐릭터의 냉각수(과부하 포함)와 오버클럭 게이지. 규칙은 기획서 3-6, 수치는 GaugeData
    // - 더미(적)는 냉각수 대신 같은 규칙의 가드 게이지로 씀 (오버클럭 최대 0칸이면 오버클럭 없음)
    // - 공격을 쓸 때의 소모는 CharacterAttack, 히트 / 가드의 변화는 CharacterHitReaction이 호출
    // - 설정창(파라미터 탭)의 게이지 % / 회복 방식을 따름 (GaugeSetting이 태그별로 넣어줌)
    // 경직을 줄이는 CharacterHitReaction(40) 다음에 회복하도록 실행 순서를 뒤로 미룸
    [DefaultExecutionOrder(45)]
    public class CharacterGauge : MonoBehaviour
    {
        // 설정창 회복 방식 스테퍼의 선택지 순서와 같음
        public enum RecoveryMode
        {
            AutoRecover, // 자동 회복 (냉각수: 게임 규칙의 자연 회복 / 오버클럭: 잠시 뒤 설정한 %까지 참)
            NoRecover,   // 회복 안 함 (시간으로는 회복하지 않음. 히트 / 가드로는 변함)
            AlwaysMax    // 항상 최대
        }

        private struct GaugeOption
        {
            public RecoveryMode Mode;
            public float Percent;
        }

        private const int StatLimit = 10;
        private const float CoolantPerStat = 200f;
        private const float OverclockPerStat = 20f;
        private const float FramesPerSecond = 60f;

        [SerializeField] private GaugeData _gaugeData;

        [Header("최대치")]
        [Tooltip("시작 냉각수(가드 게이지) 최대치. 1칸 = 1000")]
        [SerializeField] private float _baseCoolant = 4000f;
        [Tooltip("시작 오버클럭 최대치. 1칸 = 100")]
        [SerializeField] private float _baseOverclock = 300f;
        [Tooltip("냉각수에 투자한 스탯 수 (1개당 +0.2칸). 튜닝 화면이 생기기 전 확인용")]
        [Range(0, StatLimit)]
        [SerializeField] private int _coolantStatCount;
        [Tooltip("오버클럭에 투자한 스탯 수 (1개당 +20%). 튜닝 화면이 생기기 전 확인용")]
        [Range(0, StatLimit)]
        [SerializeField] private int _overclockStatCount;

        [Header("연습장 오버클럭 자동 회복")]
        [Tooltip("마지막으로 쓴 뒤 이 프레임이 지나면 설정한 %까지 참")]
        [SerializeField] private int _overclockRefillDelay = 60;
        [SerializeField] private int _overclockRefillFrames = 30;

        [Header("처음 회복 방식 (설정창의 처음 값과 같게 둠)")]
        [SerializeField] private RecoveryMode _coolantMode = RecoveryMode.AutoRecover;
        [SerializeField] private RecoveryMode _overclockMode = RecoveryMode.NoRecover;

        private static readonly Dictionary<string, GaugeOption> _coolantOptionDic = new Dictionary<string, GaugeOption>();
        private static readonly Dictionary<string, GaugeOption> _overclockOptionDic = new Dictionary<string, GaugeOption>();

        private CharacterHitReaction CG_hitReaction;
        private float _coolantPercent = 100f;
        private float _overclockPercent = 100f;
        private int _recoveryDelayLeft;
        private int _framesSinceOverclockUse;

        public GaugeData Data => _gaugeData;
        public float Coolant { get; private set; }
        public float Overclock { get; private set; }
        public float MaxCoolant => _baseCoolant + _coolantStatCount * CoolantPerStat;
        public float MaxOverclock => _baseOverclock + _overclockStatCount * OverclockPerStat;
        public bool IsOverloaded { get; private set; }

        /// <summary>비전투 상태인지 (냉각수 회복이 빨라짐). 연습장에서는 항상 전투 상태</summary>
        public bool IsPeaceful { get; set; }

        /// <summary>오버클럭이 모자라 OD 기술이 나가지 않았을 때 (게이지 UI가 깜빡임)</summary>
        public event System.Action OverclockLacked;

        // Domain Reload를 꺼둔 상태로 플레이해도 이전 설정이 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOptions()
        {
            _coolantOptionDic.Clear();
            _overclockOptionDic.Clear();
        }

        /// <summary>tag를 가진 캐릭터들의 냉각수(가드 게이지) 설정을 바꾸고 바로 적용함 (나중에 생기는 캐릭터에도 적용)</summary>
        public static void ApplyCoolantOption(string tag, RecoveryMode mode, float percent)
        {
            GaugeOption option = new GaugeOption { Mode = mode, Percent = percent };
            _coolantOptionDic[tag] = option;
            foreach (CharacterGauge gauge in FindObjectsByType<CharacterGauge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gauge.CompareTag(tag)) gauge.SetCoolantOption(option);
            }
        }

        /// <summary>tag를 가진 캐릭터들의 오버클럭 설정을 바꾸고 바로 적용함 (나중에 생기는 캐릭터에도 적용)</summary>
        public static void ApplyOverclockOption(string tag, RecoveryMode mode, float percent)
        {
            GaugeOption option = new GaugeOption { Mode = mode, Percent = percent };
            _overclockOptionDic[tag] = option;
            foreach (CharacterGauge gauge in FindObjectsByType<CharacterGauge>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gauge.CompareTag(tag)) gauge.SetOverclockOption(option);
            }
        }

        /// <summary>공격이 맞았을 때 양쪽 게이지를 바꿈. isComboHit: 콤보 2타부터인지, comboCount: 이번 히트까지의 콤보 수</summary>
        public static void ChangeByHit(CharacterGauge attacker, CharacterGauge target, AttackGaugeType type, bool isComboHit, int comboCount)
        {
            if (attacker != null) attacker.GainByHit(type, comboCount);
            if (target != null) target.TakeHit(type, isComboHit, comboCount);
        }

        /// <summary>공격이 가드됐을 때 양쪽 게이지를 바꿈</summary>
        public static void ChangeByGuard(CharacterGauge attacker, CharacterGauge target, AttackGaugeType type)
        {
            if (attacker != null) attacker.GainByBlocked(type);
            if (target != null) target.TakeGuard(type);
        }

        private void Awake()
        {
            CG_hitReaction = GetComponent<CharacterHitReaction>();
            ResetGauge();
        }

        private void Start()
        {
            if (_coolantOptionDic.TryGetValue(tag, out GaugeOption coolantOption)) SetCoolantOption(coolantOption);
            if (_overclockOptionDic.TryGetValue(tag, out GaugeOption overclockOption)) SetOverclockOption(overclockOption);
        }

        private void Update()
        {
            // 설정창이 열려 멈춰 있거나 슬로우 모션 중에 건너뛰는 프레임, 히트스톱 중에는 회복하지 않음
            if (!Game.Field.GameSpeed.IsTickFrame || HitStop.IsStoppedNow(this)) return;

            RecoverCoolant();
            RecoverOverclock();
        }

        /// <summary>재시작할 때 호출. 설정한 %로 되돌리고 과부하를 풂 (0%면 과부하)</summary>
        public void ResetGauge()
        {
            ResetCoolant();
            ResetOverclock();
        }

        /// <summary>D 기술을 쓸 때 냉각수를 소모함. 모자라도 쓸 수 있고 0이 되면 과부하, 과부하 중에는 소모하지 않음</summary>
        public void PayCoolant(AttackGaugeType type)
        {
            float cost = _gaugeData.GetCoolantCost(type);
            if (cost > 0f) UseCoolant(cost);
        }

        /// <summary>OD 기술을 쓸 만큼 오버클럭이 있는지. 모자라면 UI가 깜빡이도록 알림</summary>
        public bool CheckOverdrive()
        {
            if (Overclock >= _gaugeData.OverdriveCost) return true;

            OverclockLacked?.Invoke();
            return false;
        }

        /// <summary>OD 기술을 쓸 때 오버클럭을 소모함 (CheckOverdrive로 먼저 확인)</summary>
        public void PayOverdrive()
        {
            _framesSinceOverclockUse = 0;
            if (_overclockMode != RecoveryMode.AlwaysMax) AddOverclock(-_gaugeData.OverdriveCost);
        }

        /// <summary>브레이크에 냉각수를 소모함. 조금이라도 있으면 쓸 수 있고, 과부하 중에는 쓸 수 없음</summary>
        public bool TryPayBreak()
        {
            if (IsOverloaded || Coolant <= 0f) return false;

            UseCoolant(_gaugeData.BreakCost);
            return true;
        }

        private void UseCoolant(float amount)
        {
            if (IsOverloaded) return;

            _recoveryDelayLeft = Mathf.Max(_recoveryDelayLeft, _gaugeData.RecoveryDelayAfterUse);
            AddCoolant(-amount);
        }

        private void GainByHit(AttackGaugeType type, int comboCount)
        {
            GaugeData.GaugeChange change = _gaugeData.FindChange(type);
            if (change == null) return;

            float rate = _gaugeData.GetComboRate(comboCount);
            AddCoolant(change.HitCoolantGain * rate);
            AddOverclock(change.HitOverclockGain * rate);
        }

        private void GainByBlocked(AttackGaugeType type)
        {
            GaugeData.GaugeChange change = _gaugeData.FindChange(type);
            if (change == null) return;

            AddCoolant(change.BlockedCoolantGain);
            AddOverclock(change.BlockedOverclockGain);
        }

        // 맞으면 콤보 첫 타에만 냉각수가 줄고(과부하 중에는 회복), 오버클럭은 맞을 때마다 얻음
        private void TakeHit(AttackGaugeType type, bool isComboHit, int comboCount)
        {
            GaugeData.GaugeChange change = _gaugeData.FindChange(type);
            if (change == null) return;

            if (!isComboHit) AddCoolant(IsOverloaded ? _gaugeData.OverloadCoolantGain : -change.DamagedCoolantLoss);
            AddOverclock(change.DamagedOverclockGain * _gaugeData.GetComboRate(comboCount));
        }

        private void TakeGuard(AttackGaugeType type)
        {
            GaugeData.GaugeChange change = _gaugeData.FindChange(type);
            if (change == null) return;

            AddCoolant(IsOverloaded ? _gaugeData.OverloadCoolantGain : -change.GuardCoolantLoss);
            AddOverclock(change.GuardOverclockGain);
        }

        private void RecoverCoolant()
        {
            if (_coolantMode == RecoveryMode.AlwaysMax)
            {
                AddCoolant(MaxCoolant);
                return;
            }

            // 피격 중에는 멈추고, 경직이 풀린 뒤 조금 있다가 다시 회복
            bool isHit = CG_hitReaction != null && (CG_hitReaction.IsInHitStun || CG_hitReaction.IsDown);
            if (isHit)
            {
                _recoveryDelayLeft = Mathf.Max(_recoveryDelayLeft, _gaugeData.RecoveryDelayAfterHit);
                return;
            }

            if (_recoveryDelayLeft > 0)
            {
                _recoveryDelayLeft--;
                return;
            }

            if (_coolantMode == RecoveryMode.NoRecover) return;

            float rate = 1f;
            if (CG_hitReaction != null && CG_hitReaction.IsInGuardStun) rate *= _gaugeData.GuardRecoveryRate;
            if (IsOverloaded) rate *= _gaugeData.OverloadRecoveryRate;
            if (IsPeaceful) rate *= _gaugeData.PeaceRecoveryRate;
            AddCoolant(_gaugeData.CoolantRecoveryPerSecond * rate / FramesPerSecond);
        }

        private void RecoverOverclock()
        {
            if (_overclockMode == RecoveryMode.AlwaysMax)
            {
                Overclock = MaxOverclock;
                return;
            }

            if (_overclockMode != RecoveryMode.AutoRecover) return;

            _framesSinceOverclockUse++;
            float startOverclock = MaxOverclock * _overclockPercent / 100f;
            if (_framesSinceOverclockUse < _overclockRefillDelay || Overclock >= startOverclock) return;
            Overclock = Mathf.Min(startOverclock, Overclock + MaxOverclock / Mathf.Max(1, _overclockRefillFrames));
        }

        private void AddCoolant(float amount)
        {
            if (_coolantMode == RecoveryMode.AlwaysMax)
            {
                Coolant = MaxCoolant;
                IsOverloaded = false;
                return;
            }

            Coolant = Mathf.Clamp(Coolant + amount, 0f, MaxCoolant);
            RefreshOverload();
        }

        private void AddOverclock(float amount)
        {
            Overclock = Mathf.Clamp(Overclock + amount, 0f, MaxOverclock);
        }

        // 0이 되면 과부하, 정한 양(최대치가 더 작으면 최대치)까지 차면 풀림
        private void RefreshOverload()
        {
            if (Coolant <= 0f) IsOverloaded = true;
            else if (IsOverloaded && Coolant >= Mathf.Min(_gaugeData.OverloadReleaseCoolant, MaxCoolant)) IsOverloaded = false;
        }

        private void SetCoolantOption(GaugeOption option)
        {
            _coolantMode = option.Mode;
            _coolantPercent = option.Percent;
            ResetCoolant();
        }

        private void SetOverclockOption(GaugeOption option)
        {
            _overclockMode = option.Mode;
            _overclockPercent = option.Percent;
            ResetOverclock();
        }

        private void ResetCoolant()
        {
            _recoveryDelayLeft = 0;
            Coolant = _coolantMode == RecoveryMode.AlwaysMax ? MaxCoolant : MaxCoolant * _coolantPercent / 100f;
            IsOverloaded = Coolant <= 0f;
        }

        private void ResetOverclock()
        {
            _framesSinceOverclockUse = 0;
            Overclock = _overclockMode == RecoveryMode.AlwaysMax ? MaxOverclock : MaxOverclock * _overclockPercent / 100f;
        }

        private void OnValidate()
        {
            if (_baseCoolant < 0f) _baseCoolant = 0f;
            if (_baseOverclock < 0f) _baseOverclock = 0f;
        }
    }
}
