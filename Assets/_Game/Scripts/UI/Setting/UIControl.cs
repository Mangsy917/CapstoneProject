using UnityEngine;
using UnityEngine.InputSystem;

public class UIControl : MonoBehaviour
{
    [SerializeField] private InputActionAsset uiControl;
    [SerializeField] private string actionMapName = "UI";

    private InputAction Up, Down, Left, Right;
    private InputAction LeftPage, RightPage, Select, Cancel, Close;
    private InputAction LeftStepSize, RightStepSize;

    [Header("방향 길게 누르기")]
    [Tooltip("처음 한 칸 이동한 뒤 연속 이동이 시작되기까지 기다리는 시간(초)")]
    [SerializeField] private float repeatDelay = 0.4f;
    [Tooltip("연속 이동할 때 한 칸씩 넘어가는 간격(초)")]
    [SerializeField] private float repeatInterval = 0.08f / 3f;

    // 다음에 한 칸 더 넘어갈 시각 (실제 시간 기준, 일시정지나 슬로우와 상관없이 동작)
    private float nextUpRepeatTime;
    private float nextDownRepeatTime;
    private float nextLeftRepeatTime;
    private float nextRightRepeatTime;
    // 이전 프레임에 그 방향을 누르고 있었는지
    private bool wasUpHeld;
    private bool wasDownHeld;
    private bool wasLeftHeld;
    private bool wasRightHeld;

    // 방향 키를 누르고 있는 동안 true
    public bool uiUp;
    public bool uiDown;
    public bool uiLeft;
    public bool uiRight;

    // 이번 프레임에 한 칸 움직여야 하면 true
    // 누른 순간 true, 계속 누르고 있으면 repeatDelay 뒤부터 repeatInterval마다 true (연속 이동)
    public bool uiUpStep;
    public bool uiDownStep;
    public bool uiLeftStep;
    public bool uiRightStep;
    // 이번 프레임의 위/아래 Step이 길게 눌러서 생긴 연속 이동이면 true (처음 누른 순간은 false)
    // 메뉴는 연속 이동 중이면 맨 위/맨 아래에서 반대쪽 끝으로 넘어가지 않고 멈춤
    public bool isRepeating;

    public bool uiLeftPG;
    public bool uiRightPG;
    // 슬라이더 조정 단위(10% / 1%) 바꾸기 키 (패드 LT / RT, 누른 순간 1프레임만 true)
    public bool uiLeftStepSize;
    public bool uiRightStepSize;
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

        LeftStepSize = map.FindAction("LeftStepSize");
        RightStepSize = map.FindAction("RightStepSize");
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
        bool up = Up != null && Up.IsPressed();
        bool down = Down != null && Down.IsPressed();
        bool left = Left != null && Left.IsPressed();
        bool right = Right != null && Right.IsPressed();

        uiUpStep = GetStep(up, ref wasUpHeld, ref nextUpRepeatTime, out bool isUpRepeat);
        uiDownStep = GetStep(down, ref wasDownHeld, ref nextDownRepeatTime, out bool isDownRepeat);
        uiLeftStep = GetStep(left, ref wasLeftHeld, ref nextLeftRepeatTime, out _);
        uiRightStep = GetStep(right, ref wasRightHeld, ref nextRightRepeatTime, out _);
        isRepeating = isUpRepeat || isDownRepeat;

        bool leftPage = LeftPage != null && LeftPage.WasPressedThisFrame();
        bool rightPage = RightPage != null && RightPage.WasPressedThisFrame();
        bool select = Select != null && Select.IsPressed();
        bool cancelDown = Cancel != null && Cancel.WasPressedThisFrame();

        bool close = Close != null && Close.WasPressedThisFrame();

        uiLeftStepSize = LeftStepSize != null && LeftStepSize.WasPressedThisFrame();
        uiRightStepSize = RightStepSize != null && RightStepSize.WasPressedThisFrame();

        UpdateButton(up, down, left, right, leftPage, rightPage, select, cancelDown, close);
    }

    // 누른 순간 true, 계속 누르면 repeatDelay 뒤부터 repeatInterval마다 true
    // (간격이 한 프레임보다 짧지 않아도 평균 속도가 맞도록 다음 시각을 이전 예정 시각 기준으로 더함)
    // isRepeat: 이번 true가 길게 눌러서 생긴 연속 이동인지
    private bool GetStep(bool isHeld, ref bool wasHeld, ref float nextRepeatTime, out bool isRepeat)
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

        if (now < nextRepeatTime) return false;

        // 한 프레임에 한 칸만 움직임. 프레임이 크게 밀렸으면 지금부터 다시 셈
        nextRepeatTime += repeatInterval;
        if (nextRepeatTime < now) nextRepeatTime = now + repeatInterval;
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
