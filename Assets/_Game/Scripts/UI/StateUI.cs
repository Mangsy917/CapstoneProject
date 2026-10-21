using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class StateUI : MonoBehaviour
{
    [Header("캐릭터 공격 속성을 읽어올 오브젝트")]
    [SerializeField, FormerlySerializedAs("StateReader")] private CharactorState stateReader;

    [Header("캐릭터 행동")]
    [SerializeField, FormerlySerializedAs("CharacterMove")] private TextMeshProUGUI characterMove;

    [Header("캐릭터 행동 - 표시할 텍스트 (인스펙터에서 수정)")]
    [SerializeField] private string standUp = "서기";
    [SerializeField] private string sitDwon = "앉기";
    [SerializeField] private string jump = "점프";
    [SerializeField] private string high = "상단";
    [SerializeField] private string middle = "중단";
    [SerializeField] private string low = "하단";
    [SerializeField] private string grap = "행동5";
    [SerializeField] private string pos = "행동6";
    [SerializeField] private string parry = "행동7";
    [SerializeField] private string dash = "대쉬";
    [SerializeField] private string run = "달리기";

    [Header("캐릭터 공격 속성")]
    [SerializeField] private Image bangeok;
    [SerializeField] private Image powerCrash;
    [SerializeField] private Image reversal;

    [Header("캐릭터 지상판정")]
    [SerializeField, FormerlySerializedAs("AirOrGround")] private TextMeshProUGUI airOrGround;

    [Header("지상판정 - 표시할 텍스트 (인스펙터에서 수정)")]
    [SerializeField] private string groundText = "지상";
    [SerializeField] private string airText = "공중";

    [Header("데미지")]
    [SerializeField, FormerlySerializedAs("Damage")] private TextMeshProUGUI damage;

    [Header("카운터 판정")]
    [SerializeField, FormerlySerializedAs("Counter")] private TextMeshProUGUI counter;

    [Header("카운터 판정 - 표시할 텍스트 (인스펙터에서 수정)")]
    [SerializeField] private string normalHit = "노말히트";
    [SerializeField] private string counterHit = "카운터";
    [SerializeField] private string punishCounter = "퍼니쉬카운터";

    void Start()
    {

    }

    void Update()
    {
        if (stateReader == null) return;

        UpdateCharacterMoveText();
        UpdateAttackAttributeImages();
        UpdateAirOrGroundText();
        UpdateCounterText();
    }

    private void UpdateCharacterMoveText()
    {
        if (characterMove == null) return;

        if (stateReader.StandUp) characterMove.text = standUp;
        else if (stateReader.SitDown) characterMove.text = sitDwon;
        else if (stateReader.Jump) characterMove.text = jump;
        else if (stateReader.High) characterMove.text = high;
        else if (stateReader.Middle) characterMove.text = middle;
        else if (stateReader.Low) characterMove.text = low;
        else if (stateReader.Grap) characterMove.text = grap;
        else if (stateReader.pos) characterMove.text = pos;
        else if (stateReader.Parry) characterMove.text = parry;
        else if (stateReader.Dash) characterMove.text = dash;
        else if (stateReader.Run) characterMove.text = run;
        else characterMove.text = string.Empty;
    }

    private void UpdateAttackAttributeImages()
    {
        SetImageChannel(bangeok, stateReader.bangeok, 0);
        SetImageChannel(powerCrash, stateReader.powerCrash, 1);
        SetImageChannel(reversal, stateReader.reversal, 2);
    }

    private void SetImageChannel(Image image, bool isOn, int channelIndex)
    {
        if (image == null) return;

        Color c = image.color;
        float value = isOn ? 1f : 0f;

        switch (channelIndex)
        {
            case 0: c.r = value; break;
            case 1: c.g = value; break;
            case 2: c.b = value; break;
        }

        image.color = c;
    }

    private void UpdateAirOrGroundText()
    {
        if (airOrGround == null) return;

        if (stateReader.Ground) airOrGround.text = groundText;
        else if (stateReader.Air) airOrGround.text = airText;
        else airOrGround.text = string.Empty;
    }

    private void UpdateCounterText()
    {
        if (counter == null) return;

        if (stateReader.NormalHit) counter.text = normalHit;
        else if (stateReader.CounterHit) counter.text = counterHit;
        else if (stateReader.PunishCounter) counter.text = punishCounter;
        else counter.text = string.Empty;
    }
}