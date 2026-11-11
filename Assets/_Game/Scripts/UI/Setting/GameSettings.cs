using UnityEngine;

namespace Game.UI.Setting
{
    // 옵션의 게임 설정 값 (화면 흔들림)
    // - 씬이 바뀌어도 유지되고, 빌드에서는 PlayerPrefs에 저장해서 껐다 켜도 유지됨
    //   (에디터 플레이 모드에서는 저장하지 않음, 화면 설정과 같은 규칙)
    // - 화면 흔들림: 맞았을 때 카메라가 흔들리는지 (HitFeedback이 확인)
    public static class GameSettings
    {
        private const string GS_SaveKey = "GameSettings";

        private static bool _isScreenShakeOn = true;
        private static bool GS_isLoaded;

        public static bool IsScreenShakeOn
        {
            get { LoadOnce(); return _isScreenShakeOn; }
            set { LoadOnce(); _isScreenShakeOn = value; Save(); }
        }

        // 모든 값을 처음 값으로 되돌림 (화면 흔들림 켜기)
        public static void ResetToDefault()
        {
            LoadOnce();
            _isScreenShakeOn = true;
            Save();
        }

        // Domain Reload를 꺼둔 상태로 플레이해도 이전 값이 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _isScreenShakeOn = true;
            GS_isLoaded = false;
        }

        private static void LoadOnce()
        {
            if (GS_isLoaded) return;
            GS_isLoaded = true;

#if !UNITY_EDITOR
            // 저장 형식: "화면 흔들림"
            string[] values = PlayerPrefs.GetString(GS_SaveKey, "").Split('|');
            if (values.Length < 1 || values[0] == "") return;
            _isScreenShakeOn = values[0] == "1";
#endif
        }

        private static void Save()
        {
#if !UNITY_EDITOR
            PlayerPrefs.SetString(GS_SaveKey, _isScreenShakeOn ? "1" : "0");
            PlayerPrefs.Save();
#endif
        }
    }
}
