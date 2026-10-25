using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LoadingScene : MonoBehaviour
{
    // 로딩 화면 씬 이름
    private const string loadingSceneName = "Loading";

    // 다음에 불러올 씬 이름
    private static string nextSceneName;

    [SerializeField] private RectTransform loadingCircle;
    [SerializeField] private TMP_Text tooltipText;

    [SerializeField] private float rotateSpeed = 360f;

    [SerializeField] private float waitAfterLoad = 2f;

    [TextArea] public string[] tooltips;

    public static void LoadScene(string sceneName)
    {
        nextSceneName = sceneName;
        SceneManager.LoadScene(loadingSceneName);
    }

    IEnumerator Start()
    {
        ShowRandomTooltip();

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("LoadingScene이 연결 안되었습니다. LoadingScene.LoadScene(\"씬 이름\")으로 이동해주세요.");
            yield break;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(nextSceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            yield return null;
        }

        yield return new WaitForSecondsRealtime(waitAfterLoad);

        nextSceneName = null;
        op.allowSceneActivation = true;
    }

    void Update()
    {
        if (loadingCircle != null)
        {
            loadingCircle.Rotate(0f, 0f, -rotateSpeed * Time.unscaledDeltaTime);
        }
    }

    private void ShowRandomTooltip()
    {
        if (tooltipText == null) return;

        if (tooltips == null || tooltips.Length == 0)
        {
            tooltipText.text = "";
            return;
        }

        tooltipText.text = tooltips[Random.Range(0, tooltips.Length)];
    }
}