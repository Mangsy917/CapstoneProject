using Game.Character;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    // 화면 왼쪽 가운데의 콤보 표시에 붙여서 사용
    // 태그로 찾은 캐릭터(맞는 쪽)가 피격 경직 중에 연속으로 맞으면 "(맞은 횟수)HIT"를 보여줌
    // - 2번째 히트부터 표시 (한 번만 맞은 것은 콤보가 아님)
    // - 경직이 풀려서 콤보가 끝나면 잠시 남겨 두었다가 숨김. 그 사이에 새 콤보가 시작되면 바로 숨김
    // - 카운터 / 퍼니시 카운터로 맞으면 콤보 표시 아래에 그 판정을 보여줌 (첫 타부터)
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

        [Header("카운터 표시 (콤보 표시 아래)")]
        [Tooltip("카운터 / 퍼니시 카운터를 보여줄 텍스트 (없으면 표시 안 함)")]
        [SerializeField] private TextMeshProUGUI CC_counterText;

        [Tooltip("카운터로 맞았을 때 표시할 글자")]
        [SerializeField] private string _counterHitText = "COUNTER";

        [Tooltip("퍼니시 카운터로 맞았을 때 표시할 글자")]
        [SerializeField] private string _punishCounterText = "PUNISH COUNTER";

        private CharacterHitReaction _targetReaction;
        private int _shownComboCount;
        private float _hideTimeLeft;
        private int _seenHitCount;           // 마지막으로 확인한 맞은 / 가드한 횟수 (새로 맞았는지 알기 위해)
        private float _counterHideTimeLeft;  // 콤보가 끝난 뒤 카운터 표시를 숨기기까지 남은 시간

        private void Awake()
        {
            if (_comboText == null) _comboText = GetComponent<TextMeshProUGUI>();
            Hide();
            HideCounter();
        }

        private void Update()
        {
            if (_targetReaction == null) FindTarget();
            if (_targetReaction == null)
            {
                Hide();
                HideCounter();
                return;
            }

            UpdateCounter();

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

        // 카운터 / 퍼니시 카운터로 맞으면 콤보가 끝날 때까지 보여주고, 끝난 뒤 잠시 남겨 두었다가 숨김
        // 노말 히트로 새 콤보가 시작되면 바로 숨김 (콤보 중 노말 히트는 카운터로 시작한 표시를 그대로 둠)
        private void UpdateCounter()
        {
            if (_targetReaction.HitCount != _seenHitCount)
            {
                _seenHitCount = _targetReaction.HitCount;
                if (_targetReaction.IsInHitStun)
                {
                    if (_targetReaction.LastHitType != HitType.Normal) ShowCounter(_targetReaction.LastHitType);
                    else if (_targetReaction.ComboCount <= 1) HideCounter();
                }
            }

            if (_targetReaction.IsInHitStun || _counterHideTimeLeft <= 0f) return;
            _counterHideTimeLeft -= Time.deltaTime;
            if (_counterHideTimeLeft <= 0f) HideCounter();
        }

        private void ShowCounter(HitType hitType)
        {
            _counterHideTimeLeft = _hideDelay;
            if (CC_counterText == null) return;
            CC_counterText.text = hitType == HitType.PunishCounter ? _punishCounterText : _counterHitText;
            CC_counterText.enabled = true;
        }

        private void HideCounter()
        {
            _counterHideTimeLeft = 0f;
            if (CC_counterText != null) CC_counterText.enabled = false;
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
