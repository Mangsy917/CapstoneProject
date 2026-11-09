using UnityEngine;

namespace Game.Character
{
    // 더미(Enemy)에 붙여서 사용. 설정창 더미 설정의 더미 행동(서기 / 앉기 / 점프)대로 자세를 정함
    // - 서기: 서 있음
    // - 앉기: 앉아 있음 (Top 피격 상자가 꺼짐, CharacterHitboxes)
    // - 점프: 땅에 닿으면 점프 준비 프레임 뒤에 다시 뛰어오르기를 계속함 (제자리 점프)
    // - 가드(DummyGuard)는 행동과 따로 정해지고, 공중에 있는 동안은 가드하지 못함 (CharacterHitReaction)
    // - 피격 / 가드 경직 중에는 다음 점프를 시작하지 않음. 공중에서 맞으면 점프 궤도는 그대로 이어짐
    //   띄우는 공격(승룡권 등)에 맞으면 때린 쪽의 움직임을 따라 맞은 높이에서 새 포물선으로 떠올랐다가 쓰러지는 프레임이 끝날 때 착지함
    // - 자세는 CharactorState(서기 / 앉기 / 점프, 지상 / 공중)에도 넣음 (판정 표시용)
    // - 설정은 DummyActionSetting이 넣어줌. 다시 생성된 더미도 같은 설정을 씀
    public class DummyAction : MonoBehaviour
    {
        // 설정창 더미 행동 스테퍼의 선택지 순서와 같음
        public enum ActionType
        {
            Stand,  // 서기
            Crouch, // 앉기
            Jump    // 점프
        }

        [Header("점프 (플레이어의 CharacterMove와 같은 값)")]
        [Tooltip("점프의 가장 높은 곳까지의 높이 (바닥 기준)")]
        [SerializeField] private float DA_jumpHeight = 2.88f;
        [Tooltip("공중에 떠 있는 프레임 수")]
        [SerializeField] private int DA_jumpAirFrames = 38;
        [Tooltip("착지한 뒤 다시 뛰어오르기까지 땅에 있는 프레임 수 (점프 준비)")]
        [SerializeField] private int _jumpStartupFrames = 5;

        [Tooltip("자세를 넣을 CharactorState. 비워두면 같은 오브젝트에서 찾음 (없어도 됨)")]
        [SerializeField] private CharactorState DA_characterState;

        [Tooltip("경직 중인지 확인할 CharacterHitReaction. 비워두면 같은 오브젝트에서 찾음 (없어도 됨)")]
        [SerializeField] private CharacterHitReaction DA_hitReaction;

        // 설정창에서 정한 행동 (다시 생성된 더미도 같은 값을 쓰도록 static)
        private static ActionType _currentAction = ActionType.Stand;

        public bool DA_IsGrounded { get; private set; } = true;

        /// <summary>땅에서 앉아 있는지 (앉기 행동)</summary>
        public bool DA_IsCrouching => DA_IsGrounded && _currentAction == ActionType.Crouch;

        private float DA_groundY;
        private int DA_airFrame;  // 공중에 뜬 뒤 지난 프레임 (공중 첫 프레임 = 1)

        // 지금 하고 있는 공중 움직임 (점프, 띄우는 공격에 맞음 공통)
        // 가로는 일정한 속도, 세로는 중력으로 매 프레임 속도가 줄어드는 포물선 (CharacterMove와 같은 방식)
        private int DA_airFrames = 1;    // 공중에 떠 있는 전체 프레임 수
        private float DA_airVelocityX;   // 가로 속도 (1프레임 거리, 오른쪽이 +)
        private float DA_airVelocityY;   // 세로 속도 (1프레임 거리, 위가 +)
        private float DA_airGravity;     // 1프레임마다 세로 속도가 줄어드는 양
        private int _groundFrames; // 땅에 닿은 뒤 지난 프레임 (점프 준비)

        /// <summary>지금 움직이는 속도 (다음 프레임에 움직일 1프레임 거리, 오른쪽 / 위가 +). 땅에서는 0</summary>
        public Vector2 DA_Velocity => DA_IsGrounded ? Vector2.zero : new Vector2(DA_airVelocityX, DA_airVelocityY);

        // Domain Reload를 꺼둔 상태로 플레이해도 이전 설정이 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSettings()
        {
            _currentAction = ActionType.Stand;
        }

        /// <summary>설정창의 더미 행동을 모든 더미에 적용</summary>
        public static void ApplySetting(ActionType action)
        {
            _currentAction = action;
            foreach (var dummyAction in FindObjectsByType<DummyAction>(FindObjectsSortMode.None))
            {
                dummyAction.ResetAction();
            }
        }

        private void Awake()
        {
            if (DA_characterState == null) DA_characterState = GetComponent<CharactorState>();
            if (DA_hitReaction == null) DA_hitReaction = GetComponent<CharacterHitReaction>();
            DA_groundY = transform.position.y;
        }

        private void Update()
        {
            // 슬로우 모션 중에는 건너뛰는 프레임이 있음 (GameSpeed)
            if (!Game.Field.GameSpeed.IsTickFrame) return;

            // 히트스톱 중에는 움직이지 않음
            if (HitStop.IsStoppedNow(this)) return;

            // 공중 프레임을 다 채웠으면 이번 프레임부터 땅에 있음
            if (!DA_IsGrounded && DA_airFrame >= DA_airFrames) Land();

            if (!DA_IsGrounded)
            {
                UpdateAirborne();
            }
            else
            {
                bool isInStun = DA_hitReaction != null && DA_hitReaction.IsInStun;
                if (_currentAction == ActionType.Jump && !isInStun)
                {
                    _groundFrames++;
                    if (_groundFrames >= _jumpStartupFrames) StartJump();
                }
                else
                {
                    _groundFrames = 0;
                }
            }

            UpdateCharacterState();
        }

        /// <summary>재시작하거나 설정이 바뀌었을 때 바닥에 세우고 점프 준비를 처음부터 함</summary>
        public void ResetAction()
        {
            if (!DA_IsGrounded) Land();
            _groundFrames = 0;
            UpdateCharacterState();
        }

        /// <summary>
        /// 띄우는 공격에 맞았을 때 호출 (CharacterHitReaction). 첫 프레임 속도 velocity(1프레임 거리, 오른쪽 / 위가 +)로 떠오르고,
        /// frames 프레임째에 바닥에 닿도록 중력을 정함 (같은 프레임 수면 처음 속도가 빠를수록 높이 뜸)
        /// 이 프레임은 맞은 프레임이라 움직이지 않고, 다음 프레임부터 뜸
        /// </summary>
        public void LaunchIntoAir(Vector2 velocity, int frames)
        {
            int safeFrames = Mathf.Max(1, frames);
            float gravity = CharacterMove.GetGravityToLand(transform.position.y - DA_groundY, velocity.y, safeFrames);
            StartAirMotion(safeFrames, velocity, gravity);
        }

        /// <summary>공중에 있으면 가로 속도에 x를 더함 (공중에서 앞으로 가던 공격에 맞아 같이 밀려날 때)</summary>
        public void AddAirVelocityX(float x)
        {
            if (!DA_IsGrounded) DA_airVelocityX += x;
        }

        // 제자리 점프. 이 프레임이 공중 첫 프레임이므로 바로 한 프레임만큼 움직임
        private void StartJump()
        {
            int frames = Mathf.Max(1, DA_jumpAirFrames);
            float velocityY = 4f * DA_jumpHeight / frames;
            StartAirMotion(frames, new Vector2(0f, velocityY), gravity: 8f * DA_jumpHeight / (frames * frames));
            UpdateAirborne();
        }

        private void StartAirMotion(int frames, Vector2 velocity, float gravity)
        {
            DA_IsGrounded = false;
            DA_airFrame = 0;
            DA_airFrames = frames;
            DA_airVelocityX = velocity.x;
            DA_airVelocityY = velocity.y;
            DA_airGravity = gravity;
            _groundFrames = 0;
        }

        // 가로는 일정한 속도, 세로는 매 프레임 중력만큼 속도가 줄어듦. DA_airFrames 프레임째에 바닥 높이에 닿음
        private void UpdateAirborne()
        {
            DA_airFrame++;

            Vector3 position = transform.position;
            position.x += DA_airVelocityX;
            // 이번 프레임 동안 평균 속도(처음 속도 - 중력 절반)만큼 움직인 뒤 속도를 줄임
            position.y += DA_airVelocityY - 0.5f * DA_airGravity;
            DA_airVelocityY -= DA_airGravity;

            // 마지막 프레임은 계산 오차 없이 바닥에 맞춤
            if (DA_airFrame >= DA_airFrames) position.y = DA_groundY;

            transform.position = position;
        }

        private void Land()
        {
            Vector3 position = transform.position;
            position.y = DA_groundY;
            transform.position = position;
            DA_IsGrounded = true;
            DA_airVelocityX = 0f;
            DA_airVelocityY = 0f;
        }

        private void UpdateCharacterState()
        {
            if (DA_characterState == null) return;

            if (!DA_IsGrounded)
            {
                if (!DA_characterState.Jump) DA_characterState.Jump = true; // 공중도 함께 켜짐
                return;
            }

            if (DA_IsCrouching) { if (!DA_characterState.SitDown) DA_characterState.SitDown = true; }
            else if (!DA_characterState.StandUp) DA_characterState.StandUp = true;
            if (!DA_characterState.Ground) DA_characterState.Ground = true;
        }

        private void OnValidate()
        {
            if (DA_jumpHeight < 0f) DA_jumpHeight = 0f;
            if (DA_jumpAirFrames < 1) DA_jumpAirFrames = 1;
            if (_jumpStartupFrames < 1) _jumpStartupFrames = 1;
        }
    }
}
