using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Hub
{
    // 의뢰 보드에 핀으로 꽂힌 폴라로이드 사진 하나 (RequestBoard가 내용을 정함)
    // 사진 밑에는 조직 이름만 적고, 길이와 스테이지 컨셉은 골랐을 때 RequestDetail이 보여 줌
    // 실패한 의뢰는 '실패한 임무'가 적히고 칼로 고정되며, 칼이 꽂혀 있는 동안 재탐색해도 바뀌지 않음
    // 흔들릴 때 핀을 축으로 돌도록 피벗을 위쪽 가운데로 둠
    [RequireComponent(typeof(CanvasGroup))]
    public class RequestNote : MonoBehaviour
    {
        [Tooltip("사진 밑의 조직 이름")]
        [SerializeField] private TMP_Text _noteText;

        [Tooltip("사진이 들어갈 칸 (데이터 테이블에 사진이 없으면 꺼져서 빈 칸으로 보임)")]
        [SerializeField] private Image _photoImage;

        [Tooltip("실패한 의뢰일 때만 켜지는 '실패한 임무' 글자")]
        [SerializeField] private GameObject _failedMark;

        [Tooltip("실패한 의뢰일 때만 켜지는 칼 (고를 수 있는 항목)")]
        [SerializeField] private GameObject _knife;

        [Header("재탐색 연출 (실제 시간 기준)")]
        [Tooltip("떨어지며 사라지는 시간(초)")]
        [Min(0.01f)]
        [SerializeField] private float _fallTime = 0.35f;
        [Tooltip("떨어지는 거리")]
        [SerializeField] private float _fallDistance = 320f;
        [Tooltip("새 사진이 나타나는 시간(초)")]
        [Min(0.01f)]
        [SerializeField] private float _appearTime = 0.15f;
        [Tooltip("흔들리는 시간(초)")]
        [Min(0.01f)]
        [SerializeField] private float _swayTime = 1.4f;
        [Tooltip("처음 흔들리는 각도. 점점 줄어듦")]
        [SerializeField] private float _swayAngle = 16f;
        [Tooltip("1초에 흔들리는 횟수")]
        [SerializeField] private float _swaySpeed = 2.2f;

        private int _placeIndex = -1;

        // 흔들리지 않을 때의 자리와 기울기
        private RectTransform _noteRect;
        private CanvasGroup _noteGroup;
        private Vector2 _homePosition;
        private Quaternion _homeRotation;
        private Coroutine _noteRoutine;

        // 실패해서 칼로 고정된 의뢰인지
        public bool IsPinned { get; private set; }

        // 확대 화면(RequestDetail)에 보여 줄 내용
        public string NoteTitle { get; private set; } = "";
        public string NoteLength { get; private set; } = "";
        public string NotePlace { get; private set; } = "";
        public string NoteConcept { get; private set; } = "";
        public Sprite NotePhoto { get; private set; }

        public float FallTime => _fallTime;

        public RectTransform NoteRect
        {
            get
            {
                RememberHome();
                return _noteRect;
            }
        }

        void OnDisable()
        {
            // 연출 도중에 화면이 꺼지면 원래 모습으로 돌려놓음
            ResetPose();
        }

        // 레벨에 맞는 길이를 뽑고, 배경은 지금과 다른 것으로 바꿈
        public void Draw(RequestTable table, RequestTable.FactionInfo faction, int level)
        {
            if (faction == null) return;

            if (faction.PlaceCount > 0)
            {
                _placeIndex = _placeIndex < 0
                    ? Random.Range(0, faction.PlaceCount)
                    : (_placeIndex + 1) % faction.PlaceCount;
            }

            RequestLength requestLength = table.PickLength(level);
            string length = $"{RequestTable.GetLengthName(requestLength)} ({RequestTable.GetStageCount(requestLength)}스테이지)";
            ShowContent(faction.FactionName, length, faction.GetPlace(_placeIndex));
        }

        // 메인 의뢰: 재탐색해도 바뀌지 않음
        public void DrawMain(RequestTable table)
        {
            ShowContent(table.MainTitle, table.MainLength, table.MainPlace);
        }

        public void SetPinned(bool isPinned)
        {
            IsPinned = isPinned;
            if (_failedMark != null) _failedMark.SetActive(isPinned);
            if (_knife != null) _knife.SetActive(isPinned);
        }

        // 확대 화면의 사진이 대신 보이는 동안 보드의 사진을 숨김
        public void SetHidden(bool isHidden)
        {
            ResetPose();
            _noteGroup.alpha = isHidden ? 0f : 1f;
        }

        // 핀에서 떨어지며 사라짐
        public void PlayFall()
        {
            ResetPose();
            if (isActiveAndEnabled) _noteRoutine = StartCoroutine(FallRoutine());
        }

        // 원래 자리에 나타나며 핀을 축으로 흔들림
        public void PlayAppear()
        {
            ResetPose();
            if (isActiveAndEnabled) _noteRoutine = StartCoroutine(AppearRoutine());
        }

        // 사진이 없으면 사진 칸을 꺼서 뒤의 빈 칸이 보이게 함
        public static void ShowPhoto(Image photoImage, Sprite photo)
        {
            if (photoImage == null) return;
            photoImage.sprite = photo;
            photoImage.enabled = photo != null;
        }

        private void ShowContent(string title, string length, RequestTable.PlaceInfo place)
        {
            NoteTitle = title;
            NoteLength = length;
            NotePlace = place != null ? place.PlaceName : "";
            NoteConcept = place != null ? place.Concept : "";
            NotePhoto = place != null ? place.Photo : null;

            if (_noteText != null) _noteText.text = title;
            ShowPhoto(_photoImage, NotePhoto);
        }

        // ===== 연출 =====

        private void RememberHome()
        {
            if (_noteRect != null) return;

            _noteRect = (RectTransform)transform;
            _noteGroup = GetComponent<CanvasGroup>();
            _homePosition = _noteRect.anchoredPosition;
            _homeRotation = _noteRect.localRotation;
        }

        private void ResetPose()
        {
            RememberHome();
            if (_noteRoutine != null) StopCoroutine(_noteRoutine);
            _noteRoutine = null;

            _noteRect.anchoredPosition = _homePosition;
            _noteRect.localScale = Vector3.one;
            SetTilt(0f);
            _noteGroup.alpha = 1f;
        }

        private IEnumerator FallRoutine()
        {
            float elapsedTime = 0f;
            while (elapsedTime < _fallTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsedTime / _fallTime);

                // 점점 빠르게 떨어짐
                _noteRect.anchoredPosition = _homePosition + Vector2.down * (_fallDistance * progress * progress);
                SetTilt(20f * progress);
                _noteGroup.alpha = 1f - progress * progress;
                yield return null;
            }

            // 새 내용이 나타날 때(PlayAppear)까지 보이지 않는 상태로 있음
            _noteGroup.alpha = 0f;
        }

        private IEnumerator AppearRoutine()
        {
            float elapsedTime = 0f;
            while (elapsedTime < _swayTime)
            {
                float appear = Mathf.Clamp01(elapsedTime / _appearTime);
                _noteGroup.alpha = appear;
                _noteRect.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, appear);

                // 흔들림이 점점 줄어들어 원래 기울기에서 멈춤
                float remaining = 1f - Mathf.Clamp01(elapsedTime / _swayTime);
                float swing = Mathf.Cos(elapsedTime * _swaySpeed * Mathf.PI * 2f);
                SetTilt(_swayAngle * remaining * remaining * swing);

                yield return null;
                elapsedTime += Time.unscaledDeltaTime;
            }

            ResetPose();
        }

        // 원래 기울기에서 더 기울임
        private void SetTilt(float angle)
        {
            _noteRect.localRotation = _homeRotation * Quaternion.Euler(0f, 0f, angle);
        }
    }
}
