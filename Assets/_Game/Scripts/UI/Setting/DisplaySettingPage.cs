using UnityEngine;

namespace Game.UI.Setting
{
    // 옵션의 화면 설정 페이지에 붙여서 사용 (OptionPage와 같은 오브젝트)
    // 줄의 스테퍼 / 슬라이더를 DisplaySettings 값에 연결함. 값을 바꾸면 바로 적용됨
    // - 상단 HUD / 하단 HUD: OFF, ON 스테퍼 (ON이 1번)
    // - 상단 HUD 위치 / 하단 HUD 위치 / 감마: 0~100 슬라이더
    public class DisplaySettingPage : MonoBehaviour
    {
        [SerializeField] private SettingStepper _topHudStepper;
        [SerializeField] private SettingSliderStepper _topHudOffsetSlider;
        [SerializeField] private SettingStepper _bottomHudStepper;
        [SerializeField] private SettingSliderStepper _bottomHudOffsetSlider;
        [SerializeField] private SettingSliderStepper _gammaSlider;

        // 스테퍼 선택지에서 ON이 몇 번째인지 (OFF, ON 순서면 1)
        private const int OnIndex = 1;

        private void Awake()
        {
            if (_topHudStepper != null) _topHudStepper.onValueChanged.AddListener(index => DisplaySettings.IsTopHudVisible = index == OnIndex);
            if (_topHudOffsetSlider != null) _topHudOffsetSlider.onValueChanged.AddListener(value => DisplaySettings.TopHudOffset = value);
            if (_bottomHudStepper != null) _bottomHudStepper.onValueChanged.AddListener(index => DisplaySettings.IsBottomHudVisible = index == OnIndex);
            if (_bottomHudOffsetSlider != null) _bottomHudOffsetSlider.onValueChanged.AddListener(value => DisplaySettings.BottomHudOffset = value);
            if (_gammaSlider != null) _gammaSlider.onValueChanged.AddListener(value => DisplaySettings.Gamma = value);
        }

        // 처음 열 때는 줄들의 Awake(처음 값 설정)가 끝난 뒤인 Start에서, 그 뒤로는 열 때마다 OnEnable에서 저장된 값을 보여줌
        private bool _isStarted;

        private void Start()
        {
            _isStarted = true;
            ShowSavedValues();
        }

        private void OnEnable()
        {
            if (_isStarted) ShowSavedValues();
        }

        private void ShowSavedValues()
        {
            if (_topHudStepper != null) _topHudStepper.SetOptions(_topHudStepper.options, DisplaySettings.IsTopHudVisible ? OnIndex : 0);
            if (_topHudOffsetSlider != null) _topHudOffsetSlider.SetSavedValue(DisplaySettings.TopHudOffset);
            if (_bottomHudStepper != null) _bottomHudStepper.SetOptions(_bottomHudStepper.options, DisplaySettings.IsBottomHudVisible ? OnIndex : 0);
            if (_bottomHudOffsetSlider != null) _bottomHudOffsetSlider.SetSavedValue(DisplaySettings.BottomHudOffset);
            if (_gammaSlider != null) _gammaSlider.SetSavedValue(DisplaySettings.Gamma);
        }
    }
}
