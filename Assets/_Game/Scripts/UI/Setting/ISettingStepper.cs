// 설정 메뉴 줄의 스테퍼 공통 기능 (SettingMenu가 좌우 입력을 전달할 때 사용)
// 오른쪽 = 증가(Next), 왼쪽 = 감소(Prev)
public interface ISettingStepper
{
    void Next();
    void Prev();
}