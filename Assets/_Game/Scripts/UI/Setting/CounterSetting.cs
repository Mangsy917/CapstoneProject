using Game.Character;
using UnityEngine;

namespace Game.UI.Setting
{
    // 설정창 더미 설정(DummySetting 패널)에 붙여서 사용
    // 카운터 종류 스테퍼 값을 더미가 맞을 때의 히트 종류(CharacterHitReaction)에 연결함
    // - 스테퍼 값을 바꾼 순간에는 적용하지 않고, 설정창을 닫고 게임으로 돌아갈 때 적용함 (다른 더미 설정과 같음)
    // - 스테퍼 선택지 순서는 CharacterHitReaction.DummyCounterMode와 같아야 함 (일반, 항상 카운터, 항상 퍼니쉬 카운터, 랜덤)
    public class CounterSetting : MonoBehaviour
    {
        [SerializeField] private SettingStepper _counterStepper;

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
            int index = _counterStepper != null ? _counterStepper.Index : 0;
            CharacterHitReaction.ApplyDummyCounterSetting((CharacterHitReaction.DummyCounterMode)index);
        }
    }
}
