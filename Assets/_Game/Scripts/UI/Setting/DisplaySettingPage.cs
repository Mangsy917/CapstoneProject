using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Setting
{
    // 옵션의 화면 설정 페이지에 붙여서 사용 (OptionPage와 같은 오브젝트)
    // 줄의 스테퍼 / 슬라이더를 DisplaySettings 값에 연결함. 값을 바꾸면 바로 적용됨
    // - 상단 HUD / 하단 HUD: OFF, ON 스테퍼 (ON이 1번)
    // - 상단 HUD 위치 / 하단 HUD 위치: 고르면 멈춘 게임 화면에서 HUD를 직접 위아래로 옮김 (HudPositionEditor)
    //   씬에 그 HUD가 없으면(타이틀 화면) 고를 수 없음
    // - 하단 HUD는 아직 만드는 중이라 두 줄 모두 "제작 중"만 보임 (프리팹에서 연결하지 않음)
    // - 감마: -3 ~ +3 슬라이더 (1 단위)
    // - FPS 표시: OFF, ON 스테퍼 (켜면 화면 왼쪽 위에 지금 FPS가 보임, FrameRateDisplay)
    // - 기본값으로 되돌리기: 모든 값을 처음 값으로 (ResetToDefault)
    public class DisplaySettingPage : MonoBehaviour
    {
        [SerializeField] private SettingStepper _topHudStepper;
        [SerializeField] private SettingStepper _bottomHudStepper;
        [SerializeField] private SettingSliderStepper _gammaSlider;
        [SerializeField] private SettingStepper _frameRateStepper;

        [Header("HUD 위치 조절")]
        [SerializeField] private HudPositionEditor _hudPositionEditor;
        [SerializeField] private Button _topHudPositionButton;
        [SerializeField] private TMP_Text _topHudPositionText;
        [SerializeField] private Button _bottomHudPositionButton;
        [SerializeField] private TMP_Text _bottomHudPositionText;
        [Tooltip("게임 화면이 아닐 때(씬에 HUD가 하나도 없을 때) 위치 대신 보여줄 글자")]
        [SerializeField] private string _notInGameText = "게임 중에 조절";
        [Tooltip("게임 화면인데 그 HUD만 없을 때 위치 대신 보여줄 글자")]
        [SerializeField] private string _noHudText = "없음";

        private static readonly Color _enabledTextColor = Color.black;
        private static readonly Color _disabledTextColor = new Color(0.6f, 0.6f, 0.6f, 1f);

        // 스테퍼 선택지에서 ON이 몇 번째인지 (OFF, ON 순서면 1)
        private const int OnIndex = 1;

        // 처음 열 때는 줄들의 Awake(처음 값 설정)가 끝난 뒤인 Start에서, 그 뒤로는 열 때마다 OnEnable에서 저장된 값을 보여줌
        private bool _isStarted;

        private void Awake()
        {
            if (_topHudStepper != null) _topHudStepper.onValueChanged.AddListener(index => DisplaySettings.IsTopHudVisible = index == OnIndex);
            if (_bottomHudStepper != null) _bottomHudStepper.onValueChanged.AddListener(index => DisplaySettings.IsBottomHudVisible = index == OnIndex);
            if (_gammaSlider != null) _gammaSlider.onValueChanged.AddListener(value => DisplaySettings.Gamma = Mathf.RoundToInt(value));
            if (_frameRateStepper != null) _frameRateStepper.onValueChanged.AddListener(index => DisplaySettings.IsFrameRateVisible = index == OnIndex);

            if (_topHudPositionButton != null) _topHudPositionButton.onClick.AddListener(() => EditHudPosition(HudDisplay.HudArea.Top));
            if (_bottomHudPositionButton != null) _bottomHudPositionButton.onClick.AddListener(() => EditHudPosition(HudDisplay.HudArea.Bottom));
        }

        private void Start()
        {
            _isStarted = true;
            ShowSavedValues();
        }

        private void OnEnable()
        {
            // 위치 조절을 마치면 바뀐 위치를 다시 보여줌
            DisplaySettings.Changed += ShowHudPositions;
            if (_isStarted) ShowSavedValues();
        }

        private void OnDisable()
        {
            DisplaySettings.Changed -= ShowHudPositions;
        }

        // 기본값으로 되돌리기 줄의 OnClick에 연결
        public void ResetToDefault()
        {
            DisplaySettings.ResetToDefault();
            ShowSavedValues();
        }

        private void EditHudPosition(HudDisplay.HudArea area)
        {
            if (_hudPositionEditor != null) _hudPositionEditor.StartEditing(area);
        }

        private void ShowSavedValues()
        {
            if (_topHudStepper != null) _topHudStepper.SetOptions(_topHudStepper.options, DisplaySettings.IsTopHudVisible ? OnIndex : 0);
            if (_bottomHudStepper != null) _bottomHudStepper.SetOptions(_bottomHudStepper.options, DisplaySettings.IsBottomHudVisible ? OnIndex : 0);
            if (_gammaSlider != null) _gammaSlider.SetSavedValue(DisplaySettings.Gamma);
            if (_frameRateStepper != null) _frameRateStepper.SetOptions(_frameRateStepper.options, DisplaySettings.IsFrameRateVisible ? OnIndex : 0);
            ShowHudPositions();
        }

        // HUD 위치 줄: 그 HUD가 씬에 있으면 지금 위치 값, 없으면 고를 수 없게 하고 안내 글자
        private void ShowHudPositions()
        {
            ShowHudPosition(_topHudPositionButton, _topHudPositionText, HudDisplay.HudArea.Top);
            ShowHudPosition(_bottomHudPositionButton, _bottomHudPositionText, HudDisplay.HudArea.Bottom);
        }

        private void ShowHudPosition(Button button, TMP_Text text, HudDisplay.HudArea area)
        {
            bool hasHud = HudPositionEditor.HasHud(area);
            if (button != null) button.interactable = hasHud;
            if (text == null) return;

            bool isInGame = HudPositionEditor.HasHud(HudDisplay.HudArea.Top) || HudPositionEditor.HasHud(HudDisplay.HudArea.Bottom);
            text.text = hasHud ? Mathf.RoundToInt(DisplaySettings.GetHudOffset(area)).ToString() : isInGame ? _noHudText : _notInGameText;
            text.color = hasHud ? _enabledTextColor : _disabledTextColor;

            // 줄 이름도 같은 색으로
            TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>() : null;
            if (label != null && label != text) label.color = text.color;
        }
    }
}
