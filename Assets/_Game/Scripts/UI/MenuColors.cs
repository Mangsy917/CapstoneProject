using UnityEngine;

namespace Game.UI
{
    // 메뉴·설정·팝업에서 같이 쓰는 색 (거점, 연습장, 옵션 창의 색을 통일)
    // 여기 값만 바꾸면 선택 색이 모든 메뉴에 적용됨. 창 배경과 글자색은 씬·프리팹에 있음
    public static class MenuColors
    {
        // 선택된 버튼 배경
        public static readonly Color Highlight = new Color(0.12f, 0.55f, 0.52f, 1f);

        // 선택되지 않은 버튼 배경 (투명)
        public static readonly Color Unselected = new Color(1f, 1f, 1f, 0f);

        // 글자
        public static readonly Color EnabledText = Color.white;
        public static readonly Color DisabledText = new Color(0.5f, 0.5f, 0.5f, 1f);
    }
}
