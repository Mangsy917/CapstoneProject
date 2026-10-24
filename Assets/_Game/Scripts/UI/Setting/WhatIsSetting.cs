using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class WhatIsSetting : MonoBehaviour
{
    [SerializeField] private UIControl uiControl;
    [SerializeField] private RectTransform practiceSetting;
    [SerializeField] private RectTransform shortPracticeSetting;
    [SerializeField] private RectTransform parameter;
    [SerializeField] private RectTransform dummySetting;
    [SerializeField] private RectTransform commandList;
    [SerializeField] private RectTransform displaySetting;
    [SerializeField] private TMP_Text pSTXT;
    [SerializeField] private TMP_Text shortPSTXT;
    [SerializeField] private TMP_Text parameterTXT;
    [SerializeField] private TMP_Text dummyTXT;
    [SerializeField] private TMP_Text commandLTXT;
    [SerializeField] private TMP_Text displaySTXT;

    // SettingWindow 하위 패널 (탭과 같은 순서)
    [SerializeField] private GameObject practiceSettingWindow;
    [SerializeField] private GameObject shortPracticeSettingWindow;
    [SerializeField] private GameObject parameterWindow;
    [SerializeField] private GameObject dummySettingWindow;
    [SerializeField] private GameObject commandListWindow;
    [SerializeField] private GameObject displaySettingWindow;

    // 타이틀로 버튼을 눌렀을 때 이동할 씬 이름
    [SerializeField] private string titleSceneName = "Title";
    // "타이틀로 돌아가시겠습니까?" 팝업
    [SerializeField] private GameObject goTitlePopup;

    private const float selectValue = 255f / 255f;
    private const float notSelectValue = 50f / 255f;

    private const float selectFontSize = 55f;
    private const float notSelectFontSize = 45f;

    // 탭 사이 간격
    private const float spacing = 290f;

    // 탭 순서 (오른쪽으로 갈수록 다음 탭, 마지막 다음은 다시 처음)
    private RectTransform[] tabs;
    private TMP_Text[] texts;
    private GameObject[] windows;

    // 지금 가운데에 있는 탭 번호 (처음엔 practiceSetting)
    private int selectedIndex = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        tabs = new RectTransform[] { practiceSetting, shortPracticeSetting, parameter, dummySetting, commandList, displaySetting };
        texts = new TMP_Text[] { pSTXT, shortPSTXT, parameterTXT, dummyTXT, commandLTXT, displaySTXT };
        windows = new GameObject[] { practiceSettingWindow, shortPracticeSettingWindow, parameterWindow, dummySettingWindow, commandListWindow, displaySettingWindow };

        if (goTitlePopup != null) goTitlePopup.SetActive(false);
        Refresh();
    }

    // Update is called once per frame
    void Update()
    {
        if (uiControl == null) return;

        // 팝업이 열려 있으면 탭을 넘기지 않음
        if (SettingPopup.IsAnyOpen) return;

        // E / Right Shoulder
        if (uiControl.uiRightPG) MoveRight();
        // Q / Left Shoulder
        else if (uiControl.uiLeftPG) MoveLeft();
    }

    // 오른쪽 탭으로 (끝에서 누르면 처음으로)
    public void MoveRight()
    {
        selectedIndex = (selectedIndex + 1) % tabs.Length;
        Refresh();
    }

    // 왼쪽 탭으로 (처음에서 누르면 끝으로)
    public void MoveLeft()
    {
        selectedIndex = (selectedIndex - 1 + tabs.Length) % tabs.Length;
        Refresh();
    }

    // 클릭한 탭으로 바로 이동 (몇 칸 떨어져 있어도 한 번에)
    // 각 탭의 Button OnClick에 연결 (0: 연습설정, 1: 간편연습설정, 2: 파라미터, 3: 더미설정, 4: 커맨드리스트, 5: 화면설정)
    public void SelectTab(int index)
    {
        if (index < 0 || index >= tabs.Length || index == selectedIndex) return;

        selectedIndex = index;
        Refresh();
    }

    // 연습설정 > 타이틀로 : 확인 팝업을 띄움
    public void PressGoTitleBT()
    {
        goTitlePopup.SetActive(true);
    }

    // 타이틀로 팝업 > 예 : 타이틀 씬으로 이동
    public void PressGoTitleYesBT()
    {
        LoadingScene.LoadScene(titleSceneName);
    }

    // 타이틀로 팝업 > 아니오 : 팝업을 닫음
    public void PressGoTitleNoBT()
    {
        goTitlePopup.SetActive(false);
    }

    // 선택된 번호를 기준으로 모든 탭의 위치, 글자 크기, 알파값을 다시 맞춤
    private void Refresh()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            // 선택된 탭에 대응되는 SettingWindow 패널만 켬
            if (windows[i] != null) windows[i].SetActive(i == selectedIndex);

            // 선택된 탭에서 몇 칸 떨어져 있는지 (-2 ~ 3 안으로 맞춤)
            int offset = i - selectedIndex;
            if (offset > 3) offset -= tabs.Length;
            if (offset < -2) offset += tabs.Length;

            // 선택된 탭의 정반대(4번째 칸)는 숨기고, 나머지 5개만 보여줌
            bool isVisible = offset != 3;
            tabs[i].gameObject.SetActive(isVisible);
            if (!isVisible) continue;

            tabs[i].anchoredPosition = new Vector2(offset * spacing, 0f);

            bool isSelected = (i == selectedIndex);
            texts[i].fontSize = isSelected ? selectFontSize : notSelectFontSize;
            texts[i].color = new Color(1f, 1f, 1f, isSelected ? selectValue : notSelectValue);
        }
    }
}
