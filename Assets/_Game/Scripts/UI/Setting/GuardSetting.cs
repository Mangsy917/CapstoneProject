using Game.Character;
using UnityEngine;

namespace Game.UI.Setting
{
    // 설정창 더미 설정(DummySetting 패널)에 붙여서 사용
    // 가드 / 가드 종류 / 카운트 가드 스테퍼 값을 더미의 가드(DummyGuard)에 연결함
    // - 스테퍼 값을 바꾼 순간에는 적용하지 않고, 설정창을 닫고 게임으로 돌아갈 때 적용함
    // - 스테퍼 선택지 순서는 DummyGuard.GuardMode / GuardType과 같아야 함
    // - 카운트 가드 횟수는 스테퍼 선택지 글자(2 ~ 10)를 그대로 숫자로 씀
    public class GuardSetting : MonoBehaviour
    {
        [SerializeField] private SettingStepper GS_guardStepper;
        [SerializeField] private SettingStepper _guardTypeStepper;
        [SerializeField] private SettingStepper _countGuardStepper;

        void Awake()
        {
            // 이 패널이 꺼진 뒤에도 설정창이 닫히면 알 수 있도록 static 이벤트를 받음
            ControlReaderSwitch.SettingClosed += ApplyToDummy;
        }

        void OnDestroy()
        {
            ControlReaderSwitch.SettingClosed -= ApplyToDummy;
        }

        private void ApplyToDummy()
        {
            var mode = (DummyGuard.GuardMode)GetIndex(GS_guardStepper);
            var type = (DummyGuard.GuardType)GetIndex(_guardTypeStepper);
            DummyGuard.ApplySetting(mode, type, GetCountGuardLimit());
        }

        // 카운트 가드 스테퍼에서 고른 선택지 글자를 숫자로 바꿈 (숫자가 아니면 가장 작은 횟수 2)
        private int GetCountGuardLimit()
        {
            const int minCount = 2;
            if (_countGuardStepper == null || _countGuardStepper.options == null || _countGuardStepper.options.Length == 0) return minCount;

            string option = _countGuardStepper.options[_countGuardStepper.Index];
            return int.TryParse(option, out int count) ? count : minCount;
        }

        private int GetIndex(SettingStepper stepper)
        {
            return stepper != null ? stepper.Index : 0;
        }
    }
}
