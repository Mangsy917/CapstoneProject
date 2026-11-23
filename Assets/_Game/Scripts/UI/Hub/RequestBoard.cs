using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.UI.Hub
{
    // 의뢰 2depth의 수사보드 (의뢰 화면 오브젝트에 붙여서 사용)
    // - 가운데의 메인 의뢰를 일반 의뢰 3개(세력마다 1개)가 삼각형으로 둘러쌈. 메인 의뢰는 재탐색해도 바뀌지 않음
    // - 의뢰 재탐색: 크레딧을 내고 길이(레벨 비율)와 배경을 다시 뽑음. 비용은 재탐색할 때마다 2배
    // - 실패한 의뢰는 칼로 고정되어 재탐색해도 남음. 칼을 고르면 고정을 해제할지 묻고, 해제하면 일반 의뢰로 돌아감
    // 아직 의뢰 플레이와 저장 기능이 없어서 씬을 나가면 처음 값으로 돌아감
    public class RequestBoard : MonoBehaviour
    {
        // 비워두면 씬에서 찾음
        [SerializeField] private HubInventory RB_inventory;
        [SerializeField] private RequestTable _requestTable;

        [Tooltip("일반 의뢰 (데이터 테이블의 세력 순서)")]
        [SerializeField] private RequestNote[] _requestNotes;

        [Tooltip("재탐색 버튼의 글자 (비용 표시)")]
        [SerializeField] private TMP_Text _rerollText;

        [Tooltip("안내 문구 (크레딧 부족 등)")]
        [SerializeField] private TMP_Text _noticeText;
        [Tooltip("안내 문구를 보여 주는 시간(초)")]
        [SerializeField] private float _noticeTime = 1.5f;

        [Tooltip("고정 해제 확인 팝업. 예 버튼에 ConfirmUnpin을 연결")]
        [SerializeField] private SettingPopup _unpinPopup;

        [Tooltip("확인용 임시 값: 처음부터 실패한 것으로 둘 의뢰 (0부터, -1이면 없음)")]
        [SerializeField] private int _startFailedIndex = -1;

        private int _rerollCost;
        private int _unpinIndex = -1;
        private Coroutine _noticeRoutine;

        void Awake()
        {
            if (RB_inventory == null) RB_inventory = FindAnyObjectByType<HubInventory>(FindObjectsInactive.Include);

            _rerollCost = _requestTable.BaseRerollCost;

            for (int i = 0; i < _requestNotes.Length; i++)
            {
                DrawNote(i);
                _requestNotes[i].SetPinned(i == _startFailedIndex);
            }
        }

        void OnEnable()
        {
            if (_noticeText != null) _noticeText.text = "";
            RefreshRerollText();
        }

        void OnDisable()
        {
            _noticeRoutine = null;
        }

        // 의뢰 재탐색 버튼의 On Select에 연결
        public void Reroll()
        {
            if (!HasRerollableNote())
            {
                ShowNotice("재탐색할 수 있는 의뢰가 없습니다");
                return;
            }

            if (!RB_inventory.TrySpend(_rerollCost))
            {
                ShowNotice("크레딧이 부족합니다");
                return;
            }

            for (int i = 0; i < _requestNotes.Length; i++)
            {
                if (!_requestNotes[i].IsPinned) DrawNote(i);
            }

            // 계속 2배가 되어도 int 범위를 넘지 않도록 함
            _rerollCost = _rerollCost > int.MaxValue / 2 ? int.MaxValue : _rerollCost * 2;
            RefreshRerollText();
            ShowNotice("의뢰를 다시 찾았습니다");
        }

        // 칼의 On Select에 의뢰 번호(0부터)와 함께 연결
        public void RequestUnpin(int noteIndex)
        {
            _unpinIndex = noteIndex;
            _unpinPopup.gameObject.SetActive(true);
        }

        // 고정 해제 팝업 > 예
        public void ConfirmUnpin()
        {
            _unpinPopup.gameObject.SetActive(false);

            if (_unpinIndex < 0 || _unpinIndex >= _requestNotes.Length) return;
            _requestNotes[_unpinIndex].SetPinned(false);
            _unpinIndex = -1;
        }

        private void DrawNote(int noteIndex)
        {
            _requestNotes[noteIndex].Draw(_requestTable, _requestTable.GetFaction(noteIndex), RB_inventory.PlayerLevel);
        }

        private bool HasRerollableNote()
        {
            foreach (RequestNote note in _requestNotes)
            {
                if (!note.IsPinned) return true;
            }
            return false;
        }

        private void RefreshRerollText()
        {
            if (_rerollText != null) _rerollText.text = $"의뢰 재탐색 ({_rerollCost:N0} 크레딧)";
        }

        private void ShowNotice(string message)
        {
            if (_noticeText == null) return;
            if (_noticeRoutine != null) StopCoroutine(_noticeRoutine);

            _noticeText.text = message;
            _noticeRoutine = StartCoroutine(HideNoticeLater());
        }

        private IEnumerator HideNoticeLater()
        {
            yield return new WaitForSecondsRealtime(_noticeTime);
            _noticeText.text = "";
            _noticeRoutine = null;
        }
    }
}
