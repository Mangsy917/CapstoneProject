using Game.Character;
using UnityEngine;

namespace Game.UI.Setting
{
    // 설정창 더미 설정(DummySetting 패널)에 붙여서 사용
    // 더미 행동 스테퍼(서기 / 앉기 / 점프) 값을 더미의 행동(DummyAction)에 연결함
    // - 스테퍼 값을 바꾼 순간에는 적용하지 않고, 설정창을 닫고 게임으로 돌아갈 때 적용함
    // - 스테퍼 선택지 순서는 DummyAction.ActionType과 같아야 함
    public class DummyActionSetting : MonoBehaviour
    {
        [SerializeField] private SettingStepper _dummyActionStepper;

        // 마지막으로 적용한 행동 (설정창을 닫을 때 같은 값이면 다시 적용하지 않아서 점프가 처음부터 다시 시작되지 않음)
        private int _appliedIndex = -1;

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
            int index = _dummyActionStepper != null ? _dummyActionStepper.Index : 0;
            if (index == _appliedIndex) return;

            _appliedIndex = index;
            DummyAction.ApplySetting((DummyAction.ActionType)index);
        }
    }
}
