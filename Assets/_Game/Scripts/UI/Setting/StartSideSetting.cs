using UnityEngine;

namespace Game.UI.Setting
{
    // 시작 사이드 줄(BTN_ChooseSite)에 붙여서 사용
    // 스테퍼 값(왼쪽 / 오른쪽)에 맞춰 Player가 Enemy의 왼쪽 또는 오른쪽에 서도록 두 캐릭터의 x 위치를 바꿈
    // 스테퍼를 바꾼 순간에는 적용하지 않고, 설정창을 닫을 때(ESC 등) 적용함
    // 닫을 때 값이 달라져 있으면 화면이 순간 검게 변하고, 그동안 위치를 바꾼 뒤 서서히 원래 화면으로 돌아옴
    // - Screen Fade에는 설정창 밖에 있는 ScreenFade 오브젝트를 넣음
    public class StartSideSetting : MonoBehaviour
    {
        [SerializeField] private SettingStepper _sideStepper;
        [SerializeField] private ScreenFade _screenFade;

        // 스테퍼 Options에서 왼쪽이 몇 번째인지 (왼쪽, 오른쪽 순서면 0)
        [SerializeField] private int _leftIndex = 0;

        [SerializeField] private string SSS_playerTag = "Player";
        [SerializeField] private string SSS_enemyTag = "Enemy";

        // 마지막으로 게임에 적용한 값
        private int _appliedIndex;

        void Awake()
        {
            if (_sideStepper == null) _sideStepper = GetComponent<SettingStepper>();

            // 이 줄이 있는 탭이 꺼져 있어도 설정창이 닫히면 알 수 있도록 static 이벤트를 받음
            ControlReaderSwitch.SettingClosed += OnSettingClosed;
        }

        void Start()
        {
            // 처음 값을 적용된 값으로 기억 (설정창을 처음 열 때 캐릭터가 움직이지 않도록 위치는 건드리지 않음)
            if (_sideStepper != null) _appliedIndex = _sideStepper.Index;
        }

        void OnDestroy()
        {
            ControlReaderSwitch.SettingClosed -= OnSettingClosed;
        }

        // 설정창이 닫혔을 때: 값이 바뀌었으면 화면을 깜빡이며 적용
        private void OnSettingClosed()
        {
            if (_sideStepper == null || _sideStepper.Index == _appliedIndex) return;

            int index = _sideStepper.Index;
            _appliedIndex = index;

            if (_screenFade != null) _screenFade.Play(onBlack: () => ApplySide(index));
            else ApplySide(index);
        }

        /// <summary>
        /// 시작 사이드를 바꾸고 두 캐릭터를 바로 그 사이드로 세움 (재시작 + 레버 위에서 사용)
        /// isPlayerLeft가 false면 Player가 오른쪽(2P 자리). 설정창의 시작 사이드 스테퍼 값도 같이 바뀜
        /// </summary>
        public void SetSideAndApply(bool isPlayerLeft)
        {
            if (_sideStepper == null) return;

            // 선택지는 왼쪽 / 오른쪽 두 개라서, 왼쪽이 아닌 쪽이 오른쪽
            int rightIndex = _leftIndex == 0 ? 1 : 0;
            _sideStepper.SetIndex(isPlayerLeft ? _leftIndex : rightIndex);
            ApplyCurrent();
        }

        /// <summary>지금 고른 사이드대로 두 캐릭터를 바로 바꿔 세움 (재시작에서 사용)</summary>
        public void ApplyCurrent()
        {
            if (_sideStepper == null) return;

            _appliedIndex = _sideStepper.Index;
            ApplySide(_appliedIndex);
        }

        // Player가 고른 쪽에 서도록 Player와 Enemy의 x 위치를 맞바꿈 (이미 그쪽에 있으면 그대로 둠)
        private void ApplySide(int index)
        {
            GameObject player = GameObject.FindGameObjectWithTag(SSS_playerTag);
            Transform enemy = FindClosestEnemy(player);
            if (player == null || enemy == null) return;

            bool shouldPlayerBeLeft = index == _leftIndex;
            bool isPlayerLeft = player.transform.position.x < enemy.position.x;
            if (shouldPlayerBeLeft == isPlayerLeft) return;

            Vector3 playerPosition = player.transform.position;
            Vector3 enemyPosition = enemy.position;

            float playerX = playerPosition.x;
            playerPosition.x = enemyPosition.x;
            enemyPosition.x = playerX;

            player.transform.position = playerPosition;
            enemy.position = enemyPosition;

            // 서로 지나쳐 자리를 바꾼 것을 Pushbox에 알림 (겹쳤다고 보고 원래 자리로 밀어내지 않도록)
            CharStageBoundary.SyncAfterTeleport();
        }

        // Enemy가 여러 개면 Player와 가장 가까운 것을 사용
        private Transform FindClosestEnemy(GameObject player)
        {
            if (player == null) return null;

            Transform closestEnemy = null;
            float closestDistance = float.MaxValue;
            foreach (GameObject enemy in GameObject.FindGameObjectsWithTag(SSS_enemyTag))
            {
                float distance = (enemy.transform.position - player.transform.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = enemy.transform;
                }
            }
            return closestEnemy;
        }
    }
}
