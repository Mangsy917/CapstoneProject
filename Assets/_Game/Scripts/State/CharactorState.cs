using UnityEngine;

// 캐릭터(Player, 더미)의 지금 판정 값. StateUI가 읽어서 화면에 보여줌
// - 캐릭터 행동, 지상 판정: CharacterMove가 넣음 (CharacterMove가 없는 더미는 서기 + 지상 그대로)
// - 공격 판정 높이, 공격 특성: 공격하는 동안 CharacterAttack이 넣고, 공격이 끝나면 지움
// - 데미지: 이 캐릭터가 상대에게 준 데미지 (CharacterHealth가 맞을 때 때린 쪽에 알려줌)
public class CharactorState : MonoBehaviour
{
    [Header("캐릭터 행동")]
    public bool standUp = true; //서기
    public bool sitDown = false; //앉기
    public bool jump = false; //점프

    [Header("공격 판정 높이 (공격 중일 때만, 잡기는 높이 없음)")]
    public bool high = false; //상단
    public bool middle = false; //중단
    public bool low = false; //하단

    [Header("공격 특성 (공격 중일 때만)")]
    public bool projectile = false; //장풍
    public bool grap = false; //잡기
    public bool superArmor = false; //슈퍼아머

    [Header("캐릭터 지상 판정")]
    public bool ground = true; //지상
    public bool air = false; //공중(점프)

    [Header("카운터 판정")]
    public bool normalHit = false; //노말히트
    public bool counterHit = false; //카운터히트
    public bool punishCounter = false; //윕퍼니시

    [Header("데미지 (이 캐릭터가 준 데미지)")]
    public int comboDamage = 0; //마지막 콤보의 전체 데미지
    public int singleDamage = 0; //마지막으로 맞힌 기술 하나의 데미지

    [Tooltip("맞은 상대의 데미지가 확정된 뒤(때린 공격이 끝난 뒤) 이 프레임 수 안에 다시 맞히면 같은 콤보로 셈. 피격 경직이 생기면 경직 기준으로 바꿀 것")]
    [SerializeField] private int _comboGapFrames = 15;

    private enum ActionType { StandUp, SitDown, Jump }
    private enum HeightType { None, High, Middle, Low }
    private enum GroundType { Ground, Air }
    private enum CounterType { NormalHit, CounterHit, PunishCounter }

    public bool StandUp { get => standUp; set => SetAction(ActionType.StandUp, value); }
    public bool SitDown { get => sitDown; set => SetAction(ActionType.SitDown, value); }
    public bool Jump { get => jump; set => SetAction(ActionType.Jump, value); }

    public bool High { get => high; set => SetHeight(value ? HeightType.High : HeightType.None); }
    public bool Middle { get => middle; set => SetHeight(value ? HeightType.Middle : HeightType.None); }
    public bool Low { get => low; set => SetHeight(value ? HeightType.Low : HeightType.None); }

    public bool Projectile { get => projectile; set => projectile = value; }
    public bool Grap { get => grap; set => grap = value; }
    public bool SuperArmor { get => superArmor; set => superArmor = value; }

    public bool Ground { get => ground; set => SetGroundState(GroundType.Ground, value); }
    public bool Air { get => air; set => SetGroundState(GroundType.Air, value); }

    public bool NormalHit { get => normalHit; set => SetCounterState(CounterType.NormalHit, value); }
    public bool CounterHit { get => counterHit; set => SetCounterState(CounterType.CounterHit, value); }
    public bool PunishCounter { get => punishCounter; set => SetCounterState(CounterType.PunishCounter, value); }

    public int ComboDamage => comboDamage;
    public int SingleDamage => singleDamage;

    private Game.Character.CharacterHealth _comboTarget; // 지금 콤보를 맞고 있는 상대 (콤보가 끝났으면 null)
    private int _framesSinceSettled;                      // 상대의 데미지가 확정된 뒤 지난 프레임

    private void Update()
    {
        // 슬로우 모션 중에 건너뛰는 프레임에는 세지 않음
        if (!Game.Field.GameSpeed.IsTickFrame) return;
        if (_comboTarget == null || !_comboTarget.IsDamageSettled) return;

        _framesSinceSettled++;
        if (_framesSinceSettled > _comboGapFrames) _comboTarget = null;
    }

    /// <summary>공격이 끝났을 때 판정 높이와 공격 특성을 모두 끔</summary>
    public void ClearAttack()
    {
        SetHeight(HeightType.None);
        projectile = false;
        grap = false;
        superArmor = false;
    }

    /// <summary>상대(target)에게 damage를 줬을 때 호출. 콤보가 이어지는 중이면 콤보 데미지에 더하고, 아니면 새 콤보로 시작</summary>
    public void AddDealtDamage(int damage, Game.Character.CharacterHealth target)
    {
        bool isComboContinuing = _comboTarget != null && _comboTarget == target;
        comboDamage = isComboContinuing ? comboDamage + damage : damage;
        singleDamage = damage;
        _comboTarget = target;
        _framesSinceSettled = 0;
    }

    private void SetAction(ActionType type, bool value)
    {
        if (value)
        {
            standUp = type == ActionType.StandUp;
            sitDown = type == ActionType.SitDown;
            jump = type == ActionType.Jump;
        }
        else
        {
            switch (type)
            {
                case ActionType.StandUp: standUp = false; break;
                case ActionType.SitDown: sitDown = false; break;
                case ActionType.Jump: jump = false; break;
            }
        }
        EnsureActionDefault();

        if (value && type == ActionType.Jump) SetGroundState(GroundType.Air, true);
    }

    private void SetHeight(HeightType type)
    {
        high = type == HeightType.High;
        middle = type == HeightType.Middle;
        low = type == HeightType.Low;
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
        if (!standUp && !sitDown && !jump)
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

    private bool prevStandUp, prevSitDown, prevJump;
    private bool prevHigh, prevMiddle, prevLow;
    private bool prevGround, prevAir;
    private bool prevNormalHit, prevCounterHit, prevPunishCounter;
    private bool validateInitialized;

    private void OnValidate()
    {
        if (_comboGapFrames < 0) _comboGapFrames = 0;

        if (!validateInitialized)
        {
            CachePreviousValues();
            validateInitialized = true;
            return;
        }

        if (standUp && !prevStandUp) SetAction(ActionType.StandUp, true);
        else if (sitDown && !prevSitDown) SetAction(ActionType.SitDown, true);
        else if (jump && !prevJump) SetAction(ActionType.Jump, true);
        else EnsureActionDefault();

        if (high && !prevHigh) SetHeight(HeightType.High);
        else if (middle && !prevMiddle) SetHeight(HeightType.Middle);
        else if (low && !prevLow) SetHeight(HeightType.Low);

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
        prevStandUp = standUp; prevSitDown = sitDown; prevJump = jump;
        prevHigh = high; prevMiddle = middle; prevLow = low;
        prevGround = ground; prevAir = air;
        prevNormalHit = normalHit; prevCounterHit = counterHit; prevPunishCounter = punishCounter;
    }
}
