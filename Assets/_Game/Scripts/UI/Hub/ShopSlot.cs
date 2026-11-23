using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Hub
{
    // 상점 창의 품목 칸 하나 (ShopWindow가 품목 수만큼 복사해서 씀)
    [RequireComponent(typeof(Button))]
    public class ShopSlot : MonoBehaviour
    {
        private static readonly Color _slotHighlightColor = Game.UI.MenuColors.Highlight;
        private static readonly Color _slotNormalColor = new Color(1f, 1f, 1f, 0.1f);

        [Tooltip("품목 아이콘 자리")]
        [SerializeField] private Image _slotIcon;
        [SerializeField] private TMP_Text _slotName;
        [SerializeField] private TMP_Text _slotPrice;
        [SerializeField] private TMP_Text _slotOwned;

        private Button _slotButton;

        public Button SlotButton
        {
            get
            {
                if (_slotButton == null) _slotButton = GetComponent<Button>();
                return _slotButton;
            }
        }

        public void SetItem(ShopItemData item, int ownedCount)
        {
            if (_slotIcon != null)
            {
                _slotIcon.sprite = item != null ? item.Icon : null;
                // 아이콘이 없으면 빈 자리만 보이도록 반투명하게 둠
                _slotIcon.color = (item != null && item.Icon != null) ? Color.white : new Color(1f, 1f, 1f, 0.08f);
            }
            if (_slotName != null) _slotName.text = item != null ? item.ItemName : "";
            if (_slotPrice != null) _slotPrice.text = item != null ? $"{item.Price:N0} 크레딧" : "";
            if (_slotOwned != null) _slotOwned.text = $"보유 {ownedCount}";
        }

        public void SetSelected(bool isSelected)
        {
            if (SlotButton.image != null) SlotButton.image.color = isSelected ? _slotHighlightColor : _slotNormalColor;
        }
    }
}
