# Desktop Organizer — 개발 계획 (Dev Plan)

> **문서 버전:** 1.1.0
> **작성일:** 2026-06-18
> **최종 수정일:** 2026-07-24 — Post-MVP 로드맵(Phase 10~18) 개요 추가 (상세는 `03_FunctionSpec.md` §5 참조)
> **상태:** 승인됨 (MVP: Phase 0~9 완료, Post-MVP: Phase 10 착수 대기)
> **적용 범위:** MVP (v1.0) + Post-MVP 로드맵 개요 (Phase 10~18)

---

## 1. 기술 스택

### 선택: C# + WPF (.NET 8 LTS)

| 요구사항 | 선택 이유 |
|---|---|
| Windows Shell API (SHGetSpecialFolderPath, IShellFolder) | C# P/Invoke로 직접 호출. 별도 래퍼 불필요 |
| 투명 데스크탑 오버레이 창 | WPF `WindowStyle=None` + `AllowsTransparency=True` 기본 지원 |
| FileSystemWatcher | .NET 기본 클래스. FSW 이벤트→디바운싱 패턴 검증됨 |
| DPI 스케일링 | WPF Per-Monitor DPI 인식 (`app.manifest` 한 줄) |
| JSON 설정 저장 | `System.Text.Json` 내장, 별도 라이브러리 불필요 |
| UUID | `System.Guid` 내장 |
| MVVM 패턴 | WPF 바인딩 엔진이 MVVM에 최적화 |
| 개발 생산성 | Visual Studio / VS Code 툴링, NuGet 생태계 |

**기각한 대안:**
- **Electron**: ~150MB 번들, Win32 Shell API 접근 번거로움
- **WinUI 3**: 아직 성숙 중, 데스크탑 창 Z-order 제어에 제약
- **WinForms**: 투명 창 지원 미흡
- **Python/Qt**: 실시간 성능 우려, 배포 크기 큼

---

## 2. 프로젝트 폴더 구조

```
DesktopOrganizer/
├── DesktopOrganizer.sln
├── .gitignore
├── README.md
├── docs/
│   ├── 03_FunctionSpec.md
│   └── 04_DevPlan.md
├── src/
│   └── DesktopOrganizer/
│       ├── DesktopOrganizer.csproj   (.NET 8, WPF)
│       ├── app.manifest              (DPI awareness 선언)
│       ├── App.xaml / App.xaml.cs    (단일 인스턴스, 트레이 초기화)
│       │
│       ├── Models/
│       │   ├── IconInfo.cs
│       │   ├── Container.cs
│       │   ├── Rule.cs
│       │   ├── RuleCondition.cs
│       │   ├── AppSettings.cs
│       │   └── Layout.cs
│       │
│       ├── ViewModels/
│       │   ├── Base/
│       │   │   ├── ObservableObject.cs
│       │   │   └── RelayCommand.cs
│       │   ├── MainViewModel.cs
│       │   ├── ContainerViewModel.cs
│       │   ├── RuleEditorViewModel.cs
│       │   ├── SettingsViewModel.cs
│       │   └── LayoutViewModel.cs
│       │
│       ├── Views/
│       │   ├── OverlayWindow.xaml
│       │   ├── Controls/
│       │   │   └── ContainerControl.xaml
│       │   └── Dialogs/
│       │       ├── RuleEditorDialog.xaml
│       │       ├── StyleEditorDialog.xaml
│       │       ├── SettingsDialog.xaml
│       │       └── LayoutManagerDialog.xaml
│       │
│       ├── Services/
│       │   ├── DesktopReaderService.cs   (F-001)
│       │   ├── FileClassifierService.cs  (F-002)
│       │   ├── ExclusionService.cs       (F-003)
│       │   ├── ContainerService.cs       (F-004~F-009)
│       │   ├── IconSortService.cs        (F-010~F-011)
│       │   ├── RuleService.cs            (F-012~F-015)
│       │   ├── DesktopWatcherService.cs  (F-016)
│       │   ├── AutoOrganizeService.cs    (F-017)
│       │   ├── SettingsService.cs        (F-018~F-019)
│       │   ├── LayoutService.cs          (F-020~F-021)
│       │   └── LogService.cs             (F-022)
│       │
│       └── Interop/
│           ├── ShellApi.cs
│           ├── DesktopIconInterop.cs
│           └── DpiHelper.cs
│
└── tests/
    └── DesktopOrganizer.Tests/
        ├── DesktopOrganizer.Tests.csproj
        ├── Services/
        └── Models/
```

---

## 3. MVP 개발 Phase

### Phase 0 — 프로젝트 기반 세팅
**목표:** `dotnet build` 성공, 앱 실행 시 빈 창 표시

| 작업 | 상태 |
|------|------|
| `.gitignore` 생성 | ✅ |
| `docs/04_DevPlan.md` 작성 | ✅ |
| `DesktopOrganizer.sln` 및 WPF .NET 8 프로젝트 생성 | ✅ |
| `app.manifest` — PerMonitorV2 DPI 인식 선언 | ✅ |
| `App.xaml.cs` — 단일 인스턴스(Mutex) + 트레이 아이콘 기초 | ✅ |
| 모든 Model 클래스 정의 | ✅ |
| MVVM 기반 클래스 (ObservableObject, RelayCommand) | ✅ |
| `tests/` 프로젝트 생성 및 빌드 확인 | ✅ |

---

### Phase 1 — 데스크탑 읽기 & 파일 분류 (F-001, F-002, F-003)
**목표:** 앱 시작 시 데스크탑 아이콘 목록 출력, 단위 테스트 통과

| 작업 | 상태 |
|------|------|
| `ShellApi.cs` — SHGetSpecialFolderPath P/Invoke | ⬜ |
| `DesktopIconInterop.cs` — LVM_GETITEMPOSITION으로 아이콘 좌표 읽기 | ⬜ |
| `DesktopReaderService` — 8가지 속성 수집 | ⬜ |
| `FileClassifierService` — 확장자 기반 9개 타입 분류 | ⬜ |
| `ExclusionService` — CLSID 기반 시스템 아이콘 5종 제외 | ⬜ |
| 단위 테스트: FileClassifierService | ⬜ |

---

### Phase 2 — 투명 오버레이 창 & 앱 기반 UI
**목표:** 오버레이 표시, 빈 영역 클릭이 데스크탑으로 전달됨

| 작업 | 상태 |
|------|------|
| `OverlayWindow.xaml` — 투명 전체화면 창 | ⬜ |
| 마우스 이벤트 패스스루 | ⬜ |
| 시스템 트레이 메뉴 | ⬜ |
| `MainViewModel` — Container 목록 바인딩 | ⬜ |
| 해상도/DPI 변경 감지 | ⬜ |

---

### Phase 3 — Container 생성·편집·삭제 (F-004, F-005, F-006)
**목표:** Container 생성/수정/삭제, 재시작 후 유지

---

### Phase 4 — Container 이동·크기조절·스타일 (F-007, F-008, F-009)
**목표:** 드래그 이동, 리사이즈, 스타일 변경 동작 및 복원

---

### Phase 5 — 아이콘 자동 정렬 & 순서 저장 (F-010, F-011)
**목표:** 정렬 기준 변경 시 실제 데스크탑 아이콘 재배치

---

### Phase 6 — Rule 생성·편집·삭제·우선순위 (F-012, F-013, F-014, F-015)
**목표:** Rule 생성 후 기존 아이콘에 즉시 적용

---

### Phase 7 — 실시간 감시 & 자동 정리 (F-016, F-017)
**목표:** 파일 복사 시 Rule 매칭 → Container 자동 배치

---

### Phase 8 — 설정 저장/불러오기 & Layout (F-018, F-019, F-020, F-021)
**목표:** 재시작 후 완전한 상태 복원, Layout 저장/복원

---

### Phase 9 — 로깅 & 마무리 (F-022 + polish)
**목표:** 로그 파일 생성, 처음 실행부터 종료까지 에러 없이 동작

---

## 4. Phase 의존성

```
Phase 0 (기반)
  └─► Phase 1 (데스크탑 읽기)
        └─► Phase 2 (오버레이 창)
              └─► Phase 3 (Container CRUD)
                    ├─► Phase 4 (이동/크기/스타일)
                    ├─► Phase 5 (아이콘 정렬)
                    └─► Phase 6 (Rule 엔진)
                          └─► Phase 7 (실시간 감시)
                                └─► Phase 8 (설정/Layout)
                                      └─► Phase 9 (로깅/마무리)
```

## 5. 검증 방법

- **각 Phase 완료 후**: `dotnet build` + Phase 완료 기준 수동 확인
- **Phase 5 완료 후**: Container 생성 → 아이콘 배치 → 재시작 후 위치 복원
- **Phase 7 완료 후**: 파일 복사 → Rule 매칭 → Container 배치 → 로그 확인 (end-to-end)
- **Phase 9 완료 후**: 신규 설치 시나리오 + 재시작 10회 상태 일관성 확인

---

## 6. Post-MVP 개발 Phase (Phase 10~18)

> MVP(Phase 0~9, F-001~F-022) 완료 후, 경쟁 제품 대비 기능·UI 격차 해소를 위한 로드맵. **상세 명세(기능 ID, 완료 기준)는 `03_FunctionSpec.md` §5를 단일 진실 공급원으로 한다.** 이 절은 Phase별 목표와 순서만 요약한다. `CLAUDE.md`의 기존 워크플로우(계획 설명 → 승인 → 구현 → 완료기준 확인 → 승인 → 다음 기능)는 Post-MVP Phase에도 동일하게 적용된다.
>
> **Phase 순서 근거**: 사용자가 지정한 최우선순위는 "비주얼/개인화"이며, 향후 기관 내 배포 가능성을 고려해 이식성(상대 경로/포터블 복사본) 항목의 우선순위를 일반적인 개인용 도구보다 높였다. Phase 11(렌더링 구조 검증)은 Phase 12(개인화)의 일부 기능(Container별 아이콘 크기)의 실현 가능성을 먼저 확인하기 위해 개인화 앞에 배치한다.

### Phase 10 — 설정 다이얼로그 & 현지화 인프라 (F-023, F-024, F-025)
**목표:** `config.json` 수동 편집 없이 전역 설정을 UI로 변경 가능; 향후 다국어 확장이 번역만으로 가능한 문자열 구조 확보
**상세 명세:** `03_FunctionSpec.md` §4 F-023~F-025 (작성 완료)

---

### Phase 11 — 아이콘 렌더링 구조 검증 (Spike, F-026)
**목표:** Container 내부 아이콘을 실제 바탕화면 아이콘이 아닌 앱이 직접 그리는 방식(WPF `ItemsControl`)으로 전환할 수 있는지 Go/No-Go 결정. 1주 타임박스, 프로덕션 코드가 아닌 1개 Container 대상 프로토타입 + 결정 문서(ADR) 산출.
**상세 명세:** Phase 착수 직전 `03_FunctionSpec.md`에 F-026 상세 항목 작성 예정

---

### Phase 12 — 개인화 핵심: 테마 · 뷰 모드 · 배경 효과 (F-027~F-032) — **사용자 최우선순위**
**목표:** 기존 `ContainerStyle`/`StyleEditorDialog`/`IconSortService`를 확장하여 테마 프리셋, 스킨 팩, 그리드/리스트/상세 뷰 모드, 배경 효과, 제목 위치를 지원. F-032(Container별 아이콘 크기)는 Phase 11이 GO일 때만 포함.
**상세 명세:** Phase 착수 직전 작성 예정

---

### Phase 13 — Container별 아이콘 크기 + 통합 썸네일 서비스 (F-033~F-035)
**목표:** Phase 11 결과에 따라 분기 — GO: 소유 렌더링 엔진 정식화 + `IShellItemImageFactory` 기반 통합 썸네일 캐시 / NO-GO: 실제 아이콘 위 배지·라벨 어도너 + OS 전역 아이콘 크기 설정.
**상세 명세:** Phase 착수 직전 작성 예정 (분기 결과 반영)

---

### Phase 14 — 다중 모니터 & 좌표 정확성 (F-036~F-039)
**목표:** `OverlayWindow.FitToScreen()`의 단일 모니터 가정 수정, 모니터 구성 변경 대응, 격자 스냅, 키보드 탐색.
**상세 명세:** Phase 착수 직전 작성 예정

---

### Phase 15 — 정렬 · 검색 · 태깅 고도화 (F-040~F-043)
**목표:** 정렬 기준 확장(크기/라벨/클릭수), 클릭 횟수 추적, 색상 라벨, 빠른 검색-하이라이트. 새 서브 시스템·렌더링 의존성 없음 — 일정 지연 시 완충 역할.
**상세 명세:** Phase 착수 직전 작성 예정

---

### Phase 16 — 미디어 & 파일 생산성 기능 (F-044~F-047)
**목표:** 내장 이미지 뷰어, 클립보드 크롭, 오디오/동영상 미리보기, 빠른 메모 생성. **F-047은 비파괴 원칙의 유일한 명시적 예외(신규 파일 생성)이므로 별도 승인 필요.**
**상세 명세:** Phase 착수 직전 작성 예정

---

### Phase 17 — 조건부 표시 & Container 동작 (F-048, F-049)
**목표:** Container별 표시 조건(항상/포커스 시), 스케줄 기반 조건부 표시. 가상 데스크톱 인식은 범위 제외(별첨 §5.3 참조).
**상세 명세:** Phase 착수 직전 작성 예정

---

### Phase 18 — 코어 품질 & 기관 배포 대응 (F-050~F-053)
**목표:** 상대 경로 지원, 포터블 복사본 내보내기, 경량 업데이트 확인, DPI 회귀 전수 점검. 기관 내 여러 PC 배포 가능성을 고려해 일반적인 "나중에" 항목보다 우선순위를 높임.
**상세 명세:** Phase 착수 직전 작성 예정

---

## 7. Post-MVP 검증 방법

- **각 기능 완료 후**: 해당 기능의 `03_FunctionSpec.md` 완료 기준(Acceptance Criteria) 체크 + 사용자 승인 (MVP와 동일한 1기능씩 승인 절차)
- **각 Phase 완료 후**: `dotnet test` 전체 통과(현재 225개 xUnit 테스트) 확인 후 `CLAUDE.md` "완료된 기능" 갱신, 다음 Phase 착수 승인 요청
- **Phase 11 스파이크**: 자동화 테스트 대상이 아님 — 1-Container 데모 + Go/No-Go 결정 문서로 완료 기준을 대신함
- **UI로 확인 가능한 기능** (Phase 12 전체, Phase 13/16 다수): 자동 UI 테스트 하네스가 없으므로 실행 중인 앱에서 개발자가 직접 조작하여 확인 (기존 MVP 검증 관행과 동일)
