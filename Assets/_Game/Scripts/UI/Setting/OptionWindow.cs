using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Setting
{
    // 옵션 창 (P_OptionWindow 프리팹)에 붙여서 사용. 타이틀과 연습 씬의 옵션 버튼이 같은 창을 엶
    // 옵션을 열면 화면 전체가 옵션 배경으로 바뀜 (뒤의 게임 화면, 설정창은 보이지 않음, 씬 이동은 아님)
    // 옵션 메뉴: 컨트롤러 설정, 키보드 매핑, 사운드 설정, 화면 설정, 그래픽 설정, 닫기
    // - 메뉴에서 고른 페이지가 열리면 메뉴는 숨겨지고 그 페이지만 보임. 페이지를 닫으면 메뉴로 돌아감
    //   - 컨트롤러 설정: 공격 버튼에 연결할 패드 버튼을 바꾸는 창 (KeyBindingWindow)
    //   - 키보드 매핑: 키보드 키를 Xbox 버튼에 연결하는 창 (KeyboardMappingWindow, 오른쪽에 열림)
    //   - 화면 설정, 그래픽 설정: OptionPage
    //   - 사운드 설정: 아직 없음 (줄의 Interactable을 꺼두면 고를 수 없음)
    // - 화면이 바뀔 때(옵션을 열 때, 페이지를 열고 닫을 때) 새 화면이 서서히 나타남
    // - 위/아래: 줄 이동 (잠긴 줄은 건너뜀), 선택 키 / 마우스 클릭: 실행
    // - 취소 키 / ESC: 옵션 창 닫기 (페이지가 열려 있으면 페이지만 닫힘)
    public class OptionWindow : MonoBehaviour
    {
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

        [Tooltip("옵션 메뉴 창 (페이지가 열리면 숨김)")]
        [SerializeField] private GameObject _menuWindow;

        [Header("메뉴 줄 (위에서 아래 순서)")]
        [SerializeField] private Button _controllerSettingButton;
        [SerializeField] private Button _keyboardMappingButton;
        [SerializeField] private Button _soundSettingButton;
        [SerializeField] private Button _displaySettingButton;
        [SerializeField] private Button _graphicsSettingButton;
        [SerializeField] private Button OW_closeButton;

        [Header("페이지")]
        [SerializeField] private KeyBindingWindow _keyBindingWindow;
        [SerializeField] private KeyboardMappingWindow _keyboardMappingWindow;
        [SerializeField] private OptionPage _displaySettingPage;
        [SerializeField] private OptionPage _graphicsSettingPage;

        [Tooltip("화면이 바뀔 때 새 화면이 나타나는 시간(초, 실제 시간 기준)")]
        [SerializeField] private float OW_fadeTime = 0.2f;

        private static readonly Color OW_highlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color OW_normalColor = new Color(1f, 1f, 1f, 0f);

        private Button[] OW_menuButtons;
        private int OW_highlightIndex;

        // 지금 보이는 화면 (메뉴 창 또는 열린 페이지)
        private GameObject _shownScreen;
        private Coroutine _fadeRoutine;
        private CanvasGroup _fadingGroup;

        // 창을 연 그 프레임에는 메뉴 입력을 처리하지 않음
        private int OW_ignoreInputFrame = -1;

        // 누르고 있는 동안 계속 실행되지 않도록 이전 프레임 선택 키를 기억
        private bool OW_lastSelect;

        void Awake()
        {
            if (OW_uiControl == null) OW_uiControl = FindAnyObjectByType<UIControl>(FindObjectsInactive.Include);

            if (_controllerSettingButton != null && _keyBindingWindow != null) _controllerSettingButton.onClick.AddListener(_keyBindingWindow.Open);
            if (_keyboardMappingButton != null && _keyboardMappingWindow != null) _keyboardMappingButton.onClick.AddListener(_keyboardMappingWindow.Open);
            if (_displaySettingButton != null && _displaySettingPage != null) _displaySettingButton.onClick.AddListener(_displaySettingPage.Open);
            if (_graphicsSettingButton != null && _graphicsSettingPage != null) _graphicsSettingButton.onClick.AddListener(_graphicsSettingPage.Open);
            if (OW_closeButton != null) OW_closeButton.onClick.AddListener(Close);

            List<Button> menuButtons = new List<Button>();
            foreach (Button button in new[] { _controllerSettingButton, _keyboardMappingButton, _soundSettingButton, _displaySettingButton, _graphicsSettingButton, OW_closeButton })
            {
                if (button != null) menuButtons.Add(button);
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

            ClosePages();
        }

        void OnEnable()
        {
            OW_openCount++;

            OW_highlightIndex = 0;
            if (OW_menuButtons.Length > 0 && !OW_menuButtons[0].IsInteractable()) Move(1);
            OW_ignoreInputFrame = Time.frameCount;
            RefreshHighlight();

            // 옵션 화면 전체가 서서히 나타남
            _shownScreen = null;
            ShowCurrentScreen();
            StartFade(gameObject);

            // 창을 연 키를 아직 누르고 있어도 바로 실행되지 않도록 현재 입력을 기억
            RememberInput();
        }

        void OnDisable()
        {
            OW_openCount--;
            OW_closedFrame = Time.frameCount;
            ClosePages();
            // 꺼지면 코루틴이 멈추므로 나타나는 중이던 화면은 다 나타난 상태로 둠
            if (_fadingGroup != null) _fadingGroup.alpha = 1f;
            _fadingGroup = null;
            _fadeRoutine = null;
        }

        // 옵션 버튼의 OnClick에 연결
        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            if (FindOpenPage() != null) return;
            gameObject.SetActive(false);
        }

        void Update()
        {
            // 페이지가 열리거나 닫혔으면 보이는 화면을 바꿈
            ShowCurrentScreen();

            if (OW_uiControl == null) return;

            // 페이지가 열려 있으면 그 페이지가 입력을 처리함 (닫힌 다음 프레임까지 포함)
            if (IsPageOpen() || Time.frameCount == OW_ignoreInputFrame)
            {
                RememberInput();
                return;
            }

            bool select = OW_uiControl.uiSelect;

            // 길게 눌러 연속 이동 중이면 맨 위/맨 아래에서 멈춤
            if (OW_uiControl.uiUpStep) MoveStep(-1, canWrap: !OW_uiControl.isRepeating);
            else if (OW_uiControl.uiDownStep) MoveStep(1, canWrap: !OW_uiControl.isRepeating);

            if (select && !OW_lastSelect)
            {
                Button button = OW_menuButtons[OW_highlightIndex];
                if (button.IsInteractable()) button.onClick.Invoke();
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

        // ===== 페이지 =====

        // 입력을 받는 페이지가 있는지 (닫힌 다음 프레임까지 포함)
        private bool IsPageOpen()
        {
            return KeyBindingWindow.IsOpen || KeyboardMappingWindow.KMW_IsOpen || OptionPage.OP_IsOpen;
        }

        // 지금 켜져 있는 페이지 (없으면 null)
        private GameObject FindOpenPage()
        {
            if (_keyBindingWindow != null && _keyBindingWindow.gameObject.activeSelf) return _keyBindingWindow.gameObject;
            if (_keyboardMappingWindow != null && _keyboardMappingWindow.gameObject.activeSelf) return _keyboardMappingWindow.gameObject;
            if (_displaySettingPage != null && _displaySettingPage.gameObject.activeSelf) return _displaySettingPage.gameObject;
            if (_graphicsSettingPage != null && _graphicsSettingPage.gameObject.activeSelf) return _graphicsSettingPage.gameObject;
            return null;
        }

        private void ClosePages()
        {
            if (_keyBindingWindow != null) _keyBindingWindow.gameObject.SetActive(false);
            if (_keyboardMappingWindow != null) _keyboardMappingWindow.gameObject.SetActive(false);
            if (_displaySettingPage != null) _displaySettingPage.gameObject.SetActive(false);
            if (_graphicsSettingPage != null) _graphicsSettingPage.gameObject.SetActive(false);
        }

        // 페이지가 열려 있으면 메뉴를 숨기고, 없으면 메뉴를 보임. 화면이 바뀌면 새 화면이 서서히 나타남
        private void ShowCurrentScreen()
        {
            GameObject page = FindOpenPage();
            GameObject screen = page != null ? page : _menuWindow;
            if (_menuWindow != null && _menuWindow.activeSelf != (page == null)) _menuWindow.SetActive(page == null);

            if (screen == _shownScreen) return;

            bool isFirst = _shownScreen == null;
            _shownScreen = screen;
            // 옵션을 처음 열 때는 옵션 화면 전체가 나타나므로 따로 하지 않음
            if (!isFirst && screen != null) StartFade(screen);
        }

        // ===== 화면 전환 =====

        private void StartFade(GameObject target)
        {
            // 나타나는 중이던 화면은 다 나타난 상태로 끝냄 (반투명으로 남지 않도록)
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
            if (_fadingGroup != null) _fadingGroup.alpha = 1f;
            _fadingGroup = null;
            if (!isActiveAndEnabled) return;

            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.AddComponent<CanvasGroup>();
            _fadingGroup = group;
            _fadeRoutine = StartCoroutine(FadeIn(group));
        }

        // 설정창이 열리면 게임 시간이 멈추므로 실제 시간 기준으로 나타나게 함
        private IEnumerator FadeIn(CanvasGroup group)
        {
            float time = 0f;
            group.alpha = 0f;
            while (time < OW_fadeTime)
            {
                time += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(time / OW_fadeTime);
                yield return null;
            }
            group.alpha = 1f;
            _fadeRoutine = null;
            _fadingGroup = null;
        }

        // ===== 메뉴 =====

        private void RememberInput()
        {
            if (OW_uiControl == null) return;

            OW_lastSelect = OW_uiControl.uiSelect;
        }

        // direction 방향으로 한 칸 이동 (canWrap이 false면 끝에서 반대쪽 끝으로 넘어가지 않고 멈춤, 잠긴 줄은 건너뜀)
        private void MoveStep(int direction, bool canWrap)
        {
            if (OW_menuButtons.Length == 0) return;

            if (!canWrap)
            {
                for (int i = OW_highlightIndex + direction; i >= 0 && i < OW_menuButtons.Length; i += direction)
                {
                    if (!OW_menuButtons[i].IsInteractable()) continue;

                    OW_highlightIndex = i;
                    RefreshHighlight();
                    return;
                }
                return;
            }

            Move(direction);
            RefreshHighlight();
        }

        // direction 방향으로 고를 수 있는 다음 줄을 찾아 이동 (끝에서 반대쪽 끝으로)
        private void Move(int direction)
        {
            for (int i = 0; i < OW_menuButtons.Length; i++)
            {
                OW_highlightIndex = (OW_highlightIndex + direction + OW_menuButtons.Length) % OW_menuButtons.Length;
                if (OW_menuButtons[OW_highlightIndex].IsInteractable()) return;
            }
        }

        private void SelectButton(int index)
        {
            if (IsPageOpen() || !OW_menuButtons[index].IsInteractable()) return;

            OW_highlightIndex = index;
            RefreshHighlight();
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
    }
}
