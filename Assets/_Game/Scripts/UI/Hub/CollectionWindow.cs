using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Hub
{
    // 도감의 3depth 화면 (도감의 모든 상단 탭이 같은 스크립트를 씀)
    // 상단 탭(HubMenuItem)의 Content Page에 연결하면 HubMenu가 켜고 끔. 탭 이동과 뒤로 가기도 HubMenu가 처리함
    // - 왼쪽: 세로 목록, 오른쪽: 고른 항목의 그림·이름·분류·내용
    // - 위아래 방향키 / 마우스를 올리거나 누르기: 항목 이동 (고르면 오른쪽 내용이 바로 바뀜)
    // 팝업·옵션 창이 열려 있거나 화면이 전환되는 동안에는 입력을 받지 않음
    public class CollectionWindow : MonoBehaviour
    {
        // 비워두면 씬에서 찾음
        [SerializeField] private UIControl CW_uiControl;
        [SerializeField] private ScreenFade CW_screenFade;

        [Tooltip("이 탭에 보여 줄 목록")]
        [SerializeField] private CollectionData _collectionData;

        [Header("세로 목록")]
        [Tooltip("복사해서 쓸 줄 (꺼 둔 상태로 둠)")]
        [SerializeField] private CollectionRow _rowTemplate;
        [Tooltip("줄이 들어갈 곳 (Vertical Layout Group)")]
        [SerializeField] private RectTransform _rowRoot;

        [Header("오른쪽 내용")]
        [Tooltip("그림이 없는 탭(커맨드 리스트)은 비워 둠")]
        [SerializeField] private Image _pictureImage;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _groupText;
        [SerializeField] private TMP_Text _contentText;

        private readonly List<CollectionRow> CW_rows = new List<CollectionRow>();
        private int _selectedRow;

        // 화면이 켜진 그 프레임에는 입력을 처리하지 않음
        private int CW_ignoreInputFrame = -1;

        void Awake()
        {
            if (CW_uiControl == null) CW_uiControl = FindAnyObjectByType<UIControl>(FindObjectsInactive.Include);
            if (CW_screenFade == null) CW_screenFade = FindAnyObjectByType<ScreenFade>(FindObjectsInactive.Include);

            CreateRows();
        }

        void OnEnable()
        {
            CW_ignoreInputFrame = Time.frameCount;
            SelectRow(0);
        }

        void Update()
        {
            if (CW_uiControl == null || IsCollectionInputBlocked() || Time.frameCount == CW_ignoreInputFrame) return;

            if (CW_uiControl.uiUpStep) MoveRow(-1);
            else if (CW_uiControl.uiDownStep) MoveRow(1);
        }

        private void CreateRows()
        {
            _rowTemplate.gameObject.SetActive(false);

            for (int i = 0; i < _collectionData.EntryCount; i++)
            {
                CollectionRow row = Instantiate(_rowTemplate, _rowRoot);
                row.name = $"Row{i + 1}";
                row.gameObject.SetActive(true);
                row.SetEntry(_collectionData.GetEntry(i));
                CW_rows.Add(row);

                int rowIndex = i;
                AddPointerEnter(row.gameObject, () => PointRow(rowIndex));
                row.CR_RowButton.onClick.AddListener(() => PointRow(rowIndex));
            }
        }

        private static void AddPointerEnter(GameObject target, System.Action action)
        {
            EventTrigger trigger = target.AddComponent<EventTrigger>();
            EventTrigger.Entry pointerEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            pointerEntry.callback.AddListener(_ => action());
            trigger.triggers.Add(pointerEntry);
        }

        private void PointRow(int rowIndex)
        {
            if (!IsCollectionInputBlocked()) SelectRow(rowIndex);
        }

        // 끝에서 더 가면 반대쪽 끝으로 (길게 누르는 중에는 끝에서 멈춤)
        private void MoveRow(int direction)
        {
            if (CW_rows.Count == 0) return;

            int next = _selectedRow + direction;
            if (CW_uiControl.isRepeating && (next < 0 || next >= CW_rows.Count)) return;

            SelectRow((next + CW_rows.Count) % CW_rows.Count);
        }

        private void SelectRow(int rowIndex)
        {
            _selectedRow = rowIndex;

            for (int i = 0; i < CW_rows.Count; i++)
            {
                CW_rows[i].SetSelected(i == rowIndex);
            }

            CollectionData.Entry entry = _collectionData.GetEntry(rowIndex);
            if (entry == null) return;

            if (_pictureImage != null)
            {
                _pictureImage.sprite = entry.EntryIcon;
                // 그림이 없으면 빈 자리만 보이도록 반투명하게 둠
                _pictureImage.color = entry.EntryIcon != null ? Color.white : new Color(1f, 1f, 1f, 0.08f);
            }
            _nameText.text = entry.EntryName;
            _groupText.text = entry.EntryGroup;
            _contentText.text = string.IsNullOrEmpty(entry.EntryDescription) ? "내용 미정" : entry.EntryDescription;
        }

        private bool IsCollectionInputBlocked()
        {
            return SettingPopup.IsAnyOpen || (CW_screenFade != null && CW_screenFade.SF_IsPlaying);
        }
    }
}
