using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI.Hub
{
    // 2depth 메뉴 하나 (일반형의 상단 탭, 보드형의 포스트잇·구석 버튼)
    // 고르는 것과 실행은 HubMenu가 처리함
    // - 일반형: 고르면 Content Page(3depth 내용)가 보임. Is Built와 On Select는 쓰지 않음
    // - 보드형: 제작된 기능이면 Is Built를 켜고 On Select에 할 일을 연결
    // - Is Built가 꺼져 있으면 선택했을 때 '미완성된 시스템입니다' 팝업이 뜸
    [RequireComponent(typeof(Button))]
    public class HubMenuItem : MonoBehaviour
    {
        private static readonly Color _itemHighlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color _itemNormalColor = new Color(1f, 1f, 1f, 0f);

        [Tooltip("제작된 기능이면 켬. 꺼져 있으면 선택했을 때 '미완성된 시스템입니다' 팝업이 뜸")]
        [SerializeField] private bool _isBuilt;

        [Tooltip("선택했을 때 실행 (Is Built가 켜져 있을 때만)")]
        [SerializeField] private UnityEvent _onSelect;

        [Tooltip("선택됐을 때 켤 표시 (포스트잇의 마커 테두리 등). 비워 두면 버튼 배경색으로 표시")]
        [SerializeField] private GameObject _selectedMark;

        [Tooltip("일반형만: 이 탭을 고르면 보일 3depth 내용")]
        [SerializeField] private GameObject _contentPage;

        private Button _itemButton;

        public bool IsBuilt => _isBuilt;

        public Button ItemButton
        {
            get
            {
                if (_itemButton == null) _itemButton = GetComponent<Button>();
                return _itemButton;
            }
        }

        public void Run()
        {
            _onSelect?.Invoke();
        }

        public void SetSelected(bool isSelected)
        {
            if (_selectedMark != null) _selectedMark.SetActive(isSelected);
            else if (ItemButton.image != null) ItemButton.image.color = isSelected ? _itemHighlightColor : _itemNormalColor;

            SelectedFontSize.Apply(ItemButton, isSelected);

            if (_contentPage != null) _contentPage.SetActive(isSelected);
        }
    }
}
