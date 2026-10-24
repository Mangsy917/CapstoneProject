using Game.Character;
using UnityEngine;

namespace Game.UI.Setting
{
    // 설정창 파라미터 탭(Parameter 패널)에 붙여서 사용
    // 플레이어 / 더미의 체력 % 슬라이더와 체력 방식 스테퍼(자동 회복 / 일반 / 항상 고정)를 캐릭터 체력(CharacterHealth)에 연결함
    // - 값을 바꾸면 바로 적용되고, 체력도 설정한 %로 바뀜
    // - 다시 생성되는 Enemy도 같은 설정을 씀 (CharacterHealth가 태그별로 기억함)
    // - 스테퍼 선택지 순서는 CharacterHealth.HealthMode와 같아야 함 (자동 회복, 일반, 항상 고정)
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

        void Awake()
        {
            if (_playerHealthSlider != null) _playerHealthSlider.onValueChanged.AddListener(_ => ApplyPlayer());
            if (_playerHealthModeStepper != null) _playerHealthModeStepper.onValueChanged.AddListener(_ => ApplyPlayer());
            if (_dummyHealthSlider != null) _dummyHealthSlider.onValueChanged.AddListener(_ => ApplyDummy());
            if (_dummyHealthModeStepper != null) _dummyHealthModeStepper.onValueChanged.AddListener(_ => ApplyDummy());
        }

        void Start()
        {
            // 처음 값에 맞춰서 한 번 적용
            ApplyPlayer();
            ApplyDummy();
        }

        private void ApplyPlayer()
        {
            Apply(HS_playerTag, _playerHealthSlider, _playerHealthModeStepper);
        }

        private void ApplyDummy()
        {
            Apply(HS_enemyTag, _dummyHealthSlider, _dummyHealthModeStepper);
        }

        private void Apply(string targetTag, SettingSliderStepper slider, SettingStepper modeStepper)
        {
            float percent = slider != null ? slider.Value : 100f;
            var mode = modeStepper != null ? (CharacterHealth.HealthMode)modeStepper.Index : CharacterHealth.HealthMode.AutoRecover;
            CharacterHealth.ApplySetting(targetTag, mode, percent);
        }
    }
}
