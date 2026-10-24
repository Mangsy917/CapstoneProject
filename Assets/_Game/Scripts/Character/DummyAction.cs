using UnityEngine;

namespace Game.Character
{
    // 더미(Enemy)에 붙여서 사용. 설정창 더미 설정의 더미 행동(서기 / 앉기 / 점프)대로 자세를 정함
    // - 서기: 서 있음
    // - 앉기: 앉아 있음 (Top 피격 상자가 꺼짐, CharacterHitboxes)
    // - 점프: 땅에 닿으면 점프 준비 프레임 뒤에 다시 뛰어오르기를 계속함 (제자리 점프)
    // - 가드(DummyGuard)는 행동과 따로 정해지고, 공중에 있는 동안은 가드하지 못함 (CharacterHitReaction)
    // - 피격 / 가드 경직 중에는 다음 점프를 시작하지 않음. 공중에서 맞으면 점프 궤도는 그대로 이어짐
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
        private int _groundFrames; // 땅에 닿은 뒤 지난 프레임 (점프 준비)

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

            // 공중 프레임을 다 채웠으면 이번 프레임부터 땅에 있음
            if (!DA_IsGrounded && DA_airFrame >= DA_jumpAirFrames) Land();

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

        // 이 프레임이 공중 첫 프레임이므로 바로 한 프레임만큼 움직임
        private void StartJump()
        {
            DA_IsGrounded = false;
            DA_airFrame = 0;
            _groundFrames = 0;
            UpdateAirborne();
        }

        // DA_jumpAirFrames 프레임 동안 포물선을 그리고, 마지막 프레임에 바닥 높이에 닿음
        private void UpdateAirborne()
        {
            DA_airFrame++;
            float progress = (float)DA_airFrame / DA_jumpAirFrames;

            Vector3 position = transform.position;
            position.y = DA_groundY + 4f * DA_jumpHeight * progress * (1f - progress);
            transform.position = position;
        }

        private void Land()
        {
            Vector3 position = transform.position;
            position.y = DA_groundY;
            transform.position = position;
            DA_IsGrounded = true;
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
