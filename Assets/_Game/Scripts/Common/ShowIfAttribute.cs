using UnityEngine;

namespace Game.Common
{
    // 인스펙터에서 같은 클래스 안의 다른 칸(bool 또는 enum) 값이 맞을 때만 이 칸을 보여줌 (그리기는 Editor의 ShowIfDrawer)
    // 예) [ShowIf(nameof(_isLaunchAttack))] → _isLaunchAttack을 체크했을 때만 보임
    //     [ShowIf(nameof(_frameType), FrameType.Active)] → _frameType이 Active일 때만 보임
    // 배열 칸에 붙이면 배열 전체가 아니라 원소마다 적용되므로 배열 칸에는 쓰지 않음
    public class ShowIfAttribute : PropertyAttribute
    {
        /// <summary>값을 확인할 칸의 이름 (같은 클래스 안)</summary>
        public string ConditionName { get; }

        /// <summary>이 값일 때 보여줌. null이면 bool 칸이 체크되어 있을 때 보여줌</summary>
        public object ExpectedValue { get; }

        public ShowIfAttribute(string conditionName, object expectedValue = null)
        {
            ConditionName = conditionName;
            ExpectedValue = expectedValue;
        }
    }
}
