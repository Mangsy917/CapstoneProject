using System;
using System.Collections.Generic;
using Game.Common;
using TMPro;
using UnityEngine;

namespace Game.UI.Hub
{
    // 거점의 레벨·크레딧 표시와 보유 품목 (거점 씬의 빈 오브젝트에 붙여서 사용)
    // - 레벨, 경험치, 크레딧은 PlayerProgress에 있어서 연습장을 다녀와도 이어짐
    //   인스펙터(HubInventoryEditor)에서 플레이 전에는 처음 값, 플레이 중에는 지금 값을 조정함
    // - 보유 품목은 아직 저장 기능이 없어서 씬을 나가면 처음으로 돌아감
    public class HubInventory : MonoBehaviour
    {
        [Tooltip("게임을 켰을 때의 레벨, 경험치, 크레딧 (아래에서 조정)")]
        [SerializeField] private PlayerStartData HI_startData;

        [Tooltip("레벨을 표시할 글자 (왼쪽 위)")]
        [SerializeField] private TMP_Text _levelText;

        [Tooltip("크레딧을 표시할 글자 (왼쪽 위)")]
        [SerializeField] private TMP_Text _creditText;

        [Tooltip("경험치 바에서 채워지는 막대 (부모 크기에 꽉 차게 둠)")]
        [SerializeField] private RectTransform _experienceFill;

        private readonly Dictionary<ShopItemData, int> _ownedCountDic = new Dictionary<ShopItemData, int>();

        // 크레딧이나 보유 수가 바뀌면 알림
        public event Action InventoryChanged;

        public int Credit => PlayerProgress.PP_Credit;
        public int PlayerLevel => PlayerProgress.PP_Level;

        void Awake()
        {
            PlayerProgress.Prepare(HI_startData);
            PlayerProgress.ProgressChanged += OnProgressChanged;
            RefreshTexts();
        }

        void OnDestroy()
        {
            PlayerProgress.ProgressChanged -= OnProgressChanged;
        }

        public int GetOwnedCount(ShopItemData item)
        {
            if (item == null) return 0;
            return _ownedCountDic.TryGetValue(item, out int count) ? count : 0;
        }

        public bool CanBuy(ShopItemData item)
        {
            return item != null && Credit >= item.Price;
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
            if (Credit < amount) return false;

            PlayerProgress.SetCredit(Credit - amount);
            return true;
        }

        private void OnProgressChanged()
        {
            RefreshTexts();
            InventoryChanged?.Invoke();
        }

        private void RefreshTexts()
        {
            if (_levelText != null) _levelText.text = $"Lv. {PlayerProgress.GetLevelText()}";
            if (_creditText != null) _creditText.text = $"크레딧 {Credit:N0}";
            if (_experienceFill == null) return;

            float ratio = (float)PlayerProgress.PP_Experience / PlayerProgress.GetMaxExperience(PlayerLevel);
            _experienceFill.anchorMin = Vector2.zero;
            _experienceFill.anchorMax = new Vector2(ratio, 1f);
            _experienceFill.offsetMin = Vector2.zero;
            _experienceFill.offsetMax = Vector2.zero;
        }
    }
}
