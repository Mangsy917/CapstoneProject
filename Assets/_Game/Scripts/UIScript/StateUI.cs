using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class StateUI : MonoBehaviour
{
    [Header("캐릭터 공격 속성을 읽어올 오브젝트")]
    [SerializeField] private CharactorState StateReader;

    [Header("캐릭터 행동")]
    [SerializeField] private TextMeshProUGUI CharacterMove;

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

    [Header("캐릭터 공격 속성")]
    [SerializeField] private Image bangeok;
    [SerializeField] private Image powerCrash;
    [SerializeField] private Image reversal;

    [Header("캐릭터 지상판정")]
    [SerializeField] private TextMeshProUGUI AirOrGround;

    [Header("지상판정 - 표시할 텍스트 (인스펙터에서 수정)")]
    [SerializeField] private string groundText = "지상";
    [SerializeField] private string airText = "공중";

    [Header("데미지")]
    [SerializeField] private TextMeshProUGUI Damage;

    [Header("카운터 판정")]
    [SerializeField] private TextMeshProUGUI Counter;

    [Header("카운터 판정 - 표시할 텍스트 (인스펙터에서 수정)")]
    [SerializeField] private string normalHit = "노말히트";
    [SerializeField] private string counterHit = "카운터";
    [SerializeField] private string punishCounter = "퍼니쉬카운터";

    void Start()
    {

    }

    void Update()
    {
        if (StateReader == null) return;

        UpdateCharacterMoveText();
        UpdateAttackAttributeImages();
        UpdateAirOrGroundText();
        UpdateCounterText();
    }

    private void UpdateCharacterMoveText()
    {
        if (CharacterMove == null) return;

        if (StateReader.StandUp) CharacterMove.text = standUp;
        else if (StateReader.SitDown) CharacterMove.text = sitDwon;
        else if (StateReader.Jump) CharacterMove.text = jump;
        else if (StateReader.High) CharacterMove.text = high;
        else if (StateReader.Middle) CharacterMove.text = middle;
        else if (StateReader.Low) CharacterMove.text = low;
        else if (StateReader.Grap) CharacterMove.text = grap;
        else if (StateReader.pos) CharacterMove.text = pos;
        else if (StateReader.Parry) CharacterMove.text = parry;
        else CharacterMove.text = string.Empty;
    }

    private void UpdateAttackAttributeImages()
    {
        SetImageChannel(bangeok, StateReader.bangeok, 0);
        SetImageChannel(powerCrash, StateReader.powerCrash, 1);
        SetImageChannel(reversal, StateReader.reversal, 2);
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
        if (AirOrGround == null) return;

        if (StateReader.Ground) AirOrGround.text = groundText;
        else if (StateReader.Air) AirOrGround.text = airText;
        else AirOrGround.text = string.Empty;
    }

    private void UpdateCounterText()
    {
        if (Counter == null) return;

        if (StateReader.NormalHit) Counter.text = normalHit;
        else if (StateReader.CounterHit) Counter.text = counterHit;
        else if (StateReader.PunishCounter) Counter.text = punishCounter;
        else Counter.text = string.Empty;
    }
}