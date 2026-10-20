using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

// 설정 메뉴 한 줄(BTN_...)에 붙여서 사용하는 "< 슬라이더 > 값" 스테퍼 (체력 % 등 수치 조정용)
// - 오른쪽 화살표 / D: step만큼 증가, 왼쪽 화살표 / A: step만큼 감소
// - 슬라이더를 마우스로 끌어도 step 단위로 맞춰짐
// - 값이 바뀌면 OnValueChanged(float 값)가 호출됨 (실제 설정 적용은 여기에 연결)
public class SettingSliderStepper : MonoBehaviour, ISettingStepper
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [SerializeField] private float minValue = 0f;
    [SerializeField] private float maxValue = 100f;
    // 한 번에 바뀌는 양
    [SerializeField] private float step = 10f;
    // 처음 값 (설정 초기화 시에도 이 값으로 돌아감)
    [SerializeField] private float defaultValue = 100f;

    // 값 표시 형식 ({0} 자리에 값이 들어감)
    [SerializeField] private string valueFormat = "{0}%";

    public UnityEvent<float> onValueChanged;

    // 지금 값
    public float Value { get; private set; }

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

        Value = Snap(defaultValue);
        RefreshUI();
    }

    // 증가
    public void Next()
    {
        SetValue(Value + step);
    }

    // 감소
    public void Prev()
    {
        SetValue(Value - step);
    }

    // 처음 값으로 되돌림
    public void ResetToDefault()
    {
        SetValue(defaultValue);
    }

    public void SetValue(float value)
    {
        value = Snap(value);

        if (Mathf.Approximately(value, Value))
        {
            // 슬라이더를 끌다가 step 사이에 멈춰도 맞는 위치로 되돌림
            RefreshUI();
            return;
        }

        Value = value;
        RefreshUI();
        onValueChanged?.Invoke(Value);
    }

    // 범위 안으로 맞추고 step 단위로 반올림
    private float Snap(float value)
    {
        value = Mathf.Clamp(value, minValue, maxValue);
        if (step > 0f) value = minValue + Mathf.Round((value - minValue) / step) * step;
        return Mathf.Clamp(value, minValue, maxValue);
    }

    private void RefreshUI()
    {
        if (slider != null) slider.SetValueWithoutNotify(Value);
        if (valueText != null) valueText.text = string.Format(valueFormat, Value);
    }
}
