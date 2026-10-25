using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 타이틀 화면의 UIManager에 붙여서 사용
// 타이틀 버튼들(Start, Setting, Back Title, Exit)의 OnClick에 연결할 함수도 여기에 있음
// 키보드/컨트롤러(UIControl)와 마우스가 같은 선택 상태를 공유함
// - 위/아래(W/S): 선택 이동 (끝에서 누르면 반대쪽 끝으로)
// - 마우스 올리기: 그 버튼이 선택됨
// - 선택 키(J) / 마우스 클릭: 선택된 버튼의 OnClick 실행
// - 취소 키(K): 세팅 UI가 열려 있으면 닫음
public class TitleMenu : MonoBehaviour
{
    [SerializeField] private UIControl uiControl;
    [SerializeField] private GameObject settingUI;
    // Start 버튼을 눌렀을 때 이동할 씬 이름
    [SerializeField] private string sceneName;

    // 타이틀 메뉴 버튼 (위에서 아래 순서: Start, Setting, Exit)
    [SerializeField] private Button[] mainButtons;
    // 세팅 UI 안의 버튼 (Back Title)
    [SerializeField] private Button[] settingButtons;

    [SerializeField] private Color selectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color notSelectColor = Color.white;

    // 각 화면에서 선택된 버튼 번호 (세팅 UI를 닫으면 원래 고르던 버튼으로 돌아감)
    private int mainIndex = 0;
    private int settingIndex = 0;

    private bool wasSettingOpen;

    // 누르고 있는 동안 계속 넘어가지 않도록 이전 프레임 입력을 기억
    private bool prevUp, prevDown, prevSelect;

    void Awake()
    {
        // 모든 버튼에 마우스를 올리면 선택되도록 연결
        for (int i = 0; i < mainButtons.Length; i++)
        {
            int index = i;
            AddHover(mainButtons[i], () => { mainIndex = index; Refresh(); });
        }
        for (int i = 0; i < settingButtons.Length; i++)
        {
            int index = i;
            AddHover(settingButtons[i], () => { settingIndex = index; Refresh(); });
        }
    }

    void Start()
    {
        settingUI.SetActive(false);
        wasSettingOpen = IsSettingOpen();
        Refresh();
    }

    void Update()
    {
        // 세팅 UI가 열리거나 닫히면 그 화면의 선택 표시를 다시 맞춤
        bool settingOpen = IsSettingOpen();
        if (settingOpen != wasSettingOpen)
        {
            wasSettingOpen = settingOpen;
            if (settingOpen) settingIndex = 0;
            Refresh();
        }

        if (uiControl == null) return;

        bool up = uiControl.uiUp;
        bool down = uiControl.uiDown;
        bool select = uiControl.uiSelect;

        // 컨트롤러 설정 팝업이 열려 있으면 입력만 기억하고 메뉴는 움직이지 않음
        if (Game.UI.Setting.KeyBindingWindow.IsOpen)
        {
            prevUp = up;
            prevDown = down;
            prevSelect = select;
            return;
        }

        if (up && !prevUp) Move(-1);
        else if (down && !prevDown) Move(1);

        if (select && !prevSelect) Submit();
        else if (uiControl.uiCancelDown && settingOpen)
        {
            uiControl.uiCancelDown = false;
            PressSettingBackBT();
        }

        prevUp = up;
        prevDown = down;
        prevSelect = select;
    }

    public void PressStartBT()
    {
        LoadingScene.LoadScene(sceneName);
    }

    public void PressSettingBT()
    {
        settingUI.SetActive(true);
    }

    public void PressSettingBackBT()
    {
        settingUI.SetActive(false);
    }

    public void PressExitBT()
    {
        Application.Quit();
        // 에디터 전용 코드는 빌드에 들어가면 빌드가 실패하므로 에디터에서만 컴파일
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private bool IsSettingOpen()
    {
        return settingUI != null && settingUI.activeSelf;
    }

    // 지금 화면의 버튼 목록
    private Button[] CurrentButtons()
    {
        return IsSettingOpen() ? settingButtons : mainButtons;
    }

    private int CurrentIndex()
    {
        return IsSettingOpen() ? settingIndex : mainIndex;
    }

    // dir -1: 위, 1: 아래
    private void Move(int dir)
    {
        Button[] buttons = CurrentButtons();
        if (buttons.Length == 0) return;

        int index = (CurrentIndex() + dir + buttons.Length) % buttons.Length;
        if (IsSettingOpen()) settingIndex = index;
        else mainIndex = index;

        Refresh();
    }

    // 선택된 버튼 실행
    private void Submit()
    {
        Button[] buttons = CurrentButtons();
        if (buttons.Length == 0) return;

        Button button = buttons[CurrentIndex()];
        if (button.interactable) button.onClick.Invoke();
    }

    private void AddHover(Button button, UnityEngine.Events.UnityAction onEnter)
    {
        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => onEnter());
        trigger.triggers.Add(entry);
    }

    // 지금 화면에서 선택된 버튼만 회색, 나머지는 원래 색
    private void Refresh()
    {
        Button[] buttons = CurrentButtons();
        int selected = CurrentIndex();

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].image != null) buttons[i].image.color = (i == selected) ? selectColor : notSelectColor;
        }
    }
}
