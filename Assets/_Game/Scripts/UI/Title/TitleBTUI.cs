using UnityEngine;
using UnityEngine.SceneManagement;
using JetBrains.Annotations;

public class TitleBTUI : MonoBehaviour
{
    [SerializeField] private GameObject settingUI;
    [SerializeField] private string sceneName;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        settingUI.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PressStartBT()
    {
        LoadingScene.LoadScene(sceneName);
    }

    public void PressSettingBT()
    {
        settingUI.SetActive(true);
    }

    public void PressSettingBackBT()
    {
        settingUI.SetActive(false);
    }

    public void PressExitBT()
    {
        Application.Quit();
        // 에디터 전용 코드는 빌드에 들어가면 빌드가 실패하므로 에디터에서만 컴파일
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}