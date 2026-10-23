using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// 마지막으로 버튼을 누른 기기로 컨트롤러 종류(키보드 / 플레이스테이션 / Xbox)를 판단함
// - 판단은 static으로 한 곳에서만 함: 이 컴포넌트가 붙은 오브젝트가 꺼져 있어도(설정창을 연 동안 등)
//   ControllerTypeDetector.CurrentType과 OnControllerTypeChanged는 계속 갱신됨
// - 버튼(키, 패드 버튼, 십자키 등)을 실제로 눌렀을 때만 바뀜
//   (가만히 있어도 계속 신호를 보내는 플레이스테이션 패드 때문에 키보드 입력 중에 바뀌지 않도록)
// - 컴포넌트로 붙이면 종류별 UI 오브젝트(keyboardUI 등) 중 현재 종류만 켜줌
public class ControllerTypeDetector : MonoBehaviour
{
    public enum ControllerType
    {
        Keyboard,
        PlayStation,
        Xbox // 그 외 모든 게임패드는 Xbox로 인식
    }

    [Header("컨트롤러 종류별 UI 오브젝트")]
    [SerializeField] private GameObject keyboardUI;
    [SerializeField] private GameObject playStationUI;
    [SerializeField] private GameObject xboxUI;

    [Header("옵션")]
    [Tooltip("아직 아무 입력도 없을 때 사용할 컨트롤러 타입")]
    [SerializeField] private ControllerType defaultType = ControllerType.Keyboard;

    // Input Actions 에셋의 키보드 / 게임패드 바인딩 그룹 이름 (Control Scheme)
    public const string KeyboardGroup = "PC";
    public const string GamepadGroup = "GamePad";

    public static ControllerType CurrentType { get; private set; } = ControllerType.Keyboard;

    public static event System.Action<ControllerType> OnControllerTypeChanged;

    private static bool _hasDetected; // 한 번이라도 기기 입력으로 판단했는지

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void StartListening()
    {
        CurrentType = ControllerType.Keyboard;
        _hasDetected = false;
        InputSystem.onEvent -= OnInputEvent; // 중복 등록 방지
        InputSystem.onEvent += OnInputEvent;
    }

    /// <summary>현재 컨트롤러 종류가 쓰는 바인딩 그룹 (키보드면 PC, 패드면 GamePad)</summary>
    public static string GetBindingGroup(ControllerType type)
    {
        return type == ControllerType.Keyboard ? KeyboardGroup : GamepadGroup;
    }

    private static void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        // 상태 변화 이벤트가 아니면 무시 (연결/해제 이벤트 등 제외)
        if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            return;

        // 버튼을 새로 누른 이벤트만 사용
        if (eventPtr.GetFirstButtonPressOrNull() == null)
            return;

        if (!TryDetectType(device, out ControllerType detected))
            return;

        _hasDetected = true;
        if (detected == CurrentType) return;

        CurrentType = detected;
        OnControllerTypeChanged?.Invoke(CurrentType);
    }

    private static bool TryDetectType(InputDevice device, out ControllerType type)
    {
        type = CurrentType;

        if (device is Keyboard || device is Mouse)
        {
            type = ControllerType.Keyboard;
            return true;
        }

        if (device is Gamepad gamepad)
        {
            string layout = gamepad.layout ?? string.Empty;
            string displayName = gamepad.displayName ?? string.Empty;

            bool isPlayStation =
                layout.Contains("DualShock") ||
                layout.Contains("DualSense") ||
                displayName.Contains("DualShock") ||
                displayName.Contains("DualSense") ||
                displayName.Contains("Wireless Controller");

            type = isPlayStation ? ControllerType.PlayStation : ControllerType.Xbox;
            return true;
        }
        return false;
    }

    private void Awake()
    {
        if (!_hasDetected && CurrentType != defaultType)
        {
            CurrentType = defaultType;
            OnControllerTypeChanged?.Invoke(CurrentType);
        }
    }

    private void OnEnable()
    {
        OnControllerTypeChanged += ApplyUI;
        ApplyUI(CurrentType);
    }

    private void OnDisable()
    {
        OnControllerTypeChanged -= ApplyUI;
    }

    private void ApplyUI(ControllerType type)
    {
        if (keyboardUI != null) keyboardUI.SetActive(type == ControllerType.Keyboard);
        if (playStationUI != null) playStationUI.SetActive(type == ControllerType.PlayStation);
        if (xboxUI != null) xboxUI.SetActive(type == ControllerType.Xbox);
    }
}
