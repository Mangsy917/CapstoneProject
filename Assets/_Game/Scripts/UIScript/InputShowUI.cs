using UnityEngine;
using UnityEngine.UI;

public class InputShowUI : MonoBehaviour
{
    [Header("입력 상태를 읽어올 대상")]
    [SerializeField] private InputReader inputReader;

    [Header("방향 UI")]
    [SerializeField] private Image Direction1; // DL
    [SerializeField] private Image Direction2; // D
    [SerializeField] private Image Direction3; // DR
    [SerializeField] private Image Direction4; // L
    [SerializeField] private Image Direction5; // N (중립)
    [SerializeField] private Image Direction6; // R
    [SerializeField] private Image Direction7; // UL
    [SerializeField] private Image Direction8; // U
    [SerializeField] private Image Direction9; // UR

    [Header("공격 UI")]
    [SerializeField] private Image LButton;
    [SerializeField] private Image MButton;
    [SerializeField] private Image HButton;
    [SerializeField] private Image EXButton;

    private const float PressedValue = 255f / 255f;
    private const float ReleasedValue = 123f / 255f;

    private void Update()
    {
        if (inputReader == null)
        {
            Debug.LogWarning("[InputUIVisualizer] InputStateReader가 할당되지 않았습니다.");
            return;
        }

        UpdateImage(Direction1, inputReader.ControllerDL);
        UpdateImage(Direction2, inputReader.ControllerD);
        UpdateImage(Direction3, inputReader.ControllerDR);
        UpdateImage(Direction4, inputReader.ControllerL);
        UpdateImage(Direction5, inputReader.ControllerN);
        UpdateImage(Direction6, inputReader.ControllerR);
        UpdateImage(Direction7, inputReader.ControllerUL);
        UpdateImage(Direction8, inputReader.ControllerU);
        UpdateImage(Direction9, inputReader.ControllerUR);

        UpdateImage(LButton, inputReader.ButtonL);
        UpdateImage(MButton, inputReader.ButtonM);
        UpdateImage(HButton, inputReader.ButtonH);
        UpdateImage(EXButton, inputReader.ButtonEX);
    }

    private void UpdateImage(Image image, bool active)
    {
        if (image == null)
            return;

        float v = active ? PressedValue : ReleasedValue;
        Color c = image.color;
        c.r = v;
        c.g = v;
        c.b = v;
        image.color = c;
    }
}