using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI.Setting
{
    // 컨트롤러 설정 팝업 오른쪽의 가상 컨트롤러 (레버 + 공격 버튼 4개)
    // - 평소에는 반투명하게 보이고 입력을 받지 않음
    // - 버튼 확인 중(StartTest ~ StopTest)에만 또렷해지고, 실제로 누른 레버 방향과 공격 버튼이 밝게 켜짐
    //   설정창이 열린 동안 꺼져 있는 캐릭터 조작 입력(Char 액션 맵)을 잠깐 켜서 읽음
    // - 버튼 확인 중에는 아래에 "뒤로 돌아가려면 (키)를 누르세요"를 보여줌
    //   나가는 키: 키보드 ESC, 패드 메뉴 버튼 (플레이스테이션 Options, Xbox 메뉴)
    public class VirtualController : MonoBehaviour
    {
        [Serializable]
        public class AttackButtonView
        {
            // Input Actions 에셋의 액션 이름 (AttackL, AttackM ...)
            public string VC_actionName;
            public Image buttonImage;
        }

        [SerializeField] private InputActionAsset VC_inputActions;
        [SerializeField] private string VC_actionMapName = "Char";

        [Tooltip("키별 버튼 스프라이트 목록 (나가는 키 표시에 사용)")]
        [SerializeField] private Game.UI.ButtonIconSet VC_buttonIcons;

        [Tooltip("레버 방향 이미지 9개. 넘버패드 순서 (1 = 왼쪽 아래, 5 = 중립, 9 = 오른쪽 위)")]
        [SerializeField] private Image[] _directionImages = new Image[9];

        [SerializeField] private AttackButtonView[] _attackButtons;

        [Header("반투명")]
        [Tooltip("가상 컨트롤러 전체의 투명도를 바꿀 CanvasGroup. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [Tooltip("버튼 확인 중이 아닐 때의 불투명도 (0 = 안 보임, 1 = 또렷함)")]
        [Range(0f, 1f)]
        [SerializeField] private float _idleAlpha = 0.35f;

        [Header("나가기 안내")]
        [Tooltip("버튼 확인 중에만 보이는 '뒤로 돌아가려면 (키)를 누르세요' 묶음")]
        [SerializeField] private GameObject _exitGuide;
        [Tooltip("괄호 자리에 들어갈 나가는 키 스프라이트")]
        [SerializeField] private Image _exitKeyIcon;
        [Tooltip("스프라이트가 없을 때 대신 보여줄 키 이름")]
        [SerializeField] private TMP_Text _exitKeyText;

        // 컨트롤러 종류별 나가는 키 (스프라이트를 찾을 때 쓰는 바인딩 경로)
        private const string KeyboardExitPath = "<Keyboard>/escape";
        private const string GamepadExitPath = "<Gamepad>/start";

        // 눌렀을 때 / 뗐을 때 밝기 (게임 화면의 입력 표시와 같은 값)
        private const float PressedBrightness = 255f / 255f;
        private const float ReleasedBrightness = 123f / 255f;

        public bool IsTesting { get; private set; }

        private InputActionMap _actionMap;
        private InputAction _upAction, _downAction, _leftAction, _rightAction;
        private InputAction[] _attackActions = new InputAction[0]; // _attackButtons와 같은 순서 (매 프레임 이름으로 찾지 않도록 미리 찾아 둠)
        private bool _wasMapEnabled; // 버튼 확인을 시작하기 전에 Char 액션 맵이 켜져 있었는지

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_exitKeyIcon != null) _exitKeyIcon.preserveAspect = true;

            _actionMap = VC_inputActions != null ? VC_inputActions.FindActionMap(VC_actionMapName) : null;
            if (_actionMap != null)
            {
                _upAction = _actionMap.FindAction("Up");
                _downAction = _actionMap.FindAction("Down");
                _leftAction = _actionMap.FindAction("Left");
                _rightAction = _actionMap.FindAction("Right");

                if (_attackButtons != null)
                {
                    _attackActions = new InputAction[_attackButtons.Length];
                    for (int i = 0; i < _attackButtons.Length; i++)
                    {
                        _attackActions[i] = _actionMap.FindAction(_attackButtons[i].VC_actionName);
                    }
                }
            }
        }

        private void OnEnable()
        {
            ControllerTypeDetector.OnControllerTypeChanged += HandleControllerTypeChanged;
            RefreshExitKey();
            ShowTestState();
        }

        private void OnDisable()
        {
            ControllerTypeDetector.OnControllerTypeChanged -= HandleControllerTypeChanged;
            StopTest();
        }

        private void HandleControllerTypeChanged(ControllerTypeDetector.ControllerType type)
        {
            RefreshExitKey();
        }

        // ===== 버튼 확인 =====

        public void StartTest()
        {
            if (IsTesting || _actionMap == null) return;

            IsTesting = true;
            _wasMapEnabled = _actionMap.enabled;
            if (!_wasMapEnabled) _actionMap.Enable();
            ShowTestState();
        }

        public void StopTest()
        {
            if (!IsTesting) return;

            IsTesting = false;
            if (!_wasMapEnabled && _actionMap != null) _actionMap.Disable();
            ShowTestState();
        }

        /// <summary>이번 프레임에 나가는 키(ESC / 패드 메뉴 버튼, 플레이스테이션은 Options)를 눌렀는지</summary>
        public bool WasExitPressedThisFrame()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;

            foreach (Gamepad gamepad in Gamepad.all)
            {
                if (gamepad.startButton.wasPressedThisFrame) return true;
            }
            return false;
        }

        private void Update()
        {
            if (!IsTesting) return;
            ShowPressed(ReadDirection(), isTesting: true);
        }

        // 버튼 확인 중인지에 따라 투명도, 나가기 안내, 켜진 버튼을 맞춤
        private void ShowTestState()
        {
            if (_canvasGroup != null) _canvasGroup.alpha = IsTesting ? 1f : _idleAlpha;
            if (_exitGuide != null) _exitGuide.SetActive(IsTesting);
            ShowPressed(direction: 5, isTesting: false);
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
            for (int i = 0; i < _attackButtons.Length; i++)
            {
                InputAction action = isTesting && i < _attackActions.Length ? _attackActions[i] : null;
                SetBrightness(_attackButtons[i].buttonImage, action != null && action.IsPressed());
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

        // ===== 나가는 키 표시 =====

        private void RefreshExitKey()
        {
            var type = ControllerTypeDetector.CurrentType;
            string path;
            string keyName;
            switch (type)
            {
                case ControllerTypeDetector.ControllerType.PlayStation: path = GamepadExitPath; keyName = "Options"; break;
                case ControllerTypeDetector.ControllerType.Xbox: path = GamepadExitPath; keyName = "Menu"; break;
                default: path = KeyboardExitPath; keyName = "ESC"; break;
            }

            Sprite sprite = VC_buttonIcons != null ? VC_buttonIcons.GetSprite(type, path) : null;
            if (_exitKeyIcon != null)
            {
                _exitKeyIcon.sprite = sprite;
                _exitKeyIcon.gameObject.SetActive(sprite != null);
            }
            if (_exitKeyText != null)
            {
                _exitKeyText.text = keyName;
                _exitKeyText.gameObject.SetActive(sprite == null);
            }
        }
    }
}
