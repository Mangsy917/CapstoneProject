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

    private const float selectValue = 255f / 255f;
    private const float notSelectValue = 50f / 255f;

    private const float selectFontSize = 55f;
    private const float notSelectFontSize = 45f;

    // 탭 사이 간격
    private const float spacing = 290f;

    // 탭 순서 (오른쪽으로 갈수록 다음 탭, 마지막 다음은 다시 처음)
    private RectTransform[] tabs;
    private TMP_Text[] texts;

    // 지금 가운데에 있는 탭 번호 (처음엔 practiceSetting)
    private int selectedIndex = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        tabs = new RectTransform[] { practiceSetting, shortPracticeSetting, parameter, dummySetting, commandList, displaySetting };
        texts = new TMP_Text[] { pSTXT, shortPSTXT, parameterTXT, dummyTXT, commandLTXT, displaySTXT };

        Refresh();
    }

    // Update is called once per frame
    void Update()
    {
        if (uiControl == null) return;

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

    // 선택된 번호를 기준으로 모든 탭의 위치, 글자 크기, 알파값을 다시 맞춤
    private void Refresh()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
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
