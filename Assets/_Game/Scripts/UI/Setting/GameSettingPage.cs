using UnityEngine;

namespace Game.UI.Setting
{
    // 옵션의 게임 설정 페이지에 붙여서 사용 (OptionPage와 같은 오브젝트)
    // 줄의 스테퍼를 GameSettings 값에 연결함. 값을 바꾸면 바로 적용됨
    // - 화면 흔들림: OFF, ON 스테퍼 (ON이 1번). 끄면 맞았을 때 카메라가 흔들리지 않음
    // - 기본값으로 되돌리기: 모든 값을 처음 값으로 (ResetToDefault)
    public class GameSettingPage : MonoBehaviour
    {
        [SerializeField] private SettingStepper _screenShakeStepper;

        // 스테퍼 선택지에서 ON이 몇 번째인지 (OFF, ON 순서면 1)
        private const int ScreenShakeOnIndex = 1;

        // 처음 열 때는 줄들의 Awake(처음 값 설정)가 끝난 뒤인 Start에서, 그 뒤로는 열 때마다 OnEnable에서 저장된 값을 보여줌
        private bool _isPageStarted;

        private void Awake()
        {
            if (_screenShakeStepper != null) _screenShakeStepper.onValueChanged.AddListener(index => GameSettings.IsScreenShakeOn = index == ScreenShakeOnIndex);
        }

        private void Start()
        {
            _isPageStarted = true;
            ShowSavedValues();
        }

        private void OnEnable()
        {
            if (_isPageStarted) ShowSavedValues();
        }

        // 기본값으로 되돌리기 줄의 OnClick에 연결
        public void ResetToDefault()
        {
            GameSettings.ResetToDefault();
            ShowSavedValues();
        }

        private void ShowSavedValues()
        {
            if (_screenShakeStepper != null)
            {
                _screenShakeStepper.SetOptions(_screenShakeStepper.options, GameSettings.IsScreenShakeOn ? ScreenShakeOnIndex : 0);
            }
        }
    }
}
