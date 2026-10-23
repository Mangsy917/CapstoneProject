using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

// 설정 메뉴 한 줄(BTN_...)에 붙여서 사용하는 "< 값 >" 스테퍼
// - 오른쪽 화살표 / D: 다음 선택지(증가), 왼쪽 화살표 / A: 이전 선택지(감소)
// - 선택지는 인스펙터의 Options에서 추가/수정 (위에서부터 작은 값 -> 큰 값 순서로)
// - 값이 바뀌면 OnValueChanged(int 번호)가 호출됨 (실제 설정 적용은 여기에 연결)
public class SettingStepper : MonoBehaviour, ISettingStepper
{
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    // 선택지 목록
    public string[] options;

    // 처음 값 (설정 초기화 시에도 이 값으로 돌아감)
    [SerializeField] private int defaultIndex = 0;

    // 끝에서 넘기면 반대쪽 끝으로 돌아갈지
    [SerializeField] private bool loop = true;

    public UnityEvent<int> onValueChanged;

    // 지금 선택된 선택지 번호
    public int Index { get; private set; }

    // 탭이 처음 켜지기 전(Awake 전)에 다른 스크립트가 값을 정했는지 (예: 재시작 + 레버로 시작 위치를 바꿈)
    private bool isIndexSet;

    void Awake()
    {
        if (leftButton != null) leftButton.onClick.AddListener(Prev);
        if (rightButton != null) rightButton.onClick.AddListener(Next);

        // 이미 정해진 값이 있으면 처음 값으로 덮어쓰지 않음
        if (!isIndexSet) Index = ClampIndex(defaultIndex);
        RefreshText();
    }

    // 다음 선택지로
    public void Next()
    {
        Step(1);
    }

    // 이전 선택지로
    public void Prev()
    {
        Step(-1);
    }

    // 처음 값으로 되돌림
    public void ResetToDefault()
    {
        SetIndex(defaultIndex);
    }

    public void SetIndex(int index)
    {
        if (options == null || options.Length == 0) return;

        index = ClampIndex(index);
        isIndexSet = true;
        if (index == Index) return;

        Index = index;
        RefreshText();
        onValueChanged?.Invoke(Index);
    }

    private void Step(int dir)
    {
        if (options == null || options.Length == 0) return;

        int index = Index + dir;
        if (loop) index = (index + options.Length) % options.Length;

        SetIndex(index);
    }

    private int ClampIndex(int index)
    {
        if (options == null || options.Length == 0) return 0;
        return Mathf.Clamp(index, 0, options.Length - 1);
    }

    private void RefreshText()
    {
        if (valueText == null) return;
        valueText.text = (options != null && options.Length > 0) ? options[Index] : "";
    }
}
