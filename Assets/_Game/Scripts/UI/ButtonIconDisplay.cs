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

            Sprite sprite = BID_buttonIcons != null ? BID_buttonIcons.GetSprite(type, FindBindingPath(type)) : null;
            _iconImage.sprite = sprite;
            _iconImage.enabled = sprite != null;
        }

        // 액션에서 지금 컨트롤러 종류의 그룹(PC / GamePad)에 속한 첫 번째 바인딩 경로
        private string FindBindingPath(ControllerTypeDetector.ControllerType type)
        {
            InputAction action = BID_inputActions != null ? BID_inputActions.FindActionMap(BID_actionMapName)?.FindAction(BID_actionName) : null;
            if (action == null) return null;

            string group = ControllerTypeDetector.GetBindingGroup(type);
            foreach (InputBinding binding in action.bindings)
            {
                if (!string.IsNullOrEmpty(binding.groups) && binding.groups.Contains(group))
                    return binding.effectivePath;
            }
            return null;
        }
    }
}
