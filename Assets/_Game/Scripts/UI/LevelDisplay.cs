using Game.Common;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    // 플레이어의 레벨(과 경험치)을 글자로 보여줌. 거점과 같은 값(PlayerProgress)을 씀
    // - 체력바 위: 레벨만
    // - 연습 설정 메뉴: 레벨, 경험치
    public class LevelDisplay : MonoBehaviour
    {
        [Tooltip("거점을 거치지 않고 시작했을 때 쓸 처음 값")]
        [SerializeField] private PlayerStartData _startData;
        [SerializeField] private TMP_Text LD_levelText;
        [Tooltip("비워두면 경험치를 보여주지 않음")]
        [SerializeField] private TMP_Text _experienceText;
        [SerializeField] private string _levelFormat = "Lv. {0}";
        [SerializeField] private string _experienceFormat = "{0:N0} / {1:N0}";

        private void OnEnable()
        {
            PlayerProgress.Prepare(_startData);
            PlayerProgress.ProgressChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerProgress.ProgressChanged -= Refresh;
        }

        private void Refresh()
        {
            if (LD_levelText != null) LD_levelText.text = string.Format(_levelFormat, PlayerProgress.GetLevelText());
            if (_experienceText != null)
                _experienceText.text = string.Format(_experienceFormat, PlayerProgress.PP_Experience, PlayerProgress.GetMaxExperience(PlayerProgress.PP_Level));
        }
    }
}
