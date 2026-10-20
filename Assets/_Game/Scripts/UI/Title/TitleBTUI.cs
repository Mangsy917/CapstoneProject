using UnityEngine;
using UnityEditor.SceneManagement;
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
        UnityEditor.EditorApplication.isPlaying = false;
    }
}