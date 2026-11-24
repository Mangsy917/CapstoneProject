using Game.Character;
using UnityEngine;

namespace Game.UI.Setting
{
    // 설정창 파라미터 탭(Parameter 패널)에 붙여서 사용
    // 플레이어의 냉각수 / 오버클럭, 더미의 가드 게이지 % 슬라이더와 회복 방식 스테퍼를 CharacterGauge에 연결함
    // - 설정창을 닫고 게임으로 돌아갈 때 값이 바뀐 게이지만 적용 (HealthSetting과 같은 규칙)
    // - 스테퍼 선택지 순서는 CharacterGauge.RecoveryMode와 같아야 함 (자동 회복, 회복 안 함, 항상 최대)
    public class GaugeSetting : MonoBehaviour
    {
        // 슬라이더 + 스테퍼 한 쌍과 마지막으로 적용한 값
        [System.Serializable]
        private class GaugeRow
        {
            [SerializeField] private SettingSliderStepper _percentSlider;
            [SerializeField] private SettingStepper _modeStepper;

            public float AppliedPercent { get; private set; }
            public CharacterGauge.RecoveryMode AppliedMode { get; private set; }

            public void Remember()
            {
                AppliedPercent = _percentSlider.Value;
                AppliedMode = (CharacterGauge.RecoveryMode)_modeStepper.Index;
            }

            public bool IsChanged()
            {
                return !Mathf.Approximately(AppliedPercent, _percentSlider.Value) || (int)AppliedMode != _modeStepper.Index;
            }
        }

        [SerializeField] private GaugeRow _playerCoolantRow;
        [SerializeField] private GaugeRow _playerOverclockRow;
        [SerializeField] private GaugeRow _dummyGuardRow;

        [Header("태그")]
        [SerializeField] private string GS_playerTag = "Player";
        [SerializeField] private string GS_enemyTag = "Enemy";

        private bool _isRemembered;

        private void Awake()
        {
            // 이 패널이 있는 탭이 꺼져 있어도 설정창이 닫히면 알 수 있도록 static 이벤트를 받음
            ControlReaderSwitch.SettingClosed += ApplyChanged;
        }

        private void Start()
        {
            // 처음 값을 적용된 값으로 기억 (설정창을 처음 열 때 게이지가 바뀌지 않도록)
            _playerCoolantRow.Remember();
            _playerOverclockRow.Remember();
            _dummyGuardRow.Remember();
            _isRemembered = true;
        }

        private void OnDestroy()
        {
            ControlReaderSwitch.SettingClosed -= ApplyChanged;
        }

        private void ApplyChanged()
        {
            if (!_isRemembered) return;

            if (_playerCoolantRow.IsChanged())
            {
                _playerCoolantRow.Remember();
                CharacterGauge.ApplyCoolantOption(GS_playerTag, _playerCoolantRow.AppliedMode, _playerCoolantRow.AppliedPercent);
            }
            if (_playerOverclockRow.IsChanged())
            {
                _playerOverclockRow.Remember();
                CharacterGauge.ApplyOverclockOption(GS_playerTag, _playerOverclockRow.AppliedMode, _playerOverclockRow.AppliedPercent);
            }
            if (_dummyGuardRow.IsChanged())
            {
                _dummyGuardRow.Remember();
                CharacterGauge.ApplyCoolantOption(GS_enemyTag, _dummyGuardRow.AppliedMode, _dummyGuardRow.AppliedPercent);
            }
        }
    }
}
