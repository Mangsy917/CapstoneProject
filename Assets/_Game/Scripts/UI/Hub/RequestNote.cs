using TMPro;
using UnityEngine;

namespace Game.UI.Hub
{
    // 의뢰 보드에 꽂힌 일반 의뢰 종이 하나 (RequestBoard가 내용을 정함)
    // 실패한 의뢰는 '실패한 임무'가 적히고 칼로 고정되며, 칼이 꽂혀 있는 동안 재탐색해도 바뀌지 않음
    public class RequestNote : MonoBehaviour
    {
        [SerializeField] private TMP_Text _noteText;

        [Tooltip("실패한 의뢰일 때만 켜지는 '실패한 임무' 글자")]
        [SerializeField] private GameObject _failedMark;

        [Tooltip("실패한 의뢰일 때만 켜지는 칼 (고를 수 있는 항목)")]
        [SerializeField] private GameObject _knife;

        private int _placeIndex = -1;
        private RequestLength _requestLength;

        // 실패해서 칼로 고정된 의뢰인지
        public bool IsPinned { get; private set; }

        // 레벨에 맞는 길이를 뽑고, 배경은 지금과 다른 것으로 바꿈
        public void Draw(RequestTable table, RequestTable.FactionInfo faction, int level)
        {
            _requestLength = table.PickLength(level);

            if (faction != null && faction.PlaceCount > 0)
            {
                _placeIndex = _placeIndex < 0
                    ? Random.Range(0, faction.PlaceCount)
                    : (_placeIndex + 1) % faction.PlaceCount;
            }

            RefreshText(faction);
        }

        public void SetPinned(bool isPinned)
        {
            IsPinned = isPinned;
            if (_failedMark != null) _failedMark.SetActive(isPinned);
            if (_knife != null) _knife.SetActive(isPinned);
        }

        private void RefreshText(RequestTable.FactionInfo faction)
        {
            if (_noteText == null || faction == null) return;

            string lengthName = RequestTable.GetLengthName(_requestLength);
            int stageCount = RequestTable.GetStageCount(_requestLength);
            _noteText.text = $"{faction.FactionName}\n{faction.GetPlace(_placeIndex)}\n{lengthName} ({stageCount}스테이지)";
        }
    }
}
