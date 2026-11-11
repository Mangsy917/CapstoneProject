using Game.Field;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Setting
{
    // 연습 설정의 맵 선택 줄(BTN_SelectMap)에 붙여서 사용
    // - 줄 오른쪽에 지금 고른 맵 이름을 보여줌
    // - 줄을 누르면(선택 키 / 클릭) 맵 선택 팝업이 열림
    //   팝업은 뒤 화면이 비치는 반투명 배경 위에 맵 스크린샷 카드를 나란히 보여줌
    // - 팝업에서 왼쪽/오른쪽: 카드 이동 (끝에서 누르면 반대쪽 끝으로), 마우스 올리기: 그 카드 선택
    //   선택 키 / 카드 클릭: 그 맵으로 바꾸고 닫음, 취소 키: 바꾸지 않고 닫음
    // - 팝업이 열려 있는 동안 뒤의 설정 메뉴는 입력을 받지 않음 (SettingPopup.IsAnyOpen)
    public class MapSelectSetting : MonoBehaviour
    {
        [SerializeField] private UIControl MSS_uiControl;
        [Tooltip("맵 버전을 가진 오브젝트 (비워두면 씬에서 찾음)")]
        [SerializeField] private PracticeMap _practiceMap;
        [Tooltip("지금 고른 맵 이름을 보여줄 글자")]
        [SerializeField] private TMP_Text _mapNameText;

        [Header("맵 선택 팝업")]
        [SerializeField] private GameObject _popup;
        [Tooltip("카드 버튼 (맵 버전 순서대로)")]
        [SerializeField] private Button[] _cardButtons;
        [SerializeField] private Image[] _cardThumbnails;
        [SerializeField] private TMP_Text[] _cardNameTexts;

        private static readonly Color selectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color notSelectColor = new Color(1f, 1f, 1f, 0f);

        private bool MSS_isOpen;
        private int MSS_selectedIndex;
        // 누르고 있는 동안 계속 넘어가지 않도록 이전 프레임 입력을 기억
        private bool MSS_prevLeft, MSS_prevRight, MSS_prevSelect;

        void Awake()
        {
            if (_practiceMap == null) _practiceMap = FindAnyObjectByType<PracticeMap>();
            if (_practiceMap != null) _practiceMap.MapChanged += OnMapChanged;

            // 줄을 누르면 팝업을 엶
            Button rowButton = GetComponent<Button>();
            if (rowButton != null) rowButton.onClick.AddListener(OpenPopup);

            // ESC 등으로 설정창이 통째로 닫히면 팝업도 닫음 (다시 열 때 팝업이 남아 있지 않도록)
            ControlReaderSwitch.SettingClosed += ClosePopup;

            if (_popup != null) _popup.SetActive(false);
            AddCardEvents();
            FillCards();
            RefreshMapName();
        }

        void OnDestroy()
        {
            if (_practiceMap != null) _practiceMap.MapChanged -= OnMapChanged;
            ControlReaderSwitch.SettingClosed -= ClosePopup;
            if (MSS_isOpen) SettingPopup.RemoveOpenPopup();
        }

        void Update()
        {
            if (!MSS_isOpen || MSS_uiControl == null) return;

            bool left = MSS_uiControl.uiLeft;
            bool right = MSS_uiControl.uiRight;
            bool select = MSS_uiControl.uiSelect;

            if (left && !MSS_prevLeft) MoveSelection(-1);
            else if (right && !MSS_prevRight) MoveSelection(1);

            if (select && !MSS_prevSelect) Confirm(MSS_selectedIndex);
            else if (MSS_uiControl.uiCancelDown)
            {
                // 같은 키로 설정창까지 닫히지 않도록 처리한 입력은 지움
                MSS_uiControl.uiCancelDown = false;
                ClosePopup();
            }

            MSS_prevLeft = left;
            MSS_prevRight = right;
            MSS_prevSelect = select;
        }

        public void OpenPopup()
        {
            if (MSS_isOpen || _popup == null) return;

            MSS_isOpen = true;
            SettingPopup.AddOpenPopup();
            _popup.SetActive(true);

            // 지금 쓰는 맵 카드를 먼저 선택
            MSS_selectedIndex = _practiceMap != null ? _practiceMap.MapIndex : 0;
            RefreshCards();

            // 팝업을 연 키를 아직 누르고 있어도 바로 실행되지 않도록 현재 입력을 기억
            if (MSS_uiControl != null)
            {
                MSS_prevLeft = MSS_uiControl.uiLeft;
                MSS_prevRight = MSS_uiControl.uiRight;
                MSS_prevSelect = MSS_uiControl.uiSelect;
            }
        }

        public void ClosePopup()
        {
            if (!MSS_isOpen) return;

            MSS_isOpen = false;
            SettingPopup.RemoveOpenPopup();
            if (_popup != null) _popup.SetActive(false);
        }

        private int GetCardCount()
        {
            int count = _cardButtons != null ? _cardButtons.Length : 0;
            if (_practiceMap != null) count = Mathf.Min(count, _practiceMap.MapCount);
            return count;
        }

        private void MoveSelection(int direction)
        {
            int count = GetCardCount();
            if (count == 0) return;

            MSS_selectedIndex = (MSS_selectedIndex + direction + count) % count;
            RefreshCards();
        }

        // 고른 맵으로 바꾸고 닫음
        private void Confirm(int index)
        {
            if (_practiceMap != null) _practiceMap.SetMap(index);
            ClosePopup();
        }

        // 카드에 마우스를 올리면 선택, 클릭하면 그 맵으로 바꿈
        private void AddCardEvents()
        {
            if (_cardButtons == null) return;

            for (int i = 0; i < _cardButtons.Length; i++)
            {
                if (_cardButtons[i] == null) continue;

                int index = i;
                _cardButtons[i].onClick.AddListener(() => Confirm(index));

                EventTrigger trigger = _cardButtons[i].gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = _cardButtons[i].gameObject.AddComponent<EventTrigger>();

                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ =>
                {
                    MSS_selectedIndex = index;
                    RefreshCards();
                });
                trigger.triggers.Add(entry);
            }
        }

        // 카드에 맵 스크린샷과 이름을 채움
        private void FillCards()
        {
            if (_practiceMap == null) return;

            for (int i = 0; i < GetCardCount(); i++)
            {
                PracticeMap.MapTheme theme = _practiceMap.GetTheme(i);
                if (_cardThumbnails != null && i < _cardThumbnails.Length && _cardThumbnails[i] != null)
                    _cardThumbnails[i].sprite = theme.Thumbnail;
                if (_cardNameTexts != null && i < _cardNameTexts.Length && _cardNameTexts[i] != null)
                    _cardNameTexts[i].text = theme.MapName;
            }
        }

        // 선택된 카드만 배경을 회색으로, 나머지는 투명
        private void RefreshCards()
        {
            if (_cardButtons == null) return;

            for (int i = 0; i < _cardButtons.Length; i++)
            {
                if (_cardButtons[i] == null) continue;

                bool isSelected = i == MSS_selectedIndex;
                if (_cardButtons[i].image != null) _cardButtons[i].image.color = isSelected ? selectColor : notSelectColor;
                SelectedFontSize.Apply(_cardButtons[i], isSelected: isSelected);
            }
        }

        private void OnMapChanged(int index)
        {
            RefreshMapName();
        }

        // 줄 오른쪽에 지금 맵 이름을 보여줌
        private void RefreshMapName()
        {
            if (_mapNameText == null || _practiceMap == null) return;

            PracticeMap.MapTheme theme = _practiceMap.GetTheme(_practiceMap.MapIndex);
            _mapNameText.text = theme != null ? theme.MapName : "";
        }
    }
}
