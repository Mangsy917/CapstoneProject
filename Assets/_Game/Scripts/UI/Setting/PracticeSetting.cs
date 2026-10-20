using UnityEngine;

// SettingWindow 버튼들의 OnClick에 연결할 함수 모음
public class PracticeSetting : MonoBehaviour
{
    // 타이틀로 버튼을 눌렀을 때 이동할 씬 이름
    public string titleSceneName = "Title";

    // "타이틀로 돌아가시겠습니까?" 팝업
    [SerializeField] private GameObject goTitlePopup;

    void Start()
    {
        goTitlePopup.SetActive(false);
    }

    // 연습설정 > 타이틀로 : 확인 팝업을 띄움
    public void PressGoTitleBT()
    {
        goTitlePopup.SetActive(true);
    }

    // 타이틀로 팝업 > 예 : 타이틀 씬으로 이동
    public void PressGoTitleYesBT()
    {
        LoadingScene.LoadScene(titleSceneName);
    }

    // 타이틀로 팝업 > 아니오 : 팝업을 닫음
    public void PressGoTitleNoBT()
    {
        goTitlePopup.SetActive(false);
    }
}
