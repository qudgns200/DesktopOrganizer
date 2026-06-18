# Desktop Organizer — 개발 계획 (Dev Plan)

> **문서 버전:** 1.0.0
> **작성일:** 2026-06-18
> **상태:** 승인됨 (Phase 0 진행 중)
> **적용 범위:** MVP (v1.0)

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
