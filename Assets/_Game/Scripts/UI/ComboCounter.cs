using Game.Character;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    // 화면 왼쪽 가운데의 콤보 표시(숫자 텍스트)에 붙여서 사용
    // 태그로 찾은 캐릭터(맞는 쪽)가 피격 경직 중에 연속으로 맞으면 숫자 텍스트에 맞은 횟수를 보여주고, 옆의 HIT 텍스트도 같이 보여줌
    // (숫자와 HIT를 다른 텍스트로 나눠서 색을 따로 정할 수 있음)
    // - 2번째 히트부터 표시 (한 번만 맞은 것은 콤보가 아님)
    // - 경직이 풀려서 콤보가 끝나면 잠시 남겨 두었다가 숨김. 그 사이에 새 콤보가 시작되면 바로 숨김
    // - 카운터 / 퍼니시 카운터로 맞으면 콤보 표시 아래에 그 판정을 보여줌 (첫 타부터)
    // - 때린 쪽(Player)이 맞는 쪽보다 오른쪽에 있으면 콤보 / HIT / 카운터 표시를 화면 오른쪽으로 좌우 반전해서 보여줌
    //   사이드는 아무것도 보이지 않을 때만 정함 (콤보 도중에 상대를 넘어가도 표시가 옮겨가지 않음)
    // - Enemy가 사라졌다가 다시 생성되면 새로 찾음
    public class ComboCounter : MonoBehaviour
    {
        [Tooltip("콤보를 셀 캐릭터(맞는 쪽)의 태그")]
        [SerializeField] private string CC_targetTag = "Enemy";

        [Tooltip("콤보 수(숫자)를 보여줄 텍스트. 비워두면 같은 오브젝트에서 찾음")]
        [SerializeField] private TextMeshProUGUI _comboText;

        [Tooltip("숫자 텍스트에 표시할 글자. {0}에 맞은 횟수가 들어감")]
        [SerializeField] private string _textFormat = "{0}";

        [Tooltip("숫자 옆에서 같이 보였다 숨는 텍스트 (HIT 글자). 없으면 비워둠")]
        [SerializeField] private TextMeshProUGUI _hitText;

        [Tooltip("콤보가 끝난 뒤 표시를 남겨 둘 시간 (초)")]
        [SerializeField] private float _hideDelay = 1f;

        [Header("카운터 표시 (콤보 표시 아래)")]
        [Tooltip("카운터 / 퍼니시 카운터를 보여줄 텍스트 (없으면 표시 안 함)")]
        [SerializeField] private TextMeshProUGUI CC_counterText;

        [Tooltip("카운터로 맞았을 때 표시할 글자")]
        [SerializeField] private string _counterHitText = "COUNTER";

        [Tooltip("퍼니시 카운터로 맞았을 때 표시할 글자")]
        [SerializeField] private string _punishCounterText = "PUNISH COUNTER";

        [Header("사이드 (때린 쪽이 있는 쪽에 표시)")]
        [Tooltip("때린 쪽 캐릭터의 태그. 이 캐릭터가 맞는 쪽보다 오른쪽에 있으면 표시를 화면 오른쪽으로 좌우 반전함")]
        [SerializeField] private string _attackerTag = "Player";

        [Tooltip("콤보 숫자와 HIT 텍스트를 묶은 부모 (같이 좌우로 옮김). 비워두면 숫자 텍스트의 부모")]
        [SerializeField] private RectTransform _comboGroup;

        private CharacterHitReaction _targetReaction;
        private Transform _attacker;
        private bool _isShownOnRight;   // 지금 표시가 화면 오른쪽(좌우 반전)인지

        // 처음(왼쪽) 배치를 기억해 두었다가 오른쪽으로 반전하거나 되돌릴 때 사용
        private struct RectLayout
        {
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector2 anchoredPosition;
        }

        private RectLayout _comboGroupLayout;
        private RectLayout _comboTextLayout;
        private RectLayout _hitTextLayout;
        private RectLayout _counterTextLayout;
        private Vector4 _comboTextMargin;
        private Vector4 _hitTextMargin;
        private Vector4 _counterTextMargin;
        private HorizontalAlignmentOptions _comboTextAlignment;
        private HorizontalAlignmentOptions _hitTextAlignment;
        private HorizontalAlignmentOptions _counterTextAlignment;
        private int _shownComboCount;
        private float _hideTimeLeft;
        private int _seenHitCount;           // 마지막으로 확인한 맞은 / 가드한 횟수 (새로 맞았는지 알기 위해)
        private float _counterHideTimeLeft;  // 콤보가 끝난 뒤 카운터 표시를 숨기기까지 남은 시간

        private void Awake()
        {
            if (_comboText == null) _comboText = GetComponent<TextMeshProUGUI>();
            if (_comboGroup == null && _comboText != null) _comboGroup = _comboText.transform.parent as RectTransform;
            SaveLayout();
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

            // 아무것도 보이지 않을 때만 사이드를 정함 (콤보 도중 상대를 넘어가도 표시가 반대로 옮겨가지 않도록)
            if (!IsShowingAnything()) UpdateSide();

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

        private bool IsShowingAnything()
        {
            return _hideTimeLeft > 0f || _counterHideTimeLeft > 0f || (_targetReaction != null && _targetReaction.IsInHitStun);
        }

        // 때린 쪽이 맞는 쪽보다 오른쪽에 있으면 표시를 오른쪽으로, 아니면 처음 배치(왼쪽)로
        private void UpdateSide()
        {
            if (_attacker == null)
            {
                GameObject attacker = GameObject.FindGameObjectWithTag(_attackerTag);
                _attacker = attacker != null ? attacker.transform : null;
            }
            if (_attacker == null) return;

            bool isRight = _attacker.position.x > _targetReaction.transform.position.x;
            if (isRight != _isShownOnRight) ApplySide(isRight);
        }

        private void SaveLayout()
        {
            _comboGroupLayout = GetLayout(_comboGroup);
            if (_comboText != null)
            {
                _comboTextLayout = GetLayout(_comboText.rectTransform);
                _comboTextMargin = _comboText.margin;
                _comboTextAlignment = _comboText.horizontalAlignment;
            }
            if (_hitText != null)
            {
                _hitTextLayout = GetLayout(_hitText.rectTransform);
                _hitTextMargin = _hitText.margin;
                _hitTextAlignment = _hitText.horizontalAlignment;
            }
            if (CC_counterText != null)
            {
                _counterTextLayout = GetLayout(CC_counterText.rectTransform);
                _counterTextMargin = CC_counterText.margin;
                _counterTextAlignment = CC_counterText.horizontalAlignment;
            }
        }

        // 오른쪽이면 처음 배치를 화면 가운데 기준으로 좌우 반전 (위치, 기준점, 글자 정렬, 여백)
        // 콤보는 오른쪽에서도 "2 HIT" 순서로 읽히도록, HIT를 화면 끝에 붙이고 숫자를 그 왼쪽에 둠
        private void ApplySide(bool isRight)
        {
            _isShownOnRight = isRight;
            SetLayout(_comboGroup, _comboGroupLayout, isRight);

            if (_comboText != null)
            {
                SetLayout(_comboText.rectTransform, _comboTextLayout, isRight);
                _comboText.horizontalAlignment = isRight ? MirrorAlignment(_comboTextAlignment) : _comboTextAlignment;
                _comboText.margin = isRight ? GetRightComboTextMargin() : _comboTextMargin;
            }
            if (_hitText != null)
            {
                SetLayout(_hitText.rectTransform, _hitTextLayout, isRight);
                _hitText.horizontalAlignment = isRight ? MirrorAlignment(_hitTextAlignment) : _hitTextAlignment;
                _hitText.margin = isRight ? new Vector4(0f, _hitTextMargin.y, 0f, _hitTextMargin.w) : _hitTextMargin;
            }
            if (CC_counterText != null)
            {
                SetLayout(CC_counterText.rectTransform, _counterTextLayout, isRight);
                CC_counterText.horizontalAlignment = isRight ? MirrorAlignment(_counterTextAlignment) : _counterTextAlignment;
                CC_counterText.margin = isRight ? MirrorMargin(_counterTextMargin) : _counterTextMargin;
            }
        }

        // 오른쪽일 때 숫자 텍스트의 오른쪽 여백 = HIT 글자 너비 + 숫자와 HIT 사이 간격 (왼쪽 배치의 간격과 같게)
        private Vector4 GetRightComboTextMargin()
        {
            if (_hitText == null) return MirrorMargin(_comboTextMargin);

            float hitWidth = _hitText.GetPreferredValues(_hitText.text).x - _hitTextMargin.x - _hitTextMargin.z;
            float numberWidth = _comboText.GetPreferredValues("0").x;
            float gap = Mathf.Max(0f, _hitTextMargin.x - _comboTextMargin.x - numberWidth);
            return new Vector4(_comboTextMargin.z, _comboTextMargin.y, hitWidth + gap, _comboTextMargin.w);
        }

        private static RectLayout GetLayout(RectTransform rect)
        {
            if (rect == null) return default;
            return new RectLayout { anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot, anchoredPosition = rect.anchoredPosition };
        }

        private static void SetLayout(RectTransform rect, RectLayout layout, bool isMirrored)
        {
            if (rect == null) return;
            if (!isMirrored)
            {
                rect.anchorMin = layout.anchorMin;
                rect.anchorMax = layout.anchorMax;
                rect.pivot = layout.pivot;
                rect.anchoredPosition = layout.anchoredPosition;
                return;
            }

            rect.anchorMin = new Vector2(1f - layout.anchorMax.x, layout.anchorMin.y);
            rect.anchorMax = new Vector2(1f - layout.anchorMin.x, layout.anchorMax.y);
            rect.pivot = new Vector2(1f - layout.pivot.x, layout.pivot.y);
            rect.anchoredPosition = new Vector2(-layout.anchoredPosition.x, layout.anchoredPosition.y);
        }

        private static HorizontalAlignmentOptions MirrorAlignment(HorizontalAlignmentOptions alignment)
        {
            if (alignment == HorizontalAlignmentOptions.Left) return HorizontalAlignmentOptions.Right;
            if (alignment == HorizontalAlignmentOptions.Right) return HorizontalAlignmentOptions.Left;
            return alignment;
        }

        // 여백(왼쪽, 위, 오른쪽, 아래)의 왼쪽과 오른쪽을 바꿈
        private static Vector4 MirrorMargin(Vector4 margin)
        {
            return new Vector4(margin.z, margin.y, margin.x, margin.w);
        }

        private void Show(int comboCount)
        {
            _hideTimeLeft = _hideDelay;
            if (_hitText != null) _hitText.enabled = true;
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
            if (_hitText != null) _hitText.enabled = false;
        }
    }
}
