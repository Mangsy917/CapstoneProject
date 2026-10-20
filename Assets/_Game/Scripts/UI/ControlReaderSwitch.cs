using UnityEngine;

public class ControlReaderSwitch : MonoBehaviour
{
    [SerializeField] private GameObject gameUI;
    [SerializeField] private GameObject settingUI;
    [SerializeField] private InputReader charC;
    [SerializeField] private UIControl UIC;

    [Header("세팅 UI가 열려 있는 동안 멈출 스크립트")]
    [Tooltip("프레임 수로 동작하는 스크립트(CharacterMove, InputHistoryDisplay 등)를 넣어주세요.")]
    [SerializeField] private Behaviour[] pauseTargets;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameUI.SetActive(true);
        settingUI.SetActive(false);
        charC.gameObject.SetActive(true);
        UIC.gameObject.SetActive(false);
        SetGamePaused(false);
    }

    // Update is called once per frame
    void Update()
    {
        // ESC/Start 또는 취소 키(K/East)로 세팅 UI를 닫음 (팝업이 열려 있으면 팝업만 닫힘)
        bool cancel = UIC.uiCancelDown && !SettingPopup.IsAnyOpen;
        if (UIC.gameObject.activeSelf && (UIC.uiClose || cancel))
        {
            UIC.uiClose = false;
            UIC.uiCancelDown = false;
            settingUI.SetActive(false);
            charC.gameObject.SetActive(true);
            UIC.gameObject.SetActive(false);
            SetGamePaused(false);
        }
        else if (charC.gameObject.activeSelf && charC.uiOpen)
        {
            charC.uiOpen = false;
            settingUI.SetActive(true);
            charC.gameObject.SetActive(false);
            UIC.gameObject.SetActive(true);
            SetGamePaused(true);
        }
    }

    private void OnDestroy()
    {
        // 씬을 나갈 때 멈춘 상태가 다음 씬까지 이어지지 않도록 되돌림
        Time.timeScale = 1f;
    }

    private void SetGamePaused(bool paused)
    {
        // Time.deltaTime을 쓰는 이동, 물리, 애니메이션, WaitForSeconds를 멈춤
        Time.timeScale = paused ? 0f : 1f;

        // 프레임 수로 동작하는 스크립트는 timeScale로 멈추지 않으므로 직접 끔
        foreach (var target in pauseTargets)
        {
            if (target != null)
                target.enabled = !paused;
        }
    }
}
