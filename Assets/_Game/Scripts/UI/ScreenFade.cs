using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 화면 전체를 덮는 검은색 Image(ScreenFade)에 붙여서 사용
    // Play를 부르면 화면이 순간 검게 변하고, 그동안 할 일을 실행한 뒤 서서히 원래 화면으로 돌아옴
    // - 설정창이 닫혀도 꺼지지 않도록 설정창 밖(캔버스 맨 아래)에 둠
    // - 게임이 멈춰 있어도(timeScale 0) 동작하도록 실제 시간으로 동작함
    public class ScreenFade : MonoBehaviour
    {
        [SerializeField] private Image _fadeImage;

        // 검은 화면을 유지하는 시간(초)
        [SerializeField] private float _blackHoldTime = 0.1f;
        // 검은 화면에서 게임 화면으로 돌아오는 시간(초)
        [SerializeField] private float _fadeTime = 0.5f;

        private Coroutine _fadeRoutine;

        void Awake()
        {
            if (_fadeImage == null) _fadeImage = GetComponent<Image>();
            if (_fadeImage == null) return;

            // 검은 화면이 마우스 입력을 막지 않도록 함
            _fadeImage.raycastTarget = false;
            SetFadeAlpha(0f);
        }

        // 순간 검은 화면 -> onBlack 실행 -> 서서히 게임 화면으로
        public void Play(Action onBlack)
        {
            Play(onBlack, fadeInTime: 0f);
        }

        /// <summary>fadeInTime(초) 동안 서서히 검은 화면이 됨 -> onBlack 실행 -> 서서히 게임 화면으로 (0이면 순간 검은 화면)</summary>
        public void Play(Action onBlack, float fadeInTime)
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(PlayRoutine(onBlack, fadeInTime));
        }

        private IEnumerator PlayRoutine(Action onBlack, float fadeInTime)
        {
            float fadeInElapsed = 0f;
            while (fadeInElapsed < fadeInTime)
            {
                fadeInElapsed += Time.unscaledDeltaTime;
                SetFadeAlpha(Mathf.Clamp01(fadeInElapsed / fadeInTime));
                yield return null;
            }
            SetFadeAlpha(1f);

            // 화면이 가려진 동안 할 일
            onBlack?.Invoke();

            yield return new WaitForSecondsRealtime(_blackHoldTime);

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

        private void SetFadeAlpha(float alpha)
        {
            if (_fadeImage == null) return;

            Color color = _fadeImage.color;
            color.a = alpha;
            _fadeImage.color = color;
        }
    }
}
