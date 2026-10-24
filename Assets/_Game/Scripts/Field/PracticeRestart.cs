using Game.Character;
using Game.UI;
using Game.UI.Setting;
using UnityEngine;

namespace Game.Field
{
    // 연습 씬에서 재시작 키(키보드 스페이스바, 플레이스테이션 터치패드, 그 외 패드 Back)를 누르면 처음 상태로 되돌림
    // - 화면이 순간 검게 변하는 동안 연습 설정의 시작 사이드 / 시작 위치대로 두 캐릭터를 다시 세움
    // - 재시작 키를 누를 때 레버를 같이 누르고 있으면 그 방향으로 시작 위치를 바꿈 (화면 기준 방향)
    //   왼쪽: 왼쪽 벽, 오른쪽: 오른쪽 벽, 아래: 1P 자리(Player가 왼쪽) + 중앙, 위: 2P 자리(Player가 오른쪽),
    //   레버 없음: 설정해 둔 위치
    //   대각선은 좌우가 위치, 위아래가 사이드를 정함 (예: 왼쪽 위 = 2P 자리 + 왼쪽 벽, 오른쪽 아래 = 1P 자리 + 오른쪽 벽)
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
            foreach (var command in FindObjectsByType<CharacterCommand>(FindObjectsSortMode.None)) command.ClearBuffer();
            foreach (var projectile in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
            foreach (var health in FindObjectsByType<CharacterHealth>(FindObjectsSortMode.None)) health.ResetHealth();

            bool isUp = direction >= 7;
            bool isDown = direction <= 3;
            bool isLeft = direction % 3 == 1;
            bool isRight = direction % 3 == 0;

            // 레버를 누르지 않은 쪽(null)은 설정해 둔 값을 그대로 씀
            bool? isPlayerLeft = null;
            if (isUp) isPlayerLeft = false;
            else if (isDown) isPlayerLeft = true;

            StartPositionSetting.Place? place = null;
            if (isLeft) place = StartPositionSetting.Place.LeftEnd;
            else if (isRight) place = StartPositionSetting.Place.RightEnd;
            else if (isDown) place = StartPositionSetting.Place.Center;

            if (_startPositionSetting != null) _startPositionSetting.SetAndApply(isPlayerLeft: isPlayerLeft, place: place);
            // 설정이 없어도 Boundary 벽과 Pushbox는 새 위치를 알아야 함
            else CharStageBoundary.SyncAfterTeleport();
        }
    }
}
