using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Setting
{
    // 시작 사이드 줄(BTN_ChooseSite)에 붙여서 사용
    // 스테퍼 값(왼쪽 / 오른쪽)에 맞춰 Player가 Enemy의 왼쪽 또는 오른쪽에 서도록 두 캐릭터의 x 위치를 바꿈
    // 값이 바뀌면 화면이 순간 검게 변하고, 그동안 위치를 바꾼 뒤 서서히 원래 화면으로 돌아옴
    // - Fade Image에는 화면 전체를 덮는 검은색 Image를 넣음 (설정창보다 위에 그려지도록 배치)
    // - 설정창이 열려 있으면 게임이 멈춰 있으므로(timeScale 0) 실제 시간으로 동작함
    public class StartSideSetting : MonoBehaviour
    {
        [SerializeField] private SettingStepper _sideStepper;

        // 화면을 덮을 검은색 Image
        [SerializeField] private Image _fadeImage;

        // 스테퍼 Options에서 왼쪽이 몇 번째인지 (왼쪽, 오른쪽 순서면 0)
        [SerializeField] private int _leftIndex = 0;

        // 검은 화면을 유지하는 시간(초)
        [SerializeField] private float _blackHoldTime = 0.1f;
        // 검은 화면에서 게임 화면으로 돌아오는 시간(초)
        [SerializeField] private float _fadeTime = 0.5f;

        [SerializeField] private string SSS_playerTag = "Player";
        [SerializeField] private string SSS_enemyTag = "Enemy";

        private Coroutine _fadeRoutine;

        void Awake()
        {
            if (_sideStepper == null) _sideStepper = GetComponent<SettingStepper>();
            if (_sideStepper != null) _sideStepper.onValueChanged.AddListener(OnSideChanged);

            if (_fadeImage != null)
            {
                // 검은 화면이 마우스 입력을 막지 않도록 함
                _fadeImage.raycastTarget = false;
                SetFadeAlpha(0f);
            }
        }

        void Start()
        {
            // 처음 값에 맞춰서 한 번 적용 (시작할 때는 깜빡이지 않음)
            if (_sideStepper != null) ApplySide(_sideStepper.Index);
        }

        void OnDisable()
        {
            // 설정창이 닫히는 등 중간에 꺼지면 검은 화면이 남지 않도록 지움
            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }
            SetFadeAlpha(0f);
        }

        // 시작 사이드가 바뀌었을 때
        private void OnSideChanged(int index)
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(SwitchSideRoutine(index));
        }

        private IEnumerator SwitchSideRoutine(int index)
        {
            // 순간 검은 화면으로
            SetFadeAlpha(1f);

            // 화면이 가려진 동안 위치를 바꿈
            ApplySide(index);

            yield return new WaitForSecondsRealtime(_blackHoldTime);

            // 서서히 게임 화면으로
            float elapsedTime = 0f;
            while (elapsedTime < _fadeTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                SetFadeAlpha(1f - Mathf.Clamp01(elapsedTime / _fadeTime));
                yield return null;
            }

            SetFadeAlpha(0f);
            _fadeRoutine = null;
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

        private void SetFadeAlpha(float alpha)
        {
            if (_fadeImage == null) return;

            Color color = _fadeImage.color;
            color.a = alpha;
            _fadeImage.color = color;
        }
    }
}
