# Monitor Off i18n (ko/en) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 메뉴 3개와 알림 제목 2개를 시스템 언어 자동 + 메뉴 수동 전환으로 ko/en 지원한다.

**Architecture:** `MonitorOffKit`에 순수 로직 2파일(`L10n` 번역 테이블, `LanguageService` 선택·판별)을 추가하고, `StatusBarController`의 하드코딩 5곳을 조회로 교체한 뒤 `Language / 언어` 서브메뉴를 달아 재실행 없이 다음 메뉴 오픈부터 반영한다.

**Tech Stack:** Swift, AppKit (메뉴), Foundation (UserDefaults, Locale), XCTest.

**Spec:** `docs/superpowers/specs/2026-09-20-monitor-off-i18n-design.md`

## Global Constraints

- 대상 문자열은 정확히 5개이며 번역문은 아래 표 verbatim이다.
- `UserDefaults` 저장 키는 `appLanguage`이다.
- 서브메뉴 라벨은 `Language / 언어` 고정, 항목 라벨은 `System Default`, `한국어`, `English` 고정이다.
- 언어 변경은 재실행 없이 다음 메뉴 오픈부터 반영된다.
- `.strings` 파일·Bundle 교체·제3언어는 범위 밖이다.
- 최소 macOS 13.

**String table (verbatim):**

| key | ko | en |
|---|---|---|
| sleepNow | 지금 대기모드 | Sleep Display Now |
| launchAtLogin | 로그인 시 자동 실행 | Open at Login |
| quit | 종료 | Quit |
| sleepFailedTitle | 대기모드 전환 실패 | Failed to Sleep Display |
| loginFailedTitle | 자동 실행 설정 실패 | Failed to Set Login Item |

---

## File Structure

- Create: `Sources/MonitorOffKit/L10n.swift` — `L10nKey` 5케이스, `AppLanguage` + nested `Resolved`, ko/en 테이블, `L10n.string`. AppKit 의존 없음.
- Create: `Sources/MonitorOffKit/LanguageService.swift` — 선택 저장/조회 + `effectiveLanguage()`. AppKit 의존 없음.
- Modify: `Sources/MonitorOff/StatusBarController.swift` — 하드코딩 5곳 교체 + 서브메뉴 추가. 기존 클릭 판별·절전·자동실행 로직은 손대지 않는다.
- Test: `Tests/MonitorOffTests/L10nTests.swift` — 테이블 완전성 + effectiveLanguage + 저장 round-trip.

---

### Task 1: L10n + LanguageService + tests

**Files:**
- Create: `Sources/MonitorOffKit/L10n.swift`
- Create: `Sources/MonitorOffKit/LanguageService.swift`
- Test: `Tests/MonitorOffTests/L10nTests.swift`

**Interfaces:**
- Consumes: 없음.
- Produces: `L10nKey` (5 cases), `AppLanguage` (`system/korean/english` + nested `Resolved`), `L10n.string(_:language:) -> String`, `LanguageService` (`selection`, `setSelection(_:)`, `effectiveLanguage()`). Task 2가 그대로 소비한다.

- [ ] **Step 1: Write the failing tests**

```swift
import XCTest
@testable import MonitorOffKit

final class L10nTests: XCTestCase {
    func test_table_covers_all_keys_in_both_languages() {
        for key in L10nKey.allCases {
            let ko = L10n.string(key, language: .korean)
            let en = L10n.string(key, language: .english)
            XCTAssertFalse(ko.isEmpty, "\(key)")
            XCTAssertFalse(en.isEmpty, "\(key)")
            XCTAssertNotEqual(ko, en, "\(key)")
        }
    }

    func test_table_matches_spec_verbatim() {
        XCTAssertEqual(L10n.string(.sleepNow, language: .korean), "지금 대기모드")
        XCTAssertEqual(L10n.string(.sleepNow, language: .english), "Sleep Display Now")
        XCTAssertEqual(L10n.string(.launchAtLogin, language: .korean), "로그인 시 자동 실행")
        XCTAssertEqual(L10n.string(.launchAtLogin, language: .english), "Open at Login")
        XCTAssertEqual(L10n.string(.quit, language: .korean), "종료")
        XCTAssertEqual(L10n.string(.quit, language: .english), "Quit")
        XCTAssertEqual(L10n.string(.sleepFailedTitle, language: .korean), "대기모드 전환 실패")
        XCTAssertEqual(L10n.string(.sleepFailedTitle, language: .english), "Failed to Sleep Display")
        XCTAssertEqual(L10n.string(.loginFailedTitle, language: .korean), "자동 실행 설정 실패")
        XCTAssertEqual(L10n.string(.loginFailedTitle, language: .english), "Failed to Set Login Item")
    }

    private func service(languages: [String], stored: String? = nil) -> LanguageService {
        let defaults = UserDefaults(suiteName: #function)!
        defaults.removeObject(forKey: LanguageService.storageKey)
        if let stored { defaults.set(stored, forKey: LanguageService.storageKey) }
        return LanguageService(defaults: defaults, preferredLanguages: languages)
    }

    func test_system_korean_locale_resolves_korean() {
        XCTAssertEqual(service(languages: ["ko-KR", "en-US"]).effectiveLanguage(), .korean)
    }

    func test_system_english_locale_resolves_english() {
        XCTAssertEqual(service(languages: ["en-US"]).effectiveLanguage(), .english)
    }

    func test_system_empty_locale_defaults_english() {
        XCTAssertEqual(service(languages: []).effectiveLanguage(), .english)
    }

    func test_explicit_selection_overrides_locale() {
        XCTAssertEqual(service(languages: ["en-US"], stored: "korean").effectiveLanguage(), .korean)
        XCTAssertEqual(service(languages: ["ko-KR"], stored: "english").effectiveLanguage(), .english)
        XCTAssertEqual(service(languages: ["ko-KR"], stored: "system").effectiveLanguage(), .korean)
    }

    func test_setSelection_roundtrip() {
        let defaults = UserDefaults(suiteName: #function)!
        defaults.removeObject(forKey: LanguageService.storageKey)
        let service = LanguageService(defaults: defaults, preferredLanguages: ["en-US"])
        XCTAssertEqual(service.selection, .system)
        service.setSelection(.english)
        XCTAssertEqual(service.selection, .english)
        XCTAssertEqual(defaults.string(forKey: LanguageService.storageKey), "english")
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `swift test --filter L10nTests`
Expected: FAIL with "cannot find 'L10nKey' in scope" (이 checkout이 CLT-only라 `no such module 'XCTest'`가 나오면 그 출력 + `swift build` 실패 지점을 RED 증거로 기록하고 harness 대체는 Task 1 계획에 따라 진행)

- [ ] **Step 3: Write minimal implementation (L10n.swift)**

```swift
import Foundation

public enum L10nKey: String, CaseIterable {
    case sleepNow
    case launchAtLogin
    case quit
    case sleepFailedTitle
    case loginFailedTitle
}

public enum AppLanguage: String {
    case system
    case korean
    case english

    public enum Resolved: Equatable {
        case korean
        case english
    }
}

public enum L10n {
    public static func string(_ key: L10nKey, language: AppLanguage.Resolved) -> String {
        switch (key, language) {
        case (.sleepNow, .korean): return "지금 대기모드"
        case (.sleepNow, .english): return "Sleep Display Now"
        case (.launchAtLogin, .korean): return "로그인 시 자동 실행"
        case (.launchAtLogin, .english): return "Open at Login"
        case (.quit, .korean): return "종료"
        case (.quit, .english): return "Quit"
        case (.sleepFailedTitle, .korean): return "대기모드 전환 실패"
        case (.sleepFailedTitle, .english): return "Failed to Sleep Display"
        case (.loginFailedTitle, .korean): return "자동 실행 설정 실패"
        case (.loginFailedTitle, .english): return "Failed to Set Login Item"
        }
    }
}
```

- [ ] **Step 4: Write minimal implementation (LanguageService.swift)**

```swift
import Foundation

public struct LanguageService {
    public static let storageKey = "appLanguage"

    private let defaults: UserDefaults
    private let preferredLanguages: [String]

    public init(defaults: UserDefaults = .standard, preferredLanguages: [String] = Locale.preferredLanguages) {
        self.defaults = defaults
        self.preferredLanguages = preferredLanguages
    }

    public var selection: AppLanguage {
        get {
            guard let raw = defaults.string(forKey: Self.storageKey) else { return .system }
            return AppLanguage(rawValue: raw) ?? .system
        }
    }

    public func setSelection(_ language: AppLanguage) {
        defaults.set(language.rawValue, forKey: Self.storageKey)
    }

    public func effectiveLanguage() -> AppLanguage.Resolved {
        switch selection {
        case .korean: return .korean
        case .english: return .english
        case .system:
            guard let first = preferredLanguages.first else { return .english }
            return first.hasPrefix("ko") ? .korean : .english
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `swift test --filter L10nTests`
Expected: PASS (7 tests). CLT-only면 `swift build` 통과 + 임시 swiftc harness로 7케이스 재현 후 harness 삭제·공개.

- [ ] **Step 6: Commit**

```bash
git add Sources/MonitorOffKit/L10n.swift Sources/MonitorOffKit/LanguageService.swift Tests/MonitorOffTests/L10nTests.swift
git commit -m "feat: add ko/en L10n table and LanguageService"
```

---

### Task 2: StatusBarController wiring (menu + alerts)

**Files:**
- Modify: `Sources/MonitorOff/StatusBarController.swift` (5 string sites + submenu + helper + init param; approx lines 58-112 of current file)

**Interfaces:**
- Consumes: `L10n.string(_:language:)`, `LanguageService.selection/setSelection(_:)/effectiveLanguage()`, `AppLanguage` cases. `import MonitorOffKit`는 이미 있다.
- Produces: 언어 적용된 메뉴바. 이후 수동 E2E가 검증한다.

- [ ] **Step 1: Add languageService property, init param, and helper**

Current init signature:

```swift
init(
    statusItem: NSStatusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength),
    sleepService: SleepService = SleepService(),
    loginService: LoginItemService = LoginItemService()
)
```

Change to (added lines only, rest untouched):

```swift
private let languageService: LanguageService

init(
    statusItem: NSStatusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength),
    sleepService: SleepService = SleepService(),
    loginService: LoginItemService = LoginItemService(),
    languageService: LanguageService = LanguageService()
) {
    ...
    self.languageService = languageService
    ...
}

private func t(_ key: L10nKey) -> String {
    L10n.string(key, language: languageService.effectiveLanguage())
}
```

- [ ] **Step 2: Replace the 5 hardcoded strings with t(...)**

Exact replacements (current file):

```swift
alert.messageText = "대기모드 전환 실패"
```
→
```swift
alert.messageText = t(.sleepFailedTitle)
```

```swift
let sleepItem = NSMenuItem(title: "지금 대기모드", action: #selector(sleepMenuAction), keyEquivalent: "d")
```
→
```swift
let sleepItem = NSMenuItem(title: t(.sleepNow), action: #selector(sleepMenuAction), keyEquivalent: "d")
```

```swift
let loginItem = NSMenuItem(title: "로그인 시 자동 실행", action: #selector(loginMenuAction), keyEquivalent: "")
```
→
```swift
let loginItem = NSMenuItem(title: t(.launchAtLogin), action: #selector(loginMenuAction), keyEquivalent: "")
```

```swift
let quitItem = NSMenuItem(title: "종료", action: #selector(quitMenuAction), keyEquivalent: "q")
```
→
```swift
let quitItem = NSMenuItem(title: t(.quit), action: #selector(quitMenuAction), keyEquivalent: "q")
```

```swift
alert.messageText = "자동 실행 설정 실패"
```
→
```swift
alert.messageText = t(.loginFailedTitle)
```

- [ ] **Step 3: Add the Language submenu in showMenu()**

Insert after the login item block, before `menu.addItem(.separator())` preceding quit:

```swift
let languageItem = NSMenuItem(title: "Language / 언어", action: nil, keyEquivalent: "")
let languageMenu = NSMenu()
let current = languageService.selection
let options: [(AppLanguage, String)] = [(.system, "System Default"), (.korean, "한국어"), (.english, "English")]
for option in options {
    let item = NSMenuItem(title: option.1, action: #selector(languageMenuAction(_:)), keyEquivalent: "")
    item.target = self
    item.state = (option.0 == current) ? .on : .off
    item.representedObject = option.0.rawValue
    languageMenu.addItem(item)
}
languageItem.submenu = languageMenu
menu.addItem(languageItem)
```

And add the action method next to the other menu actions:

```swift
@objc private func languageMenuAction(_ sender: NSMenuItem) {
    guard let raw = sender.representedObject as? String,
          let language = AppLanguage(rawValue: raw) else { return }
    languageService.setSelection(language)
}
```

- [ ] **Step 4: Build + full test run**

Run: `swift build`
Expected: `Build complete!`

Run: `swift test`
Expected: PASS (15 tests: 기존 8 + 신규 7). CLT-only면 build 통과 + 미실행을 공개하고 Task 1의 harness 증거를 인용.

- [ ] **Step 5: Commit**

```bash
git add Sources/MonitorOff/StatusBarController.swift
git commit -m "feat: wire ko/en language menu into status bar"
```

---

## Self-Review

- Spec coverage: 5문자열 표(Task 1 verbatim 테스트 + Task 2 교체), UserDefaults 키(Task 1 storageKey + round-trip), 서브메뉴 라벨·항목(Task 2 verbatim), 재실행 없음(저장만 하고 rebuild 없음 — Task 2 action이 메뉴 재생성 없이 종료되므로 다음 오픈부터 반영), .strings 제외(파일 없음). 전 항목에 태스크 존재.
- Placeholder scan: `TBD/TODO/적절히/유사` 없음. 전 코드·명령·문구 구체값.
- Type consistency: `L10n.string(_:language:)` 시그니처가 Task 1 정의와 Task 2 `t(_:)` 호출 일치. `AppLanguage.Resolved`가 Task 1 enum과 테스트의 `.korean` 리터럴 일치(Equatable 채택). `LanguageService.selection/setSelection/effectiveLanguage`가 Task 1 정의와 Task 2 소비 일치. `representedObject` String↔AppLanguage round-trip 일치.
