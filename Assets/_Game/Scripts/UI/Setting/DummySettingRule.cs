using UnityEngine;

// DummySetting 패널에 붙여서 사용
// 스테퍼 값에 따라 다른 설정 줄을 숨기거나 잠금
// - 더미 조작 = CPU          : 아래 설정들을 숨기고, 바로 밑에 CPU 레벨 줄을 보여줌
// - 더미 조작 = 플레이어 조작 : 아래 설정들을 숨김
// - 가드 값에 따라 카운트 가드 횟수 / 가드 종류 줄을 잠금 (보이지만 선택 불가, 반투명)
public class DummySettingRule : MonoBehaviour
{
    [Header("더미 조작")]
    [SerializeField] private SettingStepper controlDummyStepper;
    // CPU를 골랐을 때만 보이는 CPU 레벨 줄
    [SerializeField] private GameObject cpuLevelRow;
    // 더미 조작이 CPU / 플레이어 조작일 때 숨길 줄들
    [SerializeField] private GameObject[] rowsBelowControl;

    // 더미 조작 Options에서 CPU, 플레이어 조작이 몇 번째인지 (0부터)
    [SerializeField] private int cpuIndex = 1;
    [SerializeField] private int playerIndex = 2;

    [Header("가드")]
    [SerializeField] private SettingStepper guardStepper;
    [SerializeField] private CanvasGroup countGuardRow;
    [SerializeField] private CanvasGroup guardTypeRow;

    // 잠긴 줄의 투명도 (0.3 = 30%만 보임)
    [SerializeField] private float lockedAlpha = 0.3f;

    // 가드 Options 순서 (0부터)
    private const int GuardNone = 0;        // 가드하지 않음
    private const int GuardAll = 1;         // 가드 실행
    private const int GuardCount = 2;       // 카운트 가드
    private const int GuardAfterFirst = 3;  // 첫 타 이후 가드
    private const int GuardRandom = 4;      // 랜덤

    private SettingMenu settingMenu;

    void Awake()
    {
        settingMenu = GetComponentInParent<SettingMenu>(true);

        if (controlDummyStepper != null) controlDummyStepper.onValueChanged.AddListener(OnControlDummyChanged);
        if (guardStepper != null) guardStepper.onValueChanged.AddListener(OnGuardChanged);
    }

    void Start()
    {
        // 처음 값에 맞춰서 한 번 적용
        if (controlDummyStepper != null) OnControlDummyChanged(controlDummyStepper.Index);
        if (guardStepper != null) OnGuardChanged(guardStepper.Index);
    }

    // 더미 조작이 바뀌었을 때
    private void OnControlDummyChanged(int index)
    {
        bool isCpu = index == cpuIndex;
        bool hideBelow = isCpu || index == playerIndex;

        if (cpuLevelRow != null) cpuLevelRow.SetActive(isCpu);
        foreach (GameObject row in rowsBelowControl)
        {
            if (row != null) row.SetActive(!hideBelow);
        }

        // 메뉴의 위/아래 이동 목록을 다시 만듦
        if (settingMenu != null) settingMenu.RefreshRows();
    }

    // 가드가 바뀌었을 때
    private void OnGuardChanged(int index)
    {
        bool canCountGuard = index == GuardCount;
        bool canGuardType = index == GuardAll || index == GuardCount || index == GuardAfterFirst;

        SetRowLocked(countGuardRow, !canCountGuard);
        SetRowLocked(guardTypeRow, !canGuardType);

        if (settingMenu != null) settingMenu.RefreshRows();
    }

    // 줄 잠금: 보이긴 하지만 반투명해지고, 마우스/키보드/컨트롤러로 선택할 수 없음
    private void SetRowLocked(CanvasGroup row, bool locked)
    {
        if (row == null) return;

        row.alpha = locked ? lockedAlpha : 1f;
        row.interactable = !locked;
    }
}
