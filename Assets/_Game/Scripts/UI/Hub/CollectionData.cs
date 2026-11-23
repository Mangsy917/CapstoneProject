using System;
using UnityEngine;

namespace Game.UI.Hub
{
    // 도감 탭 하나의 목록 (커맨드 리스트, 소모성 아이템, 모듈, 조우한 적, 스토리 다시보기)
    // Data/Hub 폴더에 D_ 이름으로 만들고 도감 화면(CollectionWindow)에 넣음
    [CreateAssetMenu(fileName = "D_Collection", menuName = "Game/Collection Data")]
    public class CollectionData : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("목록과 오른쪽에 표시할 이름 (Linked Item을 넣으면 쓰지 않음)")]
            [SerializeField] private string _entryName;

            [Tooltip("이름 옆에 작게 표시할 분류 (세력, 등급 등). 비워 둘 수 있음")]
            [SerializeField] private string _entryGroup;

            [Tooltip("오른쪽에 표시할 내용 (Linked Item을 넣으면 쓰지 않음)")]
            [TextArea(3, 20)]
            [SerializeField] private string _entryDescription;

            [Tooltip("오른쪽에 표시할 그림 (Linked Item을 넣으면 쓰지 않음)")]
            [SerializeField] private Sprite _entryIcon;

            [Tooltip("상점 품목을 그대로 보여 줄 때 (소모성 아이템, 모듈)")]
            [SerializeField] private ShopItemData _linkedItem;

            public string EntryName => _linkedItem != null ? _linkedItem.ItemName : _entryName;
            public string EntryGroup => _entryGroup;
            public string EntryDescription => _linkedItem != null ? _linkedItem.ItemDescription : _entryDescription;
            public Sprite EntryIcon => _linkedItem != null ? _linkedItem.Icon : _entryIcon;
        }

        [SerializeField] private Entry[] _collectionEntries;

        public int EntryCount => _collectionEntries != null ? _collectionEntries.Length : 0;

        public Entry GetEntry(int entryIndex)
        {
            return entryIndex >= 0 && entryIndex < EntryCount ? _collectionEntries[entryIndex] : null;
        }
    }
}
