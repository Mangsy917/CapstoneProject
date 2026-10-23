using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI.Setting
{
    // 컨트롤러 설정 팝업 오른쪽의 가상 컨트롤러 (레버 + 공격 버튼 4개)
    // - 공격 버튼마다 지금 쓰는 컨트롤러(키보드 / 플레이스테이션 / Xbox)에 연결된 키를 스프라이트로 보여줌
    // - 버튼 확인 중(StartTest ~ StopTest)에는 실제로 누른 레버 방향과 공격 버튼이 밝게 켜짐
    //   설정창이 열린 동안 꺼져 있는 캐릭터 조작 입력(Char 액션 맵)을 잠깐 켜서 읽음
    public class VirtualController : MonoBehaviour
    {
        [Serializable]
        public class AttackButtonView
        {
            // Input Actions 에셋의 액션 이름 (AttackL, AttackM ...)
            public string VC_actionName;
            public Image buttonImage;
            public Image keyIcon;
            public TMP_Text keyText;
        }

        [SerializeField] private InputActionAsset VC_inputActions;
        [SerializeField] private string VC_actionMapName = "Char";

        [Tooltip("키별 버튼 스프라이트 목록")]
        [SerializeField] private Game.UI.ButtonIconSet VC_buttonIcons;

        [Tooltip("레버 방향 이미지 9개. 넘버패드 순서 (1 = 왼쪽 아래, 5 = 중립, 9 = 오른쪽 위)")]
        [SerializeField] private Image[] _directionImages = new Image[9];

        [SerializeField] private AttackButtonView[] _attackButtons;

        // 눌렀을 때 / 뗐을 때 밝기 (게임 화면의 입력 표시와 같은 값)
        private const float PressedBrightness = 255f / 255f;
        private const float ReleasedBrightness = 123f / 255f;

        public bool IsTesting { get; private set; }

        private InputActionMap _actionMap;
        private InputAction _upAction, _downAction, _leftAction, _rightAction;
        private bool _wasMapEnabled; // 버튼 확인을 시작하기 전에 Char 액션 맵이 켜져 있었는지

        private void Awake()
        {
            _actionMap = VC_inputActions != null ? VC_inputActions.FindActionMap(VC_actionMapName) : null;
            if (_actionMap != null)
            {
                _upAction = _actionMap.FindAction("Up");
                _downAction = _actionMap.FindAction("Down");
                _leftAction = _actionMap.FindAction("Left");
                _rightAction = _actionMap.FindAction("Right");
            }
        }

        private void OnEnable()
        {
            ControllerTypeDetector.OnControllerTypeChanged += HandleControllerTypeChanged;
            RefreshKeys();
            ShowPressed(direction: 5, isTesting: false);
        }

        private void OnDisable()
        {
            ControllerTypeDetector.OnControllerTypeChanged -= HandleControllerTypeChanged;
            StopTest();
        }

        private void HandleControllerTypeChanged(ControllerTypeDetector.ControllerType type)
        {
            RefreshKeys();
        }

        // ===== 버튼 확인 =====

        public void StartTest()
        {
            if (IsTesting || _actionMap == null) return;

            IsTesting = true;
            _wasMapEnabled = _actionMap.enabled;
            if (!_wasMapEnabled) _actionMap.Enable();
        }

        public void StopTest()
        {
            if (!IsTesting) return;

            IsTesting = false;
            if (!_wasMapEnabled && _actionMap != null) _actionMap.Disable();
            ShowPressed(direction: 5, isTesting: false);
        }

        private void Update()
        {
            if (!IsTesting) return;
            ShowPressed(ReadDirection(), isTesting: true);
        }

        // 위아래, 좌우를 동시에 누르면 서로 지움 (InputReader와 같은 규칙)
        private int ReadDirection()
        {
            bool up = _upAction != null && _upAction.IsPressed();
            bool down = _downAction != null && _downAction.IsPressed();
            bool left = _leftAction != null && _leftAction.IsPressed();
            bool right = _rightAction != null && _rightAction.IsPressed();

            if (up && down) { up = false; down = false; }
            if (left && right) { left = false; right = false; }

            int vertical = up ? 1 : down ? -1 : 0;
            int horizontal = right ? 1 : left ? -1 : 0;
            return 5 + horizontal + vertical * 3;
        }

        // 누른 방향과 공격 버튼만 밝게. 버튼 확인 중이 아니면 레버는 중립, 버튼은 모두 어둡게
        private void ShowPressed(int direction, bool isTesting)
        {
            for (int i = 0; i < _directionImages.Length; i++)
            {
                SetBrightness(_directionImages[i], i + 1 == direction);
            }

            if (_attackButtons == null) return;
            foreach (AttackButtonView view in _attackButtons)
            {
                InputAction action = isTesting && _actionMap != null ? _actionMap.FindAction(view.VC_actionName) : null;
                SetBrightness(view.buttonImage, action != null && action.IsPressed());
            }
        }

        private static void SetBrightness(Image image, bool isPressed)
        {
            if (image == null) return;

            float brightness = isPressed ? PressedBrightness : ReleasedBrightness;
            Color color = image.color;
            color.r = brightness;
            color.g = brightness;
            color.b = brightness;
            image.color = color;
        }

        // ===== 버튼에 연결된 키 표시 =====

        /// <summary>공격 버튼마다 연결된 키를 다시 그림 (키를 바꾼 뒤에 호출)</summary>
        public void RefreshKeys()
        {
            if (_attackButtons == null || _actionMap == null) return;

            var type = ControllerTypeDetector.CurrentType;
            string group = ControllerTypeDetector.GetBindingGroup(type);

            foreach (AttackButtonView view in _attackButtons)
            {
                string path = FindBindingPath(_actionMap.FindAction(view.VC_actionName), group);
                Sprite sprite = VC_buttonIcons != null ? VC_buttonIcons.GetSprite(type, path) : null;

                if (view.keyIcon != null)
                {
                    view.keyIcon.sprite = sprite;
                    view.keyIcon.enabled = sprite != null;
                }
                if (view.keyText != null)
                {
                    string keyName = string.IsNullOrEmpty(path) ? "-"
                        : InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
                    view.keyText.text = sprite != null ? "" : keyName;
                }
            }
        }

        private static string FindBindingPath(InputAction action, string group)
        {
            if (action == null) return null;

            foreach (InputBinding binding in action.bindings)
            {
                if (!string.IsNullOrEmpty(binding.groups) && binding.groups.Contains(group))
                    return binding.effectivePath;
            }
            return null;
        }
    }
}
