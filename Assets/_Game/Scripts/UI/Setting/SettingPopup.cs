using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 예 / 아니오 팝업에 붙여서 사용
// 키보드/컨트롤러(UIControl)와 마우스가 같은 선택 상태를 공유함
// - 왼쪽/오른쪽(위/아래): 예 <-> 아니오 이동
// - 마우스 올리기: 그 버튼이 선택됨
// - 선택 키 / 마우스 클릭: 선택된 버튼의 OnClick 실행
// - 취소 키: 아니오 버튼의 OnClick 실행
// 예, 아니오를 눌렀을 때 할 일은 각 버튼의 OnClick에서 연결
public class SettingPopup : MonoBehaviour
{
    // 팝업이 하나라도 열려 있으면 뒤의 설정 메뉴는 입력을 받지 않음
    public static bool IsAnyOpen => openCount > 0;
    private static int openCount = 0;

    // Domain Reload를 꺼둔 상태로 플레이해도 이전 값이 남지 않도록 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpenCount()
    {
        openCount = 0;
    }

    [SerializeField] private UIControl uiControl;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    private static readonly Color selectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    private static readonly Color notSelectColor = new Color(1f, 1f, 1f, 0f);

    // 0: 예, 1: 아니오
    private Button[] buttons;
    private int selectedIndex = 1;

    // 누르고 있는 동안 계속 넘어가지 않도록 이전 프레임 입력을 기억
    private bool prevMove, prevSelect;

    void Awake()
    {
        buttons = new Button[] { yesButton, noButton };

        // 마우스를 올리면 그 버튼이 선택되도록 연결
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            EventTrigger trigger = buttons[i].gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = buttons[i].gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => SelectButton(index));
            trigger.triggers.Add(entry);
        }
    }

    void OnEnable()
    {
        openCount++;

        // 실수로 넘어가지 않도록 처음엔 아니오를 선택
        selectedIndex = 1;
        Refresh();

        // 팝업을 연 키를 아직 누르고 있어도 바로 실행되지 않도록 현재 입력을 기억
        if (uiControl != null)
        {
            prevMove = IsMovePressed();
            prevSelect = uiControl.uiSelect;
        }
    }

    void OnDisable()
    {
        openCount--;
    }

    // Update is called once per frame
    void Update()
    {
        if (uiControl == null) return;

        bool move = IsMovePressed();
        bool select = uiControl.uiSelect;

        if (move && !prevMove) SelectButton(1 - selectedIndex);

        if (select && !prevSelect) Submit();
        else if (uiControl.uiCancelDown)
        {
            // 같은 키로 설정창까지 닫히지 않도록 처리한 입력은 지움
            uiControl.uiCancelDown = false;
            noButton.onClick.Invoke();
        }

        prevMove = move;
        prevSelect = select;
    }

    private bool IsMovePressed()
    {
        return uiControl.uiLeft || uiControl.uiRight || uiControl.uiUp || uiControl.uiDown;
    }

    // 선택된 버튼 실행
    private void Submit()
    {
        Button button = buttons[selectedIndex];
        if (button.interactable) button.onClick.Invoke();
    }

    private void SelectButton(int index)
    {
        selectedIndex = index;
        Refresh();
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
