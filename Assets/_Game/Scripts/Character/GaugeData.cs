using System;
using UnityEngine;

namespace Game.Character
{
    // 게이지 변화량을 정하는 공격 종류 (기획서 3-6 의수 시스템의 표 구분)
    public enum AttackGaugeType
    {
        L,            // L 기본기
        H,            // H 기본기, 잡기
        DriveNormal,  // D 기본기
        DriveSpecial, // D 필살기
        Overdrive,    // OD 기본기, 필살기
        Super         // 초필살기 (아직 기술이 없음)
    }

    // 냉각수(더미는 가드 게이지)와 오버클럭의 공통 수치 (기획서 3-6)
    // 냉각수 1칸 = 1000, 오버클럭 1칸 = 100
    [CreateAssetMenu(fileName = "D_GaugeData", menuName = "Game/Gauge Data")]
    public class GaugeData : ScriptableObject
    {
        // 공격 종류 하나가 히트 / 가드될 때 양쪽 게이지가 변하는 양
        [Serializable]
        public class GaugeChange
        {
            [SerializeField] private AttackGaugeType GD_type;

            [Header("때린 쪽")]
            [SerializeField] private float _hitCoolantGain;
            [SerializeField] private float _blockedCoolantGain;
            [SerializeField] private float _hitOverclockGain;
            [SerializeField] private float _blockedOverclockGain;

            [Header("맞은 쪽 / 가드한 쪽")]
            [SerializeField] private float _damagedCoolantLoss;
            [SerializeField] private float _guardCoolantLoss;
            [SerializeField] private float _damagedOverclockGain;
            [SerializeField] private float _guardOverclockGain;

            public AttackGaugeType GD_Type => GD_type;
            public float HitCoolantGain => _hitCoolantGain;
            public float BlockedCoolantGain => _blockedCoolantGain;
            public float HitOverclockGain => _hitOverclockGain;
            public float BlockedOverclockGain => _blockedOverclockGain;
            public float DamagedCoolantLoss => _damagedCoolantLoss;
            public float GuardCoolantLoss => _guardCoolantLoss;
            public float DamagedOverclockGain => _damagedOverclockGain;
            public float GuardOverclockGain => _guardOverclockGain;

            public GaugeChange(AttackGaugeType type, float hitCoolant, float blockedCoolant, float hitOverclock, float blockedOverclock,
                               float damagedCoolant, float guardCoolant, float damagedOverclock, float guardOverclock)
            {
                GD_type = type;
                _hitCoolantGain = hitCoolant;
                _blockedCoolantGain = blockedCoolant;
                _hitOverclockGain = hitOverclock;
                _blockedOverclockGain = blockedOverclock;
                _damagedCoolantLoss = damagedCoolant;
                _guardCoolantLoss = guardCoolant;
                _damagedOverclockGain = damagedOverclock;
                _guardOverclockGain = guardOverclock;
            }
        }

        public const float CoolantPerCell = 1000f;
        public const float OverclockPerCell = 100f;

        [Header("소모량")]
        [SerializeField] private float _driveNormalCost = 500f;
        [SerializeField] private float _driveSpecialCost = 2000f;
        [SerializeField] private float _breakCost = 3000f;
        [SerializeField] private float _overdriveCost = 100f;

        [Header("냉각수 자연 회복")]
        [Tooltip("1초에 회복하는 양")]
        [SerializeField] private float _coolantRecoveryPerSecond = 300f;
        [Tooltip("D 기술 등으로 소모한 뒤 회복을 시작할 때까지의 프레임")]
        [SerializeField] private int _recoveryDelayAfterUse = 60;
        [Tooltip("피격 경직이 풀린 뒤 회복을 다시 시작할 때까지의 프레임")]
        [SerializeField] private int _recoveryDelayAfterHit = 30;
        [SerializeField] private float _guardRecoveryRate = 0.5f;
        [SerializeField] private float _overloadRecoveryRate = 0.5f;
        [Tooltip("비전투 상태(적을 모두 처치한 뒤)의 회복 배율")]
        [SerializeField] private float _peaceRecoveryRate = 3f;

        [Header("과부하")]
        [Tooltip("냉각수가 이만큼 회복되면 과부하가 풀림")]
        [SerializeField] private float _overloadReleaseCoolant = 3000f;
        [Tooltip("과부하 중 가드하거나 콤보 첫 타에 맞으면 회복하는 냉각수")]
        [SerializeField] private float _overloadCoolantGain = 300f;
        [Tooltip("과부하 중 D 기술의 데미지 배율")]
        [SerializeField] private float _overloadDamageRate = 0.7f;
        [Tooltip("과부하 중 D 기술을 맞은 쪽의 피격 경직에 더하는 프레임")]
        [SerializeField] private int _overloadHitStunFrames = -2;
        [Tooltip("과부하 중 D 기술을 가드한 쪽의 가드 경직에 더하는 프레임")]
        [SerializeField] private int _overloadBlockStunFrames = -4;
        [Tooltip("과부하 중 가드하면 깎이는 체력 (막은 기술 데미지에 대한 비율)")]
        [SerializeField] private float _overloadGuardDamageRate = 0.05f;

        [Header("콤보 보정")]
        [Tooltip("한 콤보에서 이 히트부터 회복량이 줄어듦")]
        [SerializeField] private int _reducedComboCount = 5;
        [SerializeField] private float _reducedComboRate = 0.5f;

        [Header("커맨드 입력")]
        [Tooltip("필살기를 커맨드로 입력했을 때의 데미지 배율")]
        [SerializeField] private float _commandDamageRate = 1.2f;

        [Header("공격 종류별 변화량")]
        [SerializeField] private GaugeChange[] _gaugeChanges =
        {
            new GaugeChange(AttackGaugeType.L, 160f, 40f, 10f, 5f, 200f, 100f, 5f, 2.5f),
            new GaugeChange(AttackGaugeType.H, 240f, 60f, 14f, 7f, 400f, 200f, 7f, 3.5f),
            new GaugeChange(AttackGaugeType.DriveNormal, 360f, 90f, 30f, 15f, 600f, 300f, 15f, 7.5f),
            new GaugeChange(AttackGaugeType.DriveSpecial, 500f, 125f, 45f, 22.5f, 600f, 300f, 22f, 11f),
            new GaugeChange(AttackGaugeType.Overdrive, 400f, 100f, 10f, 5f, 1000f, 500f, 12f, 6f),
            new GaugeChange(AttackGaugeType.Super, 600f, 150f, 15f, 7.5f, 1000f, 500f, 20f, 10f)
        };

        public float BreakCost => _breakCost;
        public float OverdriveCost => _overdriveCost;
        public float CoolantRecoveryPerSecond => _coolantRecoveryPerSecond;
        public int RecoveryDelayAfterUse => _recoveryDelayAfterUse;
        public int RecoveryDelayAfterHit => _recoveryDelayAfterHit;
        public float GuardRecoveryRate => _guardRecoveryRate;
        public float OverloadRecoveryRate => _overloadRecoveryRate;
        public float PeaceRecoveryRate => _peaceRecoveryRate;
        public float OverloadReleaseCoolant => _overloadReleaseCoolant;
        public float OverloadCoolantGain => _overloadCoolantGain;
        public float OverloadDamageRate => _overloadDamageRate;
        public int OverloadHitStunFrames => _overloadHitStunFrames;
        public int OverloadBlockStunFrames => _overloadBlockStunFrames;
        public float OverloadGuardDamageRate => _overloadGuardDamageRate;
        public float CommandDamageRate => _commandDamageRate;

        /// <summary>이 공격을 쓸 때 소모하는 냉각수 (D 기술이 아니면 0)</summary>
        public float GetCoolantCost(AttackGaugeType type)
        {
            if (type == AttackGaugeType.DriveNormal) return _driveNormalCost;
            return type == AttackGaugeType.DriveSpecial ? _driveSpecialCost : 0f;
        }

        /// <summary>콤보의 comboCount번째 히트일 때 회복량 배율</summary>
        public float GetComboRate(int comboCount)
        {
            return comboCount >= _reducedComboCount ? _reducedComboRate : 1f;
        }

        public GaugeChange FindChange(AttackGaugeType type)
        {
            foreach (GaugeChange change in _gaugeChanges)
            {
                if (change.GD_Type == type) return change;
            }
            return null;
        }
    }
}
