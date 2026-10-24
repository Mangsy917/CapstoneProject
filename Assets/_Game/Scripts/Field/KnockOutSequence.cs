using System.Collections;
using Game.Character;
using Game.UI;
using UnityEngine;

namespace Game.Field
{
    // 상대(Enemy)의 체력이 0이 되면 KO 연출 후 연습을 재시작함 (GameManager에 붙여서 사용)
    // 1) 화면에 K.O. 글자가 뜨고 슬로우 모션 (Time.timeScale을 낮추면 GameSpeed가 프레임을 건너뜀)
    // 2) 잠시 뒤 화면이 서서히 검게 변함
    // 3) 검은 화면 동안 K.O. 글자를 숨기고 속도를 되돌린 뒤 재시작 (PracticeRestart)
    // 4) 서서히 원래 화면으로 돌아옴
    // - 시간은 모두 실제 시간(초)으로 셈 (슬로우 모션에 영향받지 않음)
    public class KnockOutSequence : MonoBehaviour
    {
        [Tooltip("화면 가운데에 띄울 K.O. 글자 오브젝트 (평소에는 꺼둠)")]
        [SerializeField] private GameObject _knockOutText;

        [Tooltip("검은 화면 연출에 쓸 ScreenFade. 비워두면 씬에서 찾음")]
        [SerializeField] private ScreenFade KOS_screenFade;

        [Tooltip("재시작에 쓸 PracticeRestart. 비워두면 씬에서 찾음")]
        [SerializeField] private PracticeRestart _practiceRestart;

        [Tooltip("체력이 0이 되면 KO가 되는 캐릭터의 태그")]
        [SerializeField] private string KOS_enemyTag = "Enemy";

        [Header("슬로우 모션")]
        [Tooltip("슬로우 모션 속도 (1 = 평소, 0.25 = 4배 느리게)")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _slowMotionScale = 0.25f;
        [Tooltip("K.O.가 뜬 뒤 화면이 검게 변하기 시작할 때까지의 시간(초)")]
        [SerializeField] private float _slowMotionTime = 1.5f;

        [Header("검은 화면")]
        [Tooltip("검은 화면으로 서서히 변하는 시간(초)")]
        [SerializeField] private float _fadeInTime = 0.5f;

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            if (KOS_screenFade == null) KOS_screenFade = FindAnyObjectByType<ScreenFade>(FindObjectsInactive.Include);
            if (_practiceRestart == null) _practiceRestart = FindAnyObjectByType<PracticeRestart>(FindObjectsInactive.Include);
            if (_knockOutText != null) _knockOutText.SetActive(false);
        }

        private void Update()
        {
            if (IsPlaying) return;

            foreach (GameObject enemy in GameObject.FindGameObjectsWithTag(KOS_enemyTag))
            {
                var health = enemy.GetComponent<CharacterHealth>();
                if (health != null && health.CurrentHealth <= 0)
                {
                    StartCoroutine(PlayRoutine());
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            // 연출 도중 씬을 나가도 느려진 속도가 다음 씬까지 이어지지 않도록 되돌림
            if (IsPlaying) Time.timeScale = 1f;
        }

        private IEnumerator PlayRoutine()
        {
            IsPlaying = true;
            if (_knockOutText != null) _knockOutText.SetActive(true);
            Time.timeScale = _slowMotionScale;

            yield return new WaitForSecondsRealtime(_slowMotionTime);

            bool isRestarted = false;
            if (KOS_screenFade != null)
            {
                KOS_screenFade.Play(onBlack: () => { Restart(); isRestarted = true; }, fadeInTime: _fadeInTime);
                while (!isRestarted) yield return null;
            }
            else
            {
                Restart();
            }

            IsPlaying = false;
        }

        // 검은 화면 동안: K.O. 글자를 숨기고 속도를 되돌린 뒤 처음 상태로 재시작
        private void Restart()
        {
            if (_knockOutText != null) _knockOutText.SetActive(false);
            Time.timeScale = 1f;
            if (_practiceRestart != null) _practiceRestart.Restart();
        }
    }
}
