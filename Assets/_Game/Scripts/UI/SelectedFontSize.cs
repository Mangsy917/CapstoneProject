using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 메뉴에서 선택된 버튼의 글자를 조금 키워서 강조함 (라벨, 스테퍼 값과 화살표 등 버튼 안의 모든 글자)
    // 각 메뉴의 Refresh에서 Apply를 부르면 이 컴포넌트가 버튼에 자동으로 붙음 (프리팹에 넣지 않아도 됨)
    public class SelectedFontSize : MonoBehaviour
    {
        // 선택된 버튼의 글자를 원래 크기보다 얼마나 키울지 (이 값만 바꾸면 모든 메뉴에 적용됨)
        public const float FontSizeIncrease = 1.5f;

        private TMP_Text[] _rowTexts;
        private float[] _baseFontSizes;
        private bool[] _isAutoSized;

        public static void Apply(Button button, bool isSelected)
        {
            SelectedFontSize selectedFontSize = button.GetComponent<SelectedFontSize>();
            if (selectedFontSize == null) selectedFontSize = button.gameObject.AddComponent<SelectedFontSize>();
            selectedFontSize.SetSelected(isSelected);
        }

        private void SetSelected(bool isSelected)
        {
            RememberBaseSizes();

            for (int i = 0; i < _rowTexts.Length; i++)
            {
                TMP_Text text = _rowTexts[i];
                if (_isAutoSized[i]) ApplyToAutoSizedText(text, isSelected);
                else text.fontSize = _baseFontSizes[i] + (isSelected ? FontSizeIncrease : 0f);
            }
        }

        // 꺼진 오브젝트에는 Awake가 돌지 않으므로 처음 쓸 때 원래 값을 기억함
        private void RememberBaseSizes()
        {
            if (_rowTexts != null) return;

            _rowTexts = GetComponentsInChildren<TMP_Text>(includeInactive: true);
            _baseFontSizes = new float[_rowTexts.Length];
            _isAutoSized = new bool[_rowTexts.Length];

            for (int i = 0; i < _rowTexts.Length; i++)
            {
                _baseFontSizes[i] = _rowTexts[i].fontSize;
                _isAutoSized[i] = _rowTexts[i].enableAutoSizing;
            }
        }

        // Auto Size가 켜진 글자는 fontSize를 바꿔도 다시 계산되므로,
        // 자동으로 맞춘 크기를 읽고 선택된 동안에만 Auto Size를 꺼서 키움 (타이틀 메뉴 버튼 등)
        private static void ApplyToAutoSizedText(TMP_Text text, bool isSelected)
        {
            text.enableAutoSizing = true;
            if (!isSelected) return;

            text.ForceMeshUpdate();
            float fittedFontSize = text.fontSize;
            text.enableAutoSizing = false;
            text.fontSize = fittedFontSize + FontSizeIncrease;
        }
    }
}
