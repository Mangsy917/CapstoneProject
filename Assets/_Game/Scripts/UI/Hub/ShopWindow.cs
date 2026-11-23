using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI.Hub
{
    // 상점 구매 창 (보조 아이템 구매, 무기 모듈 구매가 같은 스크립트를 씀)
    // 거점 상점 탭의 하위 메뉴(On Select)에서 Open으로 엶
    // - 왼쪽: 품목 칸 (Slot Template을 품목 수만큼 복사), 오른쪽: 고른 품목의 아이콘·이름·설명·가격·보유 수와 구매 버튼
    // - 방향키: 품목 이동, 선택 키 / 구매 버튼: 구매 확인 팝업, 취소 키 / 마우스 우클릭 / 닫기 버튼: 창 닫기
    // - 마우스: 칸에 올리거나 누르면 그 품목이 선택됨
    // - 크레딧이 모자라면 사지 않고 안내 문구를 잠깐 보여 줌
    // 팝업·옵션 창이 열려 있는 동안에는 입력을 받지 않음
    public class ShopWindow : MonoBehaviour
    {
        // 창이 열려 있으면 거점 메뉴는 입력을 받지 않음 (닫힌 다음 프레임까지 열린 것으로 봄)
        public static bool SW_IsOpen => SW_openCount > 0 || Time.frameCount - SW_closedFrame <= 1;
        private static int SW_openCount = 0;
        private static int SW_closedFrame = int.MinValue / 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOpenCount()
        {
            SW_openCount = 0;
            SW_closedFrame = int.MinValue / 2;
        }

        // 비워두면 씬에서 찾음
        [SerializeField] private UIControl SW_uiControl;
        [SerializeField] private HubInventory _inventory;

        [Tooltip("파는 품목 (칸 순서대로)")]
        [SerializeField] private ShopItemData[] _shopItems;

        [Header("품목 칸")]
        [Tooltip("복사해서 쓸 칸 (꺼 둔 상태로 둠)")]
        [SerializeField] private ShopSlot _slotTemplate;
        [Tooltip("칸이 들어갈 곳 (Grid Layout Group)")]
        [SerializeField] private RectTransform _slotGrid;
        [Tooltip("한 줄의 칸 수 (위/아래 이동에 사용)")]
        [Min(1)]
        [SerializeField] private int _columnCount = 3;

        [Header("상세")]
        [SerializeField] private Image _detailIcon;
        [SerializeField] private TMP_Text _detailName;
        [SerializeField] private TMP_Text _detailDescription;
        [SerializeField] private TMP_Text _detailPrice;
        [SerializeField] private TMP_Text _detailOwned;
        [Tooltip("구매 결과 안내 (크레딧 부족 등)")]
        [SerializeField] private TMP_Text _messageText;
        [Tooltip("안내 문구를 보여 주는 시간(초)")]
        [SerializeField] private float _messageTime = 1.5f;

        [Header("버튼")]
        [SerializeField] private Button _buyButton;
        [SerializeField] private Button SW_closeButton;

        [Header("구매 확인 팝업")]
        [Tooltip("예 / 아니오 팝업. 예 버튼 OnClick에 ConfirmBuy, 아니오 버튼에 팝업 끄기를 연결")]
        [SerializeField] private SettingPopup _confirmPopup;
        [SerializeField] private TMP_Text _confirmMessage;

        private readonly List<ShopSlot> _slots = new List<ShopSlot>();
        private int _shopSelectedIndex;
        private Coroutine _messageRoutine;

        // 창을 연 그 프레임에는 입력을 처리하지 않음
        private int SW_ignoreInputFrame = -1;

        // 누르고 있는 동안 계속 실행되지 않도록 이전 프레임 선택 키를 기억
        private bool SW_lastSelect;

        void Awake()
        {
            if (SW_uiControl == null) SW_uiControl = FindAnyObjectByType<UIControl>(FindObjectsInactive.Include);
            if (_inventory == null) _inventory = FindAnyObjectByType<HubInventory>(FindObjectsInactive.Include);

            if (_buyButton != null) _buyButton.onClick.AddListener(RequestBuy);
            if (SW_closeButton != null) SW_closeButton.onClick.AddListener(Close);
            if (_confirmPopup != null) _confirmPopup.gameObject.SetActive(false);

            CreateSlots();
        }

        void OnEnable()
        {
            SW_openCount++;
            SW_ignoreInputFrame = Time.frameCount;
            if (SW_uiControl != null) SW_lastSelect = SW_uiControl.uiSelect;

            if (_inventory != null) _inventory.InventoryChanged += Refresh;

            _shopSelectedIndex = 0;
            ShowMessage("");
            Refresh();
        }

        void OnDisable()
        {
            SW_openCount--;
            SW_closedFrame = Time.frameCount;

            if (_inventory != null) _inventory.InventoryChanged -= Refresh;
            if (_confirmPopup != null) _confirmPopup.gameObject.SetActive(false);
            _messageRoutine = null;
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (SW_uiControl == null) return;

            bool select = SW_uiControl.uiSelect;

            if (SettingPopup.IsAnyOpen || Time.frameCount == SW_ignoreInputFrame)
            {
                SW_lastSelect = select;
                return;
            }

            bool isSelectPressed = select && !SW_lastSelect;
            SW_lastSelect = select;

            if (SW_uiControl.uiCancelDown || IsRightClicked())
            {
                SW_uiControl.uiCancelDown = false;
                Close();
                return;
            }

            if (SW_uiControl.uiLeftStep) MoveSelection(-1);
            else if (SW_uiControl.uiRightStep) MoveSelection(1);
            else if (SW_uiControl.uiUpStep) MoveSelection(-_columnCount);
            else if (SW_uiControl.uiDownStep) MoveSelection(_columnCount);

            if (isSelectPressed) RequestBuy();
        }

        // 구매 확인 팝업 > 예
        public void ConfirmBuy()
        {
            if (_confirmPopup != null) _confirmPopup.gameObject.SetActive(false);

            ShopItemData item = GetSelectedItem();
            if (item == null || _inventory == null) return;

            ShowMessage(_inventory.TryBuy(item) ? "구매했습니다" : "크레딧이 부족합니다");
        }

        // ===== 칸 =====

        private void CreateSlots()
        {
            if (_slotTemplate == null || _slotGrid == null || _shopItems == null) return;

            _slotTemplate.gameObject.SetActive(false);

            for (int i = 0; i < _shopItems.Length; i++)
            {
                ShopSlot slot = Instantiate(_slotTemplate, _slotGrid);
                slot.name = $"Slot{i + 1}";
                slot.gameObject.SetActive(true);
                _slots.Add(slot);

                int index = i;
                AddPointerEnter(slot.gameObject, () =>
                {
                    if (SettingPopup.IsAnyOpen) return;
                    SelectSlot(index);
                });
                slot.SlotButton.onClick.AddListener(() =>
                {
                    if (SettingPopup.IsAnyOpen) return;
                    SelectSlot(index);
                });
            }
        }

        private static void AddPointerEnter(GameObject target, System.Action action)
        {
            EventTrigger trigger = target.GetComponent<EventTrigger>();
            if (trigger == null) trigger = target.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        // 끝에서 더 가면 멈춤
        private void MoveSelection(int amount)
        {
            if (_slots.Count == 0) return;

            int next = _shopSelectedIndex + amount;
            if (next < 0 || next >= _slots.Count) return;

            SelectSlot(next);
        }

        private void SelectSlot(int index)
        {
            if (index == _shopSelectedIndex) return;

            _shopSelectedIndex = index;
            ShowMessage("");
            Refresh();
        }

        private ShopItemData GetSelectedItem()
        {
            if (_shopItems == null || _shopSelectedIndex < 0 || _shopSelectedIndex >= _shopItems.Length) return null;
            return _shopItems[_shopSelectedIndex];
        }

        private int GetOwnedCount(ShopItemData item)
        {
            return _inventory != null ? _inventory.GetOwnedCount(item) : 0;
        }

        // ===== 구매 =====

        private void RequestBuy()
        {
            if (SettingPopup.IsAnyOpen) return;

            ShopItemData item = GetSelectedItem();
            if (item == null) return;

            if (_inventory == null || !_inventory.CanBuy(item))
            {
                ShowMessage("크레딧이 부족합니다");
                return;
            }

            if (_confirmPopup == null)
            {
                ConfirmBuy();
                return;
            }

            if (_confirmMessage != null) _confirmMessage.text = $"{item.ItemName}\n{item.Price:N0} 크레딧에 구매하시겠습니까?";
            _confirmPopup.gameObject.SetActive(true);
        }

        // ===== 표시 =====

        private void Refresh()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].SetItem(_shopItems[i], GetOwnedCount(_shopItems[i]));
                _slots[i].SetSelected(i == _shopSelectedIndex);
            }

            ShopItemData item = GetSelectedItem();

            if (_detailIcon != null)
            {
                _detailIcon.sprite = item != null ? item.Icon : null;
                _detailIcon.color = (item != null && item.Icon != null) ? Color.white : new Color(1f, 1f, 1f, 0.08f);
            }
            if (_detailName != null) _detailName.text = item != null ? item.ItemName : "";
            if (_detailDescription != null) _detailDescription.text = item != null ? item.ItemDescription : "";
            if (_detailPrice != null) _detailPrice.text = item != null ? $"가격 {item.Price:N0} 크레딧" : "";
            if (_detailOwned != null) _detailOwned.text = item != null ? $"보유 {GetOwnedCount(item)}" : "";
            if (_buyButton != null) _buyButton.interactable = item != null;
        }

        private void ShowMessage(string message)
        {
            if (_messageRoutine != null) StopCoroutine(_messageRoutine);
            _messageRoutine = null;

            if (_messageText == null) return;

            _messageText.text = message;
            if (!string.IsNullOrEmpty(message) && isActiveAndEnabled) _messageRoutine = StartCoroutine(HideMessageLater());
        }

        private IEnumerator HideMessageLater()
        {
            yield return new WaitForSecondsRealtime(_messageTime);
            if (_messageText != null) _messageText.text = "";
            _messageRoutine = null;
        }

        private static bool IsRightClicked()
        {
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        }
    }
}
