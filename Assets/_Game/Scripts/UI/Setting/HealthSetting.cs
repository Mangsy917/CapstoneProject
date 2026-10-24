using Game.Character;
using UnityEngine;

namespace Game.UI.Setting
{
    // 설정창 파라미터 탭(Parameter 패널)에 붙여서 사용
    // 플레이어 / 더미의 체력 % 슬라이더와 체력 방식 스테퍼(자동 회복 / 항상 고정 / 일반)를 캐릭터 체력(CharacterHealth)에 연결함
    // - 다른 설정처럼 값을 바꾼 순간에는 적용하지 않고, 설정창을 닫고 게임으로 돌아갈 때 적용함
    //   (값이 바뀐 쪽만 적용하고, 체력도 설정한 %로 바뀜)
    // - 다시 생성되는 Enemy도 같은 설정을 씀 (CharacterHealth가 태그별로 기억함)
    // - 스테퍼 선택지 순서는 CharacterHealth.HealthMode와 같아야 함 (자동 회복, 항상 고정, 일반)
    // - 설정창의 처음 값(자동 회복, 100%)은 CharacterHealth의 처음 값과 같으므로 처음에는 적용하지 않음
    public class HealthSetting : MonoBehaviour
    {
        [Header("플레이어")]
        [SerializeField] private SettingSliderStepper _playerHealthSlider;
        [SerializeField] private SettingStepper _playerHealthModeStepper;

        [Header("더미")]
        [SerializeField] private SettingSliderStepper _dummyHealthSlider;
        [SerializeField] private SettingStepper _dummyHealthModeStepper;

        [Header("태그")]
        [SerializeField] private string HS_playerTag = "Player";
        [SerializeField] private string HS_enemyTag = "Enemy";

        // 마지막으로 게임에 적용한 값 (체력 %, 방식 번호)
        private float _appliedPlayerPercent;
        private int _appliedPlayerModeIndex;
        private float _appliedDummyPercent;
        private int _appliedDummyModeIndex;

        void Awake()
        {
            // 이 패널이 있는 탭이 꺼져 있어도 설정창이 닫히면 알 수 있도록 static 이벤트를 받음
            ControlReaderSwitch.SettingClosed += OnSettingClosed;
        }

        void Start()
        {
            // 처음 값을 적용된 값으로 기억 (설정창을 처음 열 때 체력이 바뀌지 않도록 적용은 하지 않음)
            _appliedPlayerPercent = GetPercent(_playerHealthSlider);
            _appliedPlayerModeIndex = GetModeIndex(_playerHealthModeStepper);
            _appliedDummyPercent = GetPercent(_dummyHealthSlider);
            _appliedDummyModeIndex = GetModeIndex(_dummyHealthModeStepper);
        }

        void OnDestroy()
        {
            ControlReaderSwitch.SettingClosed -= OnSettingClosed;
        }

        // 설정창이 닫혔을 때: 값이 바뀐 쪽만 체력에 적용
        private void OnSettingClosed()
        {
            float playerPercent = GetPercent(_playerHealthSlider);
            int playerModeIndex = GetModeIndex(_playerHealthModeStepper);
            if (!Mathf.Approximately(playerPercent, _appliedPlayerPercent) || playerModeIndex != _appliedPlayerModeIndex)
            {
                _appliedPlayerPercent = playerPercent;
                _appliedPlayerModeIndex = playerModeIndex;
                CharacterHealth.ApplySetting(HS_playerTag, (CharacterHealth.HealthMode)playerModeIndex, playerPercent);
            }

            float dummyPercent = GetPercent(_dummyHealthSlider);
            int dummyModeIndex = GetModeIndex(_dummyHealthModeStepper);
            if (!Mathf.Approximately(dummyPercent, _appliedDummyPercent) || dummyModeIndex != _appliedDummyModeIndex)
            {
                _appliedDummyPercent = dummyPercent;
                _appliedDummyModeIndex = dummyModeIndex;
                CharacterHealth.ApplySetting(HS_enemyTag, (CharacterHealth.HealthMode)dummyModeIndex, dummyPercent);
            }
        }

        private float GetPercent(SettingSliderStepper slider)
        {
            return slider != null ? slider.Value : 100f;
        }

        private int GetModeIndex(SettingStepper modeStepper)
        {
            return modeStepper != null ? modeStepper.Index : (int)CharacterHealth.HealthMode.AutoRecover;
        }
    }
}
