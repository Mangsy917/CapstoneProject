using Game.Character;
using UnityEngine;

namespace Game.UI
{
    // 화면 위쪽 체력바에 붙여서 사용
    // 태그로 찾은 캐릭터의 CharacterHealth를 읽어서 Fill 막대의 길이를 남은 체력 비율에 맞춤
    // - Enemy가 사라졌다가 다시 생성되면 새로 찾음
    // - 왼쪽 체력바(1P)는 오른쪽 끝부터, 오른쪽 체력바(2P)는 왼쪽 끝부터 줄어듦 (가운데 쪽부터 줄어듦)
    public class HealthBar : MonoBehaviour
    {
        [Tooltip("체력을 보여줄 캐릭터의 태그")]
        [SerializeField] private string HB_targetTag = "Player";

        [Tooltip("남은 체력만큼 길이가 바뀌는 막대 (부모 크기에 꽉 차게 둠)")]
        [SerializeField] private RectTransform _fill;

        [Tooltip("체크하면 화면 오른쪽 체력바 (막대가 오른쪽 끝에 붙어서 줄어듦)")]
        [SerializeField] private bool _isRightSide;

        private CharacterHealth _targetHealth;

        private void Update()
        {
            if (_targetHealth == null) FindTarget();

            float ratio = _targetHealth != null ? (float)_targetHealth.CurrentHealth / _targetHealth.MaxHealth : 0f;
            SetFillRatio(ratio);
        }

        private void FindTarget()
        {
            GameObject target = GameObject.FindGameObjectWithTag(HB_targetTag);
            _targetHealth = target != null ? target.GetComponent<CharacterHealth>() : null;
        }

        private void SetFillRatio(float ratio)
        {
            if (_fill == null) return;

            ratio = Mathf.Clamp01(ratio);
            _fill.anchorMin = new Vector2(_isRightSide ? 1f - ratio : 0f, 0f);
            _fill.anchorMax = new Vector2(_isRightSide ? 1f : ratio, 1f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;
        }
    }
}
