using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Controller
{
    // 키보드 키를 Xbox 패드 버튼으로 바꿔서 쓰기 위한 표 (Xbox 버튼 하나마다 키보드 키 하나)
    // - 게임 입력(Input Actions)은 패드 버튼 기준으로 정해 두고, 키보드는 이 표를 거쳐 그 패드 버튼처럼 동작함
    //   예) 표에 A = J이고, 강 공격이 A면 -> 키보드 J로 강 공격
    //   (공격 버튼에 다른 패드 버튼을 연결하면 키보드 키도 그 패드 버튼의 키로 따라 바뀜)
    // - 실제로는 Input Actions의 키보드 바인딩(PC 그룹)을 이 표에 맞춰 덮어씀 (ApplyTo)
    //   가상 패드를 만들지 않으므로 입력이 늦어지지 않고, UI는 그대로 키보드 키 그림을 보여줌
    // - 메뉴 버튼(Start)은 ESC로 고정 (어떤 키로 바꿔도 설정창을 열고 닫을 수 있도록)
    // - 씬이 바뀌어도 유지되고, 빌드에서는 PlayerPrefs에 저장해서 껐다 켜도 유지됨
    //   (에디터 플레이 모드에서는 저장하지 않음, KeyBindingStore와 같은 규칙)
    public static class KeyboardMapping
    {
        // Xbox 버튼 경로 (옵션 창의 줄 순서와 같음)
        public static readonly string[] GamepadPaths =
        {
            "<Gamepad>/dpad/up",
            "<Gamepad>/dpad/down",
            "<Gamepad>/dpad/left",
            "<Gamepad>/dpad/right",
            "<Gamepad>/buttonSouth",
            "<Gamepad>/buttonEast",
            "<Gamepad>/buttonWest",
            "<Gamepad>/buttonNorth",
            "<Gamepad>/leftShoulder",
            "<Gamepad>/rightShoulder",
            "<Gamepad>/leftTrigger",
            "<Gamepad>/rightTrigger",
            "<Gamepad>/start",
            "<Gamepad>/select",
        };

        // 처음 키 (GamepadPaths와 같은 순서, 원래 Input Actions에 적혀 있던 키보드 키와 같음)
        private static readonly string[] _defaultKeys =
        {
            "<Keyboard>/w",
            "<Keyboard>/s",
            "<Keyboard>/a",
            "<Keyboard>/d",
            "<Keyboard>/j",
            "<Keyboard>/k",
            "<Keyboard>/u",
            "<Keyboard>/i",
            "<Keyboard>/q",
            "<Keyboard>/e",
            "<Keyboard>/o",
            "<Keyboard>/p",
            "<Keyboard>/escape",
            "<Keyboard>/space",
        };

        // 바꿀 수 없는 버튼 (메뉴 버튼 = ESC)
        public const string LockedGamepadPath = "<Gamepad>/start";
        // 키 입력을 기다리는 동안 취소 키로 쓰므로 다른 버튼에는 넣을 수 없는 키
        public const string CancelKeyPath = "<Keyboard>/escape";

        private const string KM_SaveKey = "KeyboardMapping";

        // 지금 키 (GamepadPaths와 같은 순서)
        private static string[] _keys;
        private static bool KM_isLoaded;

        // Domain Reload를 꺼둔 상태로 플레이해도 이전 값이 남지 않도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _keys = null;
            KM_isLoaded = false;
        }

        // Xbox 버튼에 연결된 키보드 키 (없는 버튼이면 null)
        public static string GetKey(string gamepadPath)
        {
            LoadOnce();
            int index = IndexOfPath(GamepadPaths, gamepadPath);
            return index < 0 ? null : _keys[index];
        }

        // 키보드 키가 연결된 Xbox 버튼 (어디에도 연결되지 않은 키면 null)
        public static string FindGamepadPath(string keyboardPath)
        {
            LoadOnce();
            int index = IndexOfPath(_keys, keyboardPath);
            return index < 0 ? null : GamepadPaths[index];
        }

        public static bool IsLocked(string gamepadPath)
        {
            return IsSamePath(gamepadPath, LockedGamepadPath);
        }

        // Xbox 버튼의 키를 바꿈. 다른 버튼이 이미 그 키를 쓰고 있으면 두 버튼의 키를 서로 바꿈
        // 바꿀 수 없으면 false (고정된 버튼, 고정된 버튼의 키, 없는 버튼)
        public static bool SetKey(string gamepadPath, string keyboardPath)
        {
            LoadOnce();
            int index = IndexOfPath(GamepadPaths, gamepadPath);
            if (index < 0 || IsLocked(gamepadPath) || string.IsNullOrEmpty(keyboardPath)) return false;

            int otherIndex = IndexOfPath(_keys, keyboardPath);
            if (otherIndex == index) return true;
            if (otherIndex >= 0 && IsLocked(GamepadPaths[otherIndex])) return false;

            if (otherIndex >= 0) _keys[otherIndex] = _keys[index];
            _keys[index] = keyboardPath;
            Save();
            return true;
        }

        public static void ResetToDefault()
        {
            _keys = (string[])_defaultKeys.Clone();
            KM_isLoaded = true;
            Save();
        }

        // 에셋의 키보드 바인딩을 표에 맞춰 덮어씀
        // 액션마다 패드 바인딩 중 표에 있는 첫 번째 버튼을 찾아, 그 버튼의 키를 첫 번째 키보드 바인딩에 넣음
        // (같은 액션의 다른 키보드 바인딩은 꺼짐. 표에 있는 패드 버튼이 없는 액션은 키보드 바인딩을 그대로 둠)
        public static void ApplyTo(InputActionAsset asset)
        {
            if (asset == null) return;
            LoadOnce();

            foreach (InputActionMap map in asset.actionMaps)
            foreach (InputAction action in map.actions)
            {
                string key = FindKeyForAction(action);
                if (key == null) continue;

                bool isFirst = true;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding binding = action.bindings[i];
                    if (binding.isComposite || binding.isPartOfComposite || !IsKeyboardBinding(binding)) continue;

                    string path = isFirst ? key : "";
                    isFirst = false;

                    if (IsSamePath(binding.path, path)) action.RemoveBindingOverride(i);
                    else action.ApplyBindingOverride(i, path);
                }
            }
        }

        // 액션의 패드 바인딩 중 표에 있는 첫 번째 버튼의 키보드 키
        private static string FindKeyForAction(InputAction action)
        {
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite) continue;

                string key = GetKey(ToGamepadPath(binding.effectivePath));
                if (key != null) return key;
            }
            return null;
        }

        // Xbox 패드로 쓰는 바인딩 경로를 표의 경로 형식으로 맞춤
        // "<XInputController>/start" -> "<Gamepad>/start" (플레이스테이션 전용 "<DualShockGamepad>/..."는 제외)
        public static string ToGamepadPath(string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath)) return null;

            int slash = bindingPath.IndexOf('/');
            if (slash < 0) return null;

            string layout = bindingPath.Substring(0, slash);
            if (!IsSamePath(layout, "<Gamepad>") && !IsSamePath(layout, "<XInputController>")) return null;

            return "<Gamepad>" + bindingPath.Substring(slash);
        }

        private static bool IsKeyboardBinding(InputBinding binding)
        {
            return !string.IsNullOrEmpty(binding.path) && binding.path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase);
        }

        private static void LoadOnce()
        {
            if (KM_isLoaded) return;
            KM_isLoaded = true;

            _keys = (string[])_defaultKeys.Clone();

#if !UNITY_EDITOR
            // 저장 형식: 한 줄에 "Xbox 버튼 경로=키보드 키 경로"
            string saved = PlayerPrefs.GetString(KM_SaveKey, "");
            foreach (string line in saved.Split('\n'))
            {
                int equal = line.IndexOf('=');
                if (equal < 0) continue;

                int index = IndexOfPath(GamepadPaths, line.Substring(0, equal));
                if (index >= 0 && !IsLocked(GamepadPaths[index])) _keys[index] = line.Substring(equal + 1);
            }
#endif
        }

        private static void Save()
        {
#if !UNITY_EDITOR
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < GamepadPaths.Length; i++)
            {
                builder.Append(GamepadPaths[i]).Append('=').Append(_keys[i]).Append('\n');
            }
            PlayerPrefs.SetString(KM_SaveKey, builder.ToString());
            PlayerPrefs.Save();
#endif
        }

        private static int IndexOfPath(string[] paths, string path)
        {
            if (string.IsNullOrEmpty(path)) return -1;

            for (int i = 0; i < paths.Length; i++)
            {
                if (IsSamePath(paths[i], path)) return i;
            }
            return -1;
        }

        private static bool IsSamePath(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
