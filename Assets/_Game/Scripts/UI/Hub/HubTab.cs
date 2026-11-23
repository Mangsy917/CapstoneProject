using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI.Hub
{
    // 메인화면(거점)의 1depth 메뉴 항목 하나 (2depth 화면 오브젝트에 붙여서 사용, HubMenu의 화면 영역 안에 둠)
    // 2depth 메뉴 형태 (5. UI/UX '2depth 메뉴 형태')
    // - 일반형: 2depth 메뉴(좌우로 움직이는 탭 줄), 그 아래에 고른 탭의 3depth 내용. 상점·튜닝은 오른쪽에 일러스트
    // - 보드형: 수사보드가 화면을 채우고 보드 위의 종이·칼과 구석 버튼을 고름. 대사 없음
    // - 팝업형: 2depth 화면 없음. 선택하면 확대·페이드 없이 팝업을 띄움 (Popup Open 이벤트)
    public class HubTab : MonoBehaviour
    {
        public enum TabType
        {
            Normal,
            Board,
            Popup
        }

        [Tooltip("1depth 메뉴 바 버튼에 표시할 이름")]
        [SerializeField] private string _tabName;

        [Tooltip("항목을 고르는 동안 메뉴 바에 표시할 한 줄 설명")]
        [SerializeField] private string _description;

        [SerializeField] private TabType _tabType;

        [Tooltip("1depth 메뉴 바에 있는 이 항목의 버튼")]
        [SerializeField] private Button _tabButton;

        [Tooltip("메인화면 배경에서 이 항목과 연결된 2D 오브젝트 (없으면 비워 둠)")]
        [SerializeField] private HubObject _linkedObject;

        [Tooltip("일반형: 상단 2depth 탭(왼쪽부터 순서대로) / 보드형: 보드 위의 항목 (첫 항목이 처음에 선택됨)")]
        [SerializeField] private HubMenuItem[] _items;

        [Header("팝업형")]
        [Tooltip("제작된 팝업이면 켬. 꺼져 있으면 선택했을 때 '미완성된 시스템입니다' 팝업이 뜸")]
        [SerializeField] private bool _isPopupBuilt;

        [Tooltip("팝업형 항목을 선택했을 때 실행 (팝업 열기)")]
        [SerializeField] private UnityEvent _onPopupOpen;

        public string TabName => _tabName;
        public string Description => _description;
        public TabType Type => _tabType;
        public Button TabButton => _tabButton;
        public HubObject LinkedObject => _linkedObject;
        public HubMenuItem[] Items => _items;
        public bool IsPopupBuilt => _isPopupBuilt;

        public void OpenPopup()
        {
            _onPopupOpen?.Invoke();
        }
    }
}
