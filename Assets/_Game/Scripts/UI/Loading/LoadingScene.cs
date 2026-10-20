using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Loading 씬에 붙여서 사용
// 씬을 이동할 때는 SceneManager.LoadScene 대신 LoadingScene.LoadScene("씬 이름")을 호출
// 1. Loading 씬으로 먼저 이동
// 2. 목표 씬을 비동기로 불러오는 동안 왼쪽 아래 로딩 동그라미가 돌아가고, 오른쪽에 툴팁 표시
// 3. 로딩이 끝나면 waitAfterLoad초 기다린 뒤 목표 씬으로 넘어감
public class LoadingScene : MonoBehaviour
{
    // 로딩 화면 씬 이름 (Build Settings에 들어 있어야 함)
    private const string loadingSceneName = "Loading";

    // 다음에 불러올 씬 이름
    private static string nextSceneName;

    [SerializeField] private RectTransform loadingCircle;
    [SerializeField] private TMP_Text tooltipText;

    // 로딩 동그라미 회전 속도 (초당 각도, 시계 방향)
    [SerializeField] private float rotateSpeed = 360f;

    // 로딩이 끝난 뒤 기다리는 시간 (초)
    [SerializeField] private float waitAfterLoad = 2f;

    // 로딩 중에 보여줄 툴팁 (이 중 하나가 무작위로 표시됨)
    [TextArea] public string[] tooltips;

    // 다른 스크립트에서 씬을 이동할 때 호출
    public static void LoadScene(string sceneName)
    {
        nextSceneName = sceneName;
        SceneManager.LoadScene(loadingSceneName);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    IEnumerator Start()
    {
        ShowRandomTooltip();

        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("[LoadingScene] 불러올 씬이 없습니다. LoadingScene.LoadScene(\"씬 이름\")으로 이동해주세요.");
            yield break;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(nextSceneName);
        // 로딩이 끝나도 바로 넘어가지 않도록 막아둠
        op.allowSceneActivation = false;

        // allowSceneActivation이 false면 progress는 0.9에서 멈춤 (= 로딩 완료)
        while (op.progress < 0.9f)
        {
            yield return null;
        }

        // 일시정지(timeScale 0) 상태에서 넘어와도 기다릴 수 있도록 실제 시간으로 기다림
        yield return new WaitForSecondsRealtime(waitAfterLoad);

        nextSceneName = null;
        op.allowSceneActivation = true;
    }

    // Update is called once per frame
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
