using UnityEngine;

namespace Game.Field
{
    // 프레임 수로 동작하는 스크립트(이동, 커맨드, 공격 등)의 게임 속도
    // - 이 스크립트들은 Time.deltaTime을 쓰지 않아서 Time.timeScale만으로는 느려지지 않음
    // - 그래서 매 프레임 IsTickFrame을 보고, false인 프레임은 건너뜀
    //   예) timeScale 0.25면 4프레임에 한 번만 진행 → 4배 느린 슬로우 모션
    // - timeScale 1이면 항상 true (평소와 같음), 0이면 항상 false (멈춤)
    public static class GameSpeed
    {
        private static int _lastFrame = -1;
        private static float _accumulator;
        private static bool _isTickFrame = true;

        // Domain Reload를 꺼둔 상태로 플레이해도 이전 값이 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _lastFrame = -1;
            _accumulator = 0f;
            _isTickFrame = true;
        }

        /// <summary>이번 프레임에 게임을 한 프레임 진행하는지 (한 프레임 안에서는 몇 번 불러도 같은 값)</summary>
        public static bool IsTickFrame
        {
            get
            {
                if (_lastFrame != Time.frameCount)
                {
                    _lastFrame = Time.frameCount;
                    _accumulator += Mathf.Clamp01(Time.timeScale);
                    _isTickFrame = _accumulator >= 1f;
                    if (_isTickFrame) _accumulator -= 1f;
                }
                return _isTickFrame;
            }
        }
    }
}
