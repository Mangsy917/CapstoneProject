using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI.Hub
{
    // 거점 화면의 탭 하나 (탭 화면 오브젝트에 붙여서 사용, 탭 화면은 HubMenu의 화면 영역 안에 둠)
    // 탭 화면 형태 (5. UI/UX '탭 화면 형태')
    // - 일반형: 왼쪽에 탭 이름과 하위 메뉴(세로 목록), 오른쪽에 일러스트, 일러스트 밑에 대사 자막 상자
    //           선택하면 화면이 확대되고 대사 자막 상자가 보임
    // - 보드형: 화이트보드가 화면 전체를 채우고 포스트잇(목록)과 오른쪽 아래 구석 버튼을 고름. 대사 없음
    // - 팝업형: 하위 메뉴 없음. 선택하면 화면을 확대하지 않고 팝업을 띄움 (Popup Open 이벤트)
    public class HubTab : MonoBehaviour
    {
        public enum TabType
        {
            Normal,
            Board,
            Popup
        }

        [Tooltip("탭 줄 버튼에 표시할 이름")]
        [SerializeField] private string _tabName;

        [Tooltip("탭을 고르는 동안 탭 줄에 표시할 한 줄 설명")]
        [SerializeField] private string _description;

        [SerializeField] private TabType _tabType;

        [Tooltip("탭 줄에 있는 이 탭의 버튼")]
        [SerializeField] private Button _tabButton;

        [Tooltip("일반형: 하위 메뉴(위에서 아래 순서) / 보드형: 포스트잇 다음에 구석 버튼 순서")]
        [SerializeField] private HubMenuItem[] _items;

        [Tooltip("일반형만: 일러스트 밑의 대사 자막 상자 (선택했을 때만 보임)")]
        [SerializeField] private GameObject _dialogueBox;

        [Header("팝업형")]
        [Tooltip("제작된 팝업이면 켬. 꺼져 있으면 선택했을 때 '미완성된 시스템입니다' 팝업이 뜸")]
        [SerializeField] private bool _isPopupBuilt;

        [Tooltip("팝업형 탭을 선택했을 때 실행 (팝업 열기)")]
        [SerializeField] private UnityEvent _onPopupOpen;

        public string TabName => _tabName;
        public string Description => _description;
        public TabType Type => _tabType;
        public Button TabButton => _tabButton;
        public HubMenuItem[] Items => _items;
        public bool IsPopupBuilt => _isPopupBuilt;

        public void OpenPopup()
        {
            _onPopupOpen?.Invoke();
        }

        public void ShowDialogue(bool isShown)
        {
            if (_dialogueBox != null) _dialogueBox.SetActive(isShown && _tabType == TabType.Normal);
        }
    }
}
