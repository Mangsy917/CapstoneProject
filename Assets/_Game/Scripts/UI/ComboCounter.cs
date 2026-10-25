using Game.Character;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    // 화면 왼쪽 가운데의 콤보 표시에 붙여서 사용
    // 태그로 찾은 캐릭터(맞는 쪽)가 피격 경직 중에 연속으로 맞으면 "(맞은 횟수)HIT"를 보여줌
    // - 2번째 히트부터 표시 (한 번만 맞은 것은 콤보가 아님)
    // - 경직이 풀려서 콤보가 끝나면 잠시 남겨 두었다가 숨김. 그 사이에 새 콤보가 시작되면 바로 숨김
    // - Enemy가 사라졌다가 다시 생성되면 새로 찾음
    public class ComboCounter : MonoBehaviour
    {
        [Tooltip("콤보를 셀 캐릭터(맞는 쪽)의 태그")]
        [SerializeField] private string CC_targetTag = "Enemy";

        [Tooltip("콤보 수를 보여줄 텍스트. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private TextMeshProUGUI _comboText;

        [Tooltip("표시할 글자. {0}에 맞은 횟수가 들어감")]
        [SerializeField] private string _textFormat = "{0}HIT";

        [Tooltip("콤보가 끝난 뒤 표시를 남겨 둘 시간 (초)")]
        [SerializeField] private float _hideDelay = 1f;

        private CharacterHitReaction _targetReaction;
        private int _shownComboCount;
        private float _hideTimeLeft;

        private void Awake()
        {
            if (_comboText == null) _comboText = GetComponent<TextMeshProUGUI>();
            Hide();
        }

        private void Update()
        {
            if (_targetReaction == null) FindTarget();
            if (_targetReaction == null)
            {
                Hide();
                return;
            }

            if (_targetReaction.IsInHitStun)
            {
                if (_targetReaction.ComboCount >= 2) Show(_targetReaction.ComboCount);
                else Hide(); // 첫 타: 새 콤보가 시작됨
                return;
            }

            // 콤보가 끝남: 잠시 뒤 숨김 (Time.deltaTime을 써서 설정창이 열려 멈춘 동안에는 줄지 않음)
            if (_hideTimeLeft <= 0f) return;
            _hideTimeLeft -= Time.deltaTime;
            if (_hideTimeLeft <= 0f) Hide();
        }

        private void FindTarget()
        {
            GameObject target = GameObject.FindGameObjectWithTag(CC_targetTag);
            _targetReaction = target != null ? target.GetComponent<CharacterHitReaction>() : null;
        }

        private void Show(int comboCount)
        {
            _hideTimeLeft = _hideDelay;
            if (_comboText == null) return;
            if (comboCount != _shownComboCount)
            {
                _shownComboCount = comboCount;
                _comboText.text = string.Format(_textFormat, comboCount);
            }
            _comboText.enabled = true;
        }

        private void Hide()
        {
            _hideTimeLeft = 0f;
            _shownComboCount = 0;
            if (_comboText != null) _comboText.enabled = false;
        }
    }
}
