using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    // 액션(예: UI/Select)에 연결된 키를 지금 쓰는 컨트롤러에 맞는 버튼 스프라이트로 보여줌
    // 예) 결정: 키보드면 J, 플레이스테이션이면 ×, Xbox면 A
    // 컨트롤러 종류는 ControllerTypeDetector가 알려주고, 바뀌면 바로 다시 그림
    public class ButtonIconDisplay : MonoBehaviour
    {
        [SerializeField] private InputActionAsset BID_inputActions;
        [SerializeField] private string BID_actionMapName = "UI";
        [SerializeField] private string BID_actionName = "Select";

        [Tooltip("키별 버튼 스프라이트 목록")]
        [SerializeField] private ButtonIconSet BID_buttonIcons;

        [Tooltip("스프라이트를 넣을 Image. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private Image _iconImage;

        [Tooltip("스프라이트가 없는 키일 때 대신 키 이름을 보여줄 글자 (없으면 비워둠)")]
        [SerializeField] private TMP_Text _fallbackText;

        private void Awake()
        {
            if (_iconImage == null) _iconImage = GetComponent<Image>();
            if (_iconImage != null) _iconImage.preserveAspect = true;
        }

        private void OnEnable()
        {
            ControllerTypeDetector.OnControllerTypeChanged += Refresh;
            Refresh(ControllerTypeDetector.CurrentType);
        }

        private void OnDisable()
        {
            ControllerTypeDetector.OnControllerTypeChanged -= Refresh;
        }

        private void Refresh(ControllerTypeDetector.ControllerType type)
        {
            if (_iconImage == null) return;

            string path = FindBindingPath(type, out Sprite sprite);
            _iconImage.sprite = sprite;
            _iconImage.enabled = sprite != null;

            if (_fallbackText != null)
            {
                _fallbackText.text = sprite == null ? GetKeyName(type, path) : "";
                _fallbackText.gameObject.SetActive(sprite == null);
            }
        }

        // 액션에서 지금 컨트롤러 종류의 그룹(PC / GamePad)에 속한 바인딩 중 스프라이트가 있는 첫 번째 경로
        // 예) 메뉴: 플레이스테이션이면 홈 버튼, Xbox면 메뉴 버튼 (둘 다 GamePad 그룹이라 그림이 있는 쪽을 고름)
        // 스프라이트가 있는 바인딩이 없으면 첫 번째 바인딩 경로를 돌려줌
        private string FindBindingPath(ControllerTypeDetector.ControllerType type, out Sprite sprite)
        {
            sprite = null;
            InputAction action = BID_inputActions != null ? BID_inputActions.FindActionMap(BID_actionMapName)?.FindAction(BID_actionName) : null;
            if (action == null) return null;

            string group = ControllerTypeDetector.GetBindingGroup(type);
            string firstPath = null;
            foreach (InputBinding binding in action.bindings)
            {
                if (string.IsNullOrEmpty(binding.groups) || !binding.groups.Contains(group)) continue;

                // 플레이스테이션 전용 바인딩(터치패드 등)은 다른 패드에서 건너뜀
                bool isPlayStationOnly = binding.effectivePath.StartsWith("<DualShockGamepad>");
                if (isPlayStationOnly && type != ControllerTypeDetector.ControllerType.PlayStation) continue;

                if (firstPath == null) firstPath = binding.effectivePath;
                Sprite bindingSprite = BID_buttonIcons != null ? BID_buttonIcons.GetSprite(type, binding.effectivePath) : null;
                if (bindingSprite != null)
                {
                    sprite = bindingSprite;
                    return binding.effectivePath;
                }
            }
            return firstPath;
        }

        // 스프라이트가 없을 때 보여줄 키 이름 (Xbox는 패드에 적힌 이름으로)
        private static string GetKeyName(ControllerTypeDetector.ControllerType type, string path)
        {
            if (string.IsNullOrEmpty(path)) return "-";

            if (type == ControllerTypeDetector.ControllerType.Xbox)
            {
                if (path == "<Gamepad>/select") return "View";
                if (path == "<Gamepad>/start") return "Menu";
            }
            return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
    }
}
