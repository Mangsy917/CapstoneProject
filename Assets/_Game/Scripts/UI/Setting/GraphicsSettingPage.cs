using System.Collections.Generic;
using UnityEngine;

namespace Game.UI.Setting
{
    // 옵션의 그래픽 설정 페이지에 붙여서 사용 (OptionPage와 같은 오브젝트)
    // - 화면 모드: 전체화면 / 전체 창 모드 / 창 모드 (스테퍼 선택지 순서가 _screenModes와 같아야 함)
    // - 해상도: 모니터가 지원하는 해상도 목록 (실행할 때 채움)
    // 값을 바꾸면 바로 적용됨. 화면 모드와 해상도는 Unity가 알아서 저장함 (빌드에서 껐다 켜도 유지)
    // 에디터에서는 Game 창 크기가 바뀌지 않으므로 빌드에서 확인해야 함
    // 그림자 / 텍스처 / 이펙트 / 배경 품질은 아직 없음 (줄의 Interactable을 꺼둠)
    // 기본값으로 되돌리기: 전체 창 모드 + 모니터 해상도 (ResetToDefault)
    public class GraphicsSettingPage : MonoBehaviour
    {
        [SerializeField] private SettingStepper _screenModeStepper;
        [SerializeField] private SettingStepper _resolutionStepper;

        // 화면 모드 스테퍼 선택지 순서
        private static readonly FullScreenMode[] _screenModes =
        {
            FullScreenMode.ExclusiveFullScreen,
            FullScreenMode.FullScreenWindow,
            FullScreenMode.Windowed,
        };

        // 해상도 스테퍼 선택지 순서 (작은 것 -> 큰 것)
        private readonly List<Vector2Int> _resolutions = new List<Vector2Int>();

        private bool _isStarted;

        private void Awake()
        {
            if (_screenModeStepper != null) _screenModeStepper.onValueChanged.AddListener(ApplyScreenMode);
            if (_resolutionStepper != null) _resolutionStepper.onValueChanged.AddListener(ApplyResolution);
        }

        // 처음 열 때는 줄들의 Awake(처음 값 설정)가 끝난 뒤인 Start에서, 그 뒤로는 열 때마다 OnEnable에서 지금 값을 보여줌
        private void Start()
        {
            _isStarted = true;
            ShowCurrentValues();
        }

        private void OnEnable()
        {
            if (_isStarted) ShowCurrentValues();
        }

        // 기본값으로 되돌리기 줄의 OnClick에 연결
        // 전체 창 모드 + 모니터 해상도 (Unity 빌드의 처음 설정과 같음)
        public void ResetToDefault()
        {
            Vector2Int monitorSize = new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);
            Screen.SetResolution(monitorSize.x, monitorSize.y, DefaultScreenMode);

            // 화면 크기는 다음 프레임에 바뀌므로 지금 값 대신 바꿀 값을 보여줌
            ShowValues(DefaultScreenMode, monitorSize);
        }

        private const FullScreenMode DefaultScreenMode = FullScreenMode.FullScreenWindow;

        private void ShowCurrentValues()
        {
            ShowValues(Screen.fullScreenMode, new Vector2Int(Screen.width, Screen.height));
        }

        private void ShowValues(FullScreenMode screenMode, Vector2Int current)
        {
            if (_screenModeStepper != null)
            {
                int modeIndex = System.Array.IndexOf(_screenModes, screenMode);
                // 맥의 최대화 창 등 목록에 없는 모드는 전체 창 모드로 보여줌
                if (modeIndex < 0) modeIndex = 1;
                _screenModeStepper.SetOptions(_screenModeStepper.options, modeIndex);
            }

            if (_resolutionStepper != null)
            {
                FindResolutions();

                int currentIndex = _resolutions.IndexOf(current);
                if (currentIndex < 0)
                {
                    // 목록에 없는 크기(창 크기를 직접 바꾼 경우 등)도 고를 수 있도록 넣어 둠
                    _resolutions.Add(current);
                    _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                    currentIndex = _resolutions.IndexOf(current);
                }

                string[] names = new string[_resolutions.Count];
                for (int i = 0; i < _resolutions.Count; i++) names[i] = _resolutions[i].x + " x " + _resolutions[i].y;
                _resolutionStepper.SetOptions(names, currentIndex);
            }
        }

        // 모니터가 지원하는 해상도 (같은 크기는 주사율이 달라도 하나로)
        private void FindResolutions()
        {
            _resolutions.Clear();
            foreach (Resolution resolution in Screen.resolutions)
            {
                Vector2Int size = new Vector2Int(resolution.width, resolution.height);
                if (!_resolutions.Contains(size)) _resolutions.Add(size);
            }
            _resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        }

        private void ApplyScreenMode(int index)
        {
            if (index < 0 || index >= _screenModes.Length) return;
            Screen.fullScreenMode = _screenModes[index];
        }

        private void ApplyResolution(int index)
        {
            if (index < 0 || index >= _resolutions.Count) return;
            Screen.SetResolution(_resolutions[index].x, _resolutions[index].y, Screen.fullScreenMode);
        }
    }
}
