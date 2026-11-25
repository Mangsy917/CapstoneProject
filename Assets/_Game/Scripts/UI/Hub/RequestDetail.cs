using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI.Hub
{
    // 의뢰 보드에서 고른 의뢰의 확대 화면 (의뢰 화면의 맨 위에 두고 꺼 둔 상태로 둠)
    // - 열기: 고른 폴라로이드가 보드의 자리에서 커지며 왼쪽에 놓이고, 오른쪽에 길이와 스테이지 컨셉이 나타남
    // - 닫기 (취소 키, 마우스 우클릭): 사진이 보드의 자리로 돌아감
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

        [Header("왼쪽: 폴라로이드")]
        [Tooltip("확대되는 폴라로이드 (보드의 폴라로이드와 같은 크기로 두고, 확대한 자리와 크기(Scale)로 배치)")]
        [SerializeField] private RectTransform _bigPolaroid;
        [SerializeField] private Image _bigPhoto;
        [SerializeField] private TMP_Text _bigCaption;
        [SerializeField] private GameObject _bigFailedMark;

        [Header("오른쪽: 의뢰 내용")]
        [SerializeField] private CanvasGroup _contentGroup;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _lengthText;
        [SerializeField] private TMP_Text _placeText;
        [SerializeField] private TMP_Text _conceptText;
        [SerializeField] private Button _departButton;

        [Header("연출")]
        [Tooltip("보드를 어둡게 덮는 배경")]
        [SerializeField] private CanvasGroup _dimGroup;
        [Tooltip("커지거나 돌아가는 시간(초, 실제 시간 기준)")]
        [Min(0.01f)]
        [SerializeField] private float _openTime = 0.35f;

        private RequestNote _shownNote;
        private Coroutine _moveRoutine;

        // 확대했을 때의 자리와 크기 (씬에 배치한 값)
        private bool _hasBigPose;
        private Vector3 _bigPosition;
        private Vector3 _bigScale;
        private Quaternion _bigRotation;

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
            if (_shownNote != null) _shownNote.SetHidden(false);
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

            RememberBigPose();
            gameObject.SetActive(true);

            _shownNote = note;
            _titleText.text = note.NoteTitle;
            _lengthText.text = note.NoteLength;
            _placeText.text = note.NotePlace;
            _conceptText.text = note.NoteConcept;
            _bigCaption.text = note.NoteTitle;
            RequestNote.ShowPhoto(_bigPhoto, note.NotePhoto);
            _bigFailedMark.SetActive(note.IsPinned);

            note.SetHidden(true);
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

        private void RememberBigPose()
        {
            if (_hasBigPose) return;

            _hasBigPose = true;
            _bigPosition = _bigPolaroid.localPosition;
            _bigScale = _bigPolaroid.localScale;
            _bigRotation = _bigPolaroid.localRotation;
        }

        // 보드에 꽂힌 자리 <-> 확대한 자리
        private IEnumerator MoveRoutine(bool isOpening)
        {
            RectTransform noteRect = _shownNote.NoteRect;
            Vector3 boardPosition = _bigPolaroid.parent.InverseTransformPoint(noteRect.TransformPoint(noteRect.rect.center));
            Quaternion boardRotation = noteRect.localRotation;

            float elapsedTime = 0f;
            while (true)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsedTime / _openTime);
                float openness = isOpening ? progress : 1f - progress;

                _bigPolaroid.localPosition = Vector3.Lerp(boardPosition, _bigPosition, openness);
                _bigPolaroid.localScale = Vector3.Lerp(Vector3.one, _bigScale, openness);
                _bigPolaroid.localRotation = Quaternion.Slerp(boardRotation, _bigRotation, openness);
                _contentGroup.alpha = openness;
                _dimGroup.alpha = openness;

                // 첫 프레임에도 한 번은 기다려서 Open에서 대입한 값이 먼저 정해지게 함
                yield return null;
                if (elapsedTime >= _openTime) break;
                elapsedTime += Time.unscaledDeltaTime;
            }

            _moveRoutine = null;
            if (!isOpening) gameObject.SetActive(false);
        }

        private static bool IsBackClicked()
        {
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        }
    }
}
