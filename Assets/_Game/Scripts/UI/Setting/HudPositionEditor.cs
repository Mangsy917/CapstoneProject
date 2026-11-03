using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI.Setting
{
    // HUD 위치 조절 화면 (P_OptionWindow 안의 HudPositionEditor)에 붙여서 사용
    // 화면 설정의 "상단 HUD 위치" / "하단 HUD 위치"를 고르면 열림
    // - 옵션 창과 뒤의 설정창을 숨겨서 멈춘 게임 화면이 보이게 하고, 그 위에서 실제 HUD를 위아래로 옮김
    // - 위/아래 (마우스 휠): HUD를 한 칸(위치 값 10)씩 옮김 (길게 누르면 연속으로)
    // - 선택 키 / 마우스 왼쪽 클릭: 확인 (저장), 취소 키 / ESC / 마우스 오른쪽 클릭: 처음 위치로 되돌리고 나감
    // - 위치 조절 중에는 HUD가 꺼져 있어도 보임
    public class HudPositionEditor : MonoBehaviour
    {
        // 열려 있으면 뒤의 화면 설정 페이지는 입력을 받지 않음 (닫힌 다음 프레임까지 열린 것으로 봄)
        public static bool HPE_IsOpen => HPE_openCount > 0 || Time.frameCount - HPE_closedFrame <= 1;
        private static int HPE_openCount = 0;
        private static int HPE_closedFrame = int.MinValue / 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOpenCount()
        {
            HPE_openCount = 0;
            HPE_closedFrame = int.MinValue / 2;
        }

        // 비워두면 씬에서 찾음
        [SerializeField] private UIControl HPE_uiControl;

        [Tooltip("옵션 창의 배경 (조절하는 동안 끔)")]
        [SerializeField] private Graphic _optionBackground;
        [Tooltip("조절하는 동안 숨길 옵션 창 오브젝트 (화면 설정 창 등)")]
        [SerializeField] private GameObject[] _hiddenObjects;

        [SerializeField] private TMP_Text HPE_guideText;
        [SerializeField] private string _topHudName = "상단 HUD 위치";
        [SerializeField] private string _bottomHudName = "하단 HUD 위치";
        [SerializeField] private string _guideMessage = "위 / 아래: 이동\n결정: 확인   취소: 되돌리기";

        // 한 번에 옮기는 양 (위치 값 0~100 기준)
        [SerializeField] private float _moveAmount = 10f;

        private HudDisplay.HudArea _editingArea;
        private float _startOffset;
        private float _offset;

        // 열 때 키를 아직 누르고 있어도 바로 확인되지 않도록 기억
        private bool HPE_prevSelect;
        private int HPE_ignoreInputFrame = -1;

        // 숨긴 오브젝트의 CanvasGroup과 원래 투명도 (새로 붙인 CanvasGroup은 나갈 때 지움)
        private readonly List<CanvasGroup> _hiddenGroups = new List<CanvasGroup>();
        private readonly List<float> _hiddenAlphas = new List<float>();
        private readonly List<CanvasGroup> _addedGroups = new List<CanvasGroup>();

        // 씬에 그 위치의 HUD가 있는지 (없으면 조절할 수 없음, 예: 타이틀 화면)
        public static bool HasHud(HudDisplay.HudArea area)
        {
            foreach (HudDisplay hud in FindObjectsByType<HudDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hud.Area == area) return true;
            }
            return false;
        }

        public void StartEditing(HudDisplay.HudArea area)
        {
            if (gameObject.activeSelf || !HasHud(area)) return;

            if (HPE_uiControl == null) HPE_uiControl = FindAnyObjectByType<UIControl>(FindObjectsInactive.Include);

            _editingArea = area;
            _startOffset = DisplaySettings.GetHudOffset(area);
            _offset = _startOffset;

            gameObject.SetActive(true);
        }

        private void OnEnable()
        {
            HPE_openCount++;
            HPE_ignoreInputFrame = Time.frameCount;
            HPE_prevSelect = HPE_uiControl != null && HPE_uiControl.uiSelect;

            HideScreens();
            DisplaySettings.EditingHudArea = _editingArea;
            RefreshGuide();
        }

        private void OnDisable()
        {
            HPE_openCount--;
            HPE_closedFrame = Time.frameCount;

            ShowScreens();
            if (DisplaySettings.EditingHudArea == _editingArea) DisplaySettings.EditingHudArea = null;
        }

        private void Update()
        {
            if (HPE_uiControl == null) return;

            bool select = HPE_uiControl.uiSelect;
            if (Time.frameCount == HPE_ignoreInputFrame)
            {
                ClearCloseInput();
                HPE_prevSelect = select;
                return;
            }

            Mouse mouse = Mouse.current;
            float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;

            // 위로 올리면 상단 HUD는 위치 값이 줄고(화면 끝 쪽), 하단 HUD는 위치 값이 늚(화면 안쪽)
            int direction = 0;
            if (HPE_uiControl.uiUpStep || scroll > 0f) direction = 1;
            else if (HPE_uiControl.uiDownStep || scroll < 0f) direction = -1;

            if (direction != 0)
            {
                float change = _editingArea == HudDisplay.HudArea.Top ? -direction * _moveAmount : direction * _moveAmount;
                // 예전에 1 단위로 저장된 값(예: 37)도 10 단위 눈금(40, 30)으로 맞춰서 옮김
                _offset = Mathf.Clamp(Mathf.Round((_offset + change) / _moveAmount) * _moveAmount, 0f, 100f);
                DisplaySettings.PreviewHudOffset(_editingArea, _offset);
                RefreshGuide();
            }

            bool isConfirmed = (select && !HPE_prevSelect) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            bool isCanceled = HPE_uiControl.uiCancelDown || HPE_uiControl.uiClose || (mouse != null && mouse.rightButton.wasPressedThisFrame);

            if (isConfirmed)
            {
                DisplaySettings.SetHudOffset(_editingArea, _offset);
                Finish();
            }
            else if (isCanceled)
            {
                DisplaySettings.PreviewHudOffset(_editingArea, _startOffset);
                Finish();
            }

            HPE_prevSelect = select;
        }

        private void Finish()
        {
            ClearCloseInput();
            gameObject.SetActive(false);
        }

        // 같은 키로 뒤의 페이지, 옵션 창까지 닫히지 않도록 처리한 입력은 지움
        private void ClearCloseInput()
        {
            HPE_uiControl.uiCancelDown = false;
            HPE_uiControl.uiClose = false;
        }

        // ===== 화면 숨기기 =====

        // 옵션 창의 배경과 페이지, 옵션 창 뒤의 설정창(같은 부모 아래의 다른 오브젝트)을 숨김
        // (옵션 창이 캔버스 바로 아래에 있으면 뒤의 오브젝트는 게임 화면이므로 그대로 둠, 예: 타이틀)
        private void HideScreens()
        {
            if (_optionBackground != null) _optionBackground.enabled = false;
            foreach (GameObject hidden in _hiddenObjects) Hide(hidden);

            OptionWindow optionWindow = GetComponentInParent<OptionWindow>(true);
            Transform parent = optionWindow != null ? optionWindow.transform.parent : null;
            if (parent == null || parent.GetComponent<Canvas>() != null) return;

            foreach (Transform sibling in parent)
            {
                if (sibling != optionWindow.transform && sibling.gameObject.activeSelf) Hide(sibling.gameObject);
            }
        }

        private void Hide(GameObject target)
        {
            if (target == null) return;

            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = target.AddComponent<CanvasGroup>();
                _addedGroups.Add(group);
            }
            _hiddenGroups.Add(group);
            _hiddenAlphas.Add(group.alpha);
            group.alpha = 0f;
        }

        private void ShowScreens()
        {
            if (_optionBackground != null) _optionBackground.enabled = true;

            for (int i = 0; i < _hiddenGroups.Count; i++)
            {
                if (_hiddenGroups[i] != null) _hiddenGroups[i].alpha = _hiddenAlphas[i];
            }
            foreach (CanvasGroup added in _addedGroups)
            {
                if (added != null) Destroy(added);
            }
            _hiddenGroups.Clear();
            _hiddenAlphas.Clear();
            _addedGroups.Clear();
        }

        private void RefreshGuide()
        {
            if (HPE_guideText == null) return;

            string areaName = _editingArea == HudDisplay.HudArea.Top ? _topHudName : _bottomHudName;
            HPE_guideText.text = areaName + "  " + Mathf.RoundToInt(_offset) + "\n" + _guideMessage;
        }
    }
}
