using Unity.VisualScripting;
using UnityEngine;

public class CharactorState : MonoBehaviour
{
    [Header("캐릭터 행동")]
    public bool standUp = true; //서기
    public bool sitDown = false; //앉기
    public bool jump = false; //점프
    public bool high = false; //상단
    public bool middle = false; //중단
    public bool low = false; //하단
    public bool grap = false; //잡기
    public bool pos = false; //자세
    public bool parry = false; //패리
    public bool dash = false; //대쉬 (앞대쉬, 백대쉬)
    public bool run = false; //달리기

    [Header("공격 속성")]
    public bool bangeok = false; //반격기
    public bool powerCrash = false; //파워크래쉬
    public bool reversal = false; //리버설(무적기)


    [Header("캐릭터 지상 판정")]
    public bool ground = true; //지상
    public bool air = false; //공중(점프)

    [Header("카운터 판정")]
    public bool normalHit = false; //노말히트
    public bool counterHit = false; //카운터히트
    public bool punishCounter = false; //윕퍼니시

    private enum ActionType { StandUp, SitDown, Jump, High, Middle, Low, Grap, Pos, Parry, Dash, Run }
    private enum GroundType { Ground, Air }
    private enum CounterType { NormalHit, CounterHit, PunishCounter }

    public bool StandUp { get => standUp; set => SetAction(ActionType.StandUp, value); }
    public bool SitDown { get => sitDown; set => SetAction(ActionType.SitDown, value); }
    public bool Jump { get => jump; set => SetAction(ActionType.Jump, value); }
    public bool High { get => high; set => SetAction(ActionType.High, value); }
    public bool Middle { get => middle; set => SetAction(ActionType.Middle, value); }
    public bool Low { get => low; set => SetAction(ActionType.Low, value); }
    public bool Grap { get => grap; set => SetAction(ActionType.Grap, value); }
    public bool Pos { get => pos; set => SetAction(ActionType.Pos, value); }
    public bool Parry { get => parry; set => SetAction(ActionType.Parry, value); }
    public bool Dash { get => dash; set => SetAction(ActionType.Dash, value); }
    public bool Run { get => run; set => SetAction(ActionType.Run, value); }

    public bool Ground { get => ground; set => SetGroundState(GroundType.Ground, value); }
    public bool Air { get => air; set => SetGroundState(GroundType.Air, value); }

    public bool NormalHit { get => normalHit; set => SetCounterState(CounterType.NormalHit, value); }
    public bool CounterHit { get => counterHit; set => SetCounterState(CounterType.CounterHit, value); }
    public bool PunishCounter { get => punishCounter; set => SetCounterState(CounterType.PunishCounter, value); }

    private void SetAction(ActionType type, bool value)
    {
        if (value)
        {
            standUp = type == ActionType.StandUp;
            sitDown = type == ActionType.SitDown;
            jump = type == ActionType.Jump;
            high = type == ActionType.High;
            middle = type == ActionType.Middle;
            low = type == ActionType.Low;
            grap = type == ActionType.Grap;
            pos = type == ActionType.Pos;
            parry = type == ActionType.Parry;
            dash = type == ActionType.Dash;
            run = type == ActionType.Run;
        }
        else
        {
            switch (type)
            {
                case ActionType.StandUp: standUp = false; break;
                case ActionType.SitDown: sitDown = false; break;
                case ActionType.Jump: jump = false; break;
                case ActionType.High: high = false; break;
                case ActionType.Middle: middle = false; break;
                case ActionType.Low: low = false; break;
                case ActionType.Grap: grap = false; break;
                case ActionType.Pos: pos = false; break;
                case ActionType.Parry: parry = false; break;
                case ActionType.Dash: dash = false; break;
                case ActionType.Run: run = false; break;
            }
        }
        EnsureActionDefault();

        if (value && type == ActionType.Jump) SetGroundState(GroundType.Air, true);  
    }

    private void SetGroundState(GroundType type, bool value)
    {
        if (value)
        {
            ground = type == GroundType.Ground;
            air = type == GroundType.Air;
        }
        else
        {
            switch (type)
            {
                case GroundType.Ground: ground = false; break;
                case GroundType.Air: air = false; break;
            }
        }
        EnsureGroundDefault();
    }

    private void SetCounterState(CounterType type, bool value)
    {
        if (value)
        {
            normalHit = type == CounterType.NormalHit;
            counterHit = type == CounterType.CounterHit;
            punishCounter = type == CounterType.PunishCounter;
        }
        else
        {
            switch (type)
            {
                case CounterType.NormalHit: normalHit = false; break;
                case CounterType.CounterHit: counterHit = false; break;
                case CounterType.PunishCounter: punishCounter = false; break;
            }
        }
    }

    private void EnsureActionDefault()
    {
        if (!standUp && !sitDown && !jump && !high && !middle && !low && !grap && !pos && !parry && !dash && !run)
        {
            standUp = true;
        }
    }

    private void EnsureGroundDefault()
    {
        if (!ground && !air)
        {
            ground = true;
        }
    }

    private bool prevStandUp, prevSitDown, prevJump, prevHigh, prevMiddle, prevLow, prevGrap, prevPos, prevParry, prevDash, prevRun;
    private bool prevGround, prevAir;
    private bool prevNormalHit, prevCounterHit, prevPunishCounter;
    private bool validateInitialized;

    private void OnValidate()
    {
        if (!validateInitialized)
        {
            CachePreviousValues();
            validateInitialized = true;
            return;
        }

        if (standUp && !prevStandUp) SetAction(ActionType.StandUp, true);
        else if (sitDown && !prevSitDown) SetAction(ActionType.SitDown, true);
        else if (jump && !prevJump) SetAction(ActionType.Jump, true);
        else if (high && !prevHigh) SetAction(ActionType.High, true);
        else if (middle && !prevMiddle) SetAction(ActionType.Middle, true);
        else if (low && !prevLow) SetAction(ActionType.Low, true);
        else if (grap && !prevGrap) SetAction(ActionType.Grap, true);
        else if (pos && !prevPos) SetAction(ActionType.Pos, true);
        else if (parry && !prevParry) SetAction(ActionType.Parry, true);
        else if (dash && !prevDash) SetAction(ActionType.Dash, true);
        else if (run && !prevRun) SetAction(ActionType.Run, true);
        else EnsureActionDefault(); 

        if (ground && !prevGround) SetGroundState(GroundType.Ground, true);
        else if (air && !prevAir) SetGroundState(GroundType.Air, true);
        else EnsureGroundDefault();

        if (normalHit && !prevNormalHit) SetCounterState(CounterType.NormalHit, true);
        else if (counterHit && !prevCounterHit) SetCounterState(CounterType.CounterHit, true);
        else if (punishCounter && !prevPunishCounter) SetCounterState(CounterType.PunishCounter, true);

        CachePreviousValues();
    }

    private void CachePreviousValues()
    {
        prevStandUp = standUp; prevSitDown = sitDown; prevJump = jump; prevHigh = high;
        prevMiddle = middle; prevLow = Low; prevGrap = grap; prevPos = pos; prevParry = parry;
        prevDash = dash; prevRun = run;
        prevGround = ground; prevAir = air;
        prevNormalHit = normalHit; prevCounterHit = counterHit; prevPunishCounter = punishCounter;
    }
}