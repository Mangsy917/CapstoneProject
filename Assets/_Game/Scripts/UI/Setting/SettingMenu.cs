using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// SettingWindow에 붙여서 사용
// 키보드/컨트롤러(UIControl)와 마우스가 같은 선택 상태를 공유함
// - 위/아래: 선택 이동 (끝에서 누르면 반대쪽 끝으로)
// - 마우스 올리기: 그 버튼이 선택됨
// - 선택 키 / 마우스 클릭: 선택된 버튼의 OnClick 실행
// 선택된 버튼만 배경이 회색으로 바뀌고, 텍스트 색은 그대로 둠
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
    private bool prevUp, prevDown, prevSelect;

    void Awake()
    {
        // 모든 버튼에 마우스를 올리면 선택되도록 연결
        foreach (Button button in GetComponentsInChildren<Button>(true))
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
        bool select = uiControl.uiSelect;

        // 팝업이 열려 있으면 입력만 기억하고 메뉴는 움직이지 않음
        if (!SettingPopup.IsAnyOpen)
        {
            if (up && !prevUp) MoveUp();
            else if (down && !prevDown) MoveDown();

            if (select && !prevSelect) Submit();
        }

        prevUp = up;
        prevDown = down;
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
        buttons = currentPanel != null ? currentPanel.GetComponentsInChildren<Button>() : new Button[0];

        selectedIndex = 0;
        Refresh();
    }

    // 위 버튼으로 (처음에서 누르면 끝으로)
    public void MoveUp()
    {
        if (!HasButtons()) return;

        selectedIndex = (selectedIndex - 1 + buttons.Length) % buttons.Length;
        Refresh();
    }

    // 아래 버튼으로 (끝에서 누르면 처음으로)
    public void MoveDown()
    {
        if (!HasButtons()) return;

        selectedIndex = (selectedIndex + 1) % buttons.Length;
        Refresh();
    }

    // 선택된 버튼 실행
    public void Submit()
    {
        if (!HasButtons()) return;

        Button button = buttons[selectedIndex];
        if (button.interactable) button.onClick.Invoke();
    }

    // 마우스를 올린 버튼을 선택
    private void SelectButton(Button button)
    {
        if (!HasButtons()) return;

        int index = System.Array.IndexOf(buttons, button);
        if (index < 0 || index == selectedIndex) return;

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
