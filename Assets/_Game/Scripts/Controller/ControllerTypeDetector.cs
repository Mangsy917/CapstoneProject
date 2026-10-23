using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// 마지막으로 버튼을 누른 기기로 컨트롤러 종류(키보드 / 플레이스테이션 / Xbox)를 판단함
// - 오브젝트에 붙이지 않아도 게임이 시작되면 알아서 입력을 듣기 시작함 (씬, 오브젝트 상태와 상관없음)
// - 버튼(키, 패드 버튼, 십자키 등)을 실제로 눌렀을 때만 바뀜
//   (가만히 있어도 계속 신호를 보내는 플레이스테이션 패드 때문에 키보드 입력 중에 바뀌지 않도록)
// - 쓰는 쪽에서는 CurrentType을 읽고, OnControllerTypeChanged를 구독해서 UI를 바꿈
public static class ControllerTypeDetector
{
    public enum ControllerType
    {
        Keyboard,
        PlayStation,
        Xbox // 그 외 모든 게임패드는 Xbox로 인식
    }

    // Input Actions 에셋의 키보드 / 게임패드 바인딩 그룹 이름 (Control Scheme)
    public const string KeyboardGroup = "PC";
    public const string GamepadGroup = "GamePad";

    public static ControllerType CurrentType { get; private set; } = ControllerType.Keyboard;

    public static event System.Action<ControllerType> OnControllerTypeChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void StartListening()
    {
        CurrentType = ControllerType.Keyboard;
        InputSystem.onEvent -= OnInputEvent; // 중복 등록 방지
        InputSystem.onEvent += OnInputEvent;
    }

    /// <summary>컨트롤러 종류가 쓰는 바인딩 그룹 (키보드면 PC, 패드면 GamePad)</summary>
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
}
