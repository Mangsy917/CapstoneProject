using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

// 플레이어와 더미의 판정(CharactorState)을 한 줄에 나란히 보여줌
// (플레이어의 판정) | (무슨 판정인지) | (더미의 판정)
// - 캐릭터 행동: 서기 / 앉기 / 점프
// - 공격 속성: 상단 / 중단 / 하단과 장풍, 잡기, 슈퍼아머 (공격 중이 아니면 빈 표시)
// - 캐릭터 판정: 지상 / 공중
// - 단일 데미지: 그 캐릭터가 상대에게 마지막으로 맞힌 기술의 데미지
// - 카운터 종류
public class StateUI : MonoBehaviour
{
    // 한 캐릭터의 판정을 보여줄 텍스트들 (한 열)
    [Serializable]
    private class SideTexts
    {
        public TextMeshProUGUI actionText;
        public TextMeshProUGUI attackText;
        public TextMeshProUGUI positionText;
        public TextMeshProUGUI singleDamageText;
        public TextMeshProUGUI counterText;

        // 값이 바뀔 때만 글자를 새로 만들도록 마지막으로 만든 글자를 기억 (매 프레임 문자열을 만들지 않도록)
        [NonSerialized] public int shownAttackFlags = -1;
        [NonSerialized] public string shownAttackString;
        [NonSerialized] public int shownDamage = int.MinValue;
        [NonSerialized] public string shownDamageString;
    }

    [Header("판정을 읽어올 캐릭터")]
    [SerializeField, FormerlySerializedAs("stateReader")] private CharactorState playerState;
    [Tooltip("비워두거나 더미가 다시 생성되면 더미 태그로 찾음")]
    [SerializeField] private CharactorState dummyState;
    [SerializeField] private string dummyTag = "Enemy";

    [Header("플레이어 판정 텍스트 (왼쪽 열)")]
    [SerializeField] private SideTexts playerTexts = new SideTexts();

    [Header("더미 판정 텍스트 (오른쪽 열)")]
    [SerializeField] private SideTexts dummyTexts = new SideTexts();

    [Header("캐릭터 행동 - 표시할 텍스트 (인스펙터에서 수정)")]
    [SerializeField] private string standUp = "서기";
    [SerializeField] private string sitDwon = "앉기";
    [SerializeField] private string jump = "점프";

    [Header("공격 속성 - 표시할 텍스트 (인스펙터에서 수정)")]
    [SerializeField] private string high = "상단";
    [SerializeField] private string middle = "중단";
    [SerializeField] private string low = "하단";
    [SerializeField] private string projectileText = "장풍";
    [SerializeField] private string grap = "잡기";
    [SerializeField] private string superArmorText = "슈퍼아머";
    [Tooltip("공격 속성이 여러 개일 때 사이에 넣을 글자")]
    [SerializeField] private string traitSeparator = " · ";

    [Header("지상판정 - 표시할 텍스트 (인스펙터에서 수정)")]
    [SerializeField] private string groundText = "지상";
    [SerializeField] private string airText = "공중";

    [Header("카운터 판정 - 표시할 텍스트 (인스펙터에서 수정)")]
    [SerializeField] private string normalHit = "노말히트";
    [SerializeField] private string counterHit = "카운터";
    [SerializeField] private string punishCounter = "퍼니쉬카운터";

    [Tooltip("값이 없을 때 표시할 텍스트 (공격 중이 아님, 카운터 없음 등)")]
    [SerializeField] private string emptyText = "-";

    private readonly List<string> _attackParts = new List<string>();

    void Update()
    {
        if (dummyState == null) dummyState = FindDummyState();

        UpdateSide(playerState, playerTexts);
        UpdateSide(dummyState, dummyTexts);
    }

    private CharactorState FindDummyState()
    {
        GameObject dummy = GameObject.FindGameObjectWithTag(dummyTag);
        return dummy != null ? dummy.GetComponent<CharactorState>() : null;
    }

    private void UpdateSide(CharactorState state, SideTexts texts)
    {
        if (texts == null) return;

        // 캐릭터가 없으면(더미가 다시 생성되기 전 등) 모두 빈 표시
        if (state == null)
        {
            SetText(texts.actionText, emptyText);
            SetText(texts.attackText, emptyText);
            SetText(texts.positionText, emptyText);
            SetText(texts.singleDamageText, emptyText);
            SetText(texts.counterText, emptyText);
            return;
        }

        SetText(texts.actionText, GetActionText(state));
        SetText(texts.attackText, GetAttackText(state, texts));
        SetText(texts.positionText, state.Air ? airText : groundText);
        SetText(texts.singleDamageText, GetDamageText(state, texts));
        SetText(texts.counterText, GetCounterText(state));
    }

    private string GetActionText(CharactorState state)
    {
        if (state.Jump) return jump;
        if (state.SitDown) return sitDwon;
        return standUp;
    }

    // 예) "중단", "중단 · 장풍", "잡기", 공격 중이 아니면 "-"
    private string GetAttackText(CharactorState state, SideTexts texts)
    {
        int flags = (state.High ? 1 : 0) | (state.Middle ? 2 : 0) | (state.Low ? 4 : 0)
                  | (state.Projectile ? 8 : 0) | (state.Grap ? 16 : 0) | (state.SuperArmor ? 32 : 0);
        if (flags == texts.shownAttackFlags) return texts.shownAttackString;

        texts.shownAttackFlags = flags;
        texts.shownAttackString = BuildAttackText(state);
        return texts.shownAttackString;
    }

    private string BuildAttackText(CharactorState state)
    {
        _attackParts.Clear();
        if (state.High) _attackParts.Add(high);
        else if (state.Middle) _attackParts.Add(middle);
        else if (state.Low) _attackParts.Add(low);

        if (state.Projectile) _attackParts.Add(projectileText);
        if (state.Grap) _attackParts.Add(grap);
        if (state.SuperArmor) _attackParts.Add(superArmorText);

        return _attackParts.Count > 0 ? string.Join(traitSeparator, _attackParts) : emptyText;
    }

    private string GetDamageText(CharactorState state, SideTexts texts)
    {
        if (state.SingleDamage == texts.shownDamage) return texts.shownDamageString;

        texts.shownDamage = state.SingleDamage;
        texts.shownDamageString = state.SingleDamage.ToString();
        return texts.shownDamageString;
    }

    private string GetCounterText(CharactorState state)
    {
        if (state.NormalHit) return normalHit;
        if (state.CounterHit) return counterHit;
        if (state.PunishCounter) return punishCounter;
        return emptyText;
    }

    // 값이 바뀔 때만 넣음 (TextMeshPro가 매 프레임 다시 그리지 않도록)
    private void SetText(TextMeshProUGUI text, string value)
    {
        if (text == null || text.text == value) return;
        text.text = value;
    }
}
