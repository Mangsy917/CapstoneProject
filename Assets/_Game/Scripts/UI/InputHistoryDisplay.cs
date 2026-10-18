using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public class InputHistoryDisplay : MonoBehaviour
{
    [Header("방향/공격 bool 상태를 읽어올 대상")]
    [SerializeField] private InputReader inputReadObject;

    [Header("리스트가 생성될 위치")]
    [SerializeField, FormerlySerializedAs("ListTransform")] private RectTransform listTransform;

    [Header("버튼 UI이미지")]
    [SerializeField, FormerlySerializedAs("History")] private GameObject history;

    [Header("프레임 유지시간 텍스트")]
    [SerializeField, FormerlySerializedAs("FrameText")] private GameObject frameText;

    [Header("방향 아이콘")]
    [SerializeField, FormerlySerializedAs("DirectionUI")] private GameObject directionUI;

    [Header("공격 버튼 아이콘")]
    [SerializeField, FormerlySerializedAs("AttackUI")] private GameObject attackUI;

    [Header("방향 스프라이트")]
    [SerializeField, FormerlySerializedAs("DirectionSprites")] private Sprite[] directionSprites = new Sprite[9];

    [Header("공격 버튼 스프라이트")]
    [SerializeField, FormerlySerializedAs("AttackSprites")] private Sprite[] attackSprites = new Sprite[4];

    [Header("최대 리스트 생성 값")]
    [SerializeField] private int maxRows = 30;

    [Header("기록 프레임 최대 값")]
    [SerializeField] private int maxFrameCount = 99;

    [Header("행과 행 사이의 세로 간격(px)")]
    [SerializeField] private float rowSpacing = 6f;

    [Header("모든 행의 왼쪽 기준 X좌표(px)")]
    [SerializeField] private float rowStartX = 5f;

    private float rowHeight = 40f;
    private const int neutralIndex = 4;
    private const int attackCount = 4;

    private class HistoryRow
    {
        public GameObject rowObject;
        public Text frameTextLegacy;   
        public TMP_Text frameTextTMP;  
        public int frameCount;
        public bool[] attackIconCreated = new bool[attackCount];

        public void SetFrameText(string value)
        {
            if (frameTextLegacy != null) frameTextLegacy.text = value;
            if (frameTextTMP != null) frameTextTMP.text = value;
        }
    }

    private readonly List<HistoryRow> rows = new List<HistoryRow>();
    private HistoryRow currentRow;

    private int lastDirectionIndex = -1;
    private bool[] prevAttackStates = new bool[attackCount];

    private void OnValidate()
    {
        if (maxRows < 1) maxRows = 1;
        if (maxFrameCount < 1) maxFrameCount = 1;
        if (maxFrameCount > 99) maxFrameCount = 99; 
    }

    private void Awake()
    {
        if (listTransform != null)
        {
            var existingVlg = listTransform.GetComponent<VerticalLayoutGroup>();
            if (existingVlg != null)
                Destroy(existingVlg);

            var existingCsf = listTransform.GetComponent<ContentSizeFitter>();
            if (existingCsf != null)
                Destroy(existingCsf);
        }

        if (history != null)
        {
            RectTransform prefabRect = history.GetComponent<RectTransform>();
            if (prefabRect != null)
                rowHeight = prefabRect.rect.height;
        }
    }

    private void EnsureHorizontalLayout(GameObject rowObj)
    {
        var hlg = rowObj.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null)
        {
            hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.spacing = 4f;
        }

        if (rowObj.GetComponent<ContentSizeFitter>() == null)
        {
            var csf = rowObj.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private void Update()
    {
        if (!HasRequiredReferences())
            return;

        int dirIndex = GetCurrentDirectionIndex();
        bool[] curAttack = GetCurrentAttackStates();

        bool directionChanged = dirIndex != lastDirectionIndex;
        bool attackStateChanged = false;
        for (int i = 0; i < attackCount; i++)
        {
            if (curAttack[i] != prevAttackStates[i])
            {
                attackStateChanged = true;
                break;
            }
        }

        if (directionChanged || attackStateChanged)
        {
            lastDirectionIndex = dirIndex;
            currentRow = CreateNewRow(dirIndex);

            for (int i = 0; i < attackCount; i++)
            {
                if (curAttack[i])
                    AddAttackIcon(currentRow, i);
            }
        }

        prevAttackStates = curAttack;

        AdvanceCurrentRowFrameCount();
    }

    private bool HasRequiredReferences()
    {
        return inputReadObject != null
            && listTransform != null
            && history != null
            && frameText != null
            && directionUI != null
            && attackUI != null;
    }

    private int GetCurrentDirectionIndex()
    {
        if (inputReadObject.controllerDL) return 0;
        if (inputReadObject.controllerD) return 1;
        if (inputReadObject.controllerDR) return 2;
        if (inputReadObject.controllerL) return 3;
        if (inputReadObject.controllerN) return 4;
        if (inputReadObject.controllerR) return 5;
        if (inputReadObject.controllerUL) return 6;
        if (inputReadObject.controllerU) return 7;
        if (inputReadObject.controllerUR) return 8;
        return neutralIndex;
    }

    private HistoryRow CreateNewRow(int dirIndex)
    {
        GameObject rowObj = Instantiate(history, listTransform);
        EnsureHorizontalLayout(rowObj); 

        RectTransform rowRect = rowObj.GetComponent<RectTransform>();
        if (rowRect != null)
        {
            rowRect.localScale = Vector3.one;

            Vector2 pivot = rowRect.pivot;
            pivot.x = 0f;
            rowRect.pivot = pivot;

            Vector2 anchorMin = rowRect.anchorMin;
            Vector2 anchorMax = rowRect.anchorMax;
            anchorMin.x = 0f;
            anchorMax.x = 0f;
            rowRect.anchorMin = anchorMin;
            rowRect.anchorMax = anchorMax;

            Vector2 pos = rowRect.anchoredPosition;
            pos.x = rowStartX;
            pos.y = 0f;
            rowRect.anchoredPosition = pos;
        }

        rowObj.transform.SetAsFirstSibling();

        foreach (HistoryRow existingRow in rows)
        {
            RectTransform existingRect = existingRow.rowObject.GetComponent<RectTransform>();
            if (existingRect != null)
            {
                Vector2 existingPos = existingRect.anchoredPosition;
                existingPos.y -= (rowHeight + rowSpacing);
                existingRect.anchoredPosition = existingPos;
            }
        }

        var row = new HistoryRow
        {
            rowObject = rowObj,
            frameCount = 0
        };

        GameObject frameObj = Instantiate(frameText, rowObj.transform);
        row.frameTextLegacy = frameObj.GetComponent<Text>();
        row.frameTextTMP = frameObj.GetComponent<TMP_Text>();
        row.SetFrameText("0");

        GameObject dirObj = Instantiate(directionUI, rowObj.transform);
        Image dirImage = dirObj.GetComponent<Image>();
        if (dirImage != null && dirIndex >= 0 && dirIndex < directionSprites.Length && directionSprites[dirIndex] != null)
            dirImage.sprite = directionSprites[dirIndex];

        rows.Insert(0, row);
        TrimExcessRows();

        return row;
    }

    private void TrimExcessRows()
    {
        while (rows.Count > maxRows)
        {
            HistoryRow oldest = rows[rows.Count - 1];
            rows.RemoveAt(rows.Count - 1);

            if (oldest.rowObject != null)
                Destroy(oldest.rowObject);
        }
    }

    private void AdvanceCurrentRowFrameCount()
    {
        if (currentRow == null)
            return;

        if (currentRow.frameCount >= maxFrameCount)
            return;

        currentRow.frameCount++;
        currentRow.SetFrameText(currentRow.frameCount.ToString());
    }

    private bool[] GetCurrentAttackStates()
    {
        return new bool[attackCount]
        {
            inputReadObject.buttonL,
            inputReadObject.buttonM,
            inputReadObject.buttonH,
            inputReadObject.buttonEX
        };
    }

    private void AddAttackIcon(HistoryRow row, int attackIndex)
    {
        if (row == null)
            return;

        if (row.attackIconCreated[attackIndex])
            return;

        row.attackIconCreated[attackIndex] = true;

        GameObject atkObj = Instantiate(attackUI, row.rowObject.transform);
        Image atkImage = atkObj.GetComponent<Image>();
        if (atkImage != null && attackIndex >= 0 && attackIndex < attackSprites.Length && attackSprites[attackIndex] != null)
            atkImage.sprite = attackSprites[attackIndex];

        const int baseIndex = 2;

        int insertOffset = 0;
        for (int i = 0; i < attackIndex; i++)
        {
            if (row.attackIconCreated[i])
                insertOffset++;
        }

        atkObj.transform.SetSiblingIndex(baseIndex + insertOffset);
    }
}