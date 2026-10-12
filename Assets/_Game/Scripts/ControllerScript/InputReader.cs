using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 키보드 / 컨트롤러 입력을 읽어서 방향과 버튼 상태로 정리합니다.
/// 커맨드(기술) 판정은 CharacterMove에서 처리합니다.
///
/// 다른 스크립트보다 먼저 실행되도록 실행 순서를 앞당겨 두었습니다.
/// (CharacterMove가 같은 프레임의 최신 입력을 읽을 수 있도록)
/// </summary>
[DefaultExecutionOrder(-100)]
public class InputReader : MonoBehaviour
{
    [Header("사용할 Input Actions 에셋")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";

    private InputAction moveUp, moveDown, moveLeft, moveRight;
    private InputAction attackL, attackM, attackH, attackEX;

    [Header("컨트롤러 방향 상태")]
    public bool ControllerDL = false;
    public bool ControllerD = false;
    public bool ControllerDR = false;
    public bool ControllerL = false;
    public bool ControllerN = false;
    public bool ControllerR = false;
    public bool ControllerUL = false;
    public bool ControllerU = false;
    public bool ControllerUR = false;

    [Header("컨트롤러 공격 버튼 상태 (누르고 있는 동안 true)")]
    public bool ButtonL = false;
    public bool ButtonM = false;
    public bool ButtonH = false;
    public bool ButtonEX = false;

    [Header("이번 프레임에 새로 눌린 버튼 (누른 순간 1프레임만 true)")]
    public bool ButtonLDown = false;
    public bool ButtonMDown = false;
    public bool ButtonHDown = false;
    public bool ButtonEXDown = false;

    /// <summary>현재 방향을 넘버패드 숫자(1~9)로 표현한 값. 5 = 중립</summary>
    public int Direction { get; private set; } = 5;

    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogError("[InputReader] InputActionAsset이 할당되지 않았습니다.");
            enabled = false;
            return;
        }

        var map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);

        moveUp = map.FindAction("Up");
        moveDown = map.FindAction("Down");
        moveLeft = map.FindAction("Left");
        moveRight = map.FindAction("Right");

        attackL = map.FindAction("AttackL");
        attackM = map.FindAction("AttackM");
        attackH = map.FindAction("AttackH");
        attackEX = map.FindAction("AttackEX");

        map.Enable();

        // Input Actions 에셋에 아직 AttackM/H/EX 등이 만들어져 있지 않아도(예: L만 먼저 테스트하는 경우)
        // 에러 없이 동작하도록, 못 찾은 액션은 null로 남겨두고 경고만 출력한다.
        // (null인 액션은 "안 눌림"으로 취급한다.)
        WarnIfActionMissing(moveUp, "Up");
        WarnIfActionMissing(moveDown, "Down");
        WarnIfActionMissing(moveLeft, "Left");
        WarnIfActionMissing(moveRight, "Right");
        WarnIfActionMissing(attackL, "AttackL");
        WarnIfActionMissing(attackM, "AttackM");
        WarnIfActionMissing(attackH, "AttackH");
        WarnIfActionMissing(attackEX, "AttackEX");
    }

    private void OnDisable()
    {
        inputActions?.FindActionMap(actionMapName)?.Disable();
    }

    private void WarnIfActionMissing(InputAction action, string actionName)
    {
        if (action == null)
        {
            Debug.LogWarning($"[InputReader] '{actionName}' 액션을 찾지 못했습니다. 해당 입력은 항상 '안 눌림'으로 처리됩니다. " +
                              "(예: L만 먼저 테스트하는 중이라면 무시해도 됩니다.)");
        }
    }

    private void Update()
    {
        ReadAndApply();
    }

    private void ReadAndApply()
    {
        bool up = moveUp != null && moveUp.IsPressed();
        bool down = moveDown != null && moveDown.IsPressed();
        bool left = moveLeft != null && moveLeft.IsPressed();
        bool right = moveRight != null && moveRight.IsPressed();

        bool l = attackL != null && attackL.IsPressed();
        bool m = attackM != null && attackM.IsPressed();
        bool h = attackH != null && attackH.IsPressed();
        bool ex = attackEX != null && attackEX.IsPressed();

        bool lDown = attackL != null && attackL.WasPressedThisFrame();
        bool mDown = attackM != null && attackM.WasPressedThisFrame();
        bool hDown = attackH != null && attackH.WasPressedThisFrame();
        bool exDown = attackEX != null && attackEX.WasPressedThisFrame();

        UpdateDirectionState(up, down, left, right);
        UpdateAttackButtonStates(l, m, h, ex, lDown, mDown, hDown, exDown);
    }

    private void UpdateDirectionState(bool up, bool down, bool left, bool right)
    {
        if (up && down)
        {
            up = false;
            down = false;
        }
        if (left && right)
        {
            left = false;
            right = false;
        }

        ControllerDL = false;
        ControllerD = false;
        ControllerDR = false;
        ControllerL = false;
        ControllerN = false;
        ControllerR = false;
        ControllerUL = false;
        ControllerU = false;
        ControllerUR = false;

        if (up && right) { ControllerUR = true; Direction = 9; }
        else if (up && left) { ControllerUL = true; Direction = 7; }
        else if (down && right) { ControllerDR = true; Direction = 3; }
        else if (down && left) { ControllerDL = true; Direction = 1; }
        else if (up) { ControllerU = true; Direction = 8; }
        else if (down) { ControllerD = true; Direction = 2; }
        else if (left) { ControllerL = true; Direction = 4; }
        else if (right) { ControllerR = true; Direction = 6; }
        else { ControllerN = true; Direction = 5; }
    }

    private void UpdateAttackButtonStates(bool l, bool m, bool h, bool ex,
                                          bool lDown, bool mDown, bool hDown, bool exDown)
    {
        ButtonL = l;
        ButtonM = m;
        ButtonH = h;
        ButtonEX = ex;

        ButtonLDown = lDown;
        ButtonMDown = mDown;
        ButtonHDown = hDown;
        ButtonEXDown = exDown;
    }
}