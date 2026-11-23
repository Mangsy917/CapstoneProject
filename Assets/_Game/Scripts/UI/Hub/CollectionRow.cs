using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Hub
{
    // 도감 세로 목록의 한 줄 (CollectionWindow가 항목 수만큼 복사해서 씀)
    [RequireComponent(typeof(Button))]
    public class CollectionRow : MonoBehaviour
    {
        private static readonly Color _rowHighlightColor = new Color(0.12f, 0.55f, 0.52f, 0.6f);
        private static readonly Color _rowNormalColor = new Color(1f, 1f, 1f, 0.06f);

        [SerializeField] private TMP_Text _rowName;
        [SerializeField] private TMP_Text _rowGroup;

        private Button CR_rowButton;

        public Button CR_RowButton
        {
            get
            {
                if (CR_rowButton == null) CR_rowButton = GetComponent<Button>();
                return CR_rowButton;
            }
        }

        public void SetEntry(CollectionData.Entry entry)
        {
            if (_rowName != null) _rowName.text = entry.EntryName;
            if (_rowGroup != null) _rowGroup.text = entry.EntryGroup;
        }

        public void SetSelected(bool isSelected)
        {
            if (CR_RowButton.image != null) CR_RowButton.image.color = isSelected ? _rowHighlightColor : _rowNormalColor;
            SelectedFontSize.Apply(CR_RowButton, isSelected);
        }
    }
}
