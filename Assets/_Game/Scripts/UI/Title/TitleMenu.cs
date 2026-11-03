using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 타이틀 화면의 UIManager에 붙여서 사용
// 타이틀 버튼들(Start, Option, Exit)의 OnClick에 연결할 함수도 여기에 있음
// (Option 버튼은 옵션 창(P_OptionWindow)의 Open에 바로 연결)
// 키보드/컨트롤러(UIControl)와 마우스가 같은 선택 상태를 공유함
// - 위/아래(W/S): 선택 이동 (끝에서 누르면 반대쪽 끝으로)
// - 마우스 올리기: 그 버튼이 선택됨
// - 선택 키(J) / 마우스 클릭: 선택된 버튼의 OnClick 실행
// 옵션 창이 열려 있는 동안에는 입력을 받지 않음 (취소 키로 닫는 것은 옵션 창이 처리)
public class TitleMenu : MonoBehaviour
{
    [SerializeField] private UIControl uiControl;
    // Start 버튼을 눌렀을 때 이동할 씬 이름
    [SerializeField] private string sceneName;

    // 타이틀 메뉴 버튼 (위에서 아래 순서: Start, Option, Exit)
    [SerializeField] private Button[] mainButtons;

    [SerializeField] private Color selectColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color notSelectColor = Color.white;

    // 선택된 버튼 번호 (옵션 창을 닫으면 원래 고르던 버튼으로 돌아감)
    private int mainIndex = 0;

    // 누르고 있는 동안 계속 넘어가지 않도록 이전 프레임 입력을 기억
    private bool prevSelect;

    void Awake()
    {
        // 모든 버튼에 마우스를 올리면 선택되도록 연결
        for (int i = 0; i < mainButtons.Length; i++)
        {
            int index = i;
            AddHover(mainButtons[i], () =>
            {
                if (IsWindowOpen()) return;
                mainIndex = index;
                Refresh();
            });
        }
    }

    void Start()
    {
        Refresh();
    }

    void Update()
    {
        if (uiControl == null) return;

        bool select = uiControl.uiSelect;

        // 옵션 창(공격 버튼 설정 창 포함)이 열려 있으면 입력만 기억하고 메뉴는 움직이지 않음
        if (IsWindowOpen())
        {
            prevSelect = select;
            return;
        }

        // 길게 눌러 연속 이동 중이면 맨 위/맨 아래에서 멈춤
        if (uiControl.uiUpStep) Move(-1, canWrap: !uiControl.isRepeating);
        else if (uiControl.uiDownStep) Move(1, canWrap: !uiControl.isRepeating);

        if (select && !prevSelect) Submit();

        prevSelect = select;
    }

    public void PressStartBT()
    {
        LoadingScene.LoadScene(sceneName);
    }

    public void PressExitBT()
    {
        Application.Quit();
        // 에디터 전용 코드는 빌드에 들어가면 빌드가 실패하므로 에디터에서만 컴파일
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private static bool IsWindowOpen()
    {
        return Game.UI.Setting.OptionWindow.OW_IsOpen || Game.UI.Setting.KeyBindingWindow.IsOpen;
    }

    // dir -1: 위, 1: 아래
    // canWrap: 끝에서 반대쪽 끝으로 넘어갈 수 있는지 (false면 끝에서 멈춤)
    private void Move(int dir, bool canWrap = true)
    {
        if (mainButtons.Length == 0) return;

        int next = mainIndex + dir;
        if (!canWrap && (next < 0 || next >= mainButtons.Length)) return;

        mainIndex = (next + mainButtons.Length) % mainButtons.Length;
        Refresh();
    }

    // 선택된 버튼 실행
    private void Submit()
    {
        if (mainButtons.Length == 0) return;

        Button button = mainButtons[mainIndex];
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

    // 선택된 버튼만 회색, 나머지는 원래 색
    private void Refresh()
    {
        for (int i = 0; i < mainButtons.Length; i++)
        {
            if (mainButtons[i].image != null) mainButtons[i].image.color = (i == mainIndex) ? selectColor : notSelectColor;
        }
    }
}
