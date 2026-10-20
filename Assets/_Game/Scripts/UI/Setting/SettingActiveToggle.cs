using UnityEngine;

// OFF / ON 스테퍼가 있는 설정 줄(BTN_...)에 붙여서 사용
// 스테퍼가 ON이면 Targets 오브젝트를 켜고, OFF면 끔
// - Targets에 켜고 끌 씬 오브젝트를 넣으면 됨 (여러 개 가능)
public class SettingActiveToggle : MonoBehaviour
{
    [SerializeField] private SettingStepper stepper;

    // 켜고 끌 오브젝트들
    [SerializeField] private GameObject[] targets;

    // 스테퍼 Options에서 ON이 몇 번째인지 (OFF, ON 순서면 1)
    [SerializeField] private int onIndex = 1;

    void Awake()
    {
        if (stepper == null) stepper = GetComponent<SettingStepper>();
        if (stepper == null) return;

        stepper.onValueChanged.AddListener(Apply);
    }

    void Start()
    {
        // 처음 값에 맞춰서 한 번 적용
        if (stepper != null) Apply(stepper.Index);
    }

    private void Apply(int index)
    {
        bool isOn = index == onIndex;
        foreach (GameObject target in targets)
        {
            if (target != null) target.SetActive(isOn);
        }
    }
}
