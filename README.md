# 🗂️ Desktop Organizer

> Windows 바탕화면 아이콘을 **자동으로 분류·관리**하는 생산성 도구

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-0078D4?logo=windows)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11%20x64-0078D4?logo=windows)](https://www.microsoft.com/windows)
[![Tests](https://img.shields.io/badge/Tests-218%20passing-brightgreen)](#테스트-실행)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

---

## 소개

바탕화면이 파일로 가득 차 있나요?  
Desktop Organizer는 사용자가 정의한 **Container(영역)** 와 **Rule(규칙)** 을 기반으로 바탕화면 아이콘을 자동으로 정리합니다.

- **실제 파일을 이동하거나 삭제하지 않습니다** — 아이콘의 화면 위치(좌표)만 변경합니다.
- 새 파일이 바탕화면에 추가되면 **실시간으로 감지**하여 자동 배치합니다.
- 모든 설정은 저장되어 **프로그램 재시작 후에도 유지**됩니다.

---

## 주요 기능

| 기능 | 설명 |
|------|------|
| **Container 관리** | 바탕화면에 가상 영역을 생성·이동·크기조절·스타일 편집 |
| **Rule 기반 자동 배치** | 파일명 패턴, 확장자, 파일 종류, 날짜 등 조건으로 자동 배치 |
| **실시간 감시** | `FileSystemWatcher` + 500ms 디바운싱으로 새 파일 즉시 감지 |
| **아이콘 자동 정렬** | 이름·확장자·날짜·수동 등 9가지 기준으로 Container 내 정렬 |
| **Layout 저장/복원** | 현재 바탕화면 구성을 스냅샷으로 저장하고 언제든 복원 |
| **설정 백업** | 자동 백업 파일 3개 보관, 손상 시 자동 복구 |
| **로그 기록** | 날짜별 로그 파일, 10MB 롤링, 30일 자동 삭제 |

---

## 필수 요구사항

| 항목 | 요구사항 |
|------|----------|
| OS | Windows 10 / Windows 11 |
| 아키텍처 | **x64 전용** |
| 런타임 | [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (실행만 할 경우) |
| SDK | [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (빌드 포함) |

---

## 설치 및 실행

### 1. 저장소 클론

```bash
git clone https://github.com/qudgns200/DesktopOrganizer.git
cd DesktopOrganizer
```

### 2. 빌드

```bash
dotnet build src/DesktopOrganizer/DesktopOrganizer.csproj -c Release
```

### 3. 실행

**방법 A — 빌드된 exe 직접 실행 (권장)**

```bash
.\src\DesktopOrganizer\bin\Release\net8.0-windows\DesktopOrganizer.exe
```

**방법 B — dotnet run**

```bash
dotnet run --project src/DesktopOrganizer -c Release
```

> **주의**: `dotnet run`은 개발 편의용이며, 실제 사용은 빌드된 `.exe`를 실행하는 것이 권장됩니다.  
> 앱 시작 후 바탕화면은 그대로 유지되며, **시스템 트레이에 아이콘이 생성**됩니다.

### 4. 테스트 실행

```bash
dotnet test tests/DesktopOrganizer.Tests/DesktopOrganizer.Tests.csproj
```

총 **218개 테스트** 전체 통과를 확인할 수 있습니다.

---

## 사용 방법

### Container 만들기

1. 바탕화면 빈 곳에서 **우클릭** → **"새 Container"** 선택
2. 생성 직후 이름 입력 상태로 활성화 → 이름 입력 후 Enter
3. Container 테두리를 드래그하여 이동, 모서리를 드래그하여 크기 조절
4. Container 제목 우클릭 → **"스타일 편집..."** 으로 배경색·투명도·테두리 등 변경

### Rule 설정하기

1. 바탕화면 빈 곳에서 우클릭 → **"Rule 관리..."** 선택
2. **"+ 추가"** 버튼 → Rule 편집 다이얼로그 열림
3. 조건 설정 (파일명 패턴 / 확장자 / 파일 종류 / 날짜 등)
4. 대상 Container 선택 → 저장
5. 이후 바탕화면에 새 파일이 추가되면 **자동으로 해당 Container에 배치**

### Layout 저장/복원

- **저장**: 바탕화면 우클릭 → **"Layout 저장..."** → 이름 입력
- **복원**: 바탕화면 우클릭 → **"Layout 관리..."** → 원하는 Layout 선택 후 **"복원"**  
  > 복원 전 현재 상태가 자동으로 임시 저장됩니다.

### 시스템 트레이 메뉴

트레이 아이콘을 우클릭하면 다음 메뉴가 표시됩니다:

| 메뉴 항목 | 동작 |
|-----------|------|
| 설정 열기 | 전역 설정 (향후 업데이트 예정) |
| 감시 일시정지 / 재개 | 실시간 파일 감시 토글 |
| 로그 파일 열기 | 오늘의 로그 파일 또는 logs 폴더 열기 |
| 종료 | 프로그램 종료 |

---

## 데이터 저장 경로

모든 데이터는 `%APPDATA%\DesktopOrganizer\` 아래에 저장됩니다.

| 파일 | 경로 |
|------|------|
| 설정 파일 | `%APPDATA%\DesktopOrganizer\config.json` |
| 설정 백업 | `%APPDATA%\DesktopOrganizer\config.json.bak1~3` |
| Layout 파일 | `%APPDATA%\DesktopOrganizer\layouts\{id}.json` |
| 로그 파일 | `%APPDATA%\DesktopOrganizer\logs\desktop_organizer_YYYYMMDD.log` |

---

## 프로젝트 구조

```
DesktopOrganizer/
├── src/
│   └── DesktopOrganizer/
│       ├── App.xaml / App.xaml.cs       # 단일 인스턴스, 트레이 아이콘
│       ├── Models/                       # Container, Rule, IconInfo, Layout 등
│       ├── ViewModels/                   # MVVM ViewModel (ObservableObject, RelayCommand)
│       ├── Views/
│       │   ├── OverlayWindow.xaml        # 투명 전체화면 오버레이
│       │   ├── Controls/                 # ContainerControl 등 재사용 컨트롤
│       │   └── Dialogs/                  # RuleEditor, StyleEditor, LayoutManager 등
│       ├── Services/
│       │   ├── DesktopReaderService.cs   # F-001: 바탕화면 아이콘 읽기
│       │   ├── FileClassifierService.cs  # F-002: 확장자 기반 파일 분류
│       │   ├── ExclusionService.cs       # F-003: 시스템 아이콘 제외
│       │   ├── ContainerService.cs       # F-004~F-009: Container CRUD
│       │   ├── IconSortService.cs        # F-010: 아이콘 정렬 및 좌표 계산
│       │   ├── IconOrderService.cs       # F-011: 아이콘 순서 저장/복원
│       │   ├── RuleService.cs            # F-012~F-015: Rule CRUD 및 매칭
│       │   ├── DesktopWatcherService.cs  # F-016: FileSystemWatcher + 폴링 폴백
│       │   ├── AutoOrganizeService.cs    # F-017: 신규 파일 자동 배치
│       │   ├── SettingsService.cs        # F-018~F-019: JSON 저장/로드 + 백업
│       │   ├── LayoutService.cs          # F-020~F-021: Layout 저장/복원
│       │   └── LogService.cs             # F-022: 날짜별 로그 파일
│       └── Interop/                      # Windows Shell API P/Invoke
├── tests/
│   └── DesktopOrganizer.Tests/           # xUnit 단위 테스트 (218개)
└── docs/
    ├── 03_FunctionSpec.md                # 기능 명세서 (F-001~F-022)
    └── 04_DevPlan.md                     # 개발 Phase 계획서
```

---

## 개발 로드맵

| Phase | 내용 | 상태 |
|-------|------|------|
| Phase 0 | 프로젝트 기반 세팅 (Models, MVVM, 프로젝트 구조) | ✅ 완료 |
| Phase 1 | 데스크탑 읽기 & 파일 분류 (F-001~F-003) | ✅ 완료 |
| Phase 2 | 투명 오버레이 창 & 시스템 트레이 (F-004 기반) | ✅ 완료 |
| Phase 3 | Container 생성·편집·삭제 (F-004~F-006) | ✅ 완료 |
| Phase 4 | Container 이동·크기조절·스타일 (F-007~F-009) | ✅ 완료 |
| Phase 5 | 아이콘 자동 정렬 & 순서 저장 (F-010~F-011) | ✅ 완료 |
| Phase 6 | Rule 엔진 (F-012~F-015) | ✅ 완료 |
| Phase 7 | 실시간 감시 & 자동 정리 (F-016~F-017) | ✅ 완료 |
| Phase 8 | 설정 백업 로테이션 & Layout 저장/복원 (F-018~F-021) | ✅ 완료 |
| Phase 9 | 로그 서비스 & MVP 완성 (F-022) | ✅ 완료 |

**MVP F-001 ~ F-022 전체 구현 완료**

---

## 설계 원칙

| 원칙 | 내용 |
|------|------|
| **비파괴** | 아이콘 위치(좌표)만 변경하며 실제 파일은 건드리지 않습니다 |
| **비침습** | Windows 기본 시스템 아이콘(내 PC, 휴지통 등)은 자동 정렬 대상에서 제외합니다 |
| **Rule First Match** | 복수의 Rule이 매칭될 경우 우선순위가 가장 높은 Rule 하나만 적용합니다 |
| **지속성** | 모든 설정과 레이아웃은 저장·복원이 가능하며 백업이 유지됩니다 |
| **실시간** | 바탕화면 변경을 500ms 이내에 감지하고 처리합니다 |

---

## 라이선스

[MIT License](LICENSE)
