# Monitor Off — Design Spec

Date: 2026-09-20
Status: approved (sections validated one-by-one)
Target: `/Volumes/data/project/monitor_off`

## 1. Purpose

macOS 메뉴바에 상주하는 초경량 앱. 더블클릭하면 디스플레이만 즉시 절전, 단일클릭하면 메뉴 표시.

- 성공 기준: 더블클릭 5회 연속 실패 없음, 1초 내 화면 꺼짐
- 단일클릭 메뉴 표시는 약 0.28s 지연됨 (더블클릭 판별 비용, 스펙으로 허용)

## 2. Scope / Constraints

- 디스플레이만 절전 (`pmset displaysleepnow` 상당). 시스템 sleep 아님. 음악/다운로드는 유지.
- Swift + AppKit, `NSStatusItem`, `LSUIElement=true` (Dock 아이콘 없음)
- 타겟 macOS 13+ (`SMAppService` 사용 때문)
- 직접 실행/배포. 샌드박스 없음. 서명·공증은 후속 과제.
- YAGNI: 단축키, 카운트다운, 멀티모니터 개별 제어, 화면잠금 연동은 제외

## 3. Approach (선택: 1번)

1. **Swift + AppKit NSStatusItem 커스텀 클릭 판별 (채택)**: single/double을 직접 구분. 유일하게 요구 만족.
2. SwiftUI MenuBarExtra: 시스템이 메뉴를 관리해 더블클릭 가로채기 어려움. 제외.
3. xbar/Hammerspoon 래퍼: 빌드 빠르지만 더블클릭·UX·배포 제약. 제외.

## 4. Architecture

단일 타겟 메뉴바 앱. 파일 4개:

- `AppDelegate`
- `StatusBarController` (클릭 판별 + 메뉴)
- `SleepService` (pmset 실행)
- `LoginItemService` (자동실행)

절전은 private API 없이 `/usr/bin/pmset displaysleepnow`를 `Process`로 실행.

## 5. Components

- `StatusBarController`: `NSStatusItem.squareLength` + moon/display 템플릿 아이콘 (다크/라이트 대응). 버튼 액션에서 `clickCount` + 0.28s 타이머로 판별. 타임아웃 시 `popUpMenu`, 2차 클릭 시 타이머 취소 후 즉시 절전.
- `SleepService`: `Process(executableURL: /usr/bin/pmset, args: [displaysleepnow])` 실행 + 1초 디바운스 (중복 호출 무시).
- `MenuBuilder`: `지금 대기모드(Cmd+D) / 구분선 / 로그인 시 자동 실행(✓ 토글) / 구분선 / 종료(Cmd+Q)`.
- `LoginItemService`: `SMAppService.mainApp.{register,unregister,status}` 래핑. 시작 시 상태 읽어 체크마크 반영.

## 6. Data Flow

- 더블클릭: `1st click → 타이머 시작 → 2nd click(0.28s 내) → 타이머 취소 → SleepService.sleep()`
- 단일클릭: `1st click → 0.28s 타임아웃 → popUpMenu → [지금 대기모드] → SleepService.sleep()`
- 자동실행: `메뉴 토글 클릭 → LoginItemService.toggle() → 상태 변경 → 체크마크 갱신`

## 7. Error Handling

- `pmset` 실패: 앱 유지 + 1회 알림 + 콘솔 로그. 핵심(더블클릭 절전)과 메뉴/자동실행 격리.
- 자동실행 등록 실패: 토글 원복 + `SMAppService` 에러 문구 1줄 알럿.
- 슬립 중 클릭·중복 호출은 무시.

## 8. Testing

- 수동: (1) 더블클릭 절전, (2) 단일클릭 메뉴 지연 허용, (3) 메뉴 절전, (4) 자동실행 후 재로그인 상주, (5) 종료, (6) 다크/라이트 아이콘 가시성.
- 자동: `SleepService` 인자 생성, `LoginItemService` 상태 매핑 등 순수 로직만 XCTest 2~3개. 클릭 타이밍은 테스트 제외.

## 9. Open Items (구현 계획에서 확정)

- 번들 ID / 앱 이름 / 아이콘 에셋 최종
- 타이머 값 0.28s 고정 여부
- macOS 13 vs 14 최소버전 최종 확정
