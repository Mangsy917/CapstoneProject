using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.UI.Setting
{
    // 옵션의 화면 설정 값 (상단 / 하단 HUD 보이기와 위치, 감마)
    // - 씬이 바뀌어도 유지되고, 빌드에서는 PlayerPrefs에 저장해서 껐다 켜도 유지됨
    //   (에디터 플레이 모드에서는 저장하지 않음, 키 설정과 같은 규칙)
    // - HUD는 각 HUD 오브젝트의 HudDisplay가 Changed를 받아서 맞춤
    // - 감마는 여기서 만든 전역 Volume(Lift Gamma Gain)으로 게임 화면에 적용 (UI는 바뀌지 않음)
    public static class DisplaySettings
    {
        // HUD 위치 값(0~100)이 100일 때 화면 안쪽으로 들어가는 거리 (기준 해상도 1920x1080의 픽셀)
        public const float MaxHudOffset = 150f;
        // 감마 값(0~100)이 0 / 100일 때의 Lift Gamma Gain 감마 세기 (50이 기본, 0)
        private const float MaxGammaStrength = 0.5f;

        private const string DS_SaveKey = "DisplaySettings";

        public static event Action Changed;

        private static bool _isTopHudVisible = true;
        private static float _topHudOffset;
        private static bool _isBottomHudVisible = true;
        private static float _bottomHudOffset;
        private static float _gamma = 50f;
        private static bool DS_isLoaded;

        private static Volume _gammaVolume;
        private static LiftGammaGain _liftGammaGain;

        public static bool IsTopHudVisible
        {
            get { LoadOnce(); return _isTopHudVisible; }
            set { LoadOnce(); _isTopHudVisible = value; OnChanged(); }
        }

        // 0~100 (0: 원래 위치, 100: MaxHudOffset만큼 아래로)
        public static float TopHudOffset
        {
            get { LoadOnce(); return _topHudOffset; }
            set { LoadOnce(); _topHudOffset = Mathf.Clamp(value, 0f, 100f); OnChanged(); }
        }

        public static bool IsBottomHudVisible
        {
            get { LoadOnce(); return _isBottomHudVisible; }
            set { LoadOnce(); _isBottomHudVisible = value; OnChanged(); }
        }

        // 0~100 (0: 원래 위치, 100: MaxHudOffset만큼 위로)
        public static float BottomHudOffset
        {
            get { LoadOnce(); return _bottomHudOffset; }
            set { LoadOnce(); _bottomHudOffset = Mathf.Clamp(value, 0f, 100f); OnChanged(); }
        }

        // 0~100 (50: 기본, 작을수록 어둡게, 클수록 밝게)
        public static float Gamma
        {
            get { LoadOnce(); return _gamma; }
            set { LoadOnce(); _gamma = Mathf.Clamp(value, 0f, 100f); OnChanged(); }
        }

        // 모든 값을 처음 값으로 되돌림 (HUD 보이기, 위치 0, 감마 50)
        public static void ResetToDefault()
        {
            LoadOnce();
            _isTopHudVisible = true;
            _topHudOffset = 0f;
            _isBottomHudVisible = true;
            _bottomHudOffset = 0f;
            _gamma = 50f;
            OnChanged();
        }

        // Domain Reload를 꺼둔 상태로 플레이해도 이전 값이 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _isTopHudVisible = true;
            _topHudOffset = 0f;
            _isBottomHudVisible = true;
            _bottomHudOffset = 0f;
            _gamma = 50f;
            DS_isLoaded = false;
            _gammaVolume = null;
            _liftGammaGain = null;
            Changed = null;
        }

        // 게임이 시작되면 저장된 감마를 바로 적용
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnStart()
        {
            LoadOnce();
            ApplyGamma();
        }

        private static void OnChanged()
        {
            Save();
            ApplyGamma();
            Changed?.Invoke();
        }

        // 씬과 상관없이 남는 전역 Volume을 하나 만들어 감마만 덮어씀 (씬의 Volume보다 우선)
        private static void ApplyGamma()
        {
            if (_gammaVolume == null)
            {
                GameObject volumeObject = new GameObject("GammaVolume");
                UnityEngine.Object.DontDestroyOnLoad(volumeObject);

                _gammaVolume = volumeObject.AddComponent<Volume>();
                _gammaVolume.isGlobal = true;
                _gammaVolume.priority = 100f;

                VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
                _liftGammaGain = profile.Add<LiftGammaGain>(overrides: true);
                _gammaVolume.sharedProfile = profile;
            }

            float strength = (_gamma - 50f) / 50f * MaxGammaStrength;
            _liftGammaGain.gamma.value = new Vector4(1f, 1f, 1f, strength);
            _liftGammaGain.gamma.overrideState = true;
        }

        private static void LoadOnce()
        {
            if (DS_isLoaded) return;
            DS_isLoaded = true;

#if !UNITY_EDITOR
            // 저장 형식: "상단 보이기|상단 위치|하단 보이기|하단 위치|감마"
            string[] values = PlayerPrefs.GetString(DS_SaveKey, "").Split('|');
            if (values.Length != 5) return;

            var culture = System.Globalization.CultureInfo.InvariantCulture;
            _isTopHudVisible = values[0] == "1";
            float.TryParse(values[1], System.Globalization.NumberStyles.Float, culture, out _topHudOffset);
            _isBottomHudVisible = values[2] == "1";
            float.TryParse(values[3], System.Globalization.NumberStyles.Float, culture, out _bottomHudOffset);
            if (!float.TryParse(values[4], System.Globalization.NumberStyles.Float, culture, out _gamma)) _gamma = 50f;
#endif
        }

        private static void Save()
        {
#if !UNITY_EDITOR
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            string saved = string.Join("|",
                _isTopHudVisible ? "1" : "0",
                _topHudOffset.ToString(culture),
                _isBottomHudVisible ? "1" : "0",
                _bottomHudOffset.ToString(culture),
                _gamma.ToString(culture));
            PlayerPrefs.SetString(DS_SaveKey, saved);
            PlayerPrefs.Save();
#endif
        }
    }
}
