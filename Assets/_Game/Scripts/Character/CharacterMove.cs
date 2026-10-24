using UnityEngine;

/// <summary>
/// 격투게임 캐릭터 이동.
/// InputReader의 방향 입력(넘버패드 1~9)을 읽어서 Player 태그 오브젝트를 움직입니다.
/// 커맨드(기술) 판정은 CharacterCommand에서 처리합니다.
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

    [Header("공격")]
    [Tooltip("공격 중인지 알려줄 CharacterAttack. 공격 중에는 땅에서 움직이지 않음. 비워두면 같은 오브젝트에서 찾음")]
    [SerializeField] private Game.Character.CharacterAttack characterAttack;

    [Header("태그")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";

    [Header("걷기")]
    [Tooltip("앞으로 걸을 때 1프레임에 움직이는 거리 (예: 0.05면 60프레임에 3)")]
    [SerializeField] private float forwardWalkDistancePerFrame = 0.05f;
    [Tooltip("뒤로 걸을 때 1프레임에 움직이는 거리")]
    [SerializeField] private float backWalkDistancePerFrame = 2f / 60f;

    // 점프 전체 프레임 = 점프 준비(입력한 프레임 1 + jumpDirectionBufferFrames) + jumpAirFrames
    // 기본값: 준비 5프레임(1~5, 지상) + 공중 38프레임(6~43) = 43프레임, 44프레임째부터 다시 땅에서 움직임
    [Header("점프")]
    [Tooltip("점프의 가장 높은 곳까지의 높이 (바닥 기준)")]
    [SerializeField] private float jumpHeight = 2.88f;
    [Tooltip("공중에 떠 있는 프레임 수 (공중 판정을 가지는 프레임). 높이와 상관없이 항상 이 프레임 수만에 착지함")]
    [SerializeField] private int jumpAirFrames = 38;
    [Tooltip("앞으로 점프할 때 공중에 있는 동안 가는 전체 가로 거리")]
    [SerializeField] private float forwardJumpDistance = 1.9f;
    [Tooltip("뒤로 점프할 때 공중에 있는 동안 가는 전체 가로 거리")]
    [SerializeField] private float backJumpDistance = 1.9f;
    [Tooltip("점프 입력(7, 8, 9) 후 이 프레임 수까지는 땅에서 방향 입력을 받고, 그다음 프레임에 점프함.\n" +
             "예) 4면 입력한 프레임 포함 1~5프레임은 지상(2~5프레임까지 방향 전환 가능, 8→9면 앞 점프), 6프레임째부터 공중")]
    [SerializeField] private int jumpDirectionBufferFrames = 4;

    [Header("대쉬 / 달리기")]
    [Tooltip("같은 방향을 두 번 누를 때(66, 44), 첫 입력 후 이 프레임 수 이내에 다시 눌러야 대쉬로 인정")]
    [SerializeField] private int dashInputWindowFrames = 12;
    // 대쉬를 시작한 프레임(66의 두 번째 6을 누른 프레임)을 1로 세서 이 프레임 수만큼 움직이고, 그다음 프레임부터 다른 동작이 가능
    [Tooltip("앞대쉬로 움직이는 전체 거리")]
    [SerializeField] private float forwardDashDistance = 1.2f;
    [Tooltip("앞대쉬 전체 프레임 수")]
    [SerializeField] private int forwardDashFrames = 19;
    [Tooltip("백대쉬로 움직이는 전체 거리 (앞대쉬보다 짧게)")]
    [SerializeField] private float backDashDistance = 1f;
    [Tooltip("백대쉬 전체 프레임 수")]
    [SerializeField] private int backDashFrames = 23;
    [Tooltip("달리는 동안 1프레임에 움직이는 거리 (예: 0.075면 60프레임에 4.5)")]
    [SerializeField] private float runDistancePerFrame = 0.075f;
    [Tooltip("달리는 중 Enemy와 겹치면, 겹친 프레임부터 이 프레임 수 뒤 자동으로 달리기를 멈춤")]
    [SerializeField] private int runStopFramesAfterOverlap = 60;
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

    // ===== 이동 내부 상태 =====
    private Transform enemy;
    private float searchTimer;
    private bool isJumpStarting;         // 점프 준비 중인지
    private int jumpStartFrame;          // 점프 입력이 처음 들어온 프레임
    private int pendingJumpDirection = 8; // 점프 준비 중 마지막으로 받은 방향 (7, 8, 9)

    // 지금 하고 있는 공중 포물선 (레버 점프, 공격으로 뛰어오르기 공통)
    private int airFrame;                // 공중에 뜬 뒤 지난 프레임 (공중 첫 프레임 = 1)
    private int airFrames = 1;           // 공중에 떠 있는 전체 프레임 수
    private float airHeight;             // 가장 높은 곳까지의 높이
    private float airDistancePerFrame;   // 1프레임에 움직이는 가로 거리 (오른쪽이 +)

    // ===== 대쉬 / 달리기 내부 상태 =====
    private int previousRelativeDirection = 5;          // 직전 프레임의 상대 방향 (66 / 44 판정용)
    private int lastForwardTapFrame = int.MinValue / 2; // 마지막으로 6을 새로 누른 프레임
    private int lastBackTapFrame = int.MinValue / 2;    // 마지막으로 4를 새로 누른 프레임
    private int dashFramesLeft;                         // 남은 대쉬 프레임 (0보다 크면 대쉬 중)
    private bool isRunning;
    private int runOverlapFrames = -1;                  // 달리는 중 Enemy와 겹친 뒤 지난 프레임 (-1이면 아직 안 겹침)
    private Collider selfCollider;
    private Collider enemyCollider;
    private Game.Character.Pushbox selfPushbox;
    private Game.Character.Pushbox enemyPushbox;

    private int frameCounter = 0; // 대쉬, 점프 준비 판정에 쓰는 프레임 수

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

        if (characterAttack == null)
            characterAttack = GetComponent<Game.Character.CharacterAttack>();

        selfCollider = GetComponent<Collider>();
        selfPushbox = GetComponent<Game.Character.Pushbox>();

        if (inputReader == null)
            Debug.LogError("[CharacterMove] InputReader를 찾을 수 없습니다. Input Reader 칸에 지정해 주세요.", this);

        if (!CompareTag(playerTag))
            Debug.LogWarning($"[CharacterMove] 이 오브젝트의 태그가 '{playerTag}'가 아닙니다. Player 태그 오브젝트에 붙여 주세요.", this);
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

        // 슬로우 모션 중에는 건너뛰는 프레임이 있음 (GameSpeed)
        if (!Game.Field.GameSpeed.IsTickFrame) return;

        frameCounter++;

        // Enemy 주기적 검색
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f || enemy == null || !enemy.gameObject.activeInHierarchy)
        {
            FindEnemy();
            searchTimer = searchInterval;
        }

        // 레버 점프의 공중 프레임을 다 채웠으면 이번 프레임부터 땅에서 움직임 (착지한 프레임에 바로 다시 점프 가능)
        if (!IsGrounded && airFrame >= airFrames)
            Land();

        // 좌우 판정은 땅에 있을 때만 갱신 (점프로 상대를 넘어가는 도중에 조작이 뒤집히지 않도록)
        if (IsGrounded)
            UpdateFacing();

        RawDirection = inputReader.Direction;
        RelativeDirection = ToRelativeDirection(RawDirection, IsFacingRight);

        CancelDashTapOnOtherDirection();

        bool isAttacking = characterAttack != null && characterAttack.IsAttacking;

        // 이동 (공격 중에는 땅에서 멈추고, 공중 공격은 점프 궤도를 그대로 따라감)
        if (!IsGrounded)
            UpdateAirborne();
        else if (isAttacking)
            StopForAttack();
        else
            UpdateGrounded();

        // 공격 중에는 CharacterAttack이 넣은 상단/중단/하단/잡기 값을 덮어쓰지 않음.
        // 공격이 끝나면 이동 자세를 다시 넣도록 기록을 지워둠
        if (isAttacking)
            appliedPosture = StatePosture.None;
        else
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
        if (dashFramesLeft > 0)
        {
            UpdateDash();
            return;
        }

        // 대쉬 프레임을 다 채웠으면 이번 프레임부터 다른 동작 (앞대쉬 후 6을 누르고 있으면 달리기)
        if (IsDashing || IsBackDashing)
            EndDash();

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
                MoveBy(-facingSign * backWalkDistancePerFrame);
                break;
            case 6:
                CurrentState = MoveState.WalkForward;
                MoveBy(facingSign * forwardWalkDistancePerFrame);
                break;
            case 7:
            case 8:
            case 9:
                BeginJumpStartup(RelativeDirection);
                break;
            default: CurrentState = MoveState.Neutral; break;
        }
    }

    /// <summary>
    /// 재시작할 때 호출. 점프, 대쉬, 달리기, 대쉬 입력 대기를 모두 취소하고 바닥에 선 중립 상태로 되돌림
    /// </summary>
    public void ResetMovement()
    {
        StopForAttack();
        IsGrounded = true;
        lastForwardTapFrame = int.MinValue / 2;
        lastBackTapFrame = int.MinValue / 2;
        appliedPosture = StatePosture.None; // 다음 프레임에 서기 자세를 다시 넣음

        Vector3 position = transform.position;
        position.y = groundY;
        transform.position = position;
    }

    // 공격이 시작되면 하던 점프 준비, 대쉬, 달리기를 취소하고 제자리에 멈춤
    private void StopForAttack()
    {
        isJumpStarting = false;
        dashFramesLeft = 0;
        StopRun();
        CurrentState = MoveState.Neutral;
    }

    // 이번 프레임에 x만큼 움직임 (걷기, 대쉬, 달리기는 모두 프레임 기준)
    private void MoveBy(float x)
    {
        transform.position += new Vector3(x, 0f, 0f);
    }

    // =====================================================================
    // 대쉬 / 달리기
    // =====================================================================

    /// <summary>
    /// 66 / 44 사이에는 중립(5)만 거쳐야 대쉬로 인정되도록,
    /// 레버가 6(또는 4)도 5도 아닌 방향으로 바뀌면 기다리던 대쉬 입력을 취소.
    /// 예) 656은 대쉬, 636 / 6363636 / 414141은 대쉬가 아님
    /// 대쉬, 점프, 공격 중에도 놓치지 않도록 매 프레임 확인
    /// </summary>
    private void CancelDashTapOnOtherDirection()
    {
        if (RelativeDirection == previousRelativeDirection || RelativeDirection == 5) return;

        if (RelativeDirection != 6) lastForwardTapFrame = int.MinValue / 2;
        if (RelativeDirection != 4) lastBackTapFrame = int.MinValue / 2;
    }

    /// <summary>
    /// 6(또는 4)을 새로 누른 순간을 기록하고, dashInputWindowFrames 이내에 한 번 더 새로 누르면 대쉬 시작.
    /// "새로 누름" = 직전 프레임에는 그 방향이 아니었다가 이번 프레임에 그 방향이 된 것
    /// (그래서 66 사이에 레버를 한 번 놓아야 인정됨. 놓는 동안 중립 말고 다른 방향을 거치면 취소됨)
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
                StartDash(MoveState.ForwardDash, forwardDashFrames);
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
                StartDash(MoveState.BackDash, backDashFrames);
                return true;
            }
            lastBackTapFrame = frameCounter;
            lastForwardTapFrame = int.MinValue / 2;
        }

        return false;
    }

    // 대쉬 시작. 이 프레임이 대쉬 첫 프레임이므로 바로 한 프레임만큼 움직임
    private void StartDash(MoveState dashState, int frames)
    {
        CurrentState = dashState;
        dashFramesLeft = frames;
        isRunning = false;
        UpdateDash();
    }

    // 전체 거리를 프레임 수로 나눠서 매 프레임 같은 거리만큼 움직임
    private void UpdateDash()
    {
        float facingSign = IsFacingRight ? 1f : -1f;

        if (CurrentState == MoveState.ForwardDash)
            MoveBy(facingSign * forwardDashDistance / forwardDashFrames);
        else
            MoveBy(-facingSign * backDashDistance / backDashFrames);

        dashFramesLeft--;
    }

    private void EndDash()
    {
        // 앞대쉬가 끝났을 때 6을 계속 누르고 있으면 달리기로 이어짐
        if (CurrentState == MoveState.ForwardDash && RelativeDirection == 6)
            StartRun();
        else
            CurrentState = MoveState.Neutral;
    }

    private void StartRun()
    {
        isRunning = true;
        runOverlapFrames = -1;
        CurrentState = MoveState.Run;
    }

    private void StopRun()
    {
        isRunning = false;
        runOverlapFrames = -1;
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

        // Enemy와 겹치면 그때부터 프레임을 세기 시작
        if (runOverlapFrames < 0 && IsOverlappingEnemy())
            runOverlapFrames = 0;

        if (runOverlapFrames >= 0)
        {
            runOverlapFrames++;
            if (runOverlapFrames >= runStopFramesAfterOverlap)
            {
                // 겹친 뒤 runStopFramesAfterOverlap 프레임이 지나면 자동으로 달리기 종료 (6을 계속 누르고 있으면 걷기로 바뀜)
                StopRun();
                return false;
            }
        }

        float facingSign = IsFacingRight ? 1f : -1f;
        CurrentState = MoveState.Run;
        MoveBy(facingSign * runDistancePerFrame);
        return true;
    }

    /// <summary>
    /// Enemy와 겹쳤는지 확인.
    /// 양쪽 모두 Pushbox가 있으면 맞닿았는지로 (Pushbox가 겹침을 풀어주므로 겹치지는 않음),
    /// Collider가 있으면 Collider 범위로, 둘 다 없으면 x축 거리(overlapDistance)로 판정
    /// </summary>
    private bool IsOverlappingEnemy()
    {
        if (enemy == null) return false;

        if (selfPushbox != null && enemyPushbox != null)
            return Game.Character.Pushbox.IsTouching(selfPushbox, enemyPushbox);

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
    /// 그다음 프레임(입력한 프레임을 1로 세면 6프레임째)에 마지막으로 받은 방향으로 점프해서 공중에 뜸
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
            case 7: StartJump(MoveState.JumpBack, -facingSign * backJumpDistance); break;
            case 9: StartJump(MoveState.JumpForward, facingSign * forwardJumpDistance); break;
            default: StartJump(MoveState.JumpNeutral, 0f); break;
        }
    }

    /// <summary>
    /// 공격(승룡권 등)으로 공중에 뛰어오름. 레버 점프와 같은 방식으로 airFrames 프레임 동안 포물선을 그림
    /// forwardDistance: 공중에 있는 동안 앞으로 가는 전체 거리 (바라보는 쪽이 +, 음수면 뒤로)
    /// height: 가장 높은 곳까지의 높이 (바닥 기준)
    /// </summary>
    public void LaunchIntoAir(float forwardDistance, float height, int airFrames)
    {
        isJumpStarting = false;
        dashFramesLeft = 0;
        StopRun();

        float facingSign = IsFacingRight ? 1f : -1f;
        MoveState jumpState = forwardDistance > 0f ? MoveState.JumpForward
                            : forwardDistance < 0f ? MoveState.JumpBack
                            : MoveState.JumpNeutral;
        int frames = Mathf.Max(1, airFrames);
        StartAirArc(jumpState, height, frames, facingSign * forwardDistance / frames);
    }

    // 레버 점프 시작. xDistance는 공중에 있는 동안 가는 전체 가로 거리 (오른쪽이 +)
    private void StartJump(MoveState jumpState, float xDistance)
    {
        StartAirArc(jumpState, jumpHeight, jumpAirFrames, xDistance / jumpAirFrames);
    }

    // 공중 포물선 시작 (레버 점프, 공격으로 뛰어오르기 공통)
    // 이 프레임이 공중 첫 프레임이므로 바로 한 프레임만큼 움직임
    private void StartAirArc(MoveState jumpState, float height, int frames, float xDistancePerFrame)
    {
        CurrentState = jumpState;
        IsGrounded = false;
        airFrame = 0;
        airFrames = frames;
        airHeight = height;
        airDistancePerFrame = xDistancePerFrame;
        UpdateAirborne();
    }

    /// <summary>
    /// 공중: airFrames 프레임 동안 포물선을 그리고, 마지막 프레임에 바닥 높이에 닿음
    /// (착지 처리는 다음 프레임 Update 처음에 함. 마지막 프레임까지 공중 판정)
    /// 점프 방향은 뛰어오를 때 정해지고, 공중에서는 바뀌지 않음
    /// </summary>
    private void UpdateAirborne()
    {
        airFrame++;

        // 공중에서 지난 비율 (0 → 1). 높이는 비율 0.5에서 airHeight가 되는 포물선
        float progress = (float)airFrame / airFrames;

        Vector3 pos = transform.position;
        pos.x += airDistancePerFrame;
        pos.y = groundY + 4f * airHeight * progress * (1f - progress);
        transform.position = pos;
    }

    private void Land()
    {
        Vector3 pos = transform.position;
        pos.y = groundY;
        transform.position = pos;

        IsGrounded = true;
        CurrentState = MoveState.Neutral;
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
        enemyPushbox = enemy != null ? enemy.GetComponent<Game.Character.Pushbox>() : null;
    }

    private void OnValidate()
    {
        if (forwardWalkDistancePerFrame < 0f) forwardWalkDistancePerFrame = 0f;
        if (backWalkDistancePerFrame < 0f) backWalkDistancePerFrame = 0f;
        if (forwardJumpDistance < 0f) forwardJumpDistance = 0f;
        if (backJumpDistance < 0f) backJumpDistance = 0f;
        if (jumpHeight < 0f) jumpHeight = 0f;
        if (jumpAirFrames < 1) jumpAirFrames = 1;
        if (searchInterval < 0f) searchInterval = 0f;
        if (jumpDirectionBufferFrames < 0) jumpDirectionBufferFrames = 0;
        if (dashInputWindowFrames < 1) dashInputWindowFrames = 1;
        if (forwardDashDistance < 0f) forwardDashDistance = 0f;
        if (backDashDistance < 0f) backDashDistance = 0f;
        if (forwardDashFrames < 1) forwardDashFrames = 1;
        if (backDashFrames < 1) backDashFrames = 1;
        if (runDistancePerFrame < 0f) runDistancePerFrame = 0f;
        if (runStopFramesAfterOverlap < 0) runStopFramesAfterOverlap = 0;
        if (overlapDistance < 0f) overlapDistance = 0f;
    }
}