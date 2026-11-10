using Game.Character;
using Game.UI;
using Game.UI.Setting;
using UnityEngine;

namespace Game.Field
{
    // 연습 씬에서 재시작 키(키보드 스페이스바, 플레이스테이션 터치패드, 그 외 패드 Back)를 누르면 처음 상태로 되돌림
    // - 화면이 순간 검게 변하는 동안 연습 설정의 시작 사이드 / 시작 위치대로 두 캐릭터를 다시 세움
    // - 재시작 키를 누를 때 레버를 같이 누르고 있으면 그 방향으로 시작 위치를 바꿈 (화면 기준 방향)
    //   왼쪽: 왼쪽 벽, 오른쪽: 오른쪽 벽, 아래: 중앙 (사이드는 그대로),
    //   위: 위치는 그대로 두고 지금 사이드의 반대쪽으로 바꿈 (1P → 2P, 2P → 1P)
    //   대각선, 레버 없음: 설정해 둔 사이드 / 위치 그대로
    //   바꾼 값은 설정창의 시작 위치 / 시작 사이드에도 그대로 반영됨
    // - 하던 점프, 대쉬, 달리기, 공격, 착지 경직, 피격 경직, 입력 버퍼를 모두 취소하고 날아가던 투사체를 지움, 두 캐릭터의 체력을 가득 채움
    // - 설정창이 열려 있을 때는 캐릭터 조작 입력(InputReader)이 꺼져 있으므로 재시작되지 않음
    public class PracticeRestart : MonoBehaviour
    {
        [Tooltip("재시작 키를 읽어올 InputReader. 비워두면 씬에서 찾음")]
        [SerializeField] private InputReader PR_inputReader;

        [Tooltip("재시작할 때 화면을 깜빡일 ScreenFade. 비워두면 바로 되돌림")]
        [SerializeField] private ScreenFade PR_screenFade;

        [Tooltip("연습 설정의 시작 사이드 / 위치 설정. 비워두면 씬에서 찾음")]
        [SerializeField] private StartPositionSetting _startPositionSetting;

        [Tooltip("재시작한 뒤 컨트롤러 입력을 받지 않는 시간(초). 재시작 순간 누르고 있던 레버나 버튼으로 캐릭터가 바로 움직이지 않게 함")]
        [SerializeField] private float _inputBlockTime = 0.7f;

        private void Awake()
        {
            if (PR_inputReader == null) PR_inputReader = FindAnyObjectByType<InputReader>(FindObjectsInactive.Include);
            if (PR_screenFade == null) PR_screenFade = FindAnyObjectByType<ScreenFade>(FindObjectsInactive.Include);
            if (_startPositionSetting == null) _startPositionSetting = FindAnyObjectByType<StartPositionSetting>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (PR_inputReader == null || !PR_inputReader.isActiveAndEnabled) return;
            if (!PR_inputReader.restartDown) return;

            // 재시작 키를 누른 순간의 레버 방향 (넘버패드 1~9, 화면 기준)
            int direction = PR_inputReader.Direction;
            if (PR_screenFade != null) PR_screenFade.Play(onBlack: () => Restart(direction));
            else Restart(direction);
        }

        /// <summary>direction: 재시작할 때 누르고 있던 레버 방향 (5 = 레버 없음, 설정해 둔 위치에서 재시작)</summary>
        public void Restart(int direction = 5)
        {
            // 하던 동작을 먼저 모두 취소하고 바닥에 세움
            foreach (var move in FindObjectsByType<CharacterMove>(FindObjectsSortMode.None)) move.ResetMovement();
            foreach (var attack in FindObjectsByType<CharacterAttack>(FindObjectsSortMode.None)) attack.CancelAttack();
            foreach (var reaction in FindObjectsByType<CharacterHitReaction>(FindObjectsSortMode.None)) reaction.ResetReaction();
            foreach (var dummyAction in FindObjectsByType<DummyAction>(FindObjectsSortMode.None)) dummyAction.ResetAction();
            foreach (var command in FindObjectsByType<CharacterCommand>(FindObjectsSortMode.None)) command.ClearBuffer();
            foreach (var projectile in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
            foreach (var health in FindObjectsByType<CharacterHealth>(FindObjectsSortMode.None)) health.ResetHealth();
            HitStop.ResetAll();

            // 재시작 직후 잠깐 입력을 받지 않음
            foreach (var reader in FindObjectsByType<InputReader>(FindObjectsSortMode.None)) reader.BlockInput(seconds: _inputBlockTime);

            // 위(8)는 지금 사이드의 반대쪽으로 바꿈 (1P → 2P, 2P → 1P)
            // 아래(2)는 사이드는 그대로 두고 중앙에서 시작, 대각선은 레버 없음과 같이 설정해 둔 값을 그대로 씀
            bool isUp = direction == 8;

            StartPositionSetting.Place? place = null;
            if (direction == 4) place = StartPositionSetting.Place.LeftEnd;
            else if (direction == 6) place = StartPositionSetting.Place.RightEnd;
            else if (direction == 2) place = StartPositionSetting.Place.Center;

            if (_startPositionSetting != null) _startPositionSetting.SetAndApply(isPlayerLeft: null, place: place, shouldSwapSide: isUp);
            // 설정이 없어도 Boundary 벽과 Pushbox는 새 위치를 알아야 함
            else CharStageBoundary.SyncAfterTeleport();
        }
    }
}
