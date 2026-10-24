using UnityEngine;

namespace Game.UI.Setting
{
    // 연습 설정 패널(시작 사이드 줄과 시작 위치 줄의 부모)에 붙여서 사용
    // 두 스테퍼 값에 맞춰 Player와 Enemy를 다시 세움
    // - 시작 사이드(왼쪽 / 오른쪽): Player가 Enemy의 왼쪽 또는 오른쪽에 서도록 두 캐릭터의 x 위치를 맞바꿈
    // - 시작 위치(중앙 / 왼쪽 끝 / 오른쪽 끝): 두 캐릭터를 스테이지의 그 위치에 다시 세움 (왼쪽 / 오른쪽 순서는 그대로)
    // - 사이드를 먼저 맞춘 뒤 위치를 정함 (시작 위치는 지금의 왼쪽 / 오른쪽 순서를 그대로 쓰므로)
    // 스테퍼를 바꾼 순간에는 적용하지 않고, 설정창을 닫을 때(ESC 등) 적용함
    // 닫을 때 값이 달라져 있으면 화면이 순간 검게 변하고, 그동안 위치를 바꾼 뒤 서서히 원래 화면으로 돌아옴
    // - Screen Fade에는 설정창 밖에 있는 ScreenFade 오브젝트를 넣음
    // - 스테이지 끝은 CharStageBoundary 인스펙터의 스테이지 끝 값에서 캐릭터 절반 너비만큼 안쪽
    public class StartPositionSetting : MonoBehaviour
    {
        public enum Place
        {
            Center,  // 중앙
            LeftEnd, // 왼쪽 벽
            RightEnd // 오른쪽 벽
        }

        [SerializeField] private ScreenFade SPS_screenFade;

        [Header("시작 사이드")]
        [SerializeField] private SettingStepper _sideStepper;
        // 스테퍼 Options에서 왼쪽이 몇 번째인지 (왼쪽, 오른쪽 순서면 0)
        [SerializeField] private int _leftIndex = 0;

        [Header("시작 위치")]
        [SerializeField] private SettingStepper _placeStepper;
        // 스테이지 끝 값을 가진 오브젝트 (비워두면 씬에서 찾음)
        [SerializeField] private CharStageBoundary _stageBoundary;
        // 다시 세울 때 두 캐릭터 사이의 x 거리
        [SerializeField] private float _characterGap = 1.9f;
        // 스테퍼 Options 순서 (0부터)
        [SerializeField] private int _centerIndex = 0;
        [SerializeField] private int _leftEndIndex = 1;
        [SerializeField] private int _rightEndIndex = 2;

        [Header("태그")]
        [SerializeField] private string SPS_playerTag = "Player";
        [SerializeField] private string SPS_enemyTag = "Enemy";

        // 마지막으로 게임에 적용한 값
        private int _appliedSideIndex;
        private int _appliedPlaceIndex;

        void Awake()
        {
            if (_stageBoundary == null) _stageBoundary = FindAnyObjectByType<CharStageBoundary>();

            // 이 패널이 있는 탭이 꺼져 있어도 설정창이 닫히면 알 수 있도록 static 이벤트를 받음
            ControlReaderSwitch.SettingClosed += OnSettingClosed;
        }

        void Start()
        {
            // 처음 값을 적용된 값으로 기억 (설정창을 처음 열 때 캐릭터가 움직이지 않도록 위치는 건드리지 않음)
            if (_sideStepper != null) _appliedSideIndex = _sideStepper.Index;
            if (_placeStepper != null) _appliedPlaceIndex = _placeStepper.Index;
        }

        void OnDestroy()
        {
            ControlReaderSwitch.SettingClosed -= OnSettingClosed;
        }

        // 설정창이 닫혔을 때: 둘 중 하나라도 값이 바뀌었으면 화면을 한 번 깜빡이며 적용
        private void OnSettingClosed()
        {
            bool isSideChanged = _sideStepper != null && _sideStepper.Index != _appliedSideIndex;
            bool isPlaceChanged = _placeStepper != null && _placeStepper.Index != _appliedPlaceIndex;
            if (!isSideChanged && !isPlaceChanged) return;

            // 바뀐 쪽만 적용 (사이드만 바꿨으면 두 캐릭터가 서 있던 자리에서 좌우만 바뀜)
            if (SPS_screenFade != null) SPS_screenFade.Play(onBlack: () => Apply(shouldApplySide: isSideChanged, shouldApplyPlace: isPlaceChanged));
            else Apply(shouldApplySide: isSideChanged, shouldApplyPlace: isPlaceChanged);
        }

        /// <summary>
        /// 시작 사이드 / 위치를 바꾸고 두 캐릭터를 바로 그 자리에 세움 (재시작에서 사용)
        /// null인 값은 지금 고른 값을 그대로 씀. 설정창의 스테퍼 값도 같이 바뀜
        /// isPlayerLeft가 false면 Player가 오른쪽(2P 자리)
        /// </summary>
        public void SetAndApply(bool? isPlayerLeft, Place? place)
        {
            if (isPlayerLeft.HasValue && _sideStepper != null)
            {
                // 선택지는 왼쪽 / 오른쪽 두 개라서, 왼쪽이 아닌 쪽이 오른쪽
                int rightIndex = _leftIndex == 0 ? 1 : 0;
                _sideStepper.SetIndex(isPlayerLeft.Value ? _leftIndex : rightIndex);
            }

            if (place.HasValue && _placeStepper != null)
            {
                switch (place.Value)
                {
                    case Place.LeftEnd: _placeStepper.SetIndex(_leftEndIndex); break;
                    case Place.RightEnd: _placeStepper.SetIndex(_rightEndIndex); break;
                    default: _placeStepper.SetIndex(_centerIndex); break;
                }
            }

            ApplyCurrent();
        }

        /// <summary>지금 고른 사이드와 위치대로 두 캐릭터를 바로 다시 세움</summary>
        public void ApplyCurrent()
        {
            Apply(shouldApplySide: true, shouldApplyPlace: true);
        }

        private void Apply(bool shouldApplySide, bool shouldApplyPlace)
        {
            GameObject player = GameObject.FindGameObjectWithTag(SPS_playerTag);
            Transform enemy = FindClosestEnemy(player);

            if (player != null && enemy != null)
            {
                if (shouldApplySide && _sideStepper != null)
                {
                    _appliedSideIndex = _sideStepper.Index;
                    ApplySide(player.transform, enemy, _appliedSideIndex);
                }
                if (shouldApplyPlace && _placeStepper != null)
                {
                    _appliedPlaceIndex = _placeStepper.Index;
                    ApplyPlace(player.transform, enemy, _appliedPlaceIndex);
                }
            }

            // 순간이동한 것을 Boundary 벽과 Pushbox에 알림 (벽을 넘어가거나 겹친 것으로 보고 되돌리지 않도록)
            CharStageBoundary.SyncAfterTeleport();
        }

        // Player가 고른 쪽에 서도록 Player와 Enemy의 x 위치를 맞바꿈 (이미 그쪽에 있으면 그대로 둠)
        private void ApplySide(Transform player, Transform enemy, int index)
        {
            bool shouldPlayerBeLeft = index == _leftIndex;
            bool isPlayerLeft = player.position.x < enemy.position.x;
            if (shouldPlayerBeLeft == isPlayerLeft) return;

            float playerX = player.position.x;
            SetPositionX(player, enemy.position.x);
            SetPositionX(enemy, playerX);
        }

        // 고른 위치에 두 캐릭터를 다시 세움
        private void ApplyPlace(Transform player, Transform enemy, int index)
        {
            // 지금 왼쪽에 있는 캐릭터와 오른쪽에 있는 캐릭터
            bool isPlayerLeft = player.position.x <= enemy.position.x;
            Transform leftCharacter = isPlayerLeft ? player : enemy;
            Transform rightCharacter = isPlayerLeft ? enemy : player;

            float leftX;
            if (index == _leftEndIndex) leftX = GetStageLeftEndX(leftCharacter);
            else if (index == _rightEndIndex) leftX = GetStageRightEndX(rightCharacter) - _characterGap;
            else if (index == _centerIndex) leftX = GetStageCenterX() - _characterGap * 0.5f;
            else return;

            SetPositionX(leftCharacter, leftX);
            SetPositionX(rightCharacter, leftX + _characterGap);
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
