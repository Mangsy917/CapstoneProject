using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Controller
{
    // 바꾼 키 설정(바인딩 오버라이드)을 기억하고 Input Actions 에셋에 적용
    // - 씬이 바뀌어도 유지됨: 값을 static에 기억해 두고, 각 씬의 InputReader / UIControl이 Awake에서 Apply를 부름
    // - 빌드에서는 PlayerPrefs에 저장해서 게임을 껐다 켜도 유지됨
    // - 에디터 플레이 모드에서는 저장하지 않음 (플레이를 멈추면 기본 키로 돌아감)
    public static class KeyBindingStore
    {
        private const string SaveKey = "KeyBindingOverrides";

        // 지금 적용 중인 키 설정 (Input System의 바인딩 오버라이드 JSON)
        private static string _overridesJson = "";
        private static bool _isLoaded;

        // Domain Reload를 꺼둔 상태로 플레이해도 이전 값이 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _overridesJson = "";
            _isLoaded = false;
        }

        // 기억해 둔 키 설정을 에셋에 적용 (여러 번 불러도 결과는 같음)
        public static void Apply(InputActionAsset asset)
        {
            if (asset == null) return;

            LoadOnce();

            asset.RemoveAllBindingOverrides();
            if (!string.IsNullOrEmpty(_overridesJson)) asset.LoadBindingOverridesFromJson(_overridesJson);
        }

        // 에셋의 지금 키 설정을 기억하고, 빌드에서는 파일(PlayerPrefs)에도 저장
        public static void Save(InputActionAsset asset)
        {
            if (asset == null) return;

            _overridesJson = asset.SaveBindingOverridesAsJson();

#if !UNITY_EDITOR
            PlayerPrefs.SetString(SaveKey, _overridesJson);
            PlayerPrefs.Save();
#endif
        }

        private static void LoadOnce()
        {
            if (_isLoaded) return;
            _isLoaded = true;

#if UNITY_EDITOR
            // 에디터에서는 플레이할 때마다 기본 키로 시작
            _overridesJson = "";
#else
            _overridesJson = PlayerPrefs.GetString(SaveKey, "");
#endif
        }
    }
}
