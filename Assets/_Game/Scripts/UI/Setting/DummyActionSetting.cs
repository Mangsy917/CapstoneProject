using Game.Character;
using UnityEngine;

namespace Game.UI.Setting
{
    public class DummyActionSetting : MonoBehaviour
    {
        [SerializeField] private SettingStepper _dummyActionStepper;

        // 마지막으로 적용한 행동 (설정창을 닫을 때 같은 값이면 다시 적용하지 않아서 점프가 처음부터 다시 시작되지 않음)
        private int _appliedIndex = -1;

        void Awake()
        {
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