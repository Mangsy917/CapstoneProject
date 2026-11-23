using UnityEngine;

namespace Game.UI.Hub
{
    // 상점에서 파는 품목 하나 (보조 아이템, 무기 모듈)
    // Data/Shop 폴더에 D_ 이름으로 만들고 상점 창(ShopWindow)의 품목 목록에 넣음
    [CreateAssetMenu(fileName = "D_ShopItem", menuName = "Game/Shop Item")]
    public class ShopItemData : ScriptableObject
    {
        [SerializeField] private string _itemName;

        [TextArea]
        [SerializeField] private string _itemDescription;

        [Tooltip("크레딧 가격")]
        [Min(0)]
        [SerializeField] private int _price;

        [Tooltip("품목 아이콘 (비워 두면 빈 칸으로 보임)")]
        [SerializeField] private Sprite _icon;

        public string ItemName => _itemName;
        public string ItemDescription => _itemDescription;
        public int Price => _price;
        public Sprite Icon => _icon;
    }
}
