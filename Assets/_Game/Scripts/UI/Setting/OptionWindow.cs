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
    // 옵션 창 (P_OptionWindow 프리팹)에 붙여서 사용. 타이틀과 연습 씬의 옵션 버튼이 같은 창을 엶
    // 키보드 키를 Xbox 버튼에 연결하는 표를 보여주고 바꿈 (KeyboardMapping)
    // - 왼쪽에 Xbox 버튼, 오른쪽에 그 버튼에 연결된 키보드 키를 버튼 스프라이트로 보여줌 (없으면 글자로)
    // - 위/아래: 줄 이동, 선택 키 / 마우스 클릭: 키 바꾸기 시작 (줄) 또는 실행 (공격 버튼 설정, 기본값, 닫기)
    // - 키 바꾸기는 키보드 키로만 함. 다른 버튼이 이미 쓰는 키를 고르면 두 버튼의 키를 서로 바꿈
    // - 키 입력 대기 중에는 ESC(게임패드 Start)로 취소. 메뉴 버튼은 ESC로 고정이라 바꿀 수 없음
    // - 취소 키 / ESC: 창 닫기
    // - "공격 버튼 설정"은 공격 버튼에 연결할 패드 버튼을 바꾸는 창(KeyBindingWindow)을 엶
    public class OptionWindow : MonoBehaviour
    {
        [Serializable]
        public class MappingRow
        {
            // KeyboardMapping의 Xbox 버튼 경로 (예: <Gamepad>/buttonSouth)
            public string padPath;
            public Button padRowButton;
            public Image padIcon;
            public TMP_Text padText;
            public Image keyIcon;
            public TMP_Text keyText;
        }

        // 창이 열려 있으면 뒤의 설정 메뉴, 타이틀 메뉴, 설정창 닫기는 입력을 받지 않음
        // 닫힌 다음 프레임까지 열린 것으로 봄 (닫기를 누른 선택 키로 뒤의 메뉴가 창을 다시 여는 것을 막음)
        public static bool OW_IsOpen => OW_openCount > 0 || Time.frameCount - OW_closedFrame <= 1;
        private static int OW_openCount = 0;
        private static int OW_closedFrame = int.MinValue / 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOpenCount()
        {
            OW_openCount = 0;
            OW_closedFrame = int.MinValue / 2;
        }

        // 비워두면 씬에서 찾음
        [SerializeField] private UIControl OW_uiControl;
        [SerializeField] private InputActionAsset OW_inputActions;

        [SerializeField] private MappingRow[] _mappingRows;
        [Tooltip("공격 버튼에 연결할 패드 버튼을 바꾸는 창을 여는 줄")]
        [SerializeField] private Button _attackSettingButton;
        [SerializeField] private KeyBindingWindow _keyBindingWindow;
        [SerializeField] private Button OW_resetButton;
        [SerializeField] private Button OW_closeButton;
        [SerializeField] private TMP_Text OW_guideText;

        [Tooltip("키별 버튼 스프라이트 목록")]
        [SerializeField] private ButtonIconSet OW_buttonIcons;

        [SerializeField] private string OW_guideMessage = "바꿀 Xbox 버튼을 고르고 선택 키를 누르세요";
        [SerializeField] private string OW_waitingMessage = "새 키보드 키를 누르세요  (ESC: 취소)";
        [SerializeField] private string _lockedMessage = "메뉴 버튼은 ESC로 고정되어 있습니다";
        [SerializeField] private string OW_waitingKeyText = "...";

        private static readonly Color OW_highlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color OW_normalColor = new Color(1f, 1f, 1f, 0f);

        // 위에서 아래 순서: Xbox 버튼 줄들, 공격 버튼 설정, 기본값, 닫기
        private Button[] OW_menuButtons;
        private int OW_highlightIndex;

        private bool OW_isWaitingForKey;
        private int OW_waitingRowIndex;
        private IDisposable OW_keyListener;

        // 키를 바꾼(취소한) 그 프레임에는 메뉴 입력을 처리하지 않음 (같은 키로 다시 실행되지 않도록)
        private int OW_ignoreInputFrame = -1;

        // 누르고 있는 동안 계속 실행되지 않도록 이전 프레임 선택 키를 기억
        private bool OW_lastSelect;

        void Awake()
        {
            if (OW_uiControl == null) OW_uiControl = FindAnyObjectByType<UIControl>(FindObjectsInactive.Include);
            KeyBindingStore.Apply(OW_inputActions);

            List<Button> menuButtons = new List<Button>();
            for (int i = 0; i < _mappingRows.Length; i++)
            {
                int rowIndex = i;
                menuButtons.Add(_mappingRows[i].padRowButton);
                _mappingRows[i].padRowButton.onClick.AddListener(() => StartWaitingForKey(rowIndex));
            }
            if (_attackSettingButton != null)
            {
                menuButtons.Add(_attackSettingButton);
                _attackSettingButton.onClick.AddListener(OpenAttackSetting);
            }
            if (OW_resetButton != null)
            {
                menuButtons.Add(OW_resetButton);
                OW_resetButton.onClick.AddListener(ResetToDefault);
            }
            if (OW_closeButton != null)
            {
                menuButtons.Add(OW_closeButton);
                OW_closeButton.onClick.AddListener(Close);
            }
            OW_menuButtons = menuButtons.ToArray();

            // 마우스를 올리면 그 줄이 선택되도록 연결
            for (int i = 0; i < OW_menuButtons.Length; i++)
            {
                int index = i;
                EventTrigger trigger = OW_menuButtons[i].gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = OW_menuButtons[i].gameObject.AddComponent<EventTrigger>();

                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => SelectButton(index));
                trigger.triggers.Add(entry);
            }

            if (_keyBindingWindow != null) _keyBindingWindow.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            OW_openCount++;

            OW_highlightIndex = 0;
            OW_isWaitingForKey = false;
            OW_ignoreInputFrame = Time.frameCount;
            SetGuide(OW_guideMessage);
            RefreshRows();
            RefreshHighlight();

            // 창을 연 키를 아직 누르고 있어도 바로 실행되지 않도록 현재 입력을 기억
            RememberInput();
        }

        void OnDisable()
        {
            OW_openCount--;
            OW_closedFrame = Time.frameCount;
            StopWaitingForKey();
            if (_keyBindingWindow != null) _keyBindingWindow.gameObject.SetActive(false);
        }

        // 옵션 버튼의 OnClick에 연결
        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            if (OW_isWaitingForKey) return;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (OW_uiControl == null) return;

            // 공격 버튼 설정 창이 열려 있으면 그 창이 입력을 처리함
            if (KeyBindingWindow.IsOpen)
            {
                RememberInput();
                return;
            }

            // 키 입력 대기 중이거나 방금 키를 바꾼 프레임이면 메뉴는 움직이지 않음
            // (같은 프레임의 취소 / 닫기 입력도 여기서 지워서 뒤의 설정창까지 닫히지 않게 함)
            if (OW_isWaitingForKey || Time.frameCount == OW_ignoreInputFrame)
            {
                OW_uiControl.uiCancelDown = false;
                OW_uiControl.uiClose = false;
                RememberInput();
                return;
            }

            bool select = OW_uiControl.uiSelect;

            // 길게 눌러 연속 이동 중이면 맨 위/맨 아래에서 멈춤
            if (OW_uiControl.uiUpStep) MoveHighlight(-1, canWrap: !OW_uiControl.isRepeating);
            else if (OW_uiControl.uiDownStep) MoveHighlight(1, canWrap: !OW_uiControl.isRepeating);

            if (select && !OW_lastSelect)
            {
                OW_menuButtons[OW_highlightIndex].onClick.Invoke();
            }
            else if (OW_uiControl.uiCancelDown || OW_uiControl.uiClose)
            {
                // 같은 키로 뒤의 설정창까지 닫히지 않도록 처리한 입력은 지움
                OW_uiControl.uiCancelDown = false;
                OW_uiControl.uiClose = false;
                Close();
            }

            RememberInput();
        }

        private void RememberInput()
        {
            if (OW_uiControl == null) return;

            OW_lastSelect = OW_uiControl.uiSelect;
        }

        // canWrap: 끝에서 반대쪽 끝으로 넘어갈 수 있는지 (false면 끝에서 멈춤)
        private void MoveHighlight(int direction, bool canWrap = true)
        {
            int next = OW_highlightIndex + direction;
            if (!canWrap && (next < 0 || next >= OW_menuButtons.Length)) return;

            OW_highlightIndex = (next + OW_menuButtons.Length) % OW_menuButtons.Length;
            RefreshHighlight();
        }

        private void SelectButton(int index)
        {
            if (OW_isWaitingForKey || KeyBindingWindow.IsOpen) return;

            OW_highlightIndex = index;
            RefreshHighlight();
        }

        private void OpenAttackSetting()
        {
            if (OW_isWaitingForKey || _keyBindingWindow == null) return;
            _keyBindingWindow.Open();
        }

        public void ResetToDefault()
        {
            if (OW_isWaitingForKey) return;

            KeyboardMapping.ResetToDefault();
            KeyboardMapping.ApplyTo(OW_inputActions);
            RefreshRows();
        }

        // ===== 키 바꾸기 =====

        private void StartWaitingForKey(int rowIndex)
        {
            if (OW_isWaitingForKey) return;

            OW_highlightIndex = rowIndex;
            RefreshHighlight();

            if (KeyboardMapping.IsLocked(_mappingRows[rowIndex].padPath))
            {
                SetGuide(_lockedMessage);
                return;
            }

            OW_isWaitingForKey = true;
            OW_waitingRowIndex = rowIndex;

            RefreshRow(_mappingRows[rowIndex], isWaiting: true);
            SetGuide(OW_waitingMessage);

            // 다음에 눌리는 버튼(키보드 키, 게임패드 버튼)을 받음
            OW_keyListener = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);
        }

        private void StopWaitingForKey()
        {
            OW_keyListener?.Dispose();
            OW_keyListener = null;
            OW_isWaitingForKey = false;
        }

        private void OnAnyButtonPressed(InputControl control)
        {
            if (!OW_isWaitingForKey) return;

            // 게임패드는 Start로 취소만 할 수 있음 (키는 키보드로만 바꿈)
            if (control.device is Gamepad)
            {
                if (control == ((Gamepad)control.device).startButton) FinishWaitingForKey();
                return;
            }

            // 마우스 등 다른 기기는 무시하고 계속 기다림
            if (!(control.device is Keyboard)) return;

            // "/Keyboard/u" -> "<Keyboard>/u"
            string path = "<Keyboard>/" + control.path.Substring(control.device.path.Length + 1);

            if (string.Equals(path, KeyboardMapping.CancelKeyPath, StringComparison.OrdinalIgnoreCase))
            {
                FinishWaitingForKey();
                return;
            }

            KeyboardMapping.SetKey(_mappingRows[OW_waitingRowIndex].padPath, path);
            KeyboardMapping.ApplyTo(OW_inputActions);
            FinishWaitingForKey();
        }

        private void FinishWaitingForKey()
        {
            StopWaitingForKey();
            OW_ignoreInputFrame = Time.frameCount;
            SetGuide(OW_guideMessage);
            RefreshRows();
        }

        // ===== 화면 =====

        private void RefreshRows()
        {
            if (_mappingRows == null) return;

            for (int i = 0; i < _mappingRows.Length; i++)
            {
                RefreshRow(_mappingRows[i], isWaiting: OW_isWaitingForKey && i == OW_waitingRowIndex);
            }
        }

        // 한 줄의 Xbox 버튼과 키보드 키 표시. 스프라이트가 있으면 스프라이트, 없으면 글자, 키 입력 대기 중이면 "..."
        private void RefreshRow(MappingRow row, bool isWaiting)
        {
            Sprite padSprite = GetSprite(ControllerTypeDetector.ControllerType.Xbox, row.padPath);
            SetIconOrText(row.padIcon, row.padText, padSprite, GetPadButtonName(row.padPath));

            string keyPath = isWaiting ? null : KeyboardMapping.GetKey(row.padPath);
            Sprite keySprite = GetSprite(ControllerTypeDetector.ControllerType.Keyboard, keyPath);
            SetIconOrText(row.keyIcon, row.keyText, keySprite, isWaiting ? OW_waitingKeyText : GetKeyName(keyPath));
        }

        private Sprite GetSprite(ControllerTypeDetector.ControllerType type, string path)
        {
            return OW_buttonIcons != null && path != null ? OW_buttonIcons.GetSprite(type, path) : null;
        }

        private static void SetIconOrText(Image icon, TMP_Text text, Sprite sprite, string name)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            if (text != null) text.text = sprite != null ? "" : name;
        }

        // Xbox 버튼 이름 (스프라이트가 없을 때 글자로 보여줌)
        private static string GetPadButtonName(string padPath)
        {
            switch (padPath)
            {
                case "<Gamepad>/dpad/up": return "위";
                case "<Gamepad>/dpad/down": return "아래";
                case "<Gamepad>/dpad/left": return "왼쪽";
                case "<Gamepad>/dpad/right": return "오른쪽";
                case "<Gamepad>/buttonSouth": return "A";
                case "<Gamepad>/buttonEast": return "B";
                case "<Gamepad>/buttonWest": return "X";
                case "<Gamepad>/buttonNorth": return "Y";
                case "<Gamepad>/leftShoulder": return "LB";
                case "<Gamepad>/rightShoulder": return "RB";
                case "<Gamepad>/leftTrigger": return "LT";
                case "<Gamepad>/rightTrigger": return "RT";
                case "<Gamepad>/start": return "Menu";
                case "<Gamepad>/select": return "View";
                default: return "-";
            }
        }

        private static string GetKeyName(string keyPath)
        {
            if (string.IsNullOrEmpty(keyPath)) return "-";

            string keyName = InputControlPath.ToHumanReadableString(keyPath, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return keyName.Length == 1 ? keyName.ToUpper() : keyName;
        }

        // 선택된 줄만 회색, 나머지는 투명
        private void RefreshHighlight()
        {
            if (OW_menuButtons == null) return;

            for (int i = 0; i < OW_menuButtons.Length; i++)
            {
                if (OW_menuButtons[i].image != null) OW_menuButtons[i].image.color = (i == OW_highlightIndex) ? OW_highlightColor : OW_normalColor;
            }
        }

        private void SetGuide(string message)
        {
            if (OW_guideText != null) OW_guideText.text = message;
        }
    }
}
