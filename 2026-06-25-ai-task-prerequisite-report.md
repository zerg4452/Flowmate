# AI 기반 테스크 자동 작업 실행 선결조건 구현 보고서

## 1. 작업 배경

백로그 todo #1 "AI 기반 테스크 자동 작업 실행 기능 추가"(커스텀 `/ez-tesk` 스킬 + Claude CLI로 브랜치 생성·작업까지 자동 수행)를 본격 구현하기 전 단계로, 다음 3가지 선결조건 UI/기능을 추가했다.

1. 프로젝트 관리 화면에 프로젝트별 워크스페이스 경로 입력창 추가
2. Claude Code / Codex를 선택하는 라디오 버튼 추가
3. 워크스페이스 경로 + AI 도구 선택이 모두 완료된 프로젝트의 테스크 상세 팝업 우측에 터미널 활성화 버튼, 명령 전달용 마크다운 편집기, 이 영역 전체를 켜는 활성화 버튼 추가

## 2. 구현 내용

### 2.1 모델

- `Project` 모델에 `WorkspacePath`(string), `AiCliTool`(nullable enum) 두 속성 추가
- `AiCliTool` enum 신규 생성 (`ClaudeCode`, `Codex`)

### 2.2 프로젝트 관리 화면 (`MainWindow.xaml` 좌측 컬럼)

- "프로젝트 목록" 그룹박스 하단에 "AI 작업 설정" 그룹박스 추가
  - 워크스페이스 경로 텍스트박스 + "찾아보기" 버튼 (`Microsoft.Win32.OpenFolderDialog`로 폴더 선택)
  - "Claude Code" / "Codex" 라디오 버튼 (둘 중 하나만 선택 가능, `GroupName` 공유)
  - "AI 설정 저장" 버튼
- 라디오 버튼 바인딩을 위해 신규 컨버터 `EnumEqualsToBoolConverter` 작성 (nullable enum ↔ bool, `ConvertBack` 지원으로 양방향 바인딩 가능)

### 2.3 테스크 상세 팝업 (`MainWindow.xaml`)

- 팝업 카드(`DetailCard`)를 1열 구조에서 2열 `Grid`로 변경 (본문 영역 + 우측 "AI 작업" 패널)
- 우측 패널은 프로젝트에 워크스페이스 경로와 AI 도구가 모두 설정된 경우에만 노출 (`IsAiWorkAvailable` 바인딩)
- "활성화" 버튼을 눌러야 터미널/마크다운 영역이 펼쳐지는 2단계 구조
  - "터미널 창 활성화" 버튼 → 워크스페이스 경로에서 `cmd.exe` 실행
  - 명령 마크다운 텍스트박스 → "마크다운 저장" 버튼으로 `flowmate-task-{테스크ID}.md` 파일로 저장

### 2.4 ViewModel (`MainViewModel.cs`)

- 신규 속성: `ProjectWorkspacePath`, `ProjectAiTool`, `IsAiPanelActivated`, `AiCommandMarkdown`, `IsAiWorkAvailable`
- 신규 커맨드: `SaveProjectAiSettingsCommand`, `ActivateAiPanelCommand`, `OpenTerminalCommand`, `SaveAiCommandMarkdownCommand`
- 프로젝트 선택/테스크 팝업 오픈 시 상태 초기화 로직 반영

### 2.5 코드비하인드 (`MainWindow.xaml.cs`)

- `BrowseWorkspacePath_Click`: 폴더 선택 다이얼로그 호출 후 `ProjectWorkspacePath`에 반영

## 3. 작업 중 발생한 이슈 및 처리

푸시 시점에 동일 브랜치(`claude/task-image-attachment-19pslq`)에 별도 세션에서 작업한 커밋(`a2cdd79`, 메티 링크 가져오기·프로젝트 문서 관리·통계 LNB 표시·보드 테스크 직접 추가 기능)이 먼저 반영되어 있어 푸시가 거부됨(fetch first).

강제 푸시 대신 `git rebase`로 안전하게 통합했으며, 충돌은 다음 2개 파일에서 발생:

- **`MainViewModel.cs`**: 두 브랜치가 동일한 위치(`ToggleProjectActive()` 다음)에 각자 새 메서드를 삽입하여 충돌. 상태 공유가 없는 독립적인 메서드들이라 `OpenAddTask()`(상대측) + `SaveProjectAiSettings()`/`OpenTerminal()`/`SaveAiCommandMarkdown()`(이번 작업) 네 개를 모두 보존하는 방식으로 해결.
- **`MainWindow.xaml`**: 상대측 커밋이 프로젝트 관리 화면의 "상태 관리" 영역을 "프로젝트 문서 관리" UI로 전면 교체하면서, 이번 작업에서 추가한 "AI 작업 설정" 그룹박스의 삽입 위치와 충돌. 상대측의 문서 관리 UI는 그대로 유지하고, "AI 작업 설정" 그룹박스는 좌측 컬럼의 "프로젝트 목록" 하단으로 재배치하여 공존시킴.

## 4. 검증

- `.NET SDK`가 설치되지 않은 샌드박스 환경이라 `dotnet build`를 통한 컴파일 검증은 수행하지 못함
- XAML 파싱 유효성 검사(Python `xml.etree.ElementTree`) 통과
- 충돌 마커(`<<<<<<<`, `=======`, `>>>>>>>`) 잔존 여부 전체 검색 결과 없음 확인
- 이번 작업에서 추가한 식별자(커맨드, 속성, 코드비하인드 메서드)와 상대측 작업에서 추가한 식별자(`ProjectDocument`, Matty 관련 핸들러 등)가 모두 누락 없이 병합 결과에 존재함을 grep으로 재확인

## 5. 결과

- 커밋: `f8866f2` "Add per-project workspace path, AI CLI tool selection, and task-level AI panel scaffolding"
- 브랜치 `claude/task-image-attachment-19pslq`에 푸시 완료 (`a2cdd79..f8866f2`)
- 실제 빌드/실행 검증은 미수행 — Windows + .NET 8 SDK 환경에서 빌드 확인 필요

## 6. 다음 단계 (범위 외, 미구현)

본 보고서의 작업은 백로그 todo #1의 "선결조건"에 한정된다. 다음 단계인 `/ez-tesk` 스킬 연동 및 Claude CLI 기반 브랜치 자동 생성·작업 실행 로직은 별도 요청 시 진행한다.
