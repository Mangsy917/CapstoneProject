using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.UI.Hub
{
    // 메인화면(거점) 메뉴 (거점 씬의 UI 관리 오브젝트에 붙여서 사용, 5. UI/UX '메인화면 (거점)')
    // 모던 바 3D 배경 위의 2D 오브젝트(HubObject)와 하단 1depth 메뉴 바가 같은 선택 상태를 공유함
    // - 1depth 항목을 옮김 (좌우, LB·RB(Q·E), 메뉴·오브젝트에 마우스 올리기): 그 항목의 오브젝트가 외곽 빛으로 강조됨
    // - 선택 (선택 키, 메뉴·오브젝트 클릭): 오브젝트 쪽으로 확대되면서 검은 화면 -> 2depth 메뉴 (전체 화면)
    //   오브젝트가 없는 항목은 확대 없이 검은 화면만 거침
    // - 일반형 2depth: 상단 탭을 LB·RB(Q·E)나 클릭으로 옮기면 아래 3depth 내용이 바로 바뀜 (방향키는 3depth 내용이 씀)
    // - 2depth에서 뒤로 가기 (취소 키, 마우스 우클릭): 페이드로 메인화면에 돌아옴
    // - 메인화면에서 뒤로 가기: 확인 팝업 -> 페이드로 타이틀에 돌아감
    // - 팝업형 항목을 선택하면 확대·페이드 없이 팝업을 띄움
    // - ESC / 패드 Start(Menu·Options) 버튼: 어디서든 바로 설정 항목의 팝업(시스템 설정)을 엶
    // - 아직 제작되지 않은 메뉴(Is Built 꺼짐)를 고르면 '미완성된 시스템입니다' 팝업을 띄움
    // 팝업·옵션 창이 열려 있거나 화면이 전환되는 동안에는 입력을 받지 않음
    public class HubMenu : MonoBehaviour
    {
        private static readonly Color HM_highlightColor = new Color(0.12f, 0.55f, 0.52f, 1f);
        private static readonly Color HM_normalColor = new Color(1f, 1f, 1f, 0.06f);

        // 비워두면 씬에서 찾음
        [SerializeField] private UIControl HM_uiControl;

        [Tooltip("1depth 메뉴 바의 왼쪽부터 순서대로")]
        [SerializeField] private HubTab[] _hubTabs;

        [Header("화면")]
        [Tooltip("2depth 메뉴 화면들이 들어 있는 영역 (메인화면에서는 꺼짐)")]
        [SerializeField] private RectTransform _screenArea;

        [Tooltip("1depth 메뉴 바 (항목 버튼과 한 줄 설명, 2depth에서는 꺼짐)")]
        [SerializeField] private RectTransform _tabBar;

        [Tooltip("고른 항목을 설명하는 한 줄")]
        [SerializeField] private TMP_Text _descriptionText;

        [SerializeField] private UnfinishedPopup _unfinishedPopup;

        [Tooltip("ESC / Start 버튼을 누르면 이 항목의 팝업을 엶 (시스템 설정)")]
        [SerializeField] private HubTab _settingTab;

        [Tooltip("메인화면에서 뒤로 가기를 누르면 띄울 확인 팝업. 예 버튼에 ReturnToTitle을 연결")]
        [SerializeField] private SettingPopup _titlePopup;

        [Tooltip("타이틀 씬 이름")]
        [SerializeField] private string _titleScene = "Title";

        [Header("확대·페이드")]
        [SerializeField] private ScreenFade HM_screenFade;

        [Tooltip("메인화면을 비추는 카메라")]
        [SerializeField] private Camera _hubCamera;

        [Tooltip("선택했을 때 확대되며 검은 화면이 되는 시간(초, 실제 시간 기준)")]
        [SerializeField] private float _zoomTime = 0.6f;

        [Tooltip("뒤로 갈 때 검은 화면이 되는 시간(초, 실제 시간 기준)")]
        [SerializeField] private float _backFadeTime = 0.25f;

        [Tooltip("확대가 끝났을 때 카메라와 오브젝트 사이의 거리")]
        [SerializeField] private float _zoomDistance = 2.5f;

        private Vector3 _cameraHomePosition;
        private Coroutine _zoomRoutine;

        private int _tabIndex;
        private int _itemIndex;

        // 2depth 메뉴에 들어와 있는지
        private bool _isInsideTab;

        // 누르고 있는 동안 계속 실행되지 않도록 이전 프레임 선택 키를 기억
        private bool HM_lastSelect;

        void Awake()
        {
            if (HM_uiControl == null) HM_uiControl = FindAnyObjectByType<UIControl>(FindObjectsInactive.Include);

            if (HM_screenFade == null) HM_screenFade = FindAnyObjectByType<ScreenFade>(FindObjectsInactive.Include);
            if (_hubCamera == null) _hubCamera = Camera.main;
            if (_hubCamera != null) _cameraHomePosition = _hubCamera.transform.position;

            for (int i = 0; i < _hubTabs.Length; i++)
            {
                ConnectTab(i);
            }
        }

        void Start()
        {
            ShowTab(0);
            ShowMainScreen();
        }

        void Update()
        {
            if (HM_uiControl == null) return;

            bool select = HM_uiControl.uiSelect;

            // 팝업·옵션 창이 열려 있거나 화면이 전환 중이면 입력만 기억하고 메뉴는 움직이지 않음
            if (IsInputBlocked())
            {
                HM_lastSelect = select;
                return;
            }

            bool isSelectPressed = select && !HM_lastSelect;
            HM_lastSelect = select;

            // ESC / Start: 메인화면, 2depth 어디서든 바로 설정을 엶
            if (HM_uiControl.uiClose)
            {
                HM_uiControl.uiClose = false;
                OpenSetting();
                return;
            }

            if (HM_uiControl.uiCancelDown || IsRightClicked())
            {
                // 같은 키로 열린 팝업까지 닫히지 않도록 처리한 입력은 지움
                HM_uiControl.uiCancelDown = false;
                Back();
                return;
            }

            if (!_isInsideTab)
            {
                if (HM_uiControl.uiLeftStep || HM_uiControl.uiLeftPG) MoveTab(-1);
                else if (HM_uiControl.uiRightStep || HM_uiControl.uiRightPG) MoveTab(1);

                if (isSelectPressed) SelectTab();
                return;
            }

            // 일반형: LB·RB로 상단 탭만 옮김. 방향키와 선택 키는 3depth 내용(상점 등)이 직접 처리함
            if (_hubTabs[_tabIndex].Type == HubTab.TabType.Normal)
            {
                if (HM_uiControl.uiLeftPG) MoveTopTab(-1);
                else if (HM_uiControl.uiRightPG) MoveTopTab(1);
                return;
            }

            // 고르고 있던 항목이 사라졌으면(고정을 해제한 칼 등) 첫 항목으로 옮김
            if (!IsItemActive(_itemIndex)) SelectFirstItem();

            // 보드형: 누른 방향에 있는 항목으로 옮김
            if (HM_uiControl.uiUpStep) MoveBoardItem(Vector2.up);
            else if (HM_uiControl.uiLeftStep) MoveBoardItem(Vector2.left);
            else if (HM_uiControl.uiDownStep) MoveBoardItem(Vector2.down);
            else if (HM_uiControl.uiRightStep) MoveBoardItem(Vector2.right);

            if (isSelectPressed) RunItem(_itemIndex);
        }

        // 거점에서 다른 씬(연습장 등)으로 이동. 버튼의 OnClick에 씬 이름과 함께 연결
        public void LoadScene(string sceneName)
        {
            LoadingScene.LoadScene(sceneName);
        }

        // 페이드로 타이틀에 돌아감. 확인 팝업의 예 버튼 OnClick에 연결
        public void ReturnToTitle()
        {
            if (HM_screenFade != null && HM_screenFade.SF_IsPlaying) return;

            if (_titlePopup != null) _titlePopup.gameObject.SetActive(false);
            PlayFade(() => LoadScene(_titleScene), _backFadeTime);
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

                AddPointerEvent(tab.TabButton.gameObject, EventTriggerType.PointerEnter, () => HoverTab(tabIndex));
                tab.TabButton.onClick.AddListener(() => ClickTab(tabIndex));
            }

            if (tab.LinkedObject != null)
            {
                tab.LinkedObject.HoverStarted += () => HoverTab(tabIndex);
                tab.LinkedObject.LeftClicked += () => ClickTab(tabIndex);
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

                    // 상단 탭은 지나가기만 해도 내용이 바뀌지 않도록 클릭으로만 고름
                    if (_hubTabs[tabIndex].Type == HubTab.TabType.Normal) return;
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

        // ===== 1depth 메뉴 =====

        private void HoverTab(int tabIndex)
        {
            if (_isInsideTab || IsInputBlocked()) return;
            ShowTab(tabIndex);
        }

        private void ClickTab(int tabIndex)
        {
            if (_isInsideTab || IsInputBlocked()) return;
            ShowTab(tabIndex);
            SelectTab();
        }

        private void MoveTab(int direction)
        {
            if (_hubTabs.Length == 0) return;
            ShowTab((_tabIndex + direction + _hubTabs.Length) % _hubTabs.Length);
        }

        // 1depth 항목을 고름 (메뉴 바와 오브젝트를 강조, 선택하지는 않음)
        private void ShowTab(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= _hubTabs.Length) return;

            _tabIndex = tabIndex;

            for (int i = 0; i < _hubTabs.Length; i++)
            {
                HubTab tab = _hubTabs[i];
                if (tab == null) continue;

                bool isCurrent = i == tabIndex;
                if (tab.LinkedObject != null) tab.LinkedObject.SetHighlighted(isCurrent);

                if (tab.TabButton != null)
                {
                    if (tab.TabButton.image != null) tab.TabButton.image.color = isCurrent ? HM_highlightColor : HM_normalColor;
                    SelectedFontSize.Apply(tab.TabButton, isSelected: isCurrent);
                }
            }

            if (_descriptionText != null) _descriptionText.text = _hubTabs[tabIndex] != null ? _hubTabs[tabIndex].Description : "";
        }

        private void SelectTab()
        {
            HubTab tab = _hubTabs[_tabIndex];
            if (tab == null) return;

            // 팝업형: 확대·페이드 없이 팝업만 띄움
            if (tab.Type == HubTab.TabType.Popup)
            {
                if (tab.IsPopupBuilt) tab.OpenPopup();
                else OpenUnfinishedPopup();
                return;
            }

            if (tab.LinkedObject != null) StartZoom(tab.LinkedObject.ZoomPosition);
            PlayFade(ShowTabScreen, _zoomTime);
        }

        private void Back()
        {
            if (_isInsideTab) PlayFade(ShowMainScreen, _backFadeTime);
            else if (_titlePopup != null) _titlePopup.gameObject.SetActive(true);
        }

        // ===== 화면 전환 =====

        private void ShowTabScreen()
        {
            ResetZoom();
            _isInsideTab = true;

            for (int i = 0; i < _hubTabs.Length; i++)
            {
                if (_hubTabs[i] != null) _hubTabs[i].gameObject.SetActive(i == _tabIndex);
            }
            // 화면이 켜지기 전에 첫 항목을 골라 이전에 보던 3depth 내용이 잠깐 켜지지 않게 함
            HubTab tab = _hubTabs[_tabIndex];
            int itemCount = tab.Items != null ? tab.Items.Length : 0;
            _itemIndex = itemCount > 0 ? 0 : -1;
            SetItemsSelected(tab, _itemIndex);
            tab.ShowDialogue(true);
            SetMainScreenActive(false);
        }

        private void ShowMainScreen()
        {
            ResetZoom();
            _isInsideTab = false;

            SetMainScreenActive(true);
        }

        private void SetMainScreenActive(bool isActive)
        {
            if (_tabBar != null) _tabBar.gameObject.SetActive(isActive);
            if (_screenArea != null) _screenArea.gameObject.SetActive(!isActive);
        }

        private void PlayFade(System.Action onBlack, float fadeInTime)
        {
            if (HM_screenFade != null) HM_screenFade.Play(onBlack, fadeInTime);
            else onBlack();
        }

        // 카메라가 오브젝트 쪽으로 다가감 (검은 화면이 되면 ResetZoom으로 되돌림)
        private void StartZoom(Vector3 targetPosition)
        {
            if (_hubCamera == null || HM_screenFade == null) return;

            Vector3 endPosition = targetPosition - _hubCamera.transform.forward * _zoomDistance;
            _zoomRoutine = StartCoroutine(ZoomRoutine(endPosition));
        }

        private void ResetZoom()
        {
            if (_zoomRoutine != null) StopCoroutine(_zoomRoutine);
            _zoomRoutine = null;

            if (_hubCamera != null) _hubCamera.transform.position = _cameraHomePosition;
        }

        private IEnumerator ZoomRoutine(Vector3 endPosition)
        {
            float elapsedTime = 0f;
            while (elapsedTime < _zoomTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, elapsedTime / _zoomTime);
                _hubCamera.transform.position = Vector3.Lerp(_cameraHomePosition, endPosition, progress);
                yield return null;
            }

            _zoomRoutine = null;
        }

        // ===== 2depth 메뉴 =====

        // 상단 탭을 옮김 (양 끝에서 더 가면 반대쪽 끝으로)
        private void MoveTopTab(int direction)
        {
            HubMenuItem[] items = _hubTabs[_tabIndex].Items;
            if (items == null || items.Length == 0) return;

            SelectItem((_itemIndex + direction + items.Length) % items.Length);
        }

        // 누른 방향에 있는 항목 중 가장 가까운 것을 고름 (방향에서 벗어날수록 멀리 있는 것으로 봄)
        private void MoveBoardItem(Vector2 screenDirection)
        {
            if (!IsItemActive(_itemIndex)) return;

            HubMenuItem[] items = _hubTabs[_tabIndex].Items;
            Transform current = items[_itemIndex].transform;
            int bestIndex = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < items.Length; i++)
            {
                if (i == _itemIndex || !IsItemActive(i)) continue;

                // 캔버스 크기와 상관없도록 화면 영역 기준 위치로 비교
                Vector2 offset = _screenArea.InverseTransformPoint(items[i].transform.position) - _screenArea.InverseTransformPoint(current.position);
                float forward = Vector2.Dot(offset, screenDirection);
                if (forward <= 1f) continue;

                float sideways = (offset - screenDirection * forward).magnitude;
                float score = forward + sideways * 2f;
                if (score >= bestScore) continue;

                bestScore = score;
                bestIndex = i;
            }

            if (bestIndex >= 0) SelectItem(bestIndex);
        }

        private bool IsItemActive(int itemIndex)
        {
            HubMenuItem[] items = _hubTabs[_tabIndex].Items;
            return items != null && itemIndex >= 0 && itemIndex < items.Length
                && items[itemIndex] != null && items[itemIndex].gameObject.activeInHierarchy;
        }

        private void SelectFirstItem()
        {
            HubMenuItem[] items = _hubTabs[_tabIndex].Items;
            if (items == null) return;

            for (int i = 0; i < items.Length; i++)
            {
                if (!IsItemActive(i)) continue;

                SelectItem(i);
                return;
            }
        }

        private void SelectItem(int itemIndex)
        {
            _itemIndex = itemIndex;
            SetItemsSelected(_hubTabs[_tabIndex], _itemIndex);
        }

        private void ClickItem(int tabIndex, int itemIndex)
        {
            if (!_isInsideTab || IsInputBlocked() || tabIndex != _tabIndex) return;

            SelectItem(itemIndex);
            if (_hubTabs[tabIndex].Type != HubTab.TabType.Normal) RunItem(itemIndex);
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

        private bool IsInputBlocked()
        {
            return SettingPopup.IsAnyOpen || (HM_screenFade != null && HM_screenFade.SF_IsPlaying);
        }

        private static bool IsRightClicked()
        {
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        }
    }
}
