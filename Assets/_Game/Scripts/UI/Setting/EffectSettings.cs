using UnityEngine;

namespace Game.UI.Setting
{
    // 옵션의 그래픽 설정 중 이펙트 켜기 / 끄기
    // - 끄면 맞았을 때 캐릭터가 하얗게 번쩍이지 않음 (HitFeedback이 확인)
    // - 씬이 바뀌어도 유지되고, 빌드에서는 PlayerPrefs에 저장해서 껐다 켜도 유지됨
    //   (에디터 플레이 모드에서는 저장하지 않음, 화면 설정과 같은 규칙)
    public static class EffectSettings
    {
        private const string ES_SaveKey = "EffectSettings";

        private static bool _isEffectOn = true;
        private static bool ES_isLoaded;

        public static bool IsEffectOn
        {
            get { LoadOnce(); return _isEffectOn; }
            set { LoadOnce(); _isEffectOn = value; Save(); }
        }

        // 처음 값으로 되돌림 (이펙트 켜기)
        public static void ResetToDefault()
        {
            LoadOnce();
            _isEffectOn = true;
            Save();
        }

        // Domain Reload를 꺼둔 상태로 플레이해도 이전 값이 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _isEffectOn = true;
            ES_isLoaded = false;
        }

        private static void LoadOnce()
        {
            if (ES_isLoaded) return;
            ES_isLoaded = true;

#if !UNITY_EDITOR
            string saved = PlayerPrefs.GetString(ES_SaveKey, "");
            if (saved != "") _isEffectOn = saved == "1";
#endif
        }

        private static void Save()
        {
#if !UNITY_EDITOR
            PlayerPrefs.SetString(ES_SaveKey, _isEffectOn ? "1" : "0");
            PlayerPrefs.Save();
#endif
        }
    }
}
