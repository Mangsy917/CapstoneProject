using Game.UI.Setting;
using UnityEngine;

namespace Game.UI
{
    // 상단 / 하단 HUD 오브젝트에 붙여서 사용 (지금은 상단 = HealthBars, 하단 HUD는 아직 없음)
    // 옵션의 화면 설정(DisplaySettings)에 맞춰 HUD를 보이거나 숨기고, 위치를 화면 안쪽으로 옮김
    // - 숨길 때는 오브젝트를 끄지 않고 CanvasGroup 투명도로 숨김
    //   (다른 스크립트가 이 오브젝트를 켜고 끄는 것과 겹치지 않도록)
    // - 위치는 처음 위치에서 상단은 아래로, 하단은 위로 옮김
    public class HudDisplay : MonoBehaviour
    {
        public enum HudArea
        {
            Top,
            Bottom
        }

        [SerializeField] private HudArea _area = HudArea.Top;

        private RectTransform _rectTransform;
        private CanvasGroup HD_canvasGroup;
        private Vector2 _startPosition;

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
            if (_rectTransform != null) _startPosition = _rectTransform.anchoredPosition;

            HD_canvasGroup = GetComponent<CanvasGroup>();
            if (HD_canvasGroup == null) HD_canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public HudArea Area => _area;

        private void OnEnable()
        {
            DisplaySettings.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            DisplaySettings.Changed -= Apply;
        }

        private void Apply()
        {
            bool isTop = _area == HudArea.Top;
            // 위치를 조절하는 중이면 꺼져 있어도 보임
            bool isVisible = (isTop ? DisplaySettings.IsTopHudVisible : DisplaySettings.IsBottomHudVisible) ||
                             DisplaySettings.EditingHudArea == _area;
            float offset = (isTop ? DisplaySettings.TopHudOffset : DisplaySettings.BottomHudOffset) / 100f * DisplaySettings.MaxHudOffset;

            HD_canvasGroup.alpha = isVisible ? 1f : 0f;
            HD_canvasGroup.blocksRaycasts = isVisible;

            if (_rectTransform != null) _rectTransform.anchoredPosition = _startPosition + new Vector2(0f, isTop ? -offset : offset);
        }
    }
}
