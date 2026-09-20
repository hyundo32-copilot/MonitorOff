# Monitor Off i18n — Design Spec

Date: 2026-09-20
Status: approved
Target: `/Volumes/data/project/monitor_off`
Base spec: `docs/superpowers/specs/2026-09-20-monitor-off-design.md`

## 1. Purpose

현재 한국어 하드코딩 5곳을 ko/en 이중 지원으로. 기본은 시스템 언어 자동, 메뉴에서 수동 덮어쓰기 가능.

## 2. Scope

- 대상 문자열 5개 (StatusBarController 하드코딩): 메뉴 3 (`지금 대기모드`, `로그인 시 자동 실행`, `종료`) + 알림 제목 2 (`대기모드 전환 실패`, `자동 실행 설정 실패`). 알림 본문의 error 보간은 언어중립이라 제외.
- 제외: 정식 `.strings` 파일, 시스템 설정 연동, 제3언어, 재실행 방식 전환.

## 3. Approach (선택: 1번)

1. **Swift 내장 번역 테이블 + LanguageService (채택)**: 재실행 없이 즉시 반영, 테스트 용이.
2. 정식 Localizable.strings + Bundle 교체: 표준이나 편법·복잡. 제외.
3. 시스템 자동만: 수동 전환 요구 미충족. 제외.

## 4. Components

- `Sources/MonitorOffKit/L10n.swift` (new): `enum L10nKey { sleepNow, launchAtLogin, quit, sleepFailedTitle, loginFailedTitle }`, `enum AppLanguage { system, korean, english }`, ko/en 테이블, `L10n.string(_ key: L10nKey, language: AppLanguage.Resolved) -> String`.
- `Sources/MonitorOffKit/LanguageService.swift` (new): `UserDefaults` key `appLanguage` 저장, `effectiveLanguage()` — `.system`이면 `Locale.preferredLanguages` 첫 항목이 `ko`로 시작하면 한국어, 아니면 영어. 주입 가능한 UserDefaults + Locale로 테스트.
- `Sources/MonitorOff/StatusBarController.swift` (modify): 하드코딩 5곳을 `L10n` 조회로 교체. 메뉴에 `Language / 언어` 서브메뉴 (radio: System Default / 한국어 / English) 추가. 선택 시 저장, 다음 메뉴 오픈부터 반영.

## 5. String Table

| key | ko | en |
|---|---|---|
| sleepNow | 지금 대기모드 | Sleep Display Now |
| launchAtLogin | 로그인 시 자동 실행 | Open at Login |
| quit | 종료 | Quit |
| sleepFailedTitle | 대기모드 전환 실패 | Failed to Sleep Display |
| loginFailedTitle | 자동 실행 설정 실패 | Failed to Set Login Item |

서브메뉴 라벨은 언어중립 고정 `Language / 언어`. 서브메뉴 항목 라벨은 `System Default`, `한국어`, `English` 고정 (언어 선택 UI 관례).

## 6. Data Flow

- 메뉴 오픈: `LanguageService.effectiveLanguage()` → `L10n`으로 5곳 렌더링.
- 언어 변경: 서브메뉴 선택 → `UserDefaults` 저장 → 현재 메뉴 닫힘 (사용자가 다시 열면 새 언어).
- 앱 시작: 저장값 없으면 `.system`.

## 7. Error Handling

기존 경로 그대로 (NSLog + NSAlert), 제목만 언어 적용. 저장 실패 시나리오 없음 (UserDefaults).

## 8. Testing

- 자동: 테이블 완전성 (모든 key × 양언어 non-empty), effectiveLanguage 3모드 (system+ko/system+en/명시), UserDefaults round-trip.
- 수동: 시스템 언어 ko/en + 메뉴 전환 3종, 메뉴·알림 문구 확인.
- 성공 기준: 재실행 없이 메뉴 재오픈 시 언어 반영, 기존 절전/자동실행 동작 불변.
