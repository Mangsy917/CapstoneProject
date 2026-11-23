using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.UI.Hub
{
    // 거점의 임시 레벨·크레딧과 보유 품목 (거점 씬의 빈 오브젝트에 붙여서 사용)
    // 아직 저장 기능이 없어서 씬을 나가면 처음 값으로 돌아감
    public class HubInventory : MonoBehaviour
    {
        [Tooltip("거점에 들어왔을 때의 크레딧")]
        [Min(0)]
        [SerializeField] private int _startCredit;

        [Tooltip("임시 플레이어 레벨 (의뢰 길이 비율에 사용)")]
        [Min(1)]
        [SerializeField] private int _playerLevel = 1;

        [Tooltip("레벨을 표시할 글자 (왼쪽 위)")]
        [SerializeField] private TMP_Text _levelText;

        [Tooltip("크레딧을 표시할 글자 (왼쪽 위)")]
        [SerializeField] private TMP_Text _creditText;

        private int _credit;
        private readonly Dictionary<ShopItemData, int> _ownedCountDic = new Dictionary<ShopItemData, int>();

        // 크레딧이나 보유 수가 바뀌면 알림
        public event Action InventoryChanged;

        public int Credit => _credit;
        public int PlayerLevel => _playerLevel;

        void Awake()
        {
            _credit = _startCredit;
            if (_levelText != null) _levelText.text = $"Lv. {_playerLevel}";
            RefreshCreditText();
        }

        public int GetOwnedCount(ShopItemData item)
        {
            if (item == null) return 0;
            return _ownedCountDic.TryGetValue(item, out int count) ? count : 0;
        }

        public bool CanBuy(ShopItemData item)
        {
            return item != null && _credit >= item.Price;
        }

        // 크레딧이 모자라면 사지 않고 false
        public bool TryBuy(ShopItemData item)
        {
            if (!CanBuy(item)) return false;

            _ownedCountDic[item] = GetOwnedCount(item) + 1;
            return TrySpend(item.Price);
        }

        // 크레딧이 모자라면 쓰지 않고 false
        public bool TrySpend(int amount)
        {
            if (_credit < amount) return false;

            _credit -= amount;
            RefreshCreditText();
            InventoryChanged?.Invoke();
            return true;
        }

        private void RefreshCreditText()
        {
            if (_creditText != null) _creditText.text = $"크레딧 {_credit:N0}";
        }
    }
}
