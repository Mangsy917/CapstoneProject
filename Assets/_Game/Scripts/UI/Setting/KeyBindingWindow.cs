using System;
using System.Collections.Generic;
using Game.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace Game.UI.Setting
{
    // 컨트롤러 설정 팝업 (P_KeyBindingWindow 프리팹)에 붙여서 사용
    // 왼쪽에 공격 버튼 이름, 오른쪽에 지금 쓰는 컨트롤러(키보드 / 플레이스테이션 / Xbox)에 연결된 키를
    // 버튼 스프라이트로 보여줌 (스프라이트가 없는 키는 글자로). 컨트롤러가 바뀌면 바로 그 컨트롤러 쪽으로 바뀜
    // 줄을 고른 뒤 새 키를 누르면 그 키로 바뀜 (누른 기기에 맞춰 키보드 또는 게임패드 쪽이 바뀜)
    // - 위/아래: 줄 이동, 선택 키 / 마우스 클릭: 키 바꾸기 시작 (줄) 또는 실행 (기본값, 닫기)
    // - 취소 키 / ESC: 팝업 닫기, 키 입력 대기 중에는 ESC(게임패드 Start)로 바꾸기 취소
    // - 다른 공격 버튼이 이미 쓰는 키를 고르면 두 버튼의 키를 서로 바꿈
    // - 이동 키, 설정창 열기 키는 고를 수 없음
    // - 바꾼 키는 KeyBindingStore에 저장됨 (씬을 넘어가도 유지, 빌드에서는 껐다 켜도 유지)
    public class KeyBindingWindow : MonoBehaviour
    {
        [Serializable]
        public class BindingRow
        {
            // Input Actions 에셋의 액션 이름 (AttackL, AttackM ...)
            public string KBW_actionName;
            public Button rowButton;
            public TMP_Text keyboardText;
            public TMP_Text gamepadText;
            public Image keyboardIcon;
            public Image gamepadIcon;
        }

        // 팝업이 열려 있으면 뒤의 설정 메뉴, 타이틀 메뉴, 설정창 닫기는 입력을 받지 않음
        // 닫힌 다음 프레임까지 열린 것으로 봄 (닫기를 누른 선택 키로 뒤의 메뉴가 팝업을 다시 여는 것을 막음)
        // - UIControl은 한 프레임에 한 번 입력을 읽으므로, 실행 순서에 따라 뒤의 메뉴는
        //   팝업보다 한 프레임 늦게 같은 입력을 받을 수 있음 -> 다음 프레임까지 막아야 함
        public static bool IsOpen => KBW_openCount > 0 || Time.frameCount - KBW_closedFrame <= 1;
        private static int KBW_openCount = 0;
        private static int KBW_closedFrame = int.MinValue / 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOpenCount()
        {
            KBW_openCount = 0;
            KBW_closedFrame = int.MinValue / 2;
        }

        // 비워두면 씬에서 찾음
        [SerializeField] private UIControl KBW_uiControl;
        [SerializeField] private InputActionAsset KBW_inputActions;
        [SerializeField] private string KBW_actionMapName = "Char";

        [SerializeField] private BindingRow[] _bindingRows;
        [Tooltip("고르면 버튼 확인을 시작하는 줄 (누른 버튼이 오른쪽 가상 컨트롤러에 켜짐)")]
        [SerializeField] private Button _buttonTestButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _guideText;

        [Tooltip("키별 버튼 스프라이트 목록")]
        [SerializeField] private Game.UI.ButtonIconSet _buttonIcons;
        [Tooltip("오른쪽 가상 컨트롤러")]
        [SerializeField] private VirtualController _virtualController;

        [SerializeField] private string _guideMessage = "바꿀 버튼을 고르고 선택 키를 누르세요";
        [SerializeField] private string _waitingMessage = "새 키를 누르세요  (ESC: 취소)";
        [SerializeField] private string _testingMessage = "버튼을 눌러 확인하세요";
        [SerializeField] private string _reservedMessage = "이동 키와 설정창 열기 키는 사용할 수 없습니다";
        [SerializeField] private string _waitingKeyText = "...";

        private static readonly Color _highlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color _normalColor = new Color(1f, 1f, 1f, 0f);

        // 위에서 아래 순서: 공격 버튼 줄들, 버튼 확인, 기본값, 닫기
        private Button[] _menuButtons;
        private int _highlightIndex;

        private bool _isWaitingForKey;
        private int _waitingRowIndex;
        private IDisposable _keyListener;

        // 키를 바꾼(취소한) 그 프레임에는 메뉴 입력을 처리하지 않음 (같은 키로 다시 실행되지 않도록)
        private int _ignoreInputFrame = -1;

        // 누르고 있는 동안 계속 넘어가지 않도록 이전 프레임 입력을 기억
        private bool _lastUp, _lastDown, _lastSelect;

        void Awake()
        {
            if (KBW_uiControl == null) KBW_uiControl = FindAnyObjectByType<UIControl>(FindObjectsInactive.Include);
            KeyBindingStore.Apply(KBW_inputActions);

            List<Button> menuButtons = new List<Button>();
            for (int i = 0; i < _bindingRows.Length; i++)
            {
                int rowIndex = i;
                menuButtons.Add(_bindingRows[i].rowButton);
                _bindingRows[i].rowButton.onClick.AddListener(() => StartWaitingForKey(rowIndex));
            }
            if (_buttonTestButton != null)
            {
                menuButtons.Add(_buttonTestButton);
                _buttonTestButton.onClick.AddListener(StartButtonTest);
            }
            if (_resetButton != null)
            {
                menuButtons.Add(_resetButton);
                _resetButton.onClick.AddListener(ResetToDefault);
            }
            if (_closeButton != null)
            {
                menuButtons.Add(_closeButton);
                _closeButton.onClick.AddListener(Close);
            }
            _menuButtons = menuButtons.ToArray();

            // 마우스를 올리면 그 줄이 선택되도록 연결
            for (int i = 0; i < _menuButtons.Length; i++)
            {
                int index = i;
                EventTrigger trigger = _menuButtons[i].gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = _menuButtons[i].gameObject.AddComponent<EventTrigger>();

                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => SelectButton(index));
                trigger.triggers.Add(entry);
            }
        }

        void OnEnable()
        {
            KBW_openCount++;

            _highlightIndex = 0;
            _isWaitingForKey = false;
            _ignoreInputFrame = Time.frameCount;
            SetGuide(_guideMessage);
            RefreshKeyTexts();
            RefreshHighlight();

            // 팝업을 연 키를 아직 누르고 있어도 바로 실행되지 않도록 현재 입력을 기억
            RememberInput();

            ControllerTypeDetector.OnControllerTypeChanged += HandleControllerTypeChanged;
        }

        void OnDisable()
        {
            KBW_openCount--;
            KBW_closedFrame = Time.frameCount;
            StopWaitingForKey();
            StopButtonTest();

            ControllerTypeDetector.OnControllerTypeChanged -= HandleControllerTypeChanged;
        }

        private void HandleControllerTypeChanged(ControllerTypeDetector.ControllerType type)
        {
            RefreshKeyTexts();
        }

        // 컨트롤러 설정 버튼의 OnClick에 연결
        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            if (_isWaitingForKey) return;
            gameObject.SetActive(false);
        }

        // ===== 버튼 확인 =====

        private bool IsButtonTesting => _virtualController != null && _virtualController.IsTesting;

        private void StartButtonTest()
        {
            if (_virtualController == null || _isWaitingForKey) return;

            _virtualController.StartTest();
            SetGuide(_testingMessage);
        }

        private void StopButtonTest()
        {
            if (!IsButtonTesting) return;

            _virtualController.StopTest();
            _ignoreInputFrame = Time.frameCount;
            SetGuide(_guideMessage);
        }

        void Update()
        {
            if (KBW_uiControl == null) return;

            // 버튼 확인 중에는 메뉴를 움직이지 않음 (공격 버튼과 결정 / 취소 키가 겹치므로)
            // 나가는 키(ESC / PS 홈 버튼 / 그 외 패드 메뉴 버튼)로만 끝내고, 취소 / 닫기 입력은 지워서 창이 닫히지 않게 함
            if (IsButtonTesting)
            {
                KBW_uiControl.uiCancelDown = false;
                KBW_uiControl.uiClose = false;
                if (_virtualController.WasExitPressedThisFrame()) StopButtonTest();
                RememberInput();
                return;
            }

            // 키 입력 대기 중이거나 방금 키를 바꾼 프레임이면 메뉴는 움직이지 않음
            // (같은 프레임의 취소 / 닫기 입력도 여기서 지워서 설정창까지 닫히지 않게 함)
            if (_isWaitingForKey || Time.frameCount == _ignoreInputFrame)
            {
                KBW_uiControl.uiCancelDown = false;
                KBW_uiControl.uiClose = false;
                RememberInput();
                return;
            }

            bool up = KBW_uiControl.uiUp;
            bool down = KBW_uiControl.uiDown;
            bool select = KBW_uiControl.uiSelect;

            if (up && !_lastUp) MoveHighlight(-1);
            else if (down && !_lastDown) MoveHighlight(1);

            if (select && !_lastSelect)
            {
                _menuButtons[_highlightIndex].onClick.Invoke();
            }
            else if (KBW_uiControl.uiCancelDown || KBW_uiControl.uiClose)
            {
                // 같은 키로 뒤의 설정창까지 닫히지 않도록 처리한 입력은 지움
                KBW_uiControl.uiCancelDown = false;
                KBW_uiControl.uiClose = false;
                Close();
            }

            RememberInput();
        }

        private void RememberInput()
        {
            if (KBW_uiControl == null) return;

            _lastUp = KBW_uiControl.uiUp;
            _lastDown = KBW_uiControl.uiDown;
            _lastSelect = KBW_uiControl.uiSelect;
        }

        private void MoveHighlight(int direction)
        {
            _highlightIndex = (_highlightIndex + direction + _menuButtons.Length) % _menuButtons.Length;
            RefreshHighlight();
        }

        private void SelectButton(int index)
        {
            if (_isWaitingForKey) return;

            _highlightIndex = index;
            RefreshHighlight();
        }

        // ===== 키 바꾸기 =====

        private void StartWaitingForKey(int rowIndex)
        {
            if (_isWaitingForKey || IsButtonTesting) return;

            _isWaitingForKey = true;
            _waitingRowIndex = rowIndex;
            _highlightIndex = rowIndex;
            RefreshHighlight();

            RefreshRow(_bindingRows[rowIndex], isWaiting: true);
            SetGuide(_waitingMessage);

            // 다음에 눌리는 버튼(키보드 키, 게임패드 버튼)을 받음
            _keyListener = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);
        }

        private void StopWaitingForKey()
        {
            _keyListener?.Dispose();
            _keyListener = null;
            _isWaitingForKey = false;
        }

        private void OnAnyButtonPressed(InputControl control)
        {
            if (!_isWaitingForKey) return;

            string group;
            string layoutName;
            if (control.device is Keyboard)
            {
                group = ControllerTypeDetector.KeyboardGroup;
                layoutName = "<Keyboard>";
            }
            else if (control.device is Gamepad)
            {
                group = ControllerTypeDetector.GamepadGroup;
                layoutName = "<Gamepad>";
            }
            else
            {
                // 마우스 등 다른 기기는 무시하고 계속 기다림
                return;
            }

            // "/Keyboard/u" -> "<Keyboard>/u", "/XInputControllerWindows/buttonSouth" -> "<Gamepad>/buttonSouth"
            string path = layoutName + "/" + control.path.Substring(control.device.path.Length + 1);

            if (IsSamePath(path, "<Keyboard>/escape") || IsSamePath(path, "<Gamepad>/start"))
            {
                FinishWaitingForKey();
                return;
            }

            if (IsReservedPath(path))
            {
                SetGuide(_reservedMessage);
                return;
            }

            AssignKey(_bindingRows[_waitingRowIndex].KBW_actionName, group, path);
            KeyBindingStore.Save(KBW_inputActions);
            FinishWaitingForKey();
        }

        private void FinishWaitingForKey()
        {
            StopWaitingForKey();
            _ignoreInputFrame = Time.frameCount;
            SetGuide(_guideMessage);
            RefreshKeyTexts();
        }

        // actionName의 group 쪽 키를 newPath로 바꿈
        // 다른 공격 버튼이 이미 newPath를 쓰고 있으면 그 버튼에는 원래 키를 넣어서 서로 바꿈
        private void AssignKey(string actionName, string group, string newPath)
        {
            InputAction action = FindAction(actionName);
            int bindingIndex = FindBindingIndex(action, group);
            if (bindingIndex < 0) return;

            string oldPath = action.bindings[bindingIndex].effectivePath;

            foreach (BindingRow row in _bindingRows)
            {
                if (row.KBW_actionName == actionName) continue;

                InputAction otherAction = FindAction(row.KBW_actionName);
                int otherIndex = FindBindingIndex(otherAction, group);
                if (otherIndex < 0) continue;

                if (IsSamePath(otherAction.bindings[otherIndex].effectivePath, newPath))
                    SetBindingPath(otherAction, otherIndex, oldPath);
            }

            SetBindingPath(action, bindingIndex, newPath);
        }

        // 기본 키와 같으면 오버라이드를 지우고, 다르면 오버라이드를 넣음
        private void SetBindingPath(InputAction action, int bindingIndex, string path)
        {
            if (IsSamePath(action.bindings[bindingIndex].path, path)) action.RemoveBindingOverride(bindingIndex);
            else action.ApplyBindingOverride(bindingIndex, path);
        }

        // 공격 버튼들의 키를 기본값(Input Actions 에셋에 적힌 키)으로 되돌림
        public void ResetToDefault()
        {
            if (_isWaitingForKey) return;

            foreach (BindingRow row in _bindingRows)
            {
                InputAction action = FindAction(row.KBW_actionName);
                action?.RemoveAllBindingOverrides();
            }

            KeyBindingStore.Save(KBW_inputActions);
            RefreshKeyTexts();
        }

        // 이동 키, 설정창 열기 키 등 공격 버튼이 아닌 액션이 쓰는 키인지
        private bool IsReservedPath(string path)
        {
            InputActionMap map = FindMap();
            if (map == null) return false;

            foreach (InputAction action in map.actions)
            {
                if (IsAttackAction(action.name)) continue;

                foreach (InputBinding binding in action.bindings)
                {
                    if (IsSamePath(binding.effectivePath, path)) return true;
                }
            }
            return false;
        }

        private bool IsAttackAction(string actionName)
        {
            foreach (BindingRow row in _bindingRows)
            {
                if (row.KBW_actionName == actionName) return true;
            }
            return false;
        }

        // ===== 찾기 =====

        private InputActionMap FindMap()
        {
            return KBW_inputActions != null ? KBW_inputActions.FindActionMap(KBW_actionMapName) : null;
        }

        private InputAction FindAction(string actionName)
        {
            InputActionMap map = FindMap();
            return map != null ? map.FindAction(actionName) : null;
        }

        // action에서 group(PC / GamePad)에 속한 첫 번째 바인딩 번호
        private int FindBindingIndex(InputAction action, string group)
        {
            if (action == null) return -1;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                string groups = action.bindings[i].groups;
                if (!string.IsNullOrEmpty(groups) && groups.Contains(group)) return i;
            }
            return -1;
        }

        private static bool IsSamePath(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        // ===== 화면 =====

        // 각 줄의 키를 지금 컨트롤러 종류에 맞춰 다시 그림
        private void RefreshKeyTexts()
        {
            for (int i = 0; i < _bindingRows.Length; i++)
            {
                RefreshRow(_bindingRows[i], isWaiting: _isWaitingForKey && i == _waitingRowIndex);
            }
        }

        // 한 줄의 키 표시. 스프라이트가 있으면 스프라이트, 없으면 글자, 키 입력 대기 중이면 "..."
        private void RefreshRow(BindingRow row, bool isWaiting)
        {
            var type = ControllerTypeDetector.CurrentType;
            bool isKeyboard = type == ControllerTypeDetector.ControllerType.Keyboard;

            // 안 쓰는 쪽 열은 숨김
            SetColumnVisible(row.keyboardText, row.keyboardIcon, isKeyboard);
            SetColumnVisible(row.gamepadText, row.gamepadIcon, !isKeyboard);

            TMP_Text keyText = isKeyboard ? row.keyboardText : row.gamepadText;
            Image keyIcon = isKeyboard ? row.keyboardIcon : row.gamepadIcon;

            Sprite sprite = null;
            string keyName = _waitingKeyText;
            if (!isWaiting)
            {
                string path = FindBindingPath(FindAction(row.KBW_actionName), ControllerTypeDetector.GetBindingGroup(type));
                sprite = _buttonIcons != null ? _buttonIcons.GetSprite(type, path) : null;
                keyName = GetKeyName(path, type);
            }

            if (keyIcon != null)
            {
                keyIcon.sprite = sprite;
                keyIcon.enabled = sprite != null;
            }
            if (keyText != null) keyText.text = sprite != null ? "" : keyName;
        }

        private static void SetColumnVisible(TMP_Text keyText, Image keyIcon, bool isVisible)
        {
            if (keyText != null) keyText.gameObject.SetActive(isVisible);
            if (keyIcon != null) keyIcon.gameObject.SetActive(isVisible);
        }

        private string FindBindingPath(InputAction action, string group)
        {
            int bindingIndex = FindBindingIndex(action, group);
            return bindingIndex < 0 ? null : action.bindings[bindingIndex].effectivePath;
        }

        private static string GetKeyName(string path, ControllerTypeDetector.ControllerType type)
        {
            if (string.IsNullOrEmpty(path)) return "-";

            if (type == ControllerTypeDetector.ControllerType.PlayStation)
            {
                string buttonName = GetPlayStationButtonName(path);
                if (buttonName != null) return buttonName;
            }
            else if (type == ControllerTypeDetector.ControllerType.Xbox)
            {
                string buttonName = GetGamepadButtonName(path);
                if (buttonName != null) return buttonName;
            }

            string keyName = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return keyName.Length == 1 ? keyName.ToUpper() : keyName;
        }

        // 플레이스테이션 패드 버튼 이름 (스프라이트가 없을 때 글자로 보여줌)
        private static string GetPlayStationButtonName(string path)
        {
            string buttonPath = path.Replace("<Gamepad>/", "");
            switch (buttonPath)
            {
                case "buttonSouth": return "×";
                case "buttonEast": return "○";
                case "buttonWest": return "□";
                case "buttonNorth": return "△";
                case "leftShoulder": return "L1";
                case "rightShoulder": return "R1";
                case "leftTrigger": return "L2";
                case "rightTrigger": return "R2";
                case "leftStickPress": return "L3";
                case "rightStickPress": return "R3";
                case "select": return "Share";
                default: return null;
            }
        }

        // Xbox 패드 버튼 이름 (스프라이트가 없을 때 글자로 보여줌)
        private static string GetGamepadButtonName(string path)
        {
            string buttonPath = path.Replace("<Gamepad>/", "");
            switch (buttonPath)
            {
                case "buttonSouth": return "A";
                case "buttonEast": return "B";
                case "buttonWest": return "X";
                case "buttonNorth": return "Y";
                case "leftShoulder": return "LB";
                case "rightShoulder": return "RB";
                case "leftTrigger": return "LT";
                case "rightTrigger": return "RT";
                case "leftStickPress": return "LS";
                case "rightStickPress": return "RS";
                case "select": return "Back";
                default: return null;
            }
        }

        // 선택된 줄만 회색, 나머지는 투명
        private void RefreshHighlight()
        {
            if (_menuButtons == null) return;

            for (int i = 0; i < _menuButtons.Length; i++)
            {
                if (_menuButtons[i].image != null) _menuButtons[i].image.color = (i == _highlightIndex) ? _highlightColor : _normalColor;
            }
        }

        private void SetGuide(string message)
        {
            if (_guideText != null) _guideText.text = message;
        }
    }
}
