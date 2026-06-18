# 🗂️ Desktop Organizer

> Windows 바탕화면 아이콘을 **자동으로 분류·관리**하는 생산성 도구

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-0078D4?logo=windows)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

---

## 소개

바탕화면이 파일로 가득 차 있나요?  
Desktop Organizer는 사용자가 정의한 **Container(영역)** 와 **Rule(규칙)** 을 기반으로 바탕화면 아이콘을 자동으로 정리해줍니다.

- 실제 파일을 이동하거나 삭제하지 않습니다 — **아이콘 위치(좌표)만 변경**합니다.
- 새 파일이 바탕화면에 추가되면 **실시간으로 감지**하여 자동 배치합니다.
- 모든 설정은 저장되어 **프로그램 재시작 후에도 유지**됩니다.

---

## 주요 기능

| 기능 | 설명 |
|------|------|
| **Container 관리** | 바탕화면에 가상 영역을 생성·이동·크기조절·스타일 편집 |
| **Rule 기반 자동 정렬** | 파일명 패턴, 확장자, 파일 종류, 날짜 등 조건으로 자동 배치 |
| **실시간 감시** | 새 파일 생성 즉시 감지하여 매칭 Rule에 따라 자동 배치 |
| **아이콘 자동 정렬** | 이름, 확장자, 날짜 등 9가지 기준으로 Container 내 정렬 |
| **Layout 저장/복원** | 현재 바탕화면 구성을 스냅샷으로 저장하고 언제든 복원 |
| **설정 영속성** | 모든 Container·Rule·아이콘 순서를 JSON으로 저장 |

---

## 스크린샷

> 🚧 개발 진행 중 — 스크린샷은 UI 완성 후 추가 예정

---

## 기술 스택

| 구분 | 기술 |
|------|------|
| 언어 | C# 12 |
| 프레임워크 | .NET 8 LTS + WPF |
| UI 패턴 | MVVM |
| 설정 저장 | System.Text.Json |
| 파일 감시 | FileSystemWatcher |
| Win32 연동 | P/Invoke (Shell API) |
| 테스트 | xUnit |

---

## 시작하기

### 필수 요구사항

- Windows 10 / Windows 11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 설치 및 실행

```bash
# 저장소 클론
git clone https://github.com/qudgns200/DesktopOrganizer.git
cd DesktopOrganizer

# 빌드
dotnet build

# 실행
dotnet run --project src/DesktopOrganizer
```

### 테스트 실행

```bash
dotnet test
```

---

## 사용 방법

1. **Container 생성** — 바탕화면 빈 곳에서 우클릭 → "새 Container"
2. **Rule 추가** — 트레이 아이콘 → "설정" → Rule 탭 → "+" 버튼
3. **조건 설정** — 파일명 패턴, 확장자, 파일 종류 등 조건 지정 후 대상 Container 선택
4. **자동 정렬** — 이후 바탕화면에 추가되는 파일은 Rule에 따라 자동 배치

---

## 프로젝트 구조

```
DesktopOrganizer/
├── src/
│   └── DesktopOrganizer/
│       ├── Models/          # 데이터 모델 (Container, Rule, IconInfo 등)
│       ├── ViewModels/      # MVVM ViewModel
│       ├── Views/           # WPF XAML 화면
│       ├── Services/        # 비즈니스 로직 (분류, 정렬, 감시 등)
│       └── Interop/         # Windows Shell API P/Invoke
├── tests/
│   └── DesktopOrganizer.Tests/   # xUnit 단위 테스트
└── docs/
    ├── 03_FunctionSpec.md         # 기능 명세서
    └── 04_DevPlan.md              # 개발 계획 (Phase 0~9)
```

---

## 개발 로드맵

| Phase | 내용 | 상태 |
|-------|------|------|
| Phase 0 | 프로젝트 기반 세팅 (Models, MVVM, 프로젝트 구조) | ✅ 완료 |
| Phase 1 | 데스크탑 읽기 & 파일 분류 (F-001~F-003) | 🔲 예정 |
| Phase 2 | 투명 오버레이 창 & 시스템 트레이 | 🔲 예정 |
| Phase 3 | Container 생성·편집·삭제 (F-004~F-006) | 🔲 예정 |
| Phase 4 | Container 이동·크기조절·스타일 (F-007~F-009) | 🔲 예정 |
| Phase 5 | 아이콘 자동 정렬 & 순서 저장 (F-010~F-011) | 🔲 예정 |
| Phase 6 | Rule 엔진 (F-012~F-015) | 🔲 예정 |
| Phase 7 | 실시간 감시 & 자동 정리 (F-016~F-017) | 🔲 예정 |
| Phase 8 | 설정 저장/복원 & Layout (F-018~F-021) | 🔲 예정 |
| Phase 9 | 로깅 & 마무리 (F-022) | 🔲 예정 |

---

## 설계 원칙

- **비파괴** — 아이콘 위치(좌표)만 변경하며 실제 파일은 건드리지 않습니다.
- **비침습** — Windows 기본 시스템 아이콘(내 PC, 휴지통 등)은 자동 정렬 대상에서 제외합니다.
- **지속성** — 모든 설정과 레이아웃은 저장·복원이 가능합니다.
- **실시간** — 바탕화면 변경을 실시간으로 감지하고 처리합니다.
- **규칙 우선** — 모든 자동화는 사용자가 정의한 Rule에 의해서만 동작합니다.

---

## 라이선스

[MIT License](LICENSE)
