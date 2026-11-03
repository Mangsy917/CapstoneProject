using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.UI
{
    // 마우스 커서 모양과 보이기 / 숨기기를 관리함 (씬의 빈 오브젝트에 붙여서 사용)
    // - 커서를 Cursor Texture로 바꿈 (텍스처 타입이 Cursor로 임포트된 이미지)
    // - 빌드에서만: 게임패드 버튼이나 스틱을 쓰면 커서를 숨기고, 마우스를 움직이거나 누르면 다시 보임
    //   (에디터에서는 커서가 사라지면 불편하므로 숨기지 않음)
    // - 씬을 넘어가도 하나만 남아서 계속 동작함 (여러 씬에 넣어 두어도 처음 것만 남음)
    public class MouseCursorDisplay : MonoBehaviour
    {
        [Tooltip("마우스 커서 이미지 (텍스처 타입: Cursor)")]
        [SerializeField] private Texture2D _cursorTexture;

        [Tooltip("이미지 왼쪽 위에서부터 클릭되는 지점까지의 거리(픽셀)")]
        [SerializeField] private Vector2 _cursorHotspot = Vector2.zero;

        private static MouseCursorDisplay _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            if (_cursorTexture != null) Cursor.SetCursor(_cursorTexture, _cursorHotspot, CursorMode.Auto);
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (Application.isEditor) return;

            if (Cursor.visible && WasGamepadUsedThisFrame()) Cursor.visible = false;
            else if (!Cursor.visible && WasMouseUsedThisFrame()) Cursor.visible = true;
        }

        private static bool WasMouseUsedThisFrame()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;

            return mouse.delta.ReadValue().sqrMagnitude > 0f ||
                   mouse.scroll.ReadValue().sqrMagnitude > 0f ||
                   mouse.leftButton.wasPressedThisFrame ||
                   mouse.rightButton.wasPressedThisFrame ||
                   mouse.middleButton.wasPressedThisFrame;
        }

        // 게임패드 버튼을 새로 눌렀는지 (스틱과 십자키를 한쪽으로 민 것도 포함)
        private static bool WasGamepadUsedThisFrame()
        {
            foreach (Gamepad gamepad in Gamepad.all)
            {
                foreach (InputControl control in gamepad.allControls)
                {
                    if (control is ButtonControl button && !button.synthetic && button.wasPressedThisFrame) return true;
                }

                // 스틱 방향(left/right/up/down)은 합성 버튼이라 따로 확인
                if (gamepad.leftStick.ReadValue().sqrMagnitude > 0.25f || gamepad.rightStick.ReadValue().sqrMagnitude > 0.25f) return true;
            }
            return false;
        }
    }
}
