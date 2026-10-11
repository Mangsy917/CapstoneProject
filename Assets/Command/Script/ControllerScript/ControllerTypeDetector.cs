using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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
    [Tooltip("게임 시작 시 기본으로 표시할 컨트롤러 타입")]
    [SerializeField] private ControllerType defaultType = ControllerType.Keyboard;

    public ControllerType CurrentType { get; private set; }

    public event System.Action<ControllerType> OnControllerTypeChanged;

    private void Awake()
    {
        CurrentType = defaultType;
    }

    private void OnEnable()
    {
        InputSystem.onEvent += OnInputEvent;
        ApplyUI(CurrentType);
    }

    private void OnDisable()
    {
        InputSystem.onEvent -= OnInputEvent;
    }

    private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        // 상태 변화 이벤트가 아니면 무시 (연결/해제 이벤트 등 제외)
        if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
            return;

        ControllerType detected = DetectType(device);

        if (detected != CurrentType)
        {
            CurrentType = detected;
            ApplyUI(CurrentType);
            OnControllerTypeChanged?.Invoke(CurrentType);
        }
    }

    private ControllerType DetectType(InputDevice device)
    {
        if (device is Keyboard || device is Mouse)
            return ControllerType.Keyboard;

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

            return isPlayStation ? ControllerType.PlayStation : ControllerType.Xbox;
        }
        return CurrentType;
    }

    private void ApplyUI(ControllerType type)
    {
        if (keyboardUI != null) keyboardUI.SetActive(type == ControllerType.Keyboard);
        if (playStationUI != null) playStationUI.SetActive(type == ControllerType.PlayStation);
        if (xboxUI != null) xboxUI.SetActive(type == ControllerType.Xbox);
    }
}