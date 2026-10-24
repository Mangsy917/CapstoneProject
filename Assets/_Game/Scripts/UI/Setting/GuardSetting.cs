using Game.Character;
using UnityEngine;

namespace Game.UI.Setting
{
    // 설정창 더미 설정(DummySetting 패널)에 붙여서 사용
    // 가드 / 가드 종류 / 카운트 가드 스테퍼 값을 더미의 가드(DummyGuard)에 연결함
    // - 다른 설정처럼 값을 바꾼 순간에는 적용하지 않고, 설정창을 닫고 게임으로 돌아갈 때 적용함
    // - 스테퍼 선택지 순서는 DummyGuard.GuardMode / GuardType과 같아야 함
    // - 카운트 가드 스테퍼는 1부터 시작함 (선택지 번호 + 1 = 횟수)
    public class GuardSetting : MonoBehaviour
    {
        [SerializeField] private SettingStepper GS_guardStepper;
        [SerializeField] private SettingStepper _guardTypeStepper;
        [SerializeField] private SettingStepper _countGuardStepper;

        void Awake()
        {
            // 이 패널이 있는 탭이 꺼져 있어도 설정창이 닫히면 알 수 있도록 static 이벤트를 받음
            ControlReaderSwitch.SettingClosed += OnSettingClosed;
        }

        void OnDestroy()
        {
            ControlReaderSwitch.SettingClosed -= OnSettingClosed;
        }

        private void OnSettingClosed()
        {
            var mode = (DummyGuard.GuardMode)GetIndex(GS_guardStepper);
            var type = (DummyGuard.GuardType)GetIndex(_guardTypeStepper);
            int countGuardLimit = GetIndex(_countGuardStepper) + 1;
            DummyGuard.ApplySetting(mode, type, countGuardLimit);
        }

        private int GetIndex(SettingStepper stepper)
        {
            return stepper != null ? stepper.Index : 0;
        }
    }
}
