# CLAUDE.md — Desktop Organizer

> **모든 기능 판단의 기준은 `docs/03_FunctionSpec.md`이다.**
> 명세에 없으면 만들지 않고, 명세에 있으면 명세대로 만든다.

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 프로젝트명 | Desktop Organizer |
| 목적 | Windows 바탕화면 아이콘을 Container와 Rule로 자동 관리하는 생산성 프로그램 |
| 핵심 명세 문서 | `docs/03_FunctionSpec.md` (모든 기능의 단일 진실 공급원) |
| 현재 Phase | **Phase 2** (Phase 1 완료) |
| 완료된 기능 | Phase 0 기반 세팅, Phase 1 (F-001 바탕화면 읽기, F-002 파일 분류, F-003 시스템 아이콘 제외) |

---

## 2. 절대 규칙 (반드시 준수)

- 실제 파일을 **이동 / 복사 / 삭제**하는 코드는 절대 작성하지 않는다.
- `docs/03_FunctionSpec.md`에 없는 기능을 임의로 추가하지 않는다.
- 현재 Phase 범위를 벗어난 기능을 미리 구현하지 않는다.
- 코드 작성 전에 반드시 **구현 계획을 먼저 설명**한다.
- 기존에 동작하는 코드를 수정할 때는 **무엇을 왜 변경하는지 반드시 사전에 고지**한다.
- UI 설계, 클래스 설계, DB 설계는 명세서 범위 안에서만 결정한다.
- 추측으로 기능을 추가하거나 "있으면 좋을 것 같아서" 만드는 행위를 하지 않는다.

---

## 3. 기술 스택

| 항목 | 결정값 |
|------|--------|
| 언어 | C# 12 |
| 런타임 | .NET 8 LTS |
| UI 프레임워크 | WPF (`UseWPF=true`, `UseWindowsForms=true`) |
| UI 패턴 | MVVM (ObservableObject, RelayCommand 직접 구현) |
| 설정 저장 형식 | JSON (`System.Text.Json` 내장) |
| Win32 연동 | P/Invoke (Shell API, LVM_GETITEMPOSITION 등) |
| 로그 | 직접 구현 (F-022, `LogService`) |
| 테스트 프레임워크 | xUnit 2.7 |
| 플랫폼 | x64 전용 |
| 최소 지원 OS | Windows 10 / Windows 11 |

---

## 4. 프로젝트 폴더 구조

```
DesktopOrganizer/
├── DesktopOrganizer.sln
├── README.md
├── .gitignore
├── docs/
│   ├── 03_FunctionSpec.md         ← 기능 명세서 (단일 진실 공급원)
│   └── 04_DevPlan.md              ← 개발 Phase 계획서
├── src/
│   └── DesktopOrganizer/
│       ├── DesktopOrganizer.csproj
│       ├── app.manifest           ← PerMonitorV2 DPI 선언
│       ├── App.xaml / App.xaml.cs ← 단일 인스턴스(Mutex)
│       ├── Models/                ← 데이터 모델 (IconInfo, Container, Rule 등)
│       ├── ViewModels/
│       │   └── Base/              ← ObservableObject, RelayCommand
│       ├── Views/
│       │   ├── Controls/          ← 재사용 UserControl
│       │   └── Dialogs/           ← 모달 다이얼로그
│       ├── Services/              ← 비즈니스 로직 (F-001~F-022)
│       └── Interop/               ← Win32 Shell API P/Invoke
└── tests/
    └── DesktopOrganizer.Tests/    ← xUnit 테스트
```

---

## 5. 개발 워크플로우 규칙

### 매 작업 세션 시작 시

1. 이 파일(`CLAUDE.md`)을 읽는다.
2. `docs/03_FunctionSpec.md`에서 현재 Phase에 해당하는 기능 명세를 확인한다.
3. `docs/04_DevPlan.md`에서 이번 Phase의 범위를 확인한다.
4. 현재 완료된 기능 목록을 파악한다.

### 구현 순서 (매 기능마다 반드시 준수)

```
1. 구현 계획 설명   →   사용자 승인
        ↓
2. 코드 작성
        ↓
3. Acceptance Criteria 체크 (03_FunctionSpec.md 기준)
        ↓
4. 사용자 확인 및 승인
        ↓
5. CLAUDE.md "완료된 기능" 업데이트
        ↓
6. 다음 기능으로 이동
```

### Phase 진행 규칙

- 현재 Phase가 **사용자 승인**되기 전까지 다음 Phase 작업을 시작하지 않는다.
- 한 Phase 내에서도 기능 단위(Feature)로 하나씩 완료 후 다음으로 넘어간다.
- Phase 완료 시 이 파일의 **"현재 Phase"** 와 **"완료된 기능"** 항목을 반드시 업데이트한다.

---

## 6. 코드 작성 규칙

| 항목 | 규칙 |
|------|------|
| 주석 언어 | 영어 |
| 변수 / 함수명 | 영어, 의미 있는 이름 사용 |
| 에러 처리 | 모든 예외는 반드시 F-022(로그 기능)로 기록 |
| 하드코딩 금지 | 경로, 숫자 상수는 반드시 설정 파일 또는 상수로 분리 |
| 메서드 길이 | 단일 메서드 50줄 이하 권장 |
| 파일 이동 금지 | 아이콘 좌표(위치)만 변경, 실제 파일 시스템 조작 절대 금지 |
| 의존성 방향 | Core → UI 의존 금지 (UI가 Core에 의존하는 단방향 구조 유지) |

---

## 7. 핵심 비즈니스 규칙 요약

> 상세 내용은 `docs/03_FunctionSpec.md` 참조. 아래는 코드 작성 시 항상 염두에 둘 핵심 원칙이다.

- **비파괴 원칙**: 아이콘의 바탕화면 좌표(위치)만 변경한다. 실제 파일은 절대 이동·복사·삭제하지 않는다.
- **비침습 원칙**: Windows 기본 시스템 아이콘(휴지통, 내 PC 등)은 자동 정리 대상에서 기본 제외한다.
- **Rule First Match**: 복수의 Rule이 매칭될 경우 우선순위가 가장 높은 첫 번째 Rule만 적용한다.
- **지속성 원칙**: 모든 설정과 레이아웃은 `%APPDATA%\DesktopOrganizer\` 경로에 저장·복원된다.
- **실시간성 원칙**: 바탕화면 변경은 FileSystemWatcher로 감지하며 500ms 디바운싱을 적용한다.

---

## 8. 설정 파일 경로 규칙

| 파일 | 경로 |
|------|------|
| 메인 설정 | `%APPDATA%\DesktopOrganizer\config.json` |
| Layout 파일 | `%APPDATA%\DesktopOrganizer\layouts\{layout_id}.json` |
| 로그 파일 | `%APPDATA%\DesktopOrganizer\logs\desktop_organizer_YYYYMMDD.log` |

---

## 9. MVP 기능 목록 및 진행 상태

> Phase 완료 시마다 상태를 업데이트한다.

| 기능 ID | 기능명 | 우선순위 | 상태 |
|---------|--------|----------|------|
| F-001 | 바탕화면 읽기 | Must Have | ✅ 완료 |
| F-002 | 파일 분류 | Must Have | ✅ 완료 |
| F-003 | 기본 아이콘 제외 | Must Have | ✅ 완료 |
| F-004 | Container 생성 | Must Have | ⬜ 미완료 |
| F-005 | Container 수정 | Must Have | ⬜ 미완료 |
| F-006 | Container 삭제 | Must Have | ⬜ 미완료 |
| F-007 | Container 이동 | Must Have | ⬜ 미완료 |
| F-008 | Container 크기 변경 | Must Have | ⬜ 미완료 |
| F-009 | Container 스타일 변경 | Should Have | ⬜ 미완료 |
| F-010 | 아이콘 자동 정렬 | Must Have | ⬜ 미완료 |
| F-011 | 아이콘 순서 저장 | Must Have | ⬜ 미완료 |
| F-012 | Rule 생성 | Must Have | ⬜ 미완료 |
| F-013 | Rule 수정 | Must Have | ⬜ 미완료 |
| F-014 | Rule 삭제 | Must Have | ⬜ 미완료 |
| F-015 | Rule 우선순위 관리 | Must Have | ⬜ 미완료 |
| F-016 | 실시간 바탕화면 감시 | Must Have | ⬜ 미완료 |
| F-017 | 새 파일 자동 정리 | Must Have | ⬜ 미완료 |
| F-018 | 설정 저장 | Must Have | ⬜ 미완료 |
| F-019 | 설정 불러오기 | Must Have | ⬜ 미완료 |
| F-020 | Layout 저장 | Should Have | ⬜ 미완료 |
| F-021 | Layout 복원 | Should Have | ⬜ 미완료 |
| F-022 | 로그 기능 | Should Have | ⬜ 미완료 |

> 상태 표기: ⬜ 미완료 / 🔄 진행 중 / ✅ 완료

---

## 10. Claude Code에 대한 지시 사항

### 해도 되는 것

- 명세서에 정의된 기능을 Phase 계획에 따라 순서대로 구현한다.
- 구현 방법에 대해 복수의 선택지를 제시하고 사용자가 결정하게 한다.
- 기술적 위험이나 주의사항을 사전에 알린다.
- 기존 코드의 버그를 발견하면 즉시 보고하고 수정 여부를 묻는다.

### 하지 말아야 할 것

- 명세에 없는 기능을 "있으면 좋을 것 같아서" 추가하는 행위
- 사용자 승인 없이 다음 Phase로 넘어가는 행위
- 실제 파일 시스템을 수정하는 코드 작성
- 기존 동작 코드를 사전 고지 없이 변경하는 행위
- UI 레이아웃, DB 스키마, 클래스 구조를 명세 없이 임의로 설계하는 행위

---

*이 파일은 프로젝트가 진행되면서 지속적으로 업데이트된다.*
*마지막 업데이트: 2026-06-19 — Phase 1 완료 (F-001~F-003), Phase 2 시작 예정*
