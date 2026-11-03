using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI.Setting
{
    // 옵션 창 안의 설정 페이지(화면 설정, 그래픽 설정)에 붙여서 사용
    // 설정창 메뉴(SettingMenu)와 같은 방식으로 움직임
    // - 위/아래: 줄 이동 (잠긴 줄은 건너뜀, 길게 누르면 연속 이동하고 끝에서 멈춤)
    // - 왼쪽/오른쪽: 스테퍼 줄의 값 변경 (슬라이더 줄은 길게 누르면 연속으로 바뀜)
    // - 슬라이더 줄에서 선택 키, 키보드 O / P, 패드 LT / RT: 조정 단위(10 / 1) 바꾸기
    // - 그 외 줄에서 선택 키 / 마우스 클릭: 그 줄의 OnClick 실행 (돌아가기 등)
    // - 취소 키 / ESC: 이 페이지를 닫고 옵션 메뉴로 돌아감
    public class OptionPage : MonoBehaviour
    {
        // 페이지가 열려 있으면 뒤의 옵션 메뉴는 입력을 받지 않음
        // 닫힌 다음 프레임까지 열린 것으로 봄 (닫기를 누른 선택 키로 뒤의 메뉴가 페이지를 다시 여는 것을 막음)
        public static bool OP_IsOpen => OP_openCount > 0 || Time.frameCount - OP_closedFrame <= 1;
        private static int OP_openCount = 0;
        private static int OP_closedFrame = int.MinValue / 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOpenCount()
        {
            OP_openCount = 0;
            OP_closedFrame = int.MinValue / 2;
        }

        // 비워두면 씬에서 찾음
        [SerializeField] private UIControl OP_uiControl;

        [Tooltip("페이지의 줄 (위에서 아래 순서, 마지막은 돌아가기)")]
        [SerializeField] private Button[] _rowButtons;
        [Tooltip("누르면 이 페이지를 닫는 줄")]
        [SerializeField] private Button _backButton;

        private static readonly Color OP_selectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color OP_notSelectColor = new Color(1f, 1f, 1f, 0f);

        private int OP_selectedIndex;

        // 페이지를 연 그 프레임에는 입력을 처리하지 않음
        private int OP_ignoreInputFrame = -1;

        // 누르고 있는 동안 계속 넘어가지 않도록 이전 프레임 입력을 기억 (슬라이더 좌우는 UIControl의 Step으로 처리)
        private bool OP_prevLeft, OP_prevRight, OP_prevSelect;

        private void Awake()
        {
            if (OP_uiControl == null) OP_uiControl = FindAnyObjectByType<UIControl>(FindObjectsInactive.Include);
            if (_backButton != null) _backButton.onClick.AddListener(Close);

            // 마우스를 올리면 그 줄이 선택되도록 연결 (스테퍼 화살표에 올려도 그 줄이 선택됨)
            for (int i = 0; i < _rowButtons.Length; i++)
            {
                int index = i;
                EventTrigger trigger = _rowButtons[i].gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = _rowButtons[i].gameObject.AddComponent<EventTrigger>();

                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => SelectButton(index));
                trigger.triggers.Add(entry);
            }
        }

        private void OnEnable()
        {
            OP_openCount++;

            OP_selectedIndex = 0;
            if (_rowButtons.Length > 0 && !_rowButtons[0].IsInteractable()) Move(1);
            OP_ignoreInputFrame = Time.frameCount;
            Refresh();
            RememberInput();
        }

        private void OnDisable()
        {
            OP_openCount--;
            OP_closedFrame = Time.frameCount;
        }

        // 옵션 메뉴의 줄에서 부름
        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (OP_uiControl == null || _rowButtons.Length == 0) return;

            if (Time.frameCount == OP_ignoreInputFrame)
            {
                // 같은 프레임의 취소 / 닫기 입력은 지워서 옵션 메뉴까지 닫히지 않게 함
                OP_uiControl.uiCancelDown = false;
                OP_uiControl.uiClose = false;
                RememberInput();
                return;
            }

            // 길게 누르면 연속 이동, 연속 이동 중이면 맨 위/맨 아래에서 멈춤
            if (OP_uiControl.uiUpStep) MoveStep(-1, canWrap: !OP_uiControl.isRepeating);
            else if (OP_uiControl.uiDownStep) MoveStep(1, canWrap: !OP_uiControl.isRepeating);

            Button selected = _rowButtons[OP_selectedIndex];
            ISettingStepper stepper = selected.GetComponent<ISettingStepper>();
            if (stepper != null && selected.IsInteractable())
            {
                bool isSlider = stepper is SettingSliderStepper;
                bool isLeftStep = isSlider ? OP_uiControl.uiLeftStep : OP_uiControl.uiLeft && !OP_prevLeft;
                bool isRightStep = isSlider ? OP_uiControl.uiRightStep : OP_uiControl.uiRight && !OP_prevRight;

                if (isLeftStep) stepper.Prev();
                else if (isRightStep) stepper.Next();

                if (isSlider && (OP_uiControl.uiLeftStepSize || OP_uiControl.uiRightStepSize))
                    ((SettingSliderStepper)stepper).ToggleStepSize();
            }

            if (OP_uiControl.uiSelect && !OP_prevSelect) Submit();
            else if (OP_uiControl.uiCancelDown || OP_uiControl.uiClose)
            {
                // 같은 키로 옵션 메뉴까지 닫히지 않도록 처리한 입력은 지움
                OP_uiControl.uiCancelDown = false;
                OP_uiControl.uiClose = false;
                Close();
            }

            RememberInput();
        }

        private void RememberInput()
        {
            if (OP_uiControl == null) return;

            OP_prevLeft = OP_uiControl.uiLeft;
            OP_prevRight = OP_uiControl.uiRight;
            OP_prevSelect = OP_uiControl.uiSelect;
        }

        // 선택된 줄 실행
        private void Submit()
        {
            Button button = _rowButtons[OP_selectedIndex];
            if (!button.IsInteractable()) return;

            // 슬라이더 줄은 선택 키로 조정 단위를 바꿈
            SettingSliderStepper slider = button.GetComponent<SettingSliderStepper>();
            if (slider != null)
            {
                slider.ToggleStepSize();
                return;
            }

            // 그 외 스테퍼 줄은 선택 키로 바꾸지 않음 (값은 좌우로만 바꿈)
            if (button.GetComponent<ISettingStepper>() != null) return;
            button.onClick.Invoke();
        }

        // direction 방향으로 한 칸 이동 (canWrap이 false면 끝에서 반대쪽 끝으로 넘어가지 않고 멈춤, 잠긴 줄은 건너뜀)
        private void MoveStep(int direction, bool canWrap)
        {
            if (!canWrap)
            {
                for (int i = OP_selectedIndex + direction; i >= 0 && i < _rowButtons.Length; i += direction)
                {
                    if (!_rowButtons[i].IsInteractable()) continue;

                    OP_selectedIndex = i;
                    Refresh();
                    return;
                }
                return;
            }

            Move(direction);
            Refresh();
        }

        // direction 방향으로 고를 수 있는 다음 줄을 찾아 이동 (끝에서 반대쪽 끝으로)
        private void Move(int direction)
        {
            for (int i = 0; i < _rowButtons.Length; i++)
            {
                OP_selectedIndex = (OP_selectedIndex + direction + _rowButtons.Length) % _rowButtons.Length;
                if (_rowButtons[OP_selectedIndex].IsInteractable()) return;
            }
        }

        // 마우스를 올린 줄을 선택
        private void SelectButton(int index)
        {
            // 마우스 버튼을 누르고 있는 동안(슬라이더를 끄는 중 등)에는 다른 줄 위를 지나가도 선택을 바꾸지 않음
            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed)) return;
            if (index == OP_selectedIndex || !_rowButtons[index].IsInteractable()) return;

            OP_selectedIndex = index;
            Refresh();
        }

        // 선택된 줄만 회색, 나머지는 투명
        private void Refresh()
        {
            for (int i = 0; i < _rowButtons.Length; i++)
            {
                if (_rowButtons[i].image != null) _rowButtons[i].image.color = (i == OP_selectedIndex) ? OP_selectColor : OP_notSelectColor;
            }
        }
    }
}
