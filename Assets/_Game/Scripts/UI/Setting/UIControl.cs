using UnityEngine;
using UnityEngine.InputSystem;

public class UIControl : MonoBehaviour
{
    [SerializeField] private InputActionAsset uiControl;
    [SerializeField] private string actionMapName = "UI";

    private InputAction Up, Down, Left, Right;
    private InputAction LeftPage, RightPage, Select, Cancel, Close;

    [Header("위/아래 길게 누르기")]
    [Tooltip("처음 한 칸 이동한 뒤 연속 이동이 시작되기까지 기다리는 시간(초)")]
    [SerializeField] private float repeatDelay = 0.4f;
    [Tooltip("연속 이동할 때 한 칸씩 넘어가는 간격(초)")]
    [SerializeField] private float repeatInterval = 0.04f;

    // 다음에 한 칸 더 넘어갈 시각 (실제 시간 기준, 일시정지나 슬로우와 상관없이 동작)
    private float nextUpRepeatTime;
    private float nextDownRepeatTime;
    // 이전 프레임에 위/아래를 누르고 있었는지
    private bool wasUpHeld;
    private bool wasDownHeld;

    // 위/아래는 누른 순간 1프레임 true, 계속 누르고 있으면 repeatDelay 뒤부터 repeatInterval마다 1프레임씩 true
    // (메뉴는 false -> true로 바뀌는 순간 한 칸 이동하므로, 길게 누르면 주르륵 넘어감)
    public bool uiUp;
    public bool uiDown;
    // 이번 프레임의 위/아래 true가 길게 눌러서 생긴 연속 이동이면 true (처음 누른 순간은 false)
    // 메뉴는 연속 이동 중이면 맨 위/맨 아래에서 반대쪽 끝으로 넘어가지 않고 멈춤
    public bool isRepeating;
    public bool uiLeft;
    public bool uiRight;
    public bool uiLeftPG;
    public bool uiRightPG;
    public bool uiSelect;
    // 취소 키를 누른 그 프레임에만 true (창 닫기처럼 한 번만 처리할 때 사용, 처리한 쪽에서 false로 지움)
    public bool uiCancelDown;
    public bool uiClose;

    void Awake()
    {
        if (uiControl == null)
        {
            Debug.LogError("[InputReader] uiControl이 할당되지 않았습니다.");
            enabled = false;
            return;
        }

        // 컨트롤러 설정에서 바꾼 키를 적용 (씬을 넘어가도, 빌드에서는 껐다 켜도 유지)
        Game.Controller.KeyBindingStore.Apply(uiControl);

        var map = uiControl.FindActionMap(actionMapName, throwIfNotFound: true);

        Up = map.FindAction("Up");
        Down = map.FindAction("Down");
        Left = map.FindAction("Left");
        Right = map.FindAction("Right");

        LeftPage = map.FindAction("LeftPage");
        RightPage = map.FindAction("RightPage");
        Select = map.FindAction("Select");
        Cancel = map.FindAction("Cancel");

        Close = map.FindAction("BackToGame");
    }

    private void OnEnable()
    {
        uiControl?.FindActionMap(actionMapName)?.Enable();
    }

    private void OnDisable()
    {
        uiControl?.FindActionMap(actionMapName)?.Disable();
    }

    void Update()
    {
        ReadAndApply();
    }

    private void ReadAndApply()
    {
        bool up = GetRepeatPulse(Up != null && Up.IsPressed(), wasPulsed: uiUp, ref wasUpHeld, ref nextUpRepeatTime, out bool isUpRepeat);
        bool down = GetRepeatPulse(Down != null && Down.IsPressed(), wasPulsed: uiDown, ref wasDownHeld, ref nextDownRepeatTime, out bool isDownRepeat);
        isRepeating = isUpRepeat || isDownRepeat;
        bool left = Left != null && Left.IsPressed();
        bool right = Right != null && Right.IsPressed();

        bool leftPage = LeftPage != null && LeftPage.WasPressedThisFrame();
        bool rightPage = RightPage != null && RightPage.WasPressedThisFrame();
        bool select = Select != null && Select.IsPressed();
        bool cancelDown = Cancel != null && Cancel.WasPressedThisFrame();

        bool close = Close != null && Close.WasPressedThisFrame();

        UpdateButton(up, down, left, right, leftPage, rightPage, select, cancelDown, close);
    }

    // 누른 순간 true, 계속 누르면 repeatDelay 뒤부터 repeatInterval마다 한 프레임씩 true, 그 사이는 false
    // wasPulsed: 이전 프레임에 true를 냈는지 (true 다음 프레임은 꼭 false로 해서 메뉴가 새로 누른 것으로 보게 함)
    // isRepeat: 이번 true가 길게 눌러서 생긴 연속 이동인지
    private bool GetRepeatPulse(bool isHeld, bool wasPulsed, ref bool wasHeld, ref float nextRepeatTime, out bool isRepeat)
    {
        float now = Time.unscaledTime;
        isRepeat = false;

        if (!isHeld)
        {
            wasHeld = false;
            return false;
        }

        if (!wasHeld)
        {
            wasHeld = true;
            nextRepeatTime = now + repeatDelay;
            return true;
        }

        if (wasPulsed || now < nextRepeatTime) return false;

        nextRepeatTime = now + repeatInterval;
        isRepeat = true;
        return true;
    }

    private void UpdateButton(bool up, bool down, bool left, bool right,
                              bool leftPage, bool rightPage, bool select,
                              bool cancelDown, bool close)
    {
        uiUp = up;
        uiDown = down;
        uiLeft = left;
        uiRight = right;

        uiLeftPG = leftPage;
        uiRightPG = rightPage;
        uiSelect = select;
        uiCancelDown = cancelDown;

        uiClose = close;
    }
}