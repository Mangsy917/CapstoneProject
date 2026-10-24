using UnityEngine;

namespace Game.Character
{
    // 더미(Enemy)에 붙여서 사용. 설정창 더미 설정의 가드 / 가드 종류 / 카운트 가드 값대로 가드할지 정함
    // - 가드 (설정창 선택지 순서와 같음)
    //   가드하지 않음: 가드 안 함
    //   가드 실행: 모든 공격을 가드
    //   카운트 가드: 정한 횟수만큼 연달아 가드한 뒤에는 맞음
    //   첫 타 이후 가드: 처음 한 대는 맞고, 그 뒤로는 경직이 풀려 있으면 가드 (콤보로 이어진 공격은 맞음)
    //   랜덤: 공격마다 반반 확률로 가드
    // - 가드 종류: 전부 가드(공격 높이에 맞게 서서 / 앉아 가드), 앉아 가드만, 서서 가드만, 랜덤(공격마다 서기 / 앉기)
    //   서서 가드는 하단을, 앉아 가드는 중단을 막지 못함 (CharacterHitReaction이 판정)
    // - 맞거나 가드한 뒤 한동안 공격받지 않으면 카운트 / 첫 타 기록을 처음으로 되돌림
    // - 설정은 GuardSetting이 설정창을 닫을 때 넣어줌. 다시 생성된 더미도 같은 설정을 씀
    public class DummyGuard : MonoBehaviour
    {
        // 설정창 가드 스테퍼의 선택지 순서와 같음
        public enum GuardMode
        {
            None,       // 가드하지 않음
            All,        // 가드 실행
            Count,      // 카운트 가드
            AfterFirst, // 첫 타 이후 가드
            Random      // 랜덤
        }

        // 설정창 가드 종류 스테퍼의 선택지 순서와 같음
        public enum GuardType
        {
            All,        // 전부 가드
            CrouchOnly, // 앉아 가드만
            StandOnly,  // 서서 가드만
            Random      // 랜덤
        }

        [Tooltip("맞거나 가드한 뒤 경직이 풀리고 이 프레임 수 동안 공격받지 않으면 카운트 가드 / 첫 타 기록을 처음으로 되돌림")]
        [SerializeField] private int _resetFrames = 30;

        [Tooltip("경직 중인지 확인할 CharacterHitReaction. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CharacterHitReaction DG_hitReaction;

        // 설정창에서 정한 값 (다시 생성된 더미도 같은 값을 쓰도록 static)
        private static GuardMode _guardMode = GuardMode.None;
        private static GuardType _guardType = GuardType.All;
        private static int _countGuardLimit = 1;

        private int _guardedCount; // 연달아 가드한 횟수 (카운트 가드)
        private bool _hasBeenHit;  // 이번 공격 흐름에서 이미 맞았는지 (첫 타 이후 가드)
        private int _idleFrames;   // 경직이 풀린 뒤 공격받지 않은 프레임

        // Domain Reload를 꺼둔 상태로 플레이해도 이전 설정이 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSettings()
        {
            _guardMode = GuardMode.None;
            _guardType = GuardType.All;
            _countGuardLimit = 1;
        }

        /// <summary>설정창의 가드 설정을 모든 더미에 적용 (countGuardLimit: 카운트 가드 횟수)</summary>
        public static void ApplySetting(GuardMode mode, GuardType type, int countGuardLimit)
        {
            _guardMode = mode;
            _guardType = type;
            _countGuardLimit = Mathf.Max(1, countGuardLimit);

            foreach (var dummyGuard in FindObjectsByType<DummyGuard>(FindObjectsSortMode.None))
            {
                dummyGuard.ResetRecord();
            }
        }

        private void Awake()
        {
            if (DG_hitReaction == null) DG_hitReaction = GetComponent<CharacterHitReaction>();
        }

        private void Update()
        {
            if (!Game.Field.GameSpeed.IsTickFrame) return;
            if (DG_hitReaction != null && DG_hitReaction.IsInStun) return;

            _idleFrames++;
            if (_idleFrames >= _resetFrames) ResetRecord();
        }

        /// <summary>
        /// 이 높이의 공격을 가드하려고 하는지와 가드 자세 (isCrouching: 앉아 가드)
        /// 자세가 공격 높이에 맞지 않으면 CharacterHitReaction이 막지 못한 것으로 처리함
        /// </summary>
        public bool TryGetGuard(AttackHeight height, out bool isCrouching)
        {
            isCrouching = GetCrouching(height);
            switch (_guardMode)
            {
                case GuardMode.All: return true;
                case GuardMode.Count: return _guardedCount < _countGuardLimit;
                case GuardMode.AfterFirst: return _hasBeenHit;
                case GuardMode.Random: return UnityEngine.Random.value < 0.5f;
                default: return false;
            }
        }

        /// <summary>맞았을 때 CharacterHitReaction이 호출</summary>
        public void NotifyHit()
        {
            _hasBeenHit = true;
            _idleFrames = 0;
        }

        /// <summary>가드했을 때 CharacterHitReaction이 호출</summary>
        public void NotifyGuard()
        {
            _guardedCount++;
            _idleFrames = 0;
        }

        /// <summary>카운트 가드 / 첫 타 기록을 처음으로 되돌림 (재시작, 설정 변경, 한동안 공격받지 않았을 때)</summary>
        public void ResetRecord()
        {
            _guardedCount = 0;
            _hasBeenHit = false;
            _idleFrames = 0;
        }

        private bool GetCrouching(AttackHeight height)
        {
            switch (_guardType)
            {
                case GuardType.CrouchOnly: return true;
                case GuardType.StandOnly: return false;
                case GuardType.Random: return UnityEngine.Random.value < 0.5f;
                default: return height == AttackHeight.Low; // 전부 가드: 하단은 앉아서, 그 외는 서서
            }
        }
    }
}
