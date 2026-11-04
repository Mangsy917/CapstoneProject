using Game.UI.Setting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 화면 왼쪽 위에 지금 FPS를 보여줌 (옵션의 화면 설정에서 켜고 끔, DisplaySettings.IsFrameRateVisible)
    // - 게임이 시작될 때 스스로 만들어지고 씬을 넘어가도 남으므로 씬에 넣지 않아도 됨
    // - 설정창이 열려 게임이 멈춘 동안에도 재도록 Time.unscaledDeltaTime을 씀
    public class FrameRateDisplay : MonoBehaviour
    {
        // 숫자가 너무 빨리 바뀌어 읽기 어렵지 않도록 이 시간(초)마다 평균을 내서 보여줌
        private const float TextUpdateInterval = 0.25f;
        // 다른 UI(설정창, 화면 페이드 포함)보다 항상 위에 보이도록 Canvas의 가장 큰 값을 씀
        private const int TopSortingOrder = 32767;

        private static readonly Vector2 _referenceResolution = new Vector2(1920f, 1080f);
        private static readonly Vector2 _backgroundSize = new Vector2(150f, 48f);
        private static readonly Vector2 _screenMargin = new Vector2(16f, -16f);
        private static readonly Color _backgroundColor = new Color(0f, 0f, 0f, 0.6f);

        private Canvas _frameRateCanvas;
        private TMP_Text _frameRateText;
        private int _measuredFrames;
        private float _measuredTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnStart()
        {
            GameObject displayObject = new GameObject("FrameRateDisplay");
            DontDestroyOnLoad(displayObject);
            displayObject.AddComponent<FrameRateDisplay>();
        }

        private void Awake()
        {
            CreateView();
            DisplaySettings.Changed += ApplyVisibility;
            ApplyVisibility();
        }

        private void OnDestroy()
        {
            DisplaySettings.Changed -= ApplyVisibility;
        }

        private void Update()
        {
            _measuredFrames++;
            _measuredTime += Time.unscaledDeltaTime;
            if (_measuredTime < TextUpdateInterval) return;

            _frameRateText.SetText("FPS {0}", Mathf.RoundToInt(_measuredFrames / _measuredTime));
            _measuredFrames = 0;
            _measuredTime = 0f;
        }

        private void ApplyVisibility()
        {
            _frameRateCanvas.enabled = DisplaySettings.IsFrameRateVisible;
        }

        private void CreateView()
        {
            _frameRateCanvas = gameObject.AddComponent<Canvas>();
            _frameRateCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _frameRateCanvas.sortingOrder = TopSortingOrder;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            // GraphicRaycaster를 넣지 않아서 마우스 입력을 가로막지 않음
            GameObject backgroundObject = new GameObject("Background", typeof(RectTransform));
            backgroundObject.transform.SetParent(transform, worldPositionStays: false);

            Image background = backgroundObject.AddComponent<Image>();
            background.color = _backgroundColor;
            background.raycastTarget = false;

            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.up;
            backgroundRect.anchorMax = Vector2.up;
            backgroundRect.pivot = Vector2.up;
            backgroundRect.anchoredPosition = _screenMargin;
            backgroundRect.sizeDelta = _backgroundSize;

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(backgroundObject.transform, worldPositionStays: false);

            _frameRateText = textObject.AddComponent<TextMeshProUGUI>();
            _frameRateText.fontSize = 28f;
            _frameRateText.color = Color.white;
            _frameRateText.alignment = TextAlignmentOptions.Center;
            _frameRateText.raycastTarget = false;
            _frameRateText.text = "FPS --";

            RectTransform textRect = _frameRateText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }
    }
}
