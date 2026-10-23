using UnityEngine;

namespace Game.UI.Setting
{
    // 시작 위치 줄(BTN_StartPlace)에 붙여서 사용
    // 스테퍼 값(중앙 / 왼쪽 끝 / 오른쪽 끝)에 맞춰 Player와 Enemy를 스테이지의 그 위치에 다시 세움
    // 스테퍼를 바꾼 순간에는 적용하지 않고, 설정창을 닫을 때(ESC 등) 적용함
    // 닫을 때 값이 달라져 있으면 화면이 순간 검게 변하고, 그동안 위치를 옮긴 뒤 서서히 원래 화면으로 돌아옴
    // - 두 캐릭터의 왼쪽 / 오른쪽 순서는 그대로 둠 (순서는 StartSideSetting이 정함)
    // - 스테이지 끝은 CharStageBoundary 인스펙터의 스테이지 끝 값에서 캐릭터 절반 너비만큼 안쪽
    public class StartPlaceSetting : MonoBehaviour
    {
        public enum Place
        {
            Center,  // 중앙
            LeftEnd, // 왼쪽 벽
            RightEnd // 오른쪽 벽
        }

        [SerializeField] private SettingStepper _placeStepper;
        [SerializeField] private ScreenFade SPS_screenFade;

        // 스테이지 끝 값을 가진 오브젝트 (비워두면 씬에서 찾음)
        [SerializeField] private CharStageBoundary _stageBoundary;

        // 다시 세울 때 두 캐릭터 사이의 x 거리
        [SerializeField] private float _characterGap = 1.9f;

        // 스테퍼 Options 순서 (0부터)
        [SerializeField] private int _centerIndex = 0;
        [SerializeField] private int _leftEndIndex = 1;
        [SerializeField] private int _rightEndIndex = 2;

        [SerializeField] private string SPS_playerTag = "Player";
        [SerializeField] private string SPS_enemyTag = "Enemy";

        // 마지막으로 게임에 적용한 값
        private int SPS_appliedIndex;

        void Awake()
        {
            if (_placeStepper == null) _placeStepper = GetComponent<SettingStepper>();
            if (_stageBoundary == null) _stageBoundary = FindAnyObjectByType<CharStageBoundary>();

            // 이 줄이 있는 탭이 꺼져 있어도 설정창이 닫히면 알 수 있도록 static 이벤트를 받음
            ControlReaderSwitch.SettingClosed += OnSettingClosed;
        }

        void Start()
        {
            // 처음 값을 적용된 값으로 기억 (설정창을 처음 열 때 캐릭터가 움직이지 않도록 위치는 건드리지 않음)
            if (_placeStepper != null) SPS_appliedIndex = _placeStepper.Index;
        }

        void OnDestroy()
        {
            ControlReaderSwitch.SettingClosed -= OnSettingClosed;
        }

        // 설정창이 닫혔을 때: 값이 바뀌었으면 화면을 깜빡이며 적용
        private void OnSettingClosed()
        {
            if (_placeStepper == null || _placeStepper.Index == SPS_appliedIndex) return;

            int index = _placeStepper.Index;
            SPS_appliedIndex = index;

            if (SPS_screenFade != null) SPS_screenFade.Play(onBlack: () => ApplyPlace(index));
            else ApplyPlace(index);
        }

        /// <summary>
        /// 시작 위치를 place로 바꾸고 두 캐릭터를 바로 그 위치에 세움 (재시작 + 레버 방향에서 사용)
        /// 설정창의 시작 위치 스테퍼 값도 같이 바뀜
        /// </summary>
        public void SetPlaceAndApply(Place place)
        {
            if (_placeStepper == null) return;

            switch (place)
            {
                case Place.LeftEnd: _placeStepper.SetIndex(_leftEndIndex); break;
                case Place.RightEnd: _placeStepper.SetIndex(_rightEndIndex); break;
                default: _placeStepper.SetIndex(_centerIndex); break;
            }
            ApplyCurrent();
        }

        /// <summary>지금 고른 시작 위치에 두 캐릭터를 바로 다시 세움 (재시작에서 사용)</summary>
        public void ApplyCurrent()
        {
            if (_placeStepper == null) return;

            SPS_appliedIndex = _placeStepper.Index;
            ApplyPlace(SPS_appliedIndex);
        }

        // 고른 위치에 두 캐릭터를 다시 세움
        private void ApplyPlace(int index)
        {
            GameObject player = GameObject.FindGameObjectWithTag(SPS_playerTag);
            Transform enemy = FindClosestEnemy(player);
            if (player == null || enemy == null) return;

            // 지금 왼쪽에 있는 캐릭터와 오른쪽에 있는 캐릭터
            bool isPlayerLeft = player.transform.position.x <= enemy.position.x;
            Transform leftCharacter = isPlayerLeft ? player.transform : enemy;
            Transform rightCharacter = isPlayerLeft ? enemy : player.transform;

            float leftX;
            if (index == _leftEndIndex) leftX = GetStageLeftEndX(leftCharacter);
            else if (index == _rightEndIndex) leftX = GetStageRightEndX(rightCharacter) - _characterGap;
            else if (index == _centerIndex) leftX = GetStageCenterX() - _characterGap * 0.5f;
            else return;

            SetPositionX(leftCharacter, leftX);
            SetPositionX(rightCharacter, leftX + _characterGap);

            // 순간이동한 것을 Boundary 벽과 Pushbox에 알림 (벽을 넘어간 것으로 보고 되돌리지 않도록)
            CharStageBoundary.SyncAfterTeleport();
        }

        private float GetStageCenterX()
        {
            return _stageBoundary != null ? _stageBoundary.StageCenter : 0f;
        }

        // 캐릭터가 설 수 있는 가장 왼쪽 x (스테이지 왼쪽 끝 + 캐릭터 절반 너비)
        private float GetStageLeftEndX(Transform character)
        {
            if (_stageBoundary == null) return GetStageCenterX();
            return _stageBoundary.StageLeftEnd + GetHalfWidth(character);
        }

        // 캐릭터가 설 수 있는 가장 오른쪽 x (스테이지 오른쪽 끝 - 캐릭터 절반 너비)
        private float GetStageRightEndX(Transform character)
        {
            if (_stageBoundary == null) return GetStageCenterX();
            return _stageBoundary.StageRightEnd - GetHalfWidth(character);
        }

        private float GetHalfWidth(Transform character)
        {
            Collider characterCollider = character.GetComponent<Collider>();
            return characterCollider != null ? characterCollider.bounds.extents.x : 0f;
        }

        private void SetPositionX(Transform character, float x)
        {
            Vector3 position = character.position;
            position.x = x;
            character.position = position;
        }

        // Enemy가 여러 개면 Player와 가장 가까운 것을 사용
        private Transform FindClosestEnemy(GameObject player)
        {
            if (player == null) return null;

            Transform closestEnemy = null;
            float closestDistance = float.MaxValue;
            foreach (GameObject enemy in GameObject.FindGameObjectsWithTag(SPS_enemyTag))
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
