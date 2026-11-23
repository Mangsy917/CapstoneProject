using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI.Hub
{
    // 거점 메뉴 (거점 씬의 UI 관리 오브젝트에 붙여서 사용, 5. UI/UX '거점 화면')
    // 하단 탭 줄은 윈도우 작업 표시줄처럼 동작함
    // - 탭을 옮김 (좌우, LB·RB(Q·E), 마우스 올리기): 위 화면이 그 탭의 화면으로 바로 바뀜. 전환 연출·대사 없음
    // - 탭을 선택 (선택 키, 마우스 클릭): 탭 화면은 그대로 두고, 탭 줄만 내려가며 투명해지고 비활성화됨
    //   (Should Zoom Screen을 켜면 탭 화면이 전체 화면으로 확대됨)
    //   일반형 탭이면 일러스트 밑에 대사 자막 상자가 보임. 이제 하위 메뉴(보드형은 포스트잇·구석 버튼)를 고름
    // - 뒤로 가기 (취소 키, 뒤로 버튼, 마우스 우클릭): 탭 줄이 올라오며 선명해지고 다시 활성화됨
    // - ESC / 패드 Start(Menu·Options) 버튼: 어디서든 바로 설정 탭의 팝업(시스템 설정)을 엶
    // - 탭 줄이 활성인 동안 탭 화면의 하위 메뉴를 마우스로 누르면 그 탭을 선택한 것으로 봄 (그 하위 메뉴가 선택된 채로 확대)
    // - 팝업형 탭을 선택하면 화면을 확대하지 않고 팝업을 띄움
    // - 아직 제작되지 않은 메뉴(Is Built 꺼짐)를 고르면 '미완성된 시스템입니다' 팝업을 띄움
    // 팝업이나 옵션 창이 열려 있는 동안에는 입력을 받지 않음
    public class HubMenu : MonoBehaviour
    {
        private static readonly Color HM_highlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color HM_normalColor = new Color(1f, 1f, 1f, 0.15f);

        // 비워두면 씬에서 찾음
        [SerializeField] private UIControl HM_uiControl;

        [Tooltip("탭 줄의 왼쪽부터 순서대로")]
        [SerializeField] private HubTab[] _hubTabs;

        [Header("화면")]
        [Tooltip("탭 화면들이 들어 있는 영역 (앵커는 화면 전체로 늘려 둠)")]
        [SerializeField] private RectTransform _screenArea;

        [Tooltip("탭 화면이 화면 가장자리에서 들어가는 거리 (x 왼쪽, y 아래, z 오른쪽, w 위)")]
        [SerializeField] private Vector4 _smallScreenInset = new Vector4(160f, 250f, 160f, 130f);

        [Tooltip("켜면 탭을 선택했을 때 탭 화면이 전체 화면으로 확대됨. 끄면 화면은 그대로 두고 탭 줄만 내려가며 비활성화됨")]
        [SerializeField] private bool _shouldZoomScreen;

        [Tooltip("탭 줄 (탭 버튼과 한 줄 설명이 들어 있음)")]
        [SerializeField] private RectTransform _tabBar;

        [Tooltip("탭을 선택했을 때 탭 줄이 아래로 내려가는 거리")]
        [SerializeField] private float _tabBarHideDistance = 300f;

        [Tooltip("고른 탭을 설명하는 한 줄")]
        [SerializeField] private TMP_Text _descriptionText;

        [Tooltip("탭을 선택한 동안만 보이는 뒤로 가기 버튼")]
        [SerializeField] private Button HM_backButton;

        [SerializeField] private UnfinishedPopup _unfinishedPopup;

        [Tooltip("ESC / Start 버튼을 누르면 이 탭의 팝업을 엶 (시스템 설정 탭)")]
        [SerializeField] private HubTab _settingTab;

        [Tooltip("탭 화면이 확대·축소되는 시간(초, 실제 시간 기준)")]
        [SerializeField] private float _transitionTime = 0.25f;

        private CanvasGroup _tabBarGroup;
        private Vector2 _tabBarShownPosition;

        private int _tabIndex;
        private int _itemIndex;
        private bool _isInsideTab;

        // 0: 탭을 고르는 중 (작은 화면, 탭 줄 보임), 1: 탭을 선택함 (전체 화면, 탭 줄 숨김)
        private float _transitionProgress;
        private Coroutine _transitionRoutine;

        // 누르고 있는 동안 계속 실행되지 않도록 이전 프레임 선택 키를 기억
        private bool HM_lastSelect;

        // 탭을 선택한 그 프레임에는 하위 메뉴 입력을 처리하지 않음
        private int HM_ignoreInputFrame = -1;

        void Awake()
        {
            if (HM_uiControl == null) HM_uiControl = FindAnyObjectByType<UIControl>(FindObjectsInactive.Include);

            if (_tabBar != null)
            {
                _tabBarShownPosition = _tabBar.anchoredPosition;
                _tabBarGroup = _tabBar.GetComponent<CanvasGroup>();
                if (_tabBarGroup == null) _tabBarGroup = _tabBar.gameObject.AddComponent<CanvasGroup>();
            }

            if (HM_backButton != null) HM_backButton.onClick.AddListener(Back);

            for (int i = 0; i < _hubTabs.Length; i++)
            {
                ConnectTab(i);
            }
        }

        void Start()
        {
            ShowTab(0);
            ApplyTransition(0f);
            if (HM_backButton != null) HM_backButton.gameObject.SetActive(false);
        }

        void Update()
        {
            if (HM_uiControl == null) return;

            bool select = HM_uiControl.uiSelect;

            // 팝업·옵션 창이 열려 있으면 입력만 기억하고 메뉴는 움직이지 않음
            if (IsInputBlocked() || Time.frameCount == HM_ignoreInputFrame)
            {
                HM_lastSelect = select;
                return;
            }

            bool isSelectPressed = select && !HM_lastSelect;
            HM_lastSelect = select;

            // ESC / Start: 탭 줄, 탭 안, 상점 창 어디서든 바로 설정을 엶
            if (HM_uiControl.uiClose)
            {
                HM_uiControl.uiClose = false;
                OpenSetting();
                return;
            }

            // 상점 창이 열려 있으면 그 창이 입력을 처리함
            if (ShopWindow.SW_IsOpen) return;

            if (!_isInsideTab)
            {
                if (HM_uiControl.uiLeftStep || HM_uiControl.uiLeftPG) MoveTab(-1);
                else if (HM_uiControl.uiRightStep || HM_uiControl.uiRightPG) MoveTab(1);

                if (isSelectPressed) SelectTab(startItemIndex: 0);
                return;
            }

            if (HM_uiControl.uiCancelDown || IsRightClicked())
            {
                HM_uiControl.uiCancelDown = false;
                Back();
                return;
            }

            // 일반형은 세로 목록, 보드형은 포스트잇이 가로로 놓여 있어 위·왼쪽은 이전, 아래·오른쪽은 다음
            bool canWrap = !HM_uiControl.isRepeating;
            if (HM_uiControl.uiUpStep || HM_uiControl.uiLeftStep) MoveItem(-1, canWrap);
            else if (HM_uiControl.uiDownStep || HM_uiControl.uiRightStep) MoveItem(1, canWrap);

            if (isSelectPressed) RunItem(_itemIndex);
        }

        // 거점에서 다른 씬(연습장 등)으로 이동. 버튼의 OnClick에 씬 이름과 함께 연결
        public void LoadScene(string sceneName)
        {
            LoadingScene.LoadScene(sceneName);
        }

        // ===== 연결 =====

        private void ConnectTab(int tabIndex)
        {
            HubTab tab = _hubTabs[tabIndex];
            if (tab == null) return;

            if (tab.TabButton != null)
            {
                TMP_Text label = tab.TabButton.GetComponentInChildren<TMP_Text>(includeInactive: true);
                if (label != null && !string.IsNullOrEmpty(tab.TabName)) label.text = tab.TabName;

                AddPointerEvent(tab.TabButton.gameObject, EventTriggerType.PointerEnter, () =>
                {
                    if (_isInsideTab || IsInputBlocked()) return;
                    ShowTab(tabIndex);
                });
                tab.TabButton.onClick.AddListener(() =>
                {
                    if (_isInsideTab || IsInputBlocked()) return;
                    ShowTab(tabIndex);
                    SelectTab(startItemIndex: 0);
                });
            }

            HubMenuItem[] items = tab.Items;
            if (items == null) return;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;

                int itemIndex = i;
                AddPointerEvent(items[i].gameObject, EventTriggerType.PointerEnter, () =>
                {
                    if (!_isInsideTab || _tabIndex != tabIndex || IsInputBlocked()) return;
                    SelectItem(itemIndex);
                });
                items[i].ItemButton.onClick.AddListener(() => ClickItem(tabIndex, itemIndex));
            }
        }

        private static void AddPointerEvent(GameObject target, EventTriggerType eventType, System.Action action)
        {
            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null) trigger = target.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        // ===== 탭 =====

        private void MoveTab(int direction)
        {
            if (_hubTabs.Length == 0) return;
            ShowTab((_tabIndex + direction + _hubTabs.Length) % _hubTabs.Length);
        }

        // 위 화면을 그 탭의 화면으로 바꿈 (선택하지는 않음)
        private void ShowTab(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= _hubTabs.Length) return;

            _tabIndex = tabIndex;

            for (int i = 0; i < _hubTabs.Length; i++)
            {
                HubTab tab = _hubTabs[i];
                if (tab == null) continue;

                bool isCurrent = i == tabIndex;
                tab.gameObject.SetActive(isCurrent);
                tab.ShowDialogue(false);
                SetItemsSelected(tab, selectedIndex: -1);

                if (tab.TabButton != null)
                {
                    if (tab.TabButton.image != null) tab.TabButton.image.color = isCurrent ? HM_highlightColor : HM_normalColor;
                    SelectedFontSize.Apply(tab.TabButton, isSelected: isCurrent);
                }
            }

            if (_descriptionText != null) _descriptionText.text = _hubTabs[tabIndex] != null ? _hubTabs[tabIndex].Description : "";
        }

        private void SelectTab(int startItemIndex)
        {
            HubTab tab = _hubTabs[_tabIndex];
            if (tab == null) return;

            // 팝업형: 화면을 확대하지 않고 팝업만 띄움
            if (tab.Type == HubTab.TabType.Popup)
            {
                if (tab.IsPopupBuilt) tab.OpenPopup();
                else OpenUnfinishedPopup();
                return;
            }

            _isInsideTab = true;
            HM_ignoreInputFrame = Time.frameCount;

            int itemCount = tab.Items != null ? tab.Items.Length : 0;
            _itemIndex = itemCount > 0 ? Mathf.Clamp(startItemIndex, 0, itemCount - 1) : -1;
            SetItemsSelected(tab, _itemIndex);
            tab.ShowDialogue(true);

            if (HM_backButton != null) HM_backButton.gameObject.SetActive(true);
            StartTransition(1f);
        }

        // 탭 선택을 풀고 탭 줄로 돌아감
        private void Back()
        {
            if (!_isInsideTab) return;

            _isInsideTab = false;

            HubTab tab = _hubTabs[_tabIndex];
            if (tab != null)
            {
                tab.ShowDialogue(false);
                SetItemsSelected(tab, selectedIndex: -1);
            }

            if (HM_backButton != null) HM_backButton.gameObject.SetActive(false);
            StartTransition(0f);
        }

        // ===== 하위 메뉴 =====

        private void MoveItem(int direction, bool canWrap)
        {
            HubMenuItem[] items = _hubTabs[_tabIndex].Items;
            if (items == null || items.Length == 0) return;

            int next = _itemIndex + direction;
            if (!canWrap && (next < 0 || next >= items.Length)) return;

            SelectItem((next + items.Length) % items.Length);
        }

        private void SelectItem(int itemIndex)
        {
            _itemIndex = itemIndex;
            SetItemsSelected(_hubTabs[_tabIndex], _itemIndex);
        }

        private void ClickItem(int tabIndex, int itemIndex)
        {
            if (IsInputBlocked() || tabIndex != _tabIndex) return;

            // 탭 줄이 활성인 동안 누르면 그 탭을 선택한 것으로 봄
            if (!_isInsideTab)
            {
                SelectTab(startItemIndex: itemIndex);
                return;
            }

            SelectItem(itemIndex);
            RunItem(itemIndex);
        }

        private void RunItem(int itemIndex)
        {
            HubMenuItem[] items = _hubTabs[_tabIndex].Items;
            if (items == null || itemIndex < 0 || itemIndex >= items.Length || items[itemIndex] == null) return;

            HubMenuItem item = items[itemIndex];
            if (item.IsBuilt) item.Run();
            else OpenUnfinishedPopup();
        }

        private static void SetItemsSelected(HubTab tab, int selectedIndex)
        {
            HubMenuItem[] items = tab.Items;
            if (items == null) return;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null) items[i].SetSelected(i == selectedIndex);
            }
        }

        private void OpenUnfinishedPopup()
        {
            if (_unfinishedPopup != null) _unfinishedPopup.Open();
        }

        private void OpenSetting()
        {
            if (_settingTab == null) return;

            if (_settingTab.IsPopupBuilt) _settingTab.OpenPopup();
            else OpenUnfinishedPopup();
        }

        // ===== 입력 =====

        private static bool IsInputBlocked()
        {
            return SettingPopup.IsAnyOpen;
        }

        private static bool IsRightClicked()
        {
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        }

        // ===== 확대·축소 =====

        private void StartTransition(float target)
        {
            if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
            _transitionRoutine = StartCoroutine(TransitionRoutine(target));
        }

        private IEnumerator TransitionRoutine(float target)
        {
            // 탭 줄은 움직이기 시작할 때 바로 막고, 다 올라온 뒤에 다시 누를 수 있게 함
            if (_tabBarGroup != null)
            {
                _tabBarGroup.interactable = false;
                _tabBarGroup.blocksRaycasts = false;
            }

            float speed = _transitionTime > 0f ? 1f / _transitionTime : float.MaxValue;
            while (!Mathf.Approximately(_transitionProgress, target))
            {
                ApplyTransition(Mathf.MoveTowards(_transitionProgress, target, speed * Time.unscaledDeltaTime));
                yield return null;
            }

            ApplyTransition(target);
            _transitionRoutine = null;
        }

        private void ApplyTransition(float progress)
        {
            _transitionProgress = progress;
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            if (_screenArea != null)
            {
                float zoom = _shouldZoomScreen ? eased : 0f;
                _screenArea.offsetMin = Vector2.Lerp(new Vector2(_smallScreenInset.x, _smallScreenInset.y), Vector2.zero, zoom);
                _screenArea.offsetMax = Vector2.Lerp(new Vector2(-_smallScreenInset.z, -_smallScreenInset.w), Vector2.zero, zoom);
            }

            if (_tabBar != null) _tabBar.anchoredPosition = _tabBarShownPosition + Vector2.down * (_tabBarHideDistance * eased);

            if (_tabBarGroup != null)
            {
                _tabBarGroup.alpha = 1f - eased;
                bool isTabBarActive = !_isInsideTab && progress <= 0f;
                _tabBarGroup.interactable = isTabBarActive;
                _tabBarGroup.blocksRaycasts = isTabBarActive;
            }
        }
    }
}
