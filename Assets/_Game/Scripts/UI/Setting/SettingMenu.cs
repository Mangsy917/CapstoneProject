using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// SettingWindow에 붙여서 사용
// 키보드/컨트롤러(UIControl)와 마우스가 같은 선택 상태를 공유함
// - 위/아래: 선택 이동 (끝에서 누르면 반대쪽 끝으로)
// - 마우스 올리기: 그 버튼이 선택됨
// - 선택 키 / 마우스 클릭: 선택된 버튼의 OnClick 실행
// 선택된 버튼만 배경이 회색으로 바뀌고, 텍스트 색은 그대로 둠
// 잠긴 줄(Interactable 꺼짐)은 위/아래 이동과 마우스 올리기로 선택되지 않음
public class SettingMenu : MonoBehaviour
{
    [SerializeField] private UIControl uiControl;

    private static readonly Color selectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    private static readonly Color notSelectColor = new Color(1f, 1f, 1f, 0f);

    // 지금 켜져 있는 패널 (PracticeSetting, Parameter ...)
    private Transform currentPanel;
    private Button[] buttons;

    // 지금 선택된 버튼 번호 (패널이 바뀌면 0번부터)
    private int selectedIndex = 0;

    // 누르고 있는 동안 계속 넘어가지 않도록 이전 프레임 입력을 기억
    private bool prevUp, prevDown, prevLeft, prevRight, prevSelect;

    void Awake()
    {
        // 모든 메뉴 줄에 마우스를 올리면 선택되도록 연결
        // (스테퍼 화살표에 마우스를 올려도 부모인 메뉴 줄이 선택됨)
        // (지금은 꺼져 있지만 나중에 켜지는 줄도 포함)
        foreach (Transform panel in transform)
        foreach (Button button in GetRowButtons(panel, true))
        {
            Button target = button;
            EventTrigger trigger = target.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = target.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => SelectButton(target));
            trigger.triggers.Add(entry);
        }
    }

    void OnEnable()
    {
        // 설정창을 다시 열면 첫 번째 버튼부터 선택
        currentPanel = null;
    }

    // Update is called once per frame
    void Update()
    {
        CheckPanelChanged();
        if (!HasButtons() || uiControl == null) return;

        bool up = uiControl.uiUp;
        bool down = uiControl.uiDown;
        bool left = uiControl.uiLeft;
        bool right = uiControl.uiRight;
        bool select = uiControl.uiSelect;

        // 팝업이 열려 있으면 입력만 기억하고 메뉴는 움직이지 않음
        if (!SettingPopup.IsAnyOpen)
        {
            if (up && !prevUp) MoveUp();
            else if (down && !prevDown) MoveDown();

            // 선택된 줄에 스테퍼가 있으면 좌우로 값 변경
            // 오른쪽 = 증가, 왼쪽 = 감소
            ISettingStepper stepper = buttons[selectedIndex].GetComponent<ISettingStepper>();
            if (stepper != null && CanSelect(buttons[selectedIndex]))
            {
                if (left && !prevLeft) stepper.Prev();
                else if (right && !prevRight) stepper.Next();
            }

            if (select && !prevSelect) Submit();
        }

        prevUp = up;
        prevDown = down;
        prevLeft = left;
        prevRight = right;
        prevSelect = select;
    }

    // 탭이 바뀌어서 켜진 패널이 달라졌으면 버튼 목록을 새로 만듦
    private void CheckPanelChanged()
    {
        Transform activePanel = null;
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeSelf)
            {
                activePanel = child;
                break;
            }
        }

        if (activePanel == currentPanel) return;

        currentPanel = activePanel;
        buttons = currentPanel != null ? GetRowButtons(currentPanel, false) : new Button[0];

        selectedIndex = 0;
        if (HasButtons() && !CanSelect(buttons[0])) Move(1);
        Refresh();
    }

    // 줄이 켜지거나 꺼졌을 때 버튼 목록을 다시 만듦 (선택은 최대한 유지)
    // DummySettingRule처럼 줄을 켜고 끄는 스크립트에서 호출
    public void RefreshRows()
    {
        if (currentPanel == null) return;

        Button selected = HasButtons() ? buttons[selectedIndex] : null;
        buttons = GetRowButtons(currentPanel, false);
        if (!HasButtons()) return;

        int index = System.Array.IndexOf(buttons, selected);
        selectedIndex = Mathf.Clamp(index, 0, buttons.Length - 1);
        if (!CanSelect(buttons[selectedIndex])) Move(1);
        Refresh();
    }

    // 패널 바로 아래의 메뉴 줄 버튼만 모음 (스테퍼 화살표 버튼은 제외)
    private Button[] GetRowButtons(Transform panel, bool includeInactive)
    {
        System.Collections.Generic.List<Button> rows = new System.Collections.Generic.List<Button>();
        foreach (Transform child in panel)
        {
            Button button = child.GetComponent<Button>();
            if (button != null && (includeInactive || child.gameObject.activeSelf)) rows.Add(button);
        }
        return rows.ToArray();
    }

    // 선택할 수 있는 줄인지 (잠긴 줄은 건너뜀)
    private bool CanSelect(Button button)
    {
        return button.IsInteractable();
    }

    // 위 버튼으로 (처음에서 누르면 끝으로)
    public void MoveUp()
    {
        if (!HasButtons()) return;

        Move(-1);
        Refresh();
    }

    // 아래 버튼으로 (끝에서 누르면 처음으로)
    public void MoveDown()
    {
        if (!HasButtons()) return;

        Move(1);
        Refresh();
    }

    // dir 방향으로 선택할 수 있는 다음 줄을 찾아 이동 (잠긴 줄은 건너뜀)
    private void Move(int dir)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            selectedIndex = (selectedIndex + dir + buttons.Length) % buttons.Length;
            if (CanSelect(buttons[selectedIndex])) return;
        }
    }

    // 선택된 버튼 실행
    public void Submit()
    {
        if (!HasButtons()) return;

        Button button = buttons[selectedIndex];
        if (!CanSelect(button)) return;

        // 스테퍼 줄은 선택 키로 다음 값으로 넘김
        ISettingStepper stepper = button.GetComponent<ISettingStepper>();
        if (stepper != null) stepper.Next();
        else button.onClick.Invoke();
    }

    // 마우스를 올린 버튼을 선택
    private void SelectButton(Button button)
    {
        if (!HasButtons()) return;

        int index = System.Array.IndexOf(buttons, button);
        if (index < 0 || index == selectedIndex || !CanSelect(button)) return;

        selectedIndex = index;
        Refresh();
    }

    private bool HasButtons()
    {
        return buttons != null && buttons.Length > 0;
    }

    // 선택된 버튼만 회색, 나머지는 투명
    private void Refresh()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].image != null) buttons[i].image.color = (i == selectedIndex) ? selectColor : notSelectColor;
        }
    }
}
