using Game.Character;
using UnityEngine;

namespace Game.UI
{
    // 화면 위쪽 체력바에 붙여서 사용
    // 태그로 찾은 캐릭터의 CharacterHealth를 읽어서 Fill 막대의 길이를 남은 체력 비율에 맞춤
    // - Enemy가 사라졌다가 다시 생성되면 새로 찾음
    // - 바깥쪽부터 안쪽으로 줄어듦: 왼쪽 체력바(1P)는 왼쪽에서 오른쪽으로, 오른쪽 체력바(2P)는 오른쪽에서 왼쪽으로
    //   (남은 체력 막대는 화면 가운데 쪽 끝에 붙어 있음)
    // - 콤보 중에는 줄지 않고, 콤보가 끝나면(CharacterHealth가 쌓인 데미지를 한 번에 뺌) Fill이 한 번에 줄어듦
    //   깎인 만큼은 반투명한 Damage Fill로 남아 있다가 서서히 줄어듦
    public class HealthBar : MonoBehaviour
    {
        [Tooltip("체력을 보여줄 캐릭터의 태그")]
        [SerializeField] private string HB_targetTag = "Player";

        [Tooltip("남은 체력만큼 길이가 바뀌는 막대 (부모 크기에 꽉 차게 둠)")]
        [SerializeField] private RectTransform _fill;

        [Tooltip("깎인 체력을 잠시 보여주는 반투명 막대 (Fill 뒤에 둠, 부모 크기에 꽉 차게 둠)")]
        [SerializeField] private RectTransform _damageFill;

        [Tooltip("데미지가 확정된 뒤 반투명 막대가 줄어드는 속도 (1초에 전체 체력의 몇 배만큼)")]
        [SerializeField] private float _damageShrinkSpeed = 0.8f;

        [Tooltip("체크하면 화면 오른쪽 체력바 (막대가 왼쪽 끝에 붙어서 오른쪽부터 줄어듦)")]
        [SerializeField] private bool _isRightSide;

        private CharacterHealth _targetHealth;
        private float _damageRatio = 1f; // 반투명 막대의 길이 (전체 체력에 대한 비율)

        private void Update()
        {
            if (_targetHealth == null) FindTarget();

            float ratio = _targetHealth != null ? (float)_targetHealth.CurrentHealth / _targetHealth.MaxHealth : 0f;
            SetBarRatio(_fill, ratio);

            // 회복하면 반투명 막대도 같이 차고, 데미지가 확정되면 남은 체력까지 서서히 줄어듦
            // (Time.deltaTime을 써서 설정창이 열려 멈춘 동안에는 줄지 않음)
            if (ratio >= _damageRatio) _damageRatio = ratio;
            else if (_targetHealth == null || _targetHealth.IsDamageSettled)
                _damageRatio = Mathf.Max(ratio, _damageRatio - _damageShrinkSpeed * Time.deltaTime);

            SetBarRatio(_damageFill, _damageRatio);
        }

        private void FindTarget()
        {
            GameObject target = GameObject.FindGameObjectWithTag(HB_targetTag);
            _targetHealth = target != null ? target.GetComponent<CharacterHealth>() : null;
            if (_targetHealth != null) _damageRatio = (float)_targetHealth.CurrentHealth / _targetHealth.MaxHealth;
        }

        private void SetBarRatio(RectTransform bar, float ratio)
        {
            if (bar == null) return;

            ratio = Mathf.Clamp01(ratio);
            bar.anchorMin = new Vector2(_isRightSide ? 0f : 1f - ratio, 0f);
            bar.anchorMax = new Vector2(_isRightSide ? ratio : 1f, 1f);
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = Vector2.zero;
        }
    }
}
