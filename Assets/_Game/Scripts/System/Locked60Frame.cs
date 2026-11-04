using UnityEngine;

// 게임 전체의 프레임 상한을 60으로 정함 (씬에 붙이지 않아도 게임이 시작될 때 적용됨)
// - 60으로 고정하는 것이 아니라 상한이므로 사양이 낮으면 60보다 낮게 나올 수 있음
// - 이동, 커맨드, 공격 등이 프레임 수로 동작해서 60을 넘기면 게임이 빨라지므로 상한을 없애면 안 됨
public class Locked60Frame : MonoBehaviour
{
    private const int MaxFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyMaxFrameRate()
    {
        Application.targetFrameRate = MaxFrameRate;
    }
}
