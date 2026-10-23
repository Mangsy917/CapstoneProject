using System;
using UnityEngine;

namespace Game.UI
{
    // 키(바인딩 경로)마다 보여줄 버튼 스프라이트 목록. 키보드 / 플레이스테이션 / Xbox를 따로 가짐
    // 예) 키보드: "<Keyboard>/j" → J 스프라이트, 플레이스테이션: "<Gamepad>/buttonSouth" → × 스프라이트
    // 목록에 없는 키는 null을 돌려주므로, 쓰는 쪽에서 글자로 대신 보여줌
    [CreateAssetMenu(fileName = "D_ButtonIcons", menuName = "Game/Button Icon Set")]
    public class ButtonIconSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Input Actions의 바인딩 경로. 예) <Keyboard>/j, <Gamepad>/buttonSouth")]
            [SerializeField] private string _controlPath;
            [SerializeField] private Sprite _sprite;

            public string ControlPath => _controlPath;
            public Sprite Sprite => _sprite;

            public Entry(string controlPath, Sprite sprite)
            {
                _controlPath = controlPath;
                _sprite = sprite;
            }
        }

        [SerializeField] private Entry[] _keyboardEntries = new Entry[0];
        [SerializeField] private Entry[] _playStationEntries = new Entry[0];
        [SerializeField] private Entry[] _xboxEntries = new Entry[0];

        public Sprite GetSprite(ControllerTypeDetector.ControllerType type, string controlPath)
        {
            if (string.IsNullOrEmpty(controlPath)) return null;

            foreach (var entry in GetEntries(type))
            {
                if (entry != null && string.Equals(entry.ControlPath, controlPath, StringComparison.OrdinalIgnoreCase))
                    return entry.Sprite;
            }
            return null;
        }

        private Entry[] GetEntries(ControllerTypeDetector.ControllerType type)
        {
            switch (type)
            {
                case ControllerTypeDetector.ControllerType.PlayStation: return _playStationEntries;
                case ControllerTypeDetector.ControllerType.Xbox: return _xboxEntries;
                default: return _keyboardEntries;
            }
        }
    }
}
