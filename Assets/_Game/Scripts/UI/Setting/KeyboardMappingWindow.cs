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
    // 키보드 매핑 창 (P_OptionWindow 안의 KeyboardMapping)에 붙여서 사용. 옵션 메뉴의 "키보드 매핑"으로 열림
    // 키보드 키를 Xbox 버튼에 연결하는 표를 보여주고 바꿈 (KeyboardMapping)
    // - 왼쪽에 Xbox 버튼, 오른쪽에 그 버튼에 연결된 키보드 키를 버튼 스프라이트로 보여줌 (없으면 글자로)
    // - 위/아래: 줄 이동, 선택 키 / 마우스 클릭: 키 바꾸기 시작 (줄) 또는 실행 (기본값, 닫기)
    // - 키 바꾸기는 키보드 키로만 함. 다른 버튼이 이미 쓰는 키를 고르면 두 버튼의 키를 서로 바꿈
    // - 키 입력 대기 중에는 ESC(게임패드 Start)로 취소. 메뉴 버튼은 ESC로 고정이라 바꿀 수 없음
    // - 취소 키 / ESC: 이 창을 닫고 옵션 메뉴로 돌아감
    public class KeyboardMappingWindow : MonoBehaviour
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

        // 창이 열려 있으면 뒤의 옵션 메뉴는 입력을 받지 않음
        // 닫힌 다음 프레임까지 열린 것으로 봄 (닫기를 누른 선택 키로 뒤의 메뉴가 창을 다시 여는 것을 막음)
        public static bool KMW_IsOpen => KMW_openCount > 0 || Time.frameCount - KMW_closedFrame <= 1;
        private static int KMW_openCount = 0;
        private static int KMW_closedFrame = int.MinValue / 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOpenCount()
        {
            KMW_openCount = 0;
            KMW_closedFrame = int.MinValue / 2;
        }

        // 비워두면 씬에서 찾음
        [SerializeField] private UIControl KMW_uiControl;
        [SerializeField] private InputActionAsset KMW_inputActions;

        [SerializeField] private MappingRow[] _mappingRows;
        [SerializeField] private Button KMW_resetButton;
        [SerializeField] private Button KMW_closeButton;
        [SerializeField] private TMP_Text KMW_guideText;

        [Tooltip("키별 버튼 스프라이트 목록")]
        [SerializeField] private ButtonIconSet KMW_buttonIcons;

        [SerializeField] private string KMW_guideMessage = "바꿀 Xbox 버튼을 고르고 선택 키를 누르세요";
        [SerializeField] private string KMW_waitingMessage = "새 키보드 키를 누르세요  (ESC: 취소)";
        [SerializeField] private string _lockedMessage = "메뉴 버튼은 ESC로 고정되어 있습니다";
        [SerializeField] private string KMW_waitingKeyText = "...";

        private static readonly Color KMW_highlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color KMW_normalColor = new Color(1f, 1f, 1f, 0f);

        // 위에서 아래 순서: Xbox 버튼 줄들, 기본값, 닫기
        private Button[] KMW_menuButtons;
        private int KMW_highlightIndex;

        private bool KMW_isWaitingForKey;
        private int KMW_waitingRowIndex;
        private IDisposable KMW_keyListener;

        // 키를 바꾼(취소한) 그 프레임에는 메뉴 입력을 처리하지 않음 (같은 키로 다시 실행되지 않도록)
        private int KMW_ignoreInputFrame = -1;

        // 누르고 있는 동안 계속 실행되지 않도록 이전 프레임 선택 키를 기억
        private bool KMW_lastSelect;

        void Awake()
        {
            if (KMW_uiControl == null) KMW_uiControl = FindAnyObjectByType<UIControl>(FindObjectsInactive.Include);
            KeyBindingStore.Apply(KMW_inputActions);

            List<Button> menuButtons = new List<Button>();
            for (int i = 0; i < _mappingRows.Length; i++)
            {
                int rowIndex = i;
                menuButtons.Add(_mappingRows[i].padRowButton);
                _mappingRows[i].padRowButton.onClick.AddListener(() => StartWaitingForKey(rowIndex));
            }
            if (KMW_resetButton != null)
            {
                menuButtons.Add(KMW_resetButton);
                KMW_resetButton.onClick.AddListener(ResetToDefault);
            }
            if (KMW_closeButton != null)
            {
                menuButtons.Add(KMW_closeButton);
                KMW_closeButton.onClick.AddListener(Close);
            }
            KMW_menuButtons = menuButtons.ToArray();

            // 마우스를 올리면 그 줄이 선택되도록 연결
            for (int i = 0; i < KMW_menuButtons.Length; i++)
            {
                int index = i;
                EventTrigger trigger = KMW_menuButtons[i].gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = KMW_menuButtons[i].gameObject.AddComponent<EventTrigger>();

                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => SelectButton(index));
                trigger.triggers.Add(entry);
            }
        }

        void OnEnable()
        {
            KMW_openCount++;

            KMW_highlightIndex = 0;
            KMW_isWaitingForKey = false;
            KMW_ignoreInputFrame = Time.frameCount;
            SetGuide(KMW_guideMessage);
            RefreshRows();
            RefreshHighlight();

            // 창을 연 키를 아직 누르고 있어도 바로 실행되지 않도록 현재 입력을 기억
            RememberInput();
        }

        void OnDisable()
        {
            KMW_openCount--;
            KMW_closedFrame = Time.frameCount;
            StopWaitingForKey();
        }

        // 옵션 메뉴의 키보드 매핑 줄에서 부름
        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            if (KMW_isWaitingForKey) return;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (KMW_uiControl == null) return;

            // 키 입력 대기 중이거나 방금 키를 바꾼 프레임이면 메뉴는 움직이지 않음
            // (같은 프레임의 취소 / 닫기 입력도 여기서 지워서 뒤의 창까지 닫히지 않게 함)
            if (KMW_isWaitingForKey || Time.frameCount == KMW_ignoreInputFrame)
            {
                KMW_uiControl.uiCancelDown = false;
                KMW_uiControl.uiClose = false;
                RememberInput();
                return;
            }

            bool select = KMW_uiControl.uiSelect;

            // 길게 눌러 연속 이동 중이면 맨 위/맨 아래에서 멈춤
            if (KMW_uiControl.uiUpStep) MoveHighlight(-1, canWrap: !KMW_uiControl.isRepeating);
            else if (KMW_uiControl.uiDownStep) MoveHighlight(1, canWrap: !KMW_uiControl.isRepeating);

            if (select && !KMW_lastSelect)
            {
                KMW_menuButtons[KMW_highlightIndex].onClick.Invoke();
            }
            else if (KMW_uiControl.uiCancelDown || KMW_uiControl.uiClose)
            {
                // 같은 키로 뒤의 옵션 메뉴까지 닫히지 않도록 처리한 입력은 지움
                KMW_uiControl.uiCancelDown = false;
                KMW_uiControl.uiClose = false;
                Close();
            }

            RememberInput();
        }

        private void RememberInput()
        {
            if (KMW_uiControl == null) return;

            KMW_lastSelect = KMW_uiControl.uiSelect;
        }

        // canWrap: 끝에서 반대쪽 끝으로 넘어갈 수 있는지 (false면 끝에서 멈춤)
        private void MoveHighlight(int direction, bool canWrap = true)
        {
            int next = KMW_highlightIndex + direction;
            if (!canWrap && (next < 0 || next >= KMW_menuButtons.Length)) return;

            KMW_highlightIndex = (next + KMW_menuButtons.Length) % KMW_menuButtons.Length;
            RefreshHighlight();
        }

        private void SelectButton(int index)
        {
            if (KMW_isWaitingForKey) return;

            KMW_highlightIndex = index;
            RefreshHighlight();
        }

        public void ResetToDefault()
        {
            if (KMW_isWaitingForKey) return;

            KeyboardMapping.ResetToDefault();
            KeyboardMapping.ApplyTo(KMW_inputActions);
            RefreshRows();
        }

        // ===== 키 바꾸기 =====

        private void StartWaitingForKey(int rowIndex)
        {
            if (KMW_isWaitingForKey) return;

            KMW_highlightIndex = rowIndex;
            RefreshHighlight();

            if (KeyboardMapping.IsLocked(_mappingRows[rowIndex].padPath))
            {
                SetGuide(_lockedMessage);
                return;
            }

            KMW_isWaitingForKey = true;
            KMW_waitingRowIndex = rowIndex;

            RefreshRow(_mappingRows[rowIndex], isWaiting: true);
            SetGuide(KMW_waitingMessage);

            // 다음에 눌리는 버튼(키보드 키, 게임패드 버튼)을 받음
            KMW_keyListener = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);
        }

        private void StopWaitingForKey()
        {
            KMW_keyListener?.Dispose();
            KMW_keyListener = null;
            KMW_isWaitingForKey = false;
        }

        private void OnAnyButtonPressed(InputControl control)
        {
            if (!KMW_isWaitingForKey) return;

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

            KeyboardMapping.SetKey(_mappingRows[KMW_waitingRowIndex].padPath, path);
            KeyboardMapping.ApplyTo(KMW_inputActions);
            FinishWaitingForKey();
        }

        private void FinishWaitingForKey()
        {
            StopWaitingForKey();
            KMW_ignoreInputFrame = Time.frameCount;
            SetGuide(KMW_guideMessage);
            RefreshRows();
        }

        // ===== 화면 =====

        private void RefreshRows()
        {
            if (_mappingRows == null) return;

            for (int i = 0; i < _mappingRows.Length; i++)
            {
                RefreshRow(_mappingRows[i], isWaiting: KMW_isWaitingForKey && i == KMW_waitingRowIndex);
            }
        }

        // 한 줄의 Xbox 버튼과 키보드 키 표시. 스프라이트가 있으면 스프라이트, 없으면 글자, 키 입력 대기 중이면 "..."
        private void RefreshRow(MappingRow row, bool isWaiting)
        {
            Sprite padSprite = GetSprite(ControllerTypeDetector.ControllerType.Xbox, row.padPath);
            SetIconOrText(row.padIcon, row.padText, padSprite, GetPadButtonName(row.padPath));

            string keyPath = isWaiting ? null : KeyboardMapping.GetKey(row.padPath);
            Sprite keySprite = GetSprite(ControllerTypeDetector.ControllerType.Keyboard, keyPath);
            SetIconOrText(row.keyIcon, row.keyText, keySprite, isWaiting ? KMW_waitingKeyText : GetKeyName(keyPath));
        }

        private Sprite GetSprite(ControllerTypeDetector.ControllerType type, string path)
        {
            return KMW_buttonIcons != null && path != null ? KMW_buttonIcons.GetSprite(type, path) : null;
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
            if (KMW_menuButtons == null) return;

            for (int i = 0; i < KMW_menuButtons.Length; i++)
            {
                if (KMW_menuButtons[i].image != null) KMW_menuButtons[i].image.color = (i == KMW_highlightIndex) ? KMW_highlightColor : KMW_normalColor;
            }
        }

        private void SetGuide(string message)
        {
            if (KMW_guideText != null) KMW_guideText.text = message;
        }
    }
}
