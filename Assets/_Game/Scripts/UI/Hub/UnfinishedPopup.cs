using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI.Hub
{
    // '미완성된 시스템입니다' 팝업 (아직 제작되지 않은 메뉴를 고르면 뜸)
    // 확인을 누르면 닫히고, 팝업을 열기 전 화면으로 돌아감
    // - 선택 키 / 확인 버튼 클릭: 닫기
    // - 취소 키 / ESC / 마우스 우클릭: 닫기
    // 열려 있는 동안 뒤의 메뉴는 입력을 받지 않음 (SettingPopup.IsAnyOpen)
    public class UnfinishedPopup : MonoBehaviour
    {
        private static readonly Color _confirmHighlightColor = Game.UI.MenuColors.Highlight;

        // 비워두면 씬에서 찾음
        [SerializeField] private UIControl UP_uiControl;
        [SerializeField] private Button _confirmButton;

        // 누르고 있는 동안 계속 실행되지 않도록 이전 프레임 선택 키를 기억
        private bool UP_lastSelect;

        void Awake()
        {
            if (UP_uiControl == null) UP_uiControl = FindAnyObjectByType<UIControl>(FindObjectsInactive.Include);
            if (_confirmButton != null) _confirmButton.onClick.AddListener(Close);
        }

        void OnEnable()
        {
            SettingPopup.AddOpenPopup();

            // 버튼이 하나뿐이라 항상 선택된 상태로 보임
            if (_confirmButton != null)
            {
                if (_confirmButton.image != null) _confirmButton.image.color = _confirmHighlightColor;
                SelectedFontSize.Apply(_confirmButton, isSelected: true);
            }

            // 팝업을 연 키를 아직 누르고 있어도 바로 닫히지 않도록 현재 입력을 기억
            if (UP_uiControl != null) UP_lastSelect = UP_uiControl.uiSelect;
        }

        void OnDisable()
        {
            SettingPopup.RemoveOpenPopup();
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        void Update()
        {
            bool isRightClicked = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

            if (UP_uiControl == null)
            {
                if (isRightClicked) Close();
                return;
            }

            bool select = UP_uiControl.uiSelect;

            if (select && !UP_lastSelect)
            {
                Close();
            }
            else if (UP_uiControl.uiCancelDown || UP_uiControl.uiClose || isRightClicked)
            {
                // 같은 키로 뒤의 메뉴까지 뒤로 가지 않도록 처리한 입력은 지움
                UP_uiControl.uiCancelDown = false;
                UP_uiControl.uiClose = false;
                Close();
            }

            UP_lastSelect = select;
        }
    }
}
