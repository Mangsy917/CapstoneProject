# 프로젝트 규칙

이 문서는 이 Unity 프로젝트에서 작업할 때 따르는 규칙이다.
기존 파일에 이미 다른 규칙이 일관되게 쓰이고 있으면 그 파일의 규칙을 우선한다.
기존 이름이나 파일 위치를 이 규칙에 맞추려고 임의로 바꾸지 않는다(요청받은 경우만).

## 수정 가능한 범위
- 파일을 만들기, 수정하기, 옮기기, 이름 바꾸기, 지우기는 `Assets/_Game/` 폴더 안에서만 한다.
- `Assets/_Game/` 밖의 파일은 사용자가 프롬프트에서 그 파일이나 폴더를 수정하라고 명시한 경우에만 수정한다.
  이 범위에는 `Assets/`의 다른 폴더, `Packages/`, `ProjectSettings/`, `.gitignore`, 이 `CLAUDE.md`도 포함된다.
- `_Game` 밖의 파일을 고쳐야 작업이 끝나는 경우에는 수정하지 말고, 어느 파일을 왜 고쳐야 하는지 알리고 물어본다.
- 파일을 읽고 검색하는 것은 범위 제한 없이 해도 된다.
- Git 명령(커밋, 분기 전환 등)으로 작업 폴더의 파일이 바뀌는 것은 이 제한에 해당하지 않는다.

---

# 1. Git 작업 규칙

작업은 항상 `checkpoint` 분기에서만 한다.
`main`은 사용자가 확인한 결과물만 들어가는 분기이며, `main`에서 직접 파일을 수정하거나 커밋하지 않는다.
`main`에 병합하거나 푸시하는 최종 마무리는 사용자가 직접 한다. Claude는 이를 대신 실행하지 않는다.

## 작업 시작 전 (파일을 수정하는 요청마다 매번)
파일을 하나라도 수정하기 전에 아래 순서를 반드시 실행한다.

1. 커밋되지 않은 변경이 있는지 확인한다(`git status --porcelain`).
   있으면 작업하지 말고, 변경 목록을 보여준 뒤 어떻게 할지 물어본다.
2. 원격 정보를 가져온다(`git fetch origin`).
3. `checkpoint` 분기가 있는지 확인한다
   (로컬 `git branch --list checkpoint`, 원격 `git ls-remote --heads origin checkpoint`).
   - 둘 다 없으면: `git switch -c checkpoint main` 후 `git push -u origin checkpoint`.
   - 원격에만 있으면: `git switch checkpoint`로 받아온다.
   - 있으면: `git switch checkpoint`.
4. `main`의 최신 내용을 `checkpoint`에 반영한다(`git merge --ff-only origin/main`).
   빨리 감기가 안 되면(이미 `checkpoint`에 작업 중인 커밋이 있어서) 그대로 두고 진행한다.
   충돌이 나면 작업을 멈추고 알린다.
5. 현재 분기가 `checkpoint`인 것을 확인한 뒤 작업을 시작한다.

## 작업 중
- `checkpoint`에서 벗어나지 않는다.
- 작업 단위가 끝날 때마다 `checkpoint`에 커밋하고 `git push origin checkpoint`로 올린다.
- 커밋 메시지는 한국어로, 첫 줄에 무엇을 바꿨는지 50자 이내로 쓴다.

## 새 분기 (사용자가 요청했을 때만)
- 사용자가 분기를 만들어 달라고 하면 만든다. 요청 없이는 새 분기를 만들지 않는다.
- 분기 이름과 어느 분기에서 갈라질지는 사용자가 말한 대로 한다.
  갈라질 분기를 말하지 않으면 `checkpoint`에서 만든다.
- 만든 뒤에는 사용자가 그 분기에서 작업하라고 한 경우에만 그 분기에서 작업한다.
  그렇지 않으면 다시 `checkpoint`로 돌아와 작업한다.
- 어떤 분기에서 작업하든 `main`에서는 작업하지 않는다.

## 결과물이 마음에 들 때 (main 반영은 사용자가 직접 한다)
Claude는 `main`으로 분기를 바꾸거나, `main`에 병합·푸시하지 않는다. 대신 아래까지만 한다.
1. `checkpoint`의 변경이 모두 커밋되고 원격에 푸시되었는지 확인한다.
2. `main`에 들어갈 커밋 목록을 보여준다(`git log --oneline origin/main..checkpoint`).
3. 사용자가 직접 실행할 명령을 안내한다. 실행하지는 않는다.
   ```
   git switch main
   git pull --ff-only origin main
   git merge --no-ff checkpoint -m "checkpoint 작업 반영: <요약>"
   git push origin main
   git switch checkpoint
   git merge --ff-only main
   ```
4. 사용자가 반영을 마쳤다고 하면, 다음 작업 시작 전 절차(위 4번)로 `checkpoint`를 `main`에 맞춘다.

## 결과물이 마음에 안 들 때 (사용자가 버리라고 요청했을 때만)
- 버려질 커밋 목록을 보여주고 확인받은 뒤에만 `checkpoint`를 `main` 상태로 되돌린다.
  ```
  git switch checkpoint
  git reset --hard origin/main
  git push --force-with-lease origin checkpoint
  ```
- 이 경우 외에는 `reset --hard`와 강제 푸시를 쓰지 않는다.

## 절대 하지 않는 것
- `main`으로 분기 전환, `main`에 커밋·병합·푸시 (사용자가 직접 한다)
- `main` 분기 삭제
- 요청 없이 새 분기 만들기
- `checkpoint` 분기 삭제
- `git clean -fd`, `git rebase`, 요청 없는 커밋 기록 수정(`--amend` 포함)

## Unity 관련
- `.meta` 파일은 해당 에셋과 함께 항상 커밋한다.
- `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`는 커밋하지 않는다.
  `.gitignore`에 없으면 커밋 전에 알린다.

---

# 2. 코드 네이밍 규칙 (C#)

## 대소문자
- 지역 변수, 매개변수: camelCase, 명사로 짓는다.
  - 좋음: `count`, `itemCount`, `playerItemCount`
- 메서드: PascalCase, 동사로 시작한다.
  - 좋음: `SetPlayerInfo()`, `MoveItem()`
- 클래스, 프로퍼티, public 멤버: PascalCase.

## 멤버 변수
- private 필드는 `_camelCase`로 쓴다. 예: `_currentHealth`
- 외부에 노출할 값은 public 필드 대신 get/set 프로퍼티를 쓴다.
- 이 경우와 아래 "이름이 겹칠 때" 규칙 외에는 코드 안의 이름에 `_`를 넣지 않는다.
- 헝가리안 표기법을 쓰지 않는다.
  - 나쁨: `m_Count`, `a_Arr`, `iCount`, `strAge`

## 이름이 겹칠 때 (파일 약자 접두사)
새 멤버 변수(필드, 프로퍼티)를 만들 때, `Assets/_Game/Scripts/` 안의 다른 스크립트에
같은 이름의 멤버가 이미 있으면 새로 만드는 쪽 이름 앞에 파일 약자를 붙여 구분한다.

- 파일 약자: 그 코드 파일 이름(확장자 제외)에서 대문자만 순서대로 뽑은 것.
  - `PlayerController.cs` → `PC`, `EnemySpawner.cs` → `ES`, `Player.cs` → `P`
- 형식: `약자_원래이름`. 원래 이름의 대소문자 규칙은 그대로 둔다.
  - private 필드: `_moveSpeed` → `PC_moveSpeed` (앞의 `_`는 약자로 대신한다)
  - 프로퍼티, public 멤버: `MoveSpeed` → `PC_MoveSpeed`
- 확인 방법: 새 멤버를 만들기 전에 `Scripts/` 폴더를 같은 이름으로 검색한다.
  (`_moveSpeed`와 `moveSpeed`, `MoveSpeed`처럼 `_`와 첫 글자 대소문자만 다른 것도 같은 이름으로 본다)
- 이미 있는 다른 파일의 이름은 바꾸지 않는다. 새로 만드는 쪽에만 붙인다.
- 지역 변수와 매개변수에는 적용하지 않는다.
- 약자가 같은 다른 파일(예: `PlayerController`와 `PauseController` 모두 `PC`)에도
  같은 이름이 있으면 임의로 정하지 말고 어떻게 할지 물어본다.

## bool
- `is`, `has`, `can`, `should` 중 하나로 시작한다.
- 상태는 과거분사나 진행형으로 쓴다.
  - 좋음: `isStarted`, `isRunning`, `hasKey`, `canJump`
  - 나쁨: `isStart`, `started`, `flag`

## 길이와 줄임말
- 의미가 바로 보이도록 줄이지 않고 쓴다.
  - 나쁨: `cnt`, `btn2`, `butt` / 좋음: `count`, `confirmButton`
- 허용하는 줄임말은 다음뿐이다: `Info`(Information), `Dic`(Dictionary), `Btn`(Button)
- 그 외 줄임말이 필요해 보이면 줄이지 말고 전체 단어를 쓴다.

## 단어 선택
- 흔히 쓰는 쉬운 영어 단어를 고른다. 번역기에서 나온 낯선 단어를 피한다.
- 같은 개념에는 프로젝트 전체에서 같은 단어를 쓴다(예: Get/Fetch/Load 혼용 금지).

## 호출부 가독성
- 인자가 무엇인지 헷갈리는 호출에는 명명된 인자를 쓴다.
  - 예: `AttachWeapon(onto: playerA);`

## 스크립트
- 네임스페이스는 `Scripts/` 아래 경로를 따른다. 예: `Scripts/Player/` → `Game.Player`
- 에디터 전용 스크립트는 `Scripts/Editor/`에만 둔다.

---

# 3. 에셋 폴더 규칙

내가 만든 에셋은 모두 `Assets/_Game/` 아래에 둔다.
`_Game` 밖의 폴더(에셋 스토어, 서드파티, Packages 등)는 맨 위 "수정 가능한 범위" 규칙을 따른다.

## 폴더 구조
1단계는 파일 종류별로 나눈다. 같은 종류의 파일은 반드시 같은 최상위 폴더에 둔다.

```
Assets/_Game/
├── Scenes/
├── Scripts/
│   ├── Player/
│   ├── Enemy/
│   ├── UI/
│   ├── Common/          # 공용 유틸, 베이스 클래스
│   └── Editor/          # 에디터 전용 스크립트
├── Prefabs/
├── Materials/
├── Textures/
├── Models/
├── Animations/          # 애니메이션 클립과 애니메이터 컨트롤러
├── Shaders/
├── Audio/
│   ├── Music/
│   └── Sounds/
├── Fonts/
├── Data/                # ScriptableObject 에셋
└── Settings/            # URP/입력 시스템 등 프로젝트 설정 에셋
```

- 각 타입 폴더 안에서는 기능별 하위 폴더(`Player/`, `Enemy/`, `UI/` …)로 나눈다.
  하위 폴더 이름은 모든 타입 폴더에서 똑같이 쓴다.
  예: `Prefabs/Player/`, `Materials/Player/`, `Textures/Player/`
- 여러 기능이 같이 쓰는 에셋은 그 타입 폴더의 `Common/`에 둔다.
- 타입 폴더 안의 파일이 10개 미만이면 하위 폴더 없이 바로 둔다.
- 하위 폴더는 필요할 때만 만든다. 빈 폴더를 미리 만들지 않는다.
- 위 목록에 없는 종류의 파일이 생기면 새 최상위 폴더를 만들기 전에 먼저 물어본다.

## 파일 이동·생성 시 반드시 지킬 것
- 에셋을 옮기거나 이름을 바꿀 때는 같은 이름의 `.meta` 파일도 함께 옮기고 바꾼다.
  `.meta`를 직접 만들거나 수정하거나 지우지 않는다.
- 여러 에셋을 한꺼번에 옮기는 작업은 먼저 이동 계획(원래 경로 → 새 경로)을 보여주고 확인받는다.
- 다음 이름의 폴더는 요청받지 않으면 만들지 않는다:
  `Resources`, `Plugins`, `StreamingAssets`, `Gizmos`, `Editor Default Resources`
  (`Editor`는 `Scripts/Editor/`에만 둔다)
- `Resources.Load` 대신 직렬화 참조나 Addressables를 쓴다.

---

# 4. 파일 이름 규칙

## 폴더, 스크립트, 씬
- 폴더 이름과 스크립트(.cs) 파일 이름은 PascalCase만 쓴다. 접두사와 `_`를 쓰지 않는다.
  - 좋음: `Scripts/Player/PlayerController.cs`, `Materials/Common/`
  - 나쁨: `scripts/`, `Player_Controller.cs`, `S_PlayerController.cs`
- 스크립트 파일 이름은 그 안의 클래스 이름과 똑같이 한다.
- 씬(.unity) 파일 이름도 PascalCase만 쓰고 접두사와 `_`를 붙이지 않는다.
  - 좋음: `MainMenu`, `Stage01` / 나쁨: `L_MainMenu`, `Main_Menu`
- 약어도 PascalCase로 쓰지 않도록 풀어 쓴다. 예: `BGM/` 대신 `Music/`, `SFX/` 대신 `Sounds/`
- 예외: `_Game` 폴더 자체.

## 그 외 모든 에셋 파일
- 형식: `대문자 한 글자` + `_` + `PascalCase 이름`
- `_`는 접두사 뒤에 딱 한 번만 쓴다. 이름 부분에는 `_`, 공백, 한글을 쓰지 않는다.
  - 좋음: `T_PlayerBodyAlbedo`, `A_PlayerRun`
  - 나쁨: `T_PlayerBody_Albedo`, `Tex_PlayerBody`, `T_playerBody`, `PlayerBody`

| 타입 | 접두사 | 예 |
|---|---|---|
| 프리팹 | `P_` | `P_Player` |
| 머티리얼 | `M_` | `M_PlayerBody` |
| 텍스처, 스프라이트 | `T_` | `T_PlayerBodyAlbedo` |
| 3D 모델 (.fbx 등) | `O_` | `O_Player` |
| 애니메이션 클립 | `A_` | `A_PlayerRun` |
| 애니메이터 컨트롤러 | `C_` | `C_Player` |
| 셰이더, 셰이더 그래프 | `H_` | `H_Toon` |
| ScriptableObject 에셋 | `D_` | `D_GoblinStats` |
| 배경음 | `B_` | `B_Title` |
| 효과음 | `S_` | `S_Jump` |
| 폰트 | `F_` | `F_NanumGothic` |
| 입력 액션 | `I_` | `I_PlayerControls` |

- 표에 없는 타입의 파일을 만들 때는 접두사를 임의로 정하지 말고 먼저 물어본다.
- `Settings/` 폴더의 Unity 설정 에셋(URP 에셋 등)은 Unity가 만든 이름을 그대로 둔다.
