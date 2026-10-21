using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 격투게임 캐릭터 이동 + 커맨드(기술) 판정.
/// InputReader의 방향 입력(넘버패드 1~9)과 버튼 입력을 읽어서 Player 태그 오브젝트를 움직이고,
/// 등록된 커맨드가 입력됐는지 판정합니다.
///
/// [좌우 판정]
/// - Player.x < Enemy.x (Player가 왼쪽, 오른쪽을 바라봄)  → A 조건
/// - Player.x > Enemy.x (Player가 오른쪽, 왼쪽을 바라봄)  → B 조건
///
///   입력 | A (왼쪽에 있을 때)   | B (오른쪽에 있을 때)
///   -----+----------------------+----------------------
///    1   | 앉기 + 가드          | 앉기
///    2   | 앉기                 | 앉기
///    3   | 앉기                 | 앉기 + 가드
///    4   | 뒤로 가기 + 가드     | 앞으로 가기
///    5   | 중립                 | 중립
///    6   | 앞으로 가기          | 뒤로 가기 + 가드
///    7   | 뒤로 점프            | 앞으로 점프
///    8   | 제자리 점프          | 제자리 점프
///    9   | 앞으로 점프          | 뒤로 점프
///
/// B는 A를 좌우로 뒤집은 것과 같으므로, 내부에서는 입력을 "상대 방향"(6 = 항상 앞)으로
/// 바꾼 뒤 A 표 하나로 처리합니다.
///
/// [사용법]
/// 1. Player 태그 오브젝트에 이 스크립트를 붙입니다.
/// 2. Input Reader 칸에 InputReader 컴포넌트를 넣습니다. (같은 오브젝트에 있으면 비워둬도 자동으로 찾음)
/// </summary>
public class CharacterMove : MonoBehaviour
{
    public enum MoveState
    {
        Neutral,        // 중립
        Crouch,         // 앉기
        CrouchGuard,    // 앉기 + 가드
        WalkForward,    // 앞으로 가기
        WalkBackGuard,  // 뒤로 가기 + 가드
        JumpStartup,    // 점프 준비 (방향 입력을 받는 중, 아직 땅 위)
        JumpNeutral,    // 제자리 점프
        JumpForward,    // 앞으로 점프
        JumpBack,       // 뒤로 점프
        ForwardDash,    // 앞대쉬 (66)
        BackDash,       // 백대쉬 (44)
        Run             // 달리기 (66 후 6 유지)
    }

    [Header("입력")]
    [Tooltip("방향 입력을 읽어올 InputReader. 비워두면 같은 오브젝트에서 찾음")]
    [SerializeField] private InputReader inputReader;

    [Header("캐릭터 상태")]
    [Tooltip("이동에 맞춰 서기/앉기/점프, 지상/공중 값을 바꿀 CharactorState. 비워두면 같은 오브젝트에서 찾음")]
    [SerializeField] private CharactorState characterState;

    [Header("태그")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [Header("걷기")]
    [Tooltip("앞으로 걷는 속도")]
    [SerializeField] private float forwardWalkSpeed = 3f;
    [Tooltip("뒤로 걷는 속도")]
    [SerializeField] private float backWalkSpeed = 2.5f;

    [Header("점프")]
    [Tooltip("점프 시작 시 위로 향하는 속도")]
    [SerializeField] private float jumpVelocity = 8f;
    [Tooltip("앞으로 점프할 때 가로 속도")]
    [SerializeField] private float forwardJumpSpeed = 3f;
    [Tooltip("뒤로 점프할 때 가로 속도")]
    [SerializeField] private float backJumpSpeed = 3f;
    [Tooltip("중력 가속도")]
    [SerializeField] private float gravity = 25f;
    [Tooltip("점프 입력(7, 8, 9) 후 이 프레임 수까지는 땅에서 방향 입력을 받고, 그다음 프레임에 점프함.\n" +
             "예) 4면 1~4프레임까지 방향 전환 가능(8→9면 앞 점프), 5프레임째에 마지막 방향으로 점프")]
    [SerializeField] private int jumpDirectionBufferFrames = 4;

    [Header("대쉬 / 달리기")]
    [Tooltip("같은 방향을 두 번 누를 때(66, 44), 첫 입력 후 이 프레임 수 이내에 다시 눌러야 대쉬로 인정")]
    [SerializeField] private int dashInputWindowFrames = 12;
    [Tooltip("앞대쉬 속도 (걷기보다 빠르게)")]
    [SerializeField] private float forwardDashSpeed = 8f;
    [Tooltip("앞대쉬 지속 시간(초)")]
    [SerializeField] private float forwardDashDuration = 0.2f;
    [Tooltip("백대쉬 속도 (걷기보다 빠르게)")]
    [SerializeField] private float backDashSpeed = 7f;
    [Tooltip("백대쉬 지속 시간(초)")]
    [SerializeField] private float backDashDuration = 0.25f;
    [Tooltip("달리기 속도 = 앞으로 걷는 속도 × 이 배율")]
    [SerializeField] private float runSpeedMultiplier = 1.5f;
    [Tooltip("달리는 중 Enemy와 겹치면, 이 시간(초) 뒤 자동으로 달리기를 멈춤")]
    [SerializeField] private float runStopDelayAfterOverlap = 1f;
    [Tooltip("Collider가 없을 때 사용할 겹침 판정 거리. Player와 Enemy의 x축 거리가 이 값 이하이면 겹친 것으로 봄")]
    [SerializeField] private float overlapDistance = 1f;

    [Header("바닥")]
    [Tooltip("체크하면 시작할 때의 y 위치를 바닥 높이로 사용")]
    [SerializeField] private bool useStartYAsGround = true;
    [Tooltip("바닥 높이 (useStartYAsGround가 꺼져 있을 때 사용)")]
    [SerializeField] private float groundY = 0f;

    [Header("검색")]
    [Tooltip("Enemy를 다시 찾는 주기(초)")]
    [SerializeField] private float searchInterval = 0.25f;

    // =====================================================================
    // 커맨드(방향 입력) 판정 관련  ※ InputReader에서 옮겨온 부분
    // =====================================================================

    [Header("커맨드 입력 판정 설정")]
    [Tooltip("각 커맨드의 windowFrames를 0(또는 음수)으로 두면 이 값이 기본값으로 사용됩니다.")]
    [SerializeField] private int defaultCommandWindowFrames = 4;

    [Tooltip("방향 입력 기록을 얼마나 오래(프레임 수) 보관할지. 너무 오래된 입력은 자동으로 버려집니다.")]
    [SerializeField] private int historyMaxFrames = 60;

    [Tooltip("requireAllButtons(동시입력) 커맨드에서 comboWindowFrames를 0(또는 음수)으로 두면 이 값이 기본값으로 사용됩니다.\n" +
              "예) 2로 두면 L, M을 2프레임 이내에 누른 경우까지 '동시에 누름'으로 인정합니다.")]
    [SerializeField] private int defaultComboWindowFrames = 2;

    [Tooltip("체크하면 커맨드를 '바라보는 방향' 기준으로 판정합니다.\n" +
             "예) 오른쪽에 서 있을 때도 화면상 214가 아니라 커맨드표 그대로 236으로 파동권이 나갑니다.\n" +
             "끄면 화면상 방향(절대 방향) 그대로 판정합니다.")]
    [SerializeField] private bool useRelativeDirectionForCommands = true;

    [System.Flags]
    public enum AttackButtonType
    {
        L = 1 << 0,
        M = 1 << 1,
        H = 1 << 2,
        EX = 1 << 3
    }

    [System.Serializable]
    public class CommandData
    {
        public string commandName;
        public int[] directionSequence;
        public AttackButtonType buttons;
        public bool requireAllButtons = false;
        public int comboWindowFrames = 2;
        public bool requireNeutralDirection = false;
        public int windowFrames = 0;
        public int[] stepWindowFrames;
        public int priority = 0;
    }

    [Header("등록된 커맨드 목록")]
    [Tooltip("commandName은 커맨드 이름, " +
        "directionSequence은 레버 커맨드 숫자, " +
        "buttons은 눌러야 인식되는 버튼, " +
        "requireAllButtons은 버튼에서 선택한 버튼들을 전부 눌러야 기술이 나가도록 하는 기능, " +
        "comboWindowFrames은 버튼을 물리적으로 완벽하게 동시에 누를 수 없으니, 동시에 누를 수 있는 유예 프레임 시간, " +
        "requireNeutralDirection은 레버 입력 없이 버튼만 눌러도 나가는 기술들일 때 사용, " +
        "windowFrames은 각 버튼이나 레버 사이에 연속적으로 이어 질 수 있도록 유예 프레임 시간을 뜻함(숫자가 높을수록 널널한판정, 숫자가 낮을수록 빡빡한판정, " +
        "stepWindowFrames은 레버 방향 입력 시 다음 입력이 몇프레임 이내에 이어져야 커맨드가 성공인지, " +
        "priority는 기술을 읽을때의 우선도")]
    [SerializeField]
    private List<CommandData> commandList = new List<CommandData>
    {
        new CommandData
        {
            commandName = "전인파동권",
            directionSequence = new int[] { 2, 3, 6, 2, 3, 6 },
            buttons = AttackButtonType.L | AttackButtonType.M | AttackButtonType.H | AttackButtonType.EX,
            windowFrames = 5,
            stepWindowFrames = new int[] { 0, 0, 12, 0, 0, 0 },
            priority = 20
        },
        new CommandData
        {
            commandName = "전인파장격",
            directionSequence = new int[] { 2, 1, 4, 2, 1, 4 },
            buttons = AttackButtonType.L | AttackButtonType.M | AttackButtonType.H | AttackButtonType.EX,
            windowFrames = 5,
            stepWindowFrames = new int[] { 0, 0, 12, 0, 0, 0 },
            priority = 20
        },
        new CommandData
        {
            commandName = "약승룡권",
            directionSequence = new int[] { 6, 2, 3 },
            buttons = AttackButtonType.L,
            windowFrames = 4,
            stepWindowFrames = new int[] { 10, 8, 8 },
            priority = 10
        },
        new CommandData
        {
            commandName = "중승룡권",
            directionSequence = new int[] { 6, 2, 3 },
            buttons = AttackButtonType.M,
            windowFrames = 4,
            stepWindowFrames = new int[] { 10, 8, 8 },
            priority = 10
        },
        new CommandData
        {
            commandName = "강승룡권",
            directionSequence = new int[] { 6, 2, 3 },
            buttons = AttackButtonType.H,
            windowFrames = 5,
            stepWindowFrames = new int[] { 10, 8, 8 },
            priority = 10
        },
        new CommandData
        {
            commandName = "EX승룡권",
            directionSequence = new int[] { 6, 2, 3 },
            buttons = AttackButtonType.EX,
            windowFrames = 5,
            stepWindowFrames = new int[] { 10, 8, 8 },
            priority = 10
        },
        new CommandData
        {
            commandName = "약파동권",
            directionSequence = new int[] { 2, 3, 6 },
            buttons = AttackButtonType.L,
            windowFrames = 5,
            priority = 0
        },
        new CommandData
        {
            commandName = "중파동권",
            directionSequence = new int[] { 2, 3, 6 },
            buttons = AttackButtonType.M,
            windowFrames = 5,
            priority = 0
        },
        new CommandData
        {
            commandName = "강파동권",
            directionSequence = new int[] { 2, 3, 6 },
            buttons = AttackButtonType.H,
            windowFrames = 5,
            priority = 0
        },
        new CommandData
        {
            commandName = "EX파동권",
            directionSequence = new int[] { 2, 3, 6 },
            buttons = AttackButtonType.EX,
            windowFrames = 5,
            priority = 0
        },
        new CommandData
        {
            commandName = "약파장격",
            directionSequence = new int[] { 2, 1, 4 },
            buttons = AttackButtonType.L,
            windowFrames = 5,
            priority = 0
        },
        new CommandData
        {
            commandName = "중파장격",
            directionSequence = new int[] { 2, 1, 4 },
            buttons = AttackButtonType.M,
            windowFrames = 5,
            priority = 0
        },
        new CommandData
        {
            commandName = "강파장격",
            directionSequence = new int[] { 2, 1, 4 },
            buttons = AttackButtonType.H,
            windowFrames = 5,
            priority = 0
        },
        new CommandData
        {
            commandName = "EX파장격",
            directionSequence = new int[] { 2, 1, 4 },
            buttons = AttackButtonType.EX,
            windowFrames = 5,
            priority = 0
        },
        new CommandData
        {
            commandName = "전인스택",
            directionSequence = new int[] { 2, 2 },
            buttons = AttackButtonType.L | AttackButtonType.M | AttackButtonType.H | AttackButtonType.EX,
            windowFrames = 10,
            priority = 5
        },
        new CommandData
        {
            commandName = "잡기",
            directionSequence = new int[0],
            buttons = AttackButtonType.L | AttackButtonType.M,
            requireAllButtons = true,
            comboWindowFrames = 2,
            requireNeutralDirection = true,
            priority = 5
        },
        new CommandData
        {
            commandName = "도발",
            directionSequence = new int[0],
            buttons = AttackButtonType.L | AttackButtonType.M | AttackButtonType.H | AttackButtonType.EX,
            requireAllButtons = true,
            comboWindowFrames = 2,
            requireNeutralDirection = true,
            priority = 10
        }
    };

    // ===== 다른 스크립트(애니메이션, 피격 판정 등)에서 읽어갈 상태 =====

    /// <summary>현재 이동 상태</summary>
    public MoveState CurrentState { get; private set; } = MoveState.Neutral;

    /// <summary>true면 오른쪽을 바라봄(A 조건), false면 왼쪽을 바라봄(B 조건)</summary>
    public bool IsFacingRight { get; private set; } = true;

    /// <summary>입력된 방향 그대로의 숫자 (1~9)</summary>
    public int RawDirection { get; private set; } = 5;

    /// <summary>바라보는 방향 기준으로 바꾼 숫자 (6 = 항상 앞, 4 = 항상 뒤)</summary>
    public int RelativeDirection { get; private set; } = 5;

    public bool IsGrounded { get; private set; } = true;
    public bool IsCrouching => CurrentState == MoveState.Crouch || CurrentState == MoveState.CrouchGuard;
    public bool IsGuarding => CurrentState == MoveState.CrouchGuard || CurrentState == MoveState.WalkBackGuard;
    public bool IsJumping => !IsGrounded;
    public bool IsJumpStarting => isJumpStarting;
    public bool IsDashing => CurrentState == MoveState.ForwardDash;
    public bool IsBackDashing => CurrentState == MoveState.BackDash;
    public bool IsRunning => isRunning;

    /// <summary>가장 최근에 성공한 커맨드</summary>
    public CommandData LastCommand { get; private set; }

    /// <summary>커맨드 입력에 성공했을 때 호출되는 이벤트 (공격/애니메이션 스크립트에서 구독)</summary>
    public event Action<CommandData> OnCommandTriggered;

    // ===== 이동 내부 상태 =====
    private Transform enemy;
    private float searchTimer;
    private Vector2 airVelocity; // 점프 중 속도 (x: 가로, y: 세로)
    private bool isJumpStarting;         // 점프 준비 중인지
    private int jumpStartFrame;          // 점프 입력이 처음 들어온 프레임
    private int pendingJumpDirection = 8; // 점프 준비 중 마지막으로 받은 방향 (7, 8, 9)

    // ===== 대쉬 / 달리기 내부 상태 =====
    private int previousRelativeDirection = 5;          // 직전 프레임의 상대 방향 (66 / 44 판정용)
    private int lastForwardTapFrame = int.MinValue / 2; // 마지막으로 6을 새로 누른 프레임
    private int lastBackTapFrame = int.MinValue / 2;    // 마지막으로 4를 새로 누른 프레임
    private float dashTimer;                            // 남은 대쉬 시간 (0보다 크면 대쉬 중)
    private bool isRunning;
    private float runOverlapTimer = -1f;                // 달리는 중 Enemy와 겹친 뒤 지난 시간 (-1이면 아직 안 겹침)
    private Collider selfCollider;
    private Collider enemyCollider;

    // ===== 커맨드 판정 내부 상태 =====
    private List<CommandData> sortedCommandList;

    private static readonly AttackButtonType[] allButtonFlags =
    {
        AttackButtonType.L, AttackButtonType.M, AttackButtonType.H, AttackButtonType.EX
    };

    // 각 버튼이 "마지막으로 눌린" 프레임을 기록해, 동시입력 콤보(예: L+M)
    // 판정 시 "서로 몇 프레임 이내에 눌렸는지"를 계산하는 데 사용한다.
    private readonly Dictionary<AttackButtonType, int> lastPressFrame = new Dictionary<AttackButtonType, int>
    {
        { AttackButtonType.L, int.MinValue / 2 },
        { AttackButtonType.M, int.MinValue / 2 },
        { AttackButtonType.H, int.MinValue / 2 },
        { AttackButtonType.EX, int.MinValue / 2 },
    };

    // 방향이 "바뀐 시점"만 기록하는 입력 기록 버퍼
    private struct DirectionEntry
    {
        public int direction;
        public int frame;

        public DirectionEntry(int direction, int frame)
        {
            this.direction = direction;
            this.frame = frame;
        }
    }

    private readonly List<DirectionEntry> directionHistory = new List<DirectionEntry>();
    private int commandDirection = 5; // 커맨드 판정용 현재 방향 (시작은 중립)
    private int frameCounter = 0;

    // ===== 캐릭터 상태(CharactorState) 연결 =====
    // 이동으로 정해지는 자세. 바뀐 순간에만 CharactorState에 넣어서,
    // 다른 스크립트(공격 등)가 넣은 상단/중단/하단 같은 값을 매 프레임 덮어쓰지 않도록 함
    private enum StatePosture { None, Stand, Crouch, Jump, Dash, Run }
    private StatePosture appliedPosture = StatePosture.None;

    private void Awake()
    {
        if (inputReader == null)
            inputReader = GetComponent<InputReader>();

        if (characterState == null)
            characterState = GetComponent<CharactorState>();

        selfCollider = GetComponent<Collider>();

        if (inputReader == null)
            Debug.LogError("[CharacterMove] InputReader를 찾을 수 없습니다. Input Reader 칸에 지정해 주세요.", this);

        if (!CompareTag(playerTag))
            Debug.LogWarning($"[CharacterMove] 이 오브젝트의 태그가 '{playerTag}'가 아닙니다. Player 태그 오브젝트에 붙여 주세요.", this);

        // priority가 높은 커맨드부터, 그다음은 방향 시퀀스가 긴(=더 구체적인) 커맨드부터
        // 검사하도록 미리 정렬해둔다. (예: 623L이 236L보다 먼저 검사되도록 priority로 순서를 명시)
        sortedCommandList = commandList
            .OrderByDescending(c => c.priority)
            .ThenByDescending(c => c.directionSequence?.Length ?? 0)
            .ToList();

        // 최초 상태(중립)를 기록에 남겨둔다.
        directionHistory.Add(new DirectionEntry(commandDirection, frameCounter));
    }

    private void Start()
    {
        if (useStartYAsGround)
            groundY = transform.position.y;

        FindEnemy();
        UpdateFacing();
    }

    private void Update()
    {
        if (inputReader == null) return;

        frameCounter++;

        // Enemy 주기적 검색
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f || enemy == null || !enemy.gameObject.activeInHierarchy)
        {
            FindEnemy();
            searchTimer = searchInterval;
        }

        // 좌우 판정은 땅에 있을 때만 갱신 (점프로 상대를 넘어가는 도중에 조작이 뒤집히지 않도록)
        if (IsGrounded)
            UpdateFacing();

        RawDirection = inputReader.Direction;
        RelativeDirection = ToRelativeDirection(RawDirection, IsFacingRight);

        // 커맨드 판정
        RecordDirection(useRelativeDirectionForCommands ? RelativeDirection : RawDirection);
        PruneOldHistory();
        CheckCommands();

        // 이동
        if (IsGrounded)
            UpdateGrounded();
        else
            UpdateAirborne();

        UpdateCharacterState();

        previousRelativeDirection = RelativeDirection;
    }

    // =====================================================================
    // 캐릭터 상태(CharactorState) 연결
    // =====================================================================

    // 이동 상태 -> CharactorState
    // - 공중(점프 중)                         : 점프 + 공중
    // - 앉기, 앉기 + 가드                      : 앉기 + 지상
    // - 앞대쉬, 백대쉬                         : 대쉬 + 지상
    // - 달리기                                 : 달리기 + 지상
    // - 그 외 땅 위 (중립, 걷기, 점프 준비)       : 서기 + 지상
    // 상단/중단/하단, 잡기, 자세, 패리는 대응하는 동작이 아직 없어서 건드리지 않음
    private void UpdateCharacterState()
    {
        if (characterState == null) return;

        StatePosture posture;
        if (!IsGrounded) posture = StatePosture.Jump;
        else if (IsCrouching) posture = StatePosture.Crouch;
        else if (IsDashing || IsBackDashing) posture = StatePosture.Dash;
        else if (IsRunning) posture = StatePosture.Run;
        else posture = StatePosture.Stand;

        if (posture == appliedPosture) return;
        appliedPosture = posture;

        switch (posture)
        {
            case StatePosture.Stand:
                characterState.StandUp = true;
                characterState.Ground = true;
                break;
            case StatePosture.Crouch:
                characterState.SitDown = true;
                characterState.Ground = true;
                break;
            case StatePosture.Jump:
                characterState.Jump = true;
                characterState.Air = true;
                break;
            case StatePosture.Dash:
                characterState.Dash = true;
                characterState.Ground = true;
                break;
            case StatePosture.Run:
                characterState.Run = true;
                characterState.Ground = true;
                break;
        }
    }

    // =====================================================================
    // 이동
    // =====================================================================

    private void UpdateGrounded()
    {
        // 점프 준비 중이면 방향 입력만 받고 이동하지 않음
        if (isJumpStarting)
        {
            UpdateJumpStartup();
            return;
        }

        // 대쉬 중이면 대쉬만 처리 (방향 입력 무시)
        if (dashTimer > 0f)
        {
            UpdateDash();
            return;
        }

        // 달리는 중이면 달리기 처리 (6을 놓으면 아래 일반 이동으로 넘어감)
        if (isRunning && UpdateRun())
            return;

        // 66 / 44 입력 확인
        if (TryStartDash())
            return;

        float facingSign = IsFacingRight ? 1f : -1f;

        switch (RelativeDirection)
        {
            case 1: CurrentState = MoveState.CrouchGuard; break;
            case 2:
            case 3: CurrentState = MoveState.Crouch; break;
            case 4:
                CurrentState = MoveState.WalkBackGuard;
                Move(-facingSign * backWalkSpeed);
                break;
            case 6:
                CurrentState = MoveState.WalkForward;
                Move(facingSign * forwardWalkSpeed);
                break;
            case 7:
            case 8:
            case 9:
                BeginJumpStartup(RelativeDirection);
                break;
            default: CurrentState = MoveState.Neutral; break;
        }
    }

    private void Move(float xSpeed)
    {
        transform.position += new Vector3(xSpeed * Time.deltaTime, 0f, 0f);
    }

    // =====================================================================
    // 대쉬 / 달리기
    // =====================================================================

    /// <summary>
    /// 6(또는 4)을 새로 누른 순간을 기록하고, dashInputWindowFrames 이내에 한 번 더 새로 누르면 대쉬 시작.
    /// "새로 누름" = 직전 프레임에는 그 방향이 아니었다가 이번 프레임에 그 방향이 된 것
    /// (그래서 66 사이에 레버를 한 번 놓아야 인정됨)
    /// </summary>
    private bool TryStartDash()
    {
        bool forwardTapped = RelativeDirection == 6 && previousRelativeDirection != 6;
        bool backTapped = RelativeDirection == 4 && previousRelativeDirection != 4;

        if (forwardTapped)
        {
            if (frameCounter - lastForwardTapFrame <= dashInputWindowFrames)
            {
                lastForwardTapFrame = int.MinValue / 2; // 666이 대쉬 두 번으로 읽히지 않도록 초기화
                StartDash(MoveState.ForwardDash, forwardDashDuration);
                return true;
            }
            lastForwardTapFrame = frameCounter;
            lastBackTapFrame = int.MinValue / 2;
        }
        else if (backTapped)
        {
            if (frameCounter - lastBackTapFrame <= dashInputWindowFrames)
            {
                lastBackTapFrame = int.MinValue / 2;
                StartDash(MoveState.BackDash, backDashDuration);
                return true;
            }
            lastBackTapFrame = frameCounter;
            lastForwardTapFrame = int.MinValue / 2;
        }

        return false;
    }

    private void StartDash(MoveState dashState, float duration)
    {
        CurrentState = dashState;
        dashTimer = duration;
        isRunning = false;
    }

    private void UpdateDash()
    {
        float facingSign = IsFacingRight ? 1f : -1f;

        if (CurrentState == MoveState.ForwardDash)
            Move(facingSign * forwardDashSpeed);
        else
            Move(-facingSign * backDashSpeed);

        dashTimer -= Time.deltaTime;
        if (dashTimer > 0f) return;

        // 대쉬 끝
        dashTimer = 0f;

        // 앞대쉬가 끝났을 때 6을 계속 누르고 있으면 달리기로 이어짐
        if (CurrentState == MoveState.ForwardDash && RelativeDirection == 6)
            StartRun();
        else
            CurrentState = MoveState.Neutral;
    }

    private void StartRun()
    {
        isRunning = true;
        runOverlapTimer = -1f;
        CurrentState = MoveState.Run;
    }

    private void StopRun()
    {
        isRunning = false;
        runOverlapTimer = -1f;
    }

    /// <summary>
    /// 달리기 처리. 계속 달리면 true, 달리기가 끝났으면 false를 반환
    /// </summary>
    private bool UpdateRun()
    {
        // 6을 놓으면 달리기 종료
        if (RelativeDirection != 6)
        {
            StopRun();
            return false;
        }

        // Enemy와 겹치면 그때부터 시간을 재기 시작
        if (runOverlapTimer < 0f && IsOverlappingEnemy())
            runOverlapTimer = 0f;

        if (runOverlapTimer >= 0f)
        {
            runOverlapTimer += Time.deltaTime;
            if (runOverlapTimer >= runStopDelayAfterOverlap)
            {
                // 겹친 지 1초가 지나면 자동으로 달리기 종료 (6을 계속 누르고 있으면 걷기로 바뀜)
                StopRun();
                return false;
            }
        }

        float facingSign = IsFacingRight ? 1f : -1f;
        CurrentState = MoveState.Run;
        Move(facingSign * forwardWalkSpeed * runSpeedMultiplier);
        return true;
    }

    /// <summary>
    /// Enemy와 겹쳤는지 확인.
    /// 양쪽 모두 Collider가 있으면 Collider 범위로, 없으면 x축 거리(overlapDistance)로 판정
    /// </summary>
    private bool IsOverlappingEnemy()
    {
        if (enemy == null) return false;

        if (selfCollider != null && enemyCollider != null)
            return selfCollider.bounds.Intersects(enemyCollider.bounds);

        return Mathf.Abs(enemy.position.x - transform.position.x) <= overlapDistance;
    }

    // =====================================================================
    // 점프
    // =====================================================================

    /// <summary>
    /// 7 / 8 / 9 입력이 들어오면 바로 뛰지 않고 점프 준비 상태로 들어감
    /// </summary>
    private void BeginJumpStartup(int direction)
    {
        isJumpStarting = true;
        jumpStartFrame = frameCounter;
        pendingJumpDirection = direction;
        CurrentState = MoveState.JumpStartup;
    }

    /// <summary>
    /// 점프 준비 중: 입력한 프레임 이후 jumpDirectionBufferFrames(4)프레임까지는 방향을 계속 받고,
    /// 그다음 프레임(5프레임째)에 마지막으로 받은 방향으로 점프
    /// </summary>
    private void UpdateJumpStartup()
    {
        int elapsed = frameCounter - jumpStartFrame;

        if (elapsed <= jumpDirectionBufferFrames)
        {
            // 위쪽 방향(7, 8, 9)이 들어오면 가장 마지막 입력으로 갱신
            // (위쪽이 아닌 방향으로 바뀌어도 점프는 취소되지 않고, 마지막 위쪽 방향을 유지)
            if (RelativeDirection >= 7)
                pendingJumpDirection = RelativeDirection;
            return;
        }

        // 유예 프레임이 끝났으면 점프
        isJumpStarting = false;
        float facingSign = IsFacingRight ? 1f : -1f;

        switch (pendingJumpDirection)
        {
            case 7: StartJump(MoveState.JumpBack, -facingSign * backJumpSpeed); break;
            case 9: StartJump(MoveState.JumpForward, facingSign * forwardJumpSpeed); break;
            default: StartJump(MoveState.JumpNeutral, 0f); break;
        }
    }

    private void StartJump(MoveState jumpState, float xSpeed)
    {
        CurrentState = jumpState;
        IsGrounded = false;
        airVelocity = new Vector2(xSpeed, jumpVelocity);
    }

    private void UpdateAirborne()
    {
        // 점프 방향은 점프 준비 때 정해지고, 공중에서는 바뀌지 않음
        airVelocity.y -= gravity * Time.deltaTime;

        Vector3 pos = transform.position;
        pos.x += airVelocity.x * Time.deltaTime;
        pos.y += airVelocity.y * Time.deltaTime;

        // 착지
        if (pos.y <= groundY)
        {
            pos.y = groundY;
            IsGrounded = true;
            airVelocity = Vector2.zero;
            CurrentState = MoveState.Neutral;
        }

        transform.position = pos;
    }

    // =====================================================================
    // 방향
    // =====================================================================

    /// <summary>
    /// 왼쪽을 바라보고 있으면 입력을 좌우로 뒤집어서 "6 = 앞"이 되도록 변환
    /// (1↔3, 4↔6, 7↔9)
    /// </summary>
    public static int ToRelativeDirection(int raw, bool facingRight)
    {
        if (facingRight) return raw;

        switch (raw)
        {
            case 1: return 3;
            case 3: return 1;
            case 4: return 6;
            case 6: return 4;
            case 7: return 9;
            case 9: return 7;
            default: return raw;
        }
    }

    private void UpdateFacing()
    {
        // Enemy가 없으면(재생성 대기 등) 이전 방향 유지
        if (enemy == null) return;

        float dx = enemy.position.x - transform.position.x;
        if (dx > 0f) IsFacingRight = true;        // A 조건
        else if (dx < 0f) IsFacingRight = false;  // B 조건
        // x가 완전히 같으면 이전 방향 유지
    }

    private void FindEnemy()
    {
        enemy = null;
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float closestSqr = float.MaxValue;

        foreach (GameObject e in enemies)
        {
            float sqr = (e.transform.position - transform.position).sqrMagnitude;
            if (sqr < closestSqr)
            {
                closestSqr = sqr;
                enemy = e.transform;
            }
        }

        enemyCollider = enemy != null ? enemy.GetComponent<Collider>() : null;
    }

    // =====================================================================
    // 커맨드 판정  ※ InputReader에서 옮겨온 부분
    // =====================================================================

    private void RecordDirection(int newDirection)
    {
        // 방향이 바뀐 순간만 기록한다 (같은 방향을 계속 누르고 있는 프레임은 기록하지 않음)
        if (newDirection != commandDirection)
        {
            commandDirection = newDirection;
            directionHistory.Add(new DirectionEntry(commandDirection, frameCounter));
        }
    }

    private void PruneOldHistory()
    {
        int cutoffFrame = frameCounter - historyMaxFrames;
        int removeCount = 0;
        while (removeCount < directionHistory.Count - 1 && directionHistory[removeCount].frame < cutoffFrame)
        {
            removeCount++;
        }
        if (removeCount > 0)
        {
            directionHistory.RemoveRange(0, removeCount);
        }
    }

    private void CheckCommands()
    {
        var pressedButtons = new List<AttackButtonType>();

        // 이번 프레임에 새로 눌린 버튼을 모으면서, 동시입력 판정에 쓸 "마지막으로 눌린 프레임"도 갱신한다.
        if (inputReader.buttonLDown) { pressedButtons.Add(AttackButtonType.L); lastPressFrame[AttackButtonType.L] = frameCounter; }
        if (inputReader.buttonMDown) { pressedButtons.Add(AttackButtonType.M); lastPressFrame[AttackButtonType.M] = frameCounter; }
        if (inputReader.buttonHDown) { pressedButtons.Add(AttackButtonType.H); lastPressFrame[AttackButtonType.H] = frameCounter; }
        if (inputReader.buttonEXDown) { pressedButtons.Add(AttackButtonType.EX); lastPressFrame[AttackButtonType.EX] = frameCounter; }

        if (pressedButtons.Count == 0) return;

        // 한 프레임에 여러 버튼이 동시에 눌리는 경우(예: L, M이 정확히 같은 프레임에 눌림),
        // 같은 콤보 커맨드가 버튼별로 중복 출력되지 않도록 이번 프레임에 이미 채택된 커맨드를 기록해둔다.
        var triggeredThisFrame = new HashSet<CommandData>();

        foreach (var button in pressedButtons)
        {
            foreach (var command in sortedCommandList)
            {
                if (triggeredThisFrame.Contains(command)) continue;

                // 커맨드에 이 버튼이 체크돼 있지 않으면(비트가 없으면) 건너뛴다.
                if ((command.buttons & button) == 0) continue;

                // 동시입력(콤보) 커맨드는 buttons에 포함된 버튼이 전부, 서로 comboWindowFrames 이내에 눌려 있어야 한다.
                if (command.requireAllButtons && !IsButtonComboSatisfied(command)) continue;

                // 방향이 중립일 때만 발동하는 커맨드(예: 중립 잡기) 조건 체크
                if (command.requireNeutralDirection && commandDirection != 5) continue;

                // directionSequence가 비어 있으면 방향 조건 없이(버튼 조건만으로) 통과시킨다.
                bool motionOk = command.directionSequence == null || command.directionSequence.Length == 0
                    || CheckMotionInput(command);

                if (motionOk)
                {
                    Debug.Log($"[Command] '{command.commandName}' 커맨드 입력 성공! (버튼: {button}, 프레임: {frameCounter})");
                    triggeredThisFrame.Add(command);
                    LastCommand = command;
                    OnCommandTriggered?.Invoke(command);
                    break; // 이 버튼에 대해서는 가장 구체적인/우선순위 높은 커맨드 하나만 채택하고 종료
                }
            }
        }
    }

    private bool IsButtonComboSatisfied(CommandData command)
    {
        int? minFrame = null;
        int? maxFrame = null;

        foreach (var flag in allButtonFlags)
        {
            if ((command.buttons & flag) == 0) continue; // 이 커맨드가 요구하는 버튼이 아님

            if (!IsButtonCurrentlyPressed(flag)) return false; // 요구 버튼 중 하나라도 안 눌려 있으면 실패

            int pressFrame = lastPressFrame[flag];
            if (minFrame == null || pressFrame < minFrame) minFrame = pressFrame;
            if (maxFrame == null || pressFrame > maxFrame) maxFrame = pressFrame;
        }

        if (minFrame == null) return false; // buttons에 체크된 버튼이 하나도 없음

        int window = command.comboWindowFrames > 0 ? command.comboWindowFrames : defaultComboWindowFrames;
        return (maxFrame.Value - minFrame.Value) <= window;
    }

    private bool IsButtonCurrentlyPressed(AttackButtonType flag)
    {
        switch (flag)
        {
            case AttackButtonType.L: return inputReader.buttonL;
            case AttackButtonType.M: return inputReader.buttonM;
            case AttackButtonType.H: return inputReader.buttonH;
            case AttackButtonType.EX: return inputReader.buttonEX;
            default: return false;
        }
    }

    private int GetGapWindow(CommandData command, int gapIndex)
    {
        var steps = command.stepWindowFrames;
        if (steps != null && steps.Length == command.directionSequence.Length &&
            gapIndex >= 0 && gapIndex < steps.Length && steps[gapIndex] > 0)
        {
            return steps[gapIndex];
        }

        return command.windowFrames > 0 ? command.windowFrames : defaultCommandWindowFrames;
    }

    private bool CheckMotionInput(CommandData command)
    {
        int[] motion = command.directionSequence;
        if (motion == null || motion.Length == 0) return false;

        int motionIndex = motion.Length - 1; // 가장 최근에 요구되는 방향부터 역순으로 매칭
        int lastMatchedFrame = frameCounter;  // 버튼이 눌린 현재 프레임을 기준점으로 시작

        for (int i = directionHistory.Count - 1; i >= 0; i--)
        {
            if (motionIndex < 0) break;

            DirectionEntry entry = directionHistory[i];

            if (entry.direction == motion[motionIndex])
            {
                // 직전에 매칭된 시점(버튼 입력 또는 다음 방향)과의 프레임 차가
                // 이 구간에 허용된 유예 프레임을 넘으면 실패로 처리한다.
                int gapWindow = GetGapWindow(command, motionIndex);
                if (lastMatchedFrame - entry.frame > gapWindow)
                {
                    return false;
                }

                lastMatchedFrame = entry.frame;
                motionIndex--;
            }
            // 매칭되지 않는 방향(예: 623 도중 스쳐 지나가는 중립, 6236L의 여분의 6)은 그냥 건너뛴다.
        }

        return motionIndex < 0; // 모든 방향 단계를 순서대로 다 찾았으면 성공
    }

    private void OnValidate()
    {
        if (forwardWalkSpeed < 0f) forwardWalkSpeed = 0f;
        if (backWalkSpeed < 0f) backWalkSpeed = 0f;
        if (forwardJumpSpeed < 0f) forwardJumpSpeed = 0f;
        if (backJumpSpeed < 0f) backJumpSpeed = 0f;
        if (jumpVelocity < 0f) jumpVelocity = 0f;
        if (gravity < 0.01f) gravity = 0.01f;
        if (searchInterval < 0f) searchInterval = 0f;
        if (historyMaxFrames < 1) historyMaxFrames = 1;
        if (jumpDirectionBufferFrames < 0) jumpDirectionBufferFrames = 0;
        if (dashInputWindowFrames < 1) dashInputWindowFrames = 1;
        if (forwardDashSpeed < 0f) forwardDashSpeed = 0f;
        if (backDashSpeed < 0f) backDashSpeed = 0f;
        if (forwardDashDuration < 0f) forwardDashDuration = 0f;
        if (backDashDuration < 0f) backDashDuration = 0f;
        if (runSpeedMultiplier < 0f) runSpeedMultiplier = 0f;
        if (runStopDelayAfterOverlap < 0f) runStopDelayAfterOverlap = 0f;
        if (overlapDistance < 0f) overlapDistance = 0f;
    }
}