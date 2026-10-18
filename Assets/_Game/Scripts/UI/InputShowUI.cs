using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class InputShowUI : MonoBehaviour
{
    [Header("입력 상태를 읽어올 대상")]
    [SerializeField] private InputReader inputReader;

    [Header("방향 UI")]
    [SerializeField, FormerlySerializedAs("Direction1")] private Image direction1; // DL
    [SerializeField, FormerlySerializedAs("Direction2")] private Image direction2; // D
    [SerializeField, FormerlySerializedAs("Direction3")] private Image direction3; // DR
    [SerializeField, FormerlySerializedAs("Direction4")] private Image direction4; // L
    [SerializeField, FormerlySerializedAs("Direction5")] private Image direction5; // N (중립)
    [SerializeField, FormerlySerializedAs("Direction6")] private Image direction6; // R
    [SerializeField, FormerlySerializedAs("Direction7")] private Image direction7; // UL
    [SerializeField, FormerlySerializedAs("Direction8")] private Image direction8; // U
    [SerializeField, FormerlySerializedAs("Direction9")] private Image direction9; // UR

    [Header("공격 UI")]
    [SerializeField, FormerlySerializedAs("LButton")] private Image lButton;
    [SerializeField, FormerlySerializedAs("MButton")] private Image mButton;
    [SerializeField, FormerlySerializedAs("HButton")] private Image hButton;
    [SerializeField, FormerlySerializedAs("EXButton")] private Image exButton;

    private const float pressedValue = 255f / 255f;
    private const float releasedValue = 123f / 255f;

    private void Update()
    {
        if (inputReader == null)
        {
            Debug.LogWarning("[InputUIVisualizer] InputStateReader가 할당되지 않았습니다.");
            return;
        }

        UpdateImage(direction1, inputReader.controllerDL);
        UpdateImage(direction2, inputReader.controllerD);
        UpdateImage(direction3, inputReader.controllerDR);
        UpdateImage(direction4, inputReader.controllerL);
        UpdateImage(direction5, inputReader.controllerN);
        UpdateImage(direction6, inputReader.controllerR);
        UpdateImage(direction7, inputReader.controllerUL);
        UpdateImage(direction8, inputReader.controllerU);
        UpdateImage(direction9, inputReader.controllerUR);

        UpdateImage(lButton, inputReader.buttonL);
        UpdateImage(mButton, inputReader.buttonM);
        UpdateImage(hButton, inputReader.buttonH);
        UpdateImage(exButton, inputReader.buttonEX);
    }

    private void UpdateImage(Image image, bool active)
    {
        if (image == null)
            return;

        float v = active ? pressedValue : releasedValue;
        Color c = image.color;
        c.r = v;
        c.g = v;
        c.b = v;
        image.color = c;
    }
}