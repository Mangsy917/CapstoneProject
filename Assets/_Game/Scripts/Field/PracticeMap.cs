using UnityEngine;

namespace Game.Field
{
    // 연습 맵의 색 버전을 바꿈 (맵 오브젝트에 붙여서 사용)
    // - Themes에 맵 버전마다 이름, 켜고 끌 오브젝트(바닥, 벽, 거리 눈금), 선택 팝업에 보일 스크린샷을 넣음
    // - 고른 버전의 오브젝트만 켜고 나머지는 끔
    // - 고른 버전은 저장되어 씬을 다시 열거나 게임을 다시 켜도 유지됨
    public class PracticeMap : MonoBehaviour
    {
        [System.Serializable]
        public class MapTheme
        {
            [Tooltip("맵 선택 팝업과 연습 설정에 보일 이름")]
            [SerializeField] private string _mapName;
            [Tooltip("이 버전을 고르면 켜지는 오브젝트")]
            [SerializeField] private GameObject _mapRoot;
            [Tooltip("맵 선택 팝업에 보일 스크린샷")]
            [SerializeField] private Sprite _thumbnail;

            public string MapName => _mapName;
            public GameObject MapRoot => _mapRoot;
            public Sprite Thumbnail => _thumbnail;
        }

        private const string PM_SaveKey = "PracticeMapIndex";

        [SerializeField] private MapTheme[] _themes;
        [Tooltip("저장된 값이 없을 때 처음 고를 버전 번호 (0부터)")]
        [SerializeField] private int _defaultIndex = 1;

        // 맵이 바뀌면 바뀐 번호를 알림 (설정창의 값 글자 갱신 등)
        public event System.Action<int> MapChanged;

        // 지금 고른 버전 번호
        public int MapIndex { get; private set; }

        public int MapCount => _themes != null ? _themes.Length : 0;

        void Awake()
        {
            MapIndex = ClampIndex(PlayerPrefs.GetInt(PM_SaveKey, _defaultIndex));
            ApplyMap();
        }

        public MapTheme GetTheme(int index)
        {
            if (MapCount == 0) return null;
            return _themes[ClampIndex(index)];
        }

        /// <summary>맵 버전을 바꾸고 저장함</summary>
        public void SetMap(int index)
        {
            if (MapCount == 0) return;

            index = ClampIndex(index);
            PlayerPrefs.SetInt(PM_SaveKey, index);
            PlayerPrefs.Save();
            if (index == MapIndex) return;

            MapIndex = index;
            ApplyMap();
            MapChanged?.Invoke(MapIndex);
        }

        // 고른 버전의 오브젝트만 켬
        private void ApplyMap()
        {
            for (int i = 0; i < MapCount; i++)
            {
                if (_themes[i].MapRoot != null) _themes[i].MapRoot.SetActive(i == MapIndex);
            }
        }

        private int ClampIndex(int index)
        {
            return Mathf.Clamp(index, 0, Mathf.Max(0, MapCount - 1));
        }
    }
}
