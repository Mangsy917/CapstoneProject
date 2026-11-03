using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

// 설정 메뉴 한 줄(BTN_...)에 붙여서 사용하는 "< 슬라이더 > 값" 스테퍼 (체력 % 등 수치 조정용)
// - 오른쪽 화살표 / D: 조정 단위만큼 증가, 왼쪽 화살표 / A: 조정 단위만큼 감소 (길게 누르면 연속으로 바뀜)
// - 조정 단위는 step(큰 단위, 예: 10%)과 fineStep(작은 단위, 예: 1%) 중 하나
//   옆의 작은 단위 스테퍼(< 10% >)를 누르거나, 이 줄에서 선택 키, 키보드 O / P, 패드 LT / RT를 누르면 바뀜
// - 큰 단위로 움직이면 그 단위의 눈금에 맞춰짐 (예: 37%에서 오른쪽 -> 40%, 왼쪽 -> 30%)
// - 슬라이더를 마우스로 끌어도 지금 조정 단위로 맞춰짐
// - 값이 바뀌면 OnValueChanged(float 값)가 호출됨 (실제 설정 적용은 여기에 연결)
public class SettingSliderStepper : MonoBehaviour, ISettingStepper
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [SerializeField] private float minValue = 0f;
    [SerializeField] private float maxValue = 100f;
    // 한 번에 바뀌는 양 (큰 단위)
    [SerializeField] private float step = 10f;
    // 한 번에 바뀌는 양 (작은 단위)
    [SerializeField] private float fineStep = 1f;
    // 처음 값
    [SerializeField] private float defaultValue = 100f;

    // 값 표시 형식 ({0} 자리에 값이 들어감, 조정 단위 표시에도 같이 씀)
    [SerializeField] private string valueFormat = "{0}%";

    [Header("조정 단위 스테퍼 (비워두면 선택 키로만 바꿈)")]
    [SerializeField] private TMP_Text stepSizeText;
    [SerializeField] private Button stepSizeLeftButton;
    [SerializeField] private Button stepSizeRightButton;

    public UnityEvent<float> onValueChanged;

    // 지금 값
    public float Value { get; private set; }

    // 작은 단위(fineStep)로 조정 중인지
    private bool isFineStep;

    // 지금 조정 단위
    private float CurrentStep => isFineStep ? fineStep : step;

    void Awake()
    {
        if (slider != null)
        {
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.onValueChanged.AddListener(SetValue);
        }
        if (leftButton != null) leftButton.onClick.AddListener(Prev);
        if (rightButton != null) rightButton.onClick.AddListener(Next);
        if (stepSizeLeftButton != null) stepSizeLeftButton.onClick.AddListener(ToggleStepSize);
        if (stepSizeRightButton != null) stepSizeRightButton.onClick.AddListener(ToggleStepSize);

        Value = Snap(defaultValue, step);
        RefreshUI();
    }

    // 증가 (지금 단위의 다음 눈금으로)
    public void Next()
    {
        float currentStep = CurrentStep;
        if (currentStep <= 0f) return;

        float index = Mathf.Floor((Value - minValue) / currentStep + 0.001f);
        SetValue(minValue + (index + 1f) * currentStep);
    }

    // 감소 (지금 단위의 이전 눈금으로)
    public void Prev()
    {
        float currentStep = CurrentStep;
        if (currentStep <= 0f) return;

        float index = Mathf.Ceil((Value - minValue) / currentStep - 0.001f);
        SetValue(minValue + (index - 1f) * currentStep);
    }

    // 조정 단위를 큰 단위 <-> 작은 단위로 바꿈
    public void ToggleStepSize()
    {
        isFineStep = !isFineStep;
        RefreshUI();
    }

    public void SetValue(float value)
    {
        value = Snap(value, CurrentStep);

        if (Mathf.Approximately(value, Value))
        {
            // 슬라이더를 끌다가 단위 사이에 멈춰도 맞는 위치로 되돌림
            RefreshUI();
            return;
        }

        Value = value;
        RefreshUI();
        onValueChanged?.Invoke(Value);
    }

    // 저장해 둔 값을 보여줄 때 사용 (작은 단위로 맞추므로 37%도 그대로 보임, OnValueChanged는 부르지 않음)
    public void SetSavedValue(float value)
    {
        Value = Snap(value, fineStep);
        RefreshUI();
    }

    // 범위 안으로 맞추고 snapStep 단위로 반올림
    private float Snap(float value, float snapStep)
    {
        value = Mathf.Clamp(value, minValue, maxValue);
        if (snapStep > 0f) value = minValue + Mathf.Round((value - minValue) / snapStep) * snapStep;
        return Mathf.Clamp(value, minValue, maxValue);
    }

    private void RefreshUI()
    {
        if (slider != null) slider.SetValueWithoutNotify(Value);
        if (valueText != null) valueText.text = string.Format(valueFormat, Value);
        if (stepSizeText != null) stepSizeText.text = string.Format(valueFormat, CurrentStep);
    }
}
