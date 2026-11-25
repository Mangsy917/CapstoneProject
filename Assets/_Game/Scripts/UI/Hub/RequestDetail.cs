using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI.Hub
{
    // 의뢰 보드에서 의뢰를 골랐을 때의 확대와 정보창 (의뢰 화면의 맨 위에 두고 꺼 둔 상태로 둠)
    // - 열기: 카메라가 보드로 다가가듯 보드 전체가 커지며, 고른 폴라로이드가 왼쪽 2/3의 가운데에 옴
    //   오른쪽 1/3에는 정보창(구역 이름, 세력, 길이, 의뢰 내용)이 화면 밖에서 밀려 들어옴
    // - 닫기 (취소 키, 마우스 우클릭): 보드가 원래 크기로 돌아가고 정보창이 나감
    // - 선택 키 / 출발 버튼: 아직 의뢰 플레이가 없어서 '미완성된 시스템입니다' 팝업을 띄움
    // - ESC / Start: 시스템 설정을 엶
    // 열려 있는 동안 보드(HubMenu)는 입력을 받지 않음
    public class RequestDetail : MonoBehaviour
    {
        // 닫힌 다음 프레임까지 열린 것으로 봄 (닫은 키로 보드까지 뒤로 가는 것을 막음)
        public static bool RD_IsOpen => RD_openCount > 0 || Time.frameCount - RD_closedFrame <= 1;
        private static int RD_openCount;
        private static int RD_closedFrame = int.MinValue / 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOpenState()
        {
            RD_openCount = 0;
            RD_closedFrame = int.MinValue / 2;
        }

        // 비워두면 씬에서 찾음
        [SerializeField] private UIControl RD_uiControl;
        [SerializeField] private HubMenu _hubMenu;

        [Header("왼쪽: 보드")]
        [Tooltip("보드 전체 (판, 실, 사진이 모두 들어 있는 영역)")]
        [SerializeField] private RectTransform _boardView;
        [Tooltip("다가갔을 때의 배율")]
        [Min(1f)]
        [SerializeField] private float _zoomScale = 3f;
        [Tooltip("다가간 동안 숨길 것 (재탐색 버튼, 화면 이름, 트라우마 표시)")]
        [SerializeField] private CanvasGroup[] RD_hiddenGroups;

        [Header("오른쪽: 정보창 (화면 너비의 1/3)")]
        [SerializeField] private RectTransform _infoPanel;
        [SerializeField] private TMP_Text _areaText;
        [SerializeField] private TMP_Text _factionText;
        [SerializeField] private TMP_Text _lengthText;
        [SerializeField] private TMP_Text _requestText;
        [SerializeField] private Button _departButton;

        [Header("연출")]
        [Tooltip("다가가거나 돌아가는 시간(초, 실제 시간 기준)")]
        [Min(0.01f)]
        [SerializeField] private float _openTime = 0.5f;

        private RequestNote _shownNote;
        private HubMenuItem _shownItem;
        private Coroutine _moveRoutine;

        // 누르고 있는 동안 계속 실행되지 않도록 이전 프레임 선택 키를 기억
        private bool RD_lastSelect;

        void Awake()
        {
            if (RD_uiControl == null) RD_uiControl = FindAnyObjectByType<UIControl>(FindObjectsInactive.Include);
            if (_hubMenu == null) _hubMenu = FindAnyObjectByType<HubMenu>(FindObjectsInactive.Include);
            if (_departButton != null) _departButton.onClick.AddListener(Depart);
        }

        void OnEnable()
        {
            RD_openCount++;

            // 화면을 연 키를 아직 누르고 있어도 바로 출발하지 않도록 현재 입력을 기억
            if (RD_uiControl != null) RD_lastSelect = RD_uiControl.uiSelect;

            // 버튼이 하나뿐이라 항상 선택된 상태로 보임
            if (_departButton != null)
            {
                if (_departButton.image != null) _departButton.image.color = Game.UI.MenuColors.Highlight;
                SelectedFontSize.Apply(_departButton, isSelected: true);
            }
        }

        void OnDisable()
        {
            RD_openCount--;
            RD_closedFrame = Time.frameCount;

            _moveRoutine = null;
            ApplyView(0f);
            if (_shownItem != null) _shownItem.SetSelected(true);
            _shownItem = null;
            _shownNote = null;
        }

        void Update()
        {
            if (RD_uiControl == null) return;

            bool select = RD_uiControl.uiSelect;
            bool isSelectPressed = select && !RD_lastSelect;
            RD_lastSelect = select;

            // 커지거나 돌아가는 중, 팝업·옵션 창이 위에 열려 있을 때는 입력을 받지 않음
            if (_moveRoutine != null || SettingPopup.IsAnyOpen) return;

            if (RD_uiControl.uiClose)
            {
                RD_uiControl.uiClose = false;
                _hubMenu.OpenSetting();
            }
            else if (RD_uiControl.uiCancelDown || IsBackClicked())
            {
                // 같은 키로 보드까지 뒤로 가지 않도록 처리한 입력은 지움
                RD_uiControl.uiCancelDown = false;
                Close();
            }
            else if (isSelectPressed)
            {
                Depart();
            }
        }

        public void Open(RequestNote note)
        {
            if (note == null || gameObject.activeSelf) return;

            gameObject.SetActive(true);

            _shownNote = note;
            note.StopMotion();

            // 다가간 동안에는 선택 테두리를 숨김
            _shownItem = note.GetComponent<HubMenuItem>();
            if (_shownItem != null) _shownItem.SetSelected(false);

            _areaText.text = note.NotePlace;
            _factionText.text = note.NoteFaction;
            _lengthText.text = note.NoteLength;
            _requestText.text = string.IsNullOrEmpty(note.NoteContent) ? "의뢰 내용 미정" : note.NoteContent;

            _moveRoutine = StartCoroutine(MoveRoutine(isOpening: true));
        }

        private void Close()
        {
            _moveRoutine = StartCoroutine(MoveRoutine(isOpening: false));
        }

        private void Depart()
        {
            if (_moveRoutine != null || SettingPopup.IsAnyOpen) return;
            _hubMenu.OpenUnfinishedPopup();
        }

        // 보드 전체가 보이는 상태 <-> 고른 폴라로이드로 다가간 상태
        private IEnumerator MoveRoutine(bool isOpening)
        {
            float elapsedTime = 0f;
            while (true)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsedTime / _openTime);
                ApplyView(isOpening ? progress : 1f - progress);

                yield return null;
                if (elapsedTime >= _openTime) break;
                elapsedTime += Time.unscaledDeltaTime;
            }

            _moveRoutine = null;
            if (!isOpening) gameObject.SetActive(false);
        }

        // 0: 보드 전체, 1: 다가간 상태
        private void ApplyView(float openness)
        {
            if (_shownNote != null)
            {
                // 고른 폴라로이드의 가운데가 정보창을 뺀 왼쪽 영역의 가운데에 오도록 보드를 옮김
                RectTransform noteRect = _shownNote.NoteRect;
                Vector2 noteCenter = _boardView.InverseTransformPoint(noteRect.TransformPoint(noteRect.rect.center));
                Vector2 leftCenter = Vector2.left * (_infoPanel.rect.width * 0.5f);
                Vector2 endPosition = leftCenter - noteCenter * _zoomScale;

                _boardView.localScale = Vector3.one * Mathf.Lerp(1f, _zoomScale, openness);
                _boardView.anchoredPosition = endPosition * openness;
            }

            _infoPanel.anchoredPosition = Vector2.right * (_infoPanel.rect.width * (1f - openness));

            foreach (CanvasGroup group in RD_hiddenGroups)
            {
                if (group != null) group.alpha = 1f - openness;
            }
        }

        private static bool IsBackClicked()
        {
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        }
    }
}
