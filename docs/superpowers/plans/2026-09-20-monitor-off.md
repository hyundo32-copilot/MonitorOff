# Monitor Off Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** macOS 메뉴바 상주 앱으로 더블클릭 시 디스플레이 즉시 절전, 단일클릭 시 메뉴 제공을 구현한다.

**Architecture:** SPM executable + AppKit `NSStatusItem` 단일 프로세스. 클릭 판별(`ClickDiscriminator` + 0.28s 타이머)은 `StatusBarController`가 담당하고, 절전은 `SleepService`가 `/usr/bin/pmset displaysleepnow`로 실행, 자동실행은 `LoginItemService`가 `SMAppService`로 감싼다.

**Tech Stack:** Swift 6.3 (Swift 5 language mode 허용), AppKit, ServiceManagement (SMAppService), XCTest, SPM executableTarget, macOS 13+.

**Spec:** `docs/superpowers/specs/2026-09-20-monitor-off-design.md`

## Global Constraints

- 디스플레이만 절전한다. 시스템 sleep이 아니다.
- `LSUIElement=true` (Dock 아이콘 없음).
- 최소 macOS 13 (SMAppService 때문).
- 절전 실행은 `/usr/bin/pmset displaysleepnow`, private API 금지.
- 더블클릭 판별 지연 0.28s를 스펙으로 허용한다. 단일클릭 메뉴가 0.28s 늦게 뜨는 것은 정상이다.
- 메뉴 문구 그대로: `지금 대기모드`, `로그인 시 자동 실행`, `종료`.
- 샌드박스 없음. 서명·공증은 범위 밖.
- 번들 ID 기본값: `com.monitoroff.app`. 앱 이름: `MonitorOff`.

---

## File Structure

스펙의 4파일 + 판별 로직 1파일 (테스트 가능성 때문에 분리, YAGNI 내에서 최소):

- Create: `Package.swift` — executableTarget `MonitorOff`, macOS 13 platform, ServiceManagement는 SDK 내장이라 별도 dependency 없음.
- Create: `Sources/MonitorOff/Main.swift` — `@main` 진입점, `NSApplication.shared`, `AppDelegate` 연결.
- Create: `Sources/MonitorOff/AppDelegate.swift` — `NSApplicationDelegate`, `StatusBarController` 소유, 앱 시작/종료 wiring.
- Create: `Sources/MonitorOff/StatusBarController.swift` — `NSStatusItem` 소유, 버튼 액션, 0.28s 타이머, `NSMenu` 표시, `SleepService`/`LoginItemService` 호출. `@MainActor`.
- Create: `Sources/MonitorOff/ClickDiscriminator.swift` — 순수 로직. 시간만으로 single/double 판정. AppKit 의존 없음.
- Create: `Sources/MonitorOff/SleepService.swift` — `Process` 실행 래퍼. Runner 주입으로 테스트 가능.
- Create: `Sources/MonitorOff/LoginItemService.swift` — `SMAppService.mainApp` 래퍼. Protocol + Mock으로 테스트 가능.
- Create: `Resources/Info.plist` — `LSUIElement=true`, `CFBundleIdentifier=com.monitoroff.app`, `CFBundleName=MonitorOff`.
- Create: `scripts/make-app.sh` — `swift build -c release` 결과를 `MonitorOff.app/Contents/{MacOS,Resources,Info.plist}` 구조로 감싸는 스크립트.
- Test: `Tests/MonitorOffTests/SleepServiceTests.swift`
- Test: `Tests/MonitorOffTests/ClickDiscriminatorTests.swift`
- Test: `Tests/MonitorOffTests/LoginItemServiceTests.swift`

---

### Task 1: SPM 스캐폴드 (빌드되는 메뉴바 껍데기)

**Files:**
- Create: `Package.swift`
- Create: `Sources/MonitorOff/Main.swift`
- Create: `Sources/MonitorOff/AppDelegate.swift`

**Interfaces:**
- Consumes: 없음 (첫 태스크).
- Produces: `AppDelegate` (later tasks: `StatusBarController`를 소유할 자리), 빌드 가능한 `MonitorOff` 실행파일.

- [ ] **Step 1: Package.swift 작성**

```swift
// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "MonitorOff",
    platforms: [.macOS(.v13)],
    targets: [
        .executableTarget(
            name: "MonitorOff",
            path: "Sources/MonitorOff"
        ),
        .testTarget(
            name: "MonitorOffTests",
            dependencies: ["MonitorOff"],
            path: "Tests/MonitorOffTests"
        ),
    ]
)
```

- [ ] **Step 2: Main.swift 작성**

```swift
import AppKit

@main
struct MonitorOffMain {
    static func main() {
        let app = NSApplication.shared
        let delegate = AppDelegate()
        app.delegate = delegate
        app.run()
    }
}
```

- [ ] **Step 3: AppDelegate 껍데기 작성**

```swift
import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Task 5에서 StatusBarController 연결. 지금은 껍데기만.
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        return false
    }
}
```

- [ ] **Step 4: 빌드 확인**

Run: `swift build`
Expected: `Build complete!` (경고는 허용, 에러 불허)

- [ ] **Step 5: Commit**

```bash
git add Package.swift Sources/MonitorOff/Main.swift Sources/MonitorOff/AppDelegate.swift
git commit -m "feat: add SPM scaffold with AppDelegate shell"
```

---

### Task 2: SleepService (pmset 실행)

**Files:**
- Create: `Sources/MonitorOff/SleepService.swift`
- Test: `Tests/MonitorOffTests/SleepServiceTests.swift`

**Interfaces:**
- Consumes: 없음.
- Produces: `SleepService.sleep() throws`, `ProcessRunner` typealias `(URL, [String]) throws -> Void`. `StatusBarController`는 `let sleep: () throws -> Void` 클로저로 이것을 소비한다.

- [ ] **Step 1: 실패하는 테스트 작성**

```swift
import XCTest
@testable import MonitorOff

final class SleepServiceTests: XCTestCase {
    func test_sleep_invokes_pmset_displaysleepnow() throws {
        var captured: (URL, [String])?
        let service = SleepService { url, args in
            captured = (url, args)
        }
        try service.sleep()
        XCTAssertEqual(captured?.0.path, "/usr/bin/pmset")
        XCTAssertEqual(captured?.1, ["displaysleepnow"])
    }

    func test_sleep_propagates_runner_error() {
        struct Boom: Error {}
        let service = SleepService { _, _ in throw Boom() }
        XCTAssertThrowsError(try service.sleep())
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

Run: `swift test --filter SleepServiceTests`
Expected: FAIL with "cannot find 'SleepService' in scope"

- [ ] **Step 3: 최소 구현**

```swift
import Foundation

struct SleepService {
    typealias ProcessRunner = (URL, [String]) throws -> Void

    private let runner: ProcessRunner

    init(runner: @escaping ProcessRunner = SleepService.defaultRunner) {
        self.runner = runner
    }

    func sleep() throws {
        try runner(URL(fileURLWithPath: "/usr/bin/pmset"), ["displaysleepnow"])
    }

    static func defaultRunner(url: URL, args: [String]) throws {
        let process = Process()
        process.executableURL = url
        process.arguments = args
        try process.run()
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `swift test --filter SleepServiceTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add Sources/MonitorOff/SleepService.swift Tests/MonitorOffTests/SleepServiceTests.swift
git commit -m "feat: add SleepService with pmset displaysleepnow"
```

---

### Task 3: ClickDiscriminator (single/double 순수 로직)

**Files:**
- Create: `Sources/MonitorOff/ClickDiscriminator.swift`
- Test: `Tests/MonitorOffTests/ClickDiscriminatorTests.swift`

**Interfaces:**
- Consumes: 없음.
- Produces: `enum ClickOutcome { case single, double }`, `struct ClickDiscriminator { mutating func click(at: Date) -> ClickOutcome?; mutating func timeout() -> ClickOutcome }`. `StatusBarController`가 그대로 사용한다. `interval`은 `0.28`.

- [ ] **Step 1: 실패하는 테스트 작성**

```swift
import XCTest
@testable import MonitorOff

final class ClickDiscriminatorTests: XCTestCase {
    func test_first_click_returns_nil_pending() {
        var d = ClickDiscriminator(interval: 0.28)
        XCTAssertNil(d.click(at: Date(timeIntervalSince1970: 100)))
    }

    func test_second_click_inside_interval_is_double() {
        var d = ClickDiscriminator(interval: 0.28)
        _ = d.click(at: Date(timeIntervalSince1970: 100))
        let outcome = d.click(at: Date(timeIntervalSince1970: 100.1))
        XCTAssertEqual(outcome, .double)
    }

    func test_timeout_after_single_click_is_single() {
        var d = ClickDiscriminator(interval: 0.28)
        _ = d.click(at: Date(timeIntervalSince1970: 100))
        XCTAssertEqual(d.timeout(), .single)
    }

    func test_second_click_outside_interval_resets_to_pending() {
        var d = ClickDiscriminator(interval: 0.28)
        _ = d.click(at: Date(timeIntervalSince1970: 100))
        let outcome = d.click(at: Date(timeIntervalSince1970: 101))
        XCTAssertNil(outcome)
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

Run: `swift test --filter ClickDiscriminatorTests`
Expected: FAIL with "cannot find 'ClickDiscriminator' in scope"

- [ ] **Step 3: 최소 구현**

```swift
import Foundation

enum ClickOutcome: Equatable {
    case single
    case double
}

struct ClickDiscriminator {
    let interval: TimeInterval
    private var pendingDate: Date?

    init(interval: TimeInterval = 0.28) {
        self.interval = interval
    }

    mutating func click(at date: Date) -> ClickOutcome? {
        guard let pending = pendingDate else {
            pendingDate = date
            return nil
        }
        if date.timeIntervalSince(pending) <= interval {
            pendingDate = nil
            return .double
        }
        pendingDate = date
        return nil
    }

    mutating func timeout() -> ClickOutcome {
        pendingDate = nil
        return .single
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `swift test --filter ClickDiscriminatorTests`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add Sources/MonitorOff/ClickDiscriminator.swift Tests/MonitorOffTests/ClickDiscriminatorTests.swift
git commit -m "feat: add ClickDiscriminator single/double logic"
```

---

### Task 4: LoginItemService (자동실행)

**Files:**
- Create: `Sources/MonitorOff/LoginItemService.swift`
- Test: `Tests/MonitorOffTests/LoginItemServiceTests.swift`

**Interfaces:**
- Consumes: 없음.
- Produces: `protocol AppServiceBackend { var isRegistered: Bool { get }; func register() throws; func unregister() throws }`, `struct LoginItemService { var isEnabled: Bool { get }; func setEnabled(_:) throws }`. `StatusBarController`는 `LoginItemService`를 소유하고 메뉴 체크마크에 `isEnabled`를 사용한다.

- [ ] **Step 1: 실패하는 테스트 작성**

```swift
import XCTest
@testable import MonitorOff

final class FakeBackend: AppServiceBackend {
    var isRegistered = false
    var registerCalls = 0
    var unregisterCalls = 0
    func register() throws { registerCalls += 1; isRegistered = true }
    func unregister() throws { unregisterCalls += 1; isRegistered = false }
}

final class LoginItemServiceTests: XCTestCase {
    func test_setEnabled_true_registers() throws {
        let backend = FakeBackend()
        let service = LoginItemService(backend: backend)
        try service.setEnabled(true)
        XCTAssertEqual(backend.registerCalls, 1)
        XCTAssertTrue(service.isEnabled)
    }

    func test_setEnabled_false_unregisters() throws {
        let backend = FakeBackend()
        backend.isRegistered = true
        let service = LoginItemService(backend: backend)
        try service.setEnabled(false)
        XCTAssertEqual(backend.unregisterCalls, 1)
        XCTAssertFalse(service.isEnabled)
    }
}
```

- [ ] **Step 2: 테스트 실행해 실패 확인**

Run: `swift test --filter LoginItemServiceTests`
Expected: FAIL with "cannot find 'LoginItemService' in scope"

- [ ] **Step 3: 최소 구현**

```swift
import Foundation
import ServiceManagement

protocol AppServiceBackend {
    var isRegistered: Bool { get }
    func register() throws
    func unregister() throws
}

struct SMAppServiceBackend: AppServiceBackend {
    var isRegistered: Bool {
        SMAppService.mainApp.status == .enabled
    }

    func register() throws {
        try SMAppService.mainApp.register()
    }

    func unregister() throws {
        try SMAppService.mainApp.unregister()
    }
}

struct LoginItemService {
    private let backend: AppServiceBackend

    init(backend: AppServiceBackend = SMAppServiceBackend()) {
        self.backend = backend
    }

    var isEnabled: Bool { backend.isRegistered }

    func setEnabled(_ enabled: Bool) throws {
        if enabled {
            try backend.register()
        } else {
            try backend.unregister()
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `swift test --filter LoginItemServiceTests`
Expected: PASS (2 tests). 참고: 실제 `SMAppServiceBackend`는 건드리지 않고 `FakeBackend`만 사용하므로 등록 상태가 바뀌지 않는다.

- [ ] **Step 5: Commit**

```bash
git add Sources/MonitorOff/LoginItemService.swift Tests/MonitorOffTests/LoginItemServiceTests.swift
git commit -m "feat: add LoginItemService with SMAppService backend"
```

---

### Task 5: StatusBarController (클릭 배선 + 메뉴 + 절전 연결)

**Files:**
- Modify: `Sources/MonitorOff/AppDelegate.swift`
- Create: `Sources/MonitorOff/StatusBarController.swift`

**Interfaces:**
- Consumes: `SleepService.sleep()`, `LoginItemService.isEnabled/setEnabled(_:)`, `ClickDiscriminator.click(at:)/timeout()`.
- Produces: 메뉴바에 상주하는 `StatusBarController`. 이후 Task 6이 `.app` 번들로 감싼다.

- [ ] **Step 1: StatusBarController 구현**

```swift
import AppKit

@MainActor
final class StatusBarController: NSObject {
    static let doubleClickInterval: TimeInterval = 0.28

    private let statusItem: NSStatusItem
    private let onSleep: () throws -> Void
    private let loginService: LoginItemService
    private var discriminator = ClickDiscriminator(interval: doubleClickInterval)
    private var pendingTimer: Timer?
    private var lastSleepAt = Date.distantPast

    init(
        statusItem: NSStatusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength),
        sleepService: SleepService = SleepService(),
        loginService: LoginItemService = LoginItemService()
    ) {
        self.statusItem = statusItem
        let service = sleepService
        self.onSleep = { try service.sleep() }
        self.loginService = loginService
        super.init()
        configureButton()
    }

    private func configureButton() {
        guard let button = statusItem.button else { return }
        button.image = NSImage(systemSymbolName: "moon.fill", accessibilityDescription: "Monitor Off")
        button.target = self
        button.action = #selector(handleStatusButtonAction(_:))
        button.sendAction(on: [.leftMouseDown, .leftMouseUp])
    }

    @objc private func handleStatusButtonAction(_ sender: Any?) {
        let now = Date()
        if let outcome = discriminator.click(at: now) {
            if outcome == .double {
                pendingTimer?.invalidate()
                pendingTimer = nil
                performSleep()
            }
            return
        }
        pendingTimer?.invalidate()
        pendingTimer = Timer.scheduledTimer(withTimeInterval: Self.doubleClickInterval, repeats: false) { [weak self] _ in
            guard let self else { return }
            Task { @MainActor in
                let result = self.discriminator.timeout()
                self.pendingTimer = nil
                if result == .single {
                    self.showMenu()
                }
            }
        }
    }

    private func performSleep() {
        if Date().timeIntervalSince(lastSleepAt) < 1.0 { return }
        lastSleepAt = Date()
        do {
            try onSleep()
        } catch {
            NSLog("MonitorOff sleep failed: \(error)")
            let alert = NSAlert()
            alert.messageText = "대기모드 전환 실패"
            alert.informativeText = "\(error)"
            alert.runModal()
        }
    }

    private func showMenu() {
        let menu = NSMenu()
        let sleepItem = NSMenuItem(title: "지금 대기모드", action: #selector(sleepMenuAction), keyEquivalent: "d")
        sleepItem.target = self
        menu.addItem(sleepItem)
        menu.addItem(.separator())
        let loginItem = NSMenuItem(title: "로그인 시 자동 실행", action: #selector(loginMenuAction), keyEquivalent: "")
        loginItem.target = self
        loginItem.state = loginService.isEnabled ? .on : .off
        menu.addItem(loginItem)
        menu.addItem(.separator())
        let quitItem = NSMenuItem(title: "종료", action: #selector(quitMenuAction), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)
        statusItem.button?.performClick(nil)
        statusItem.menu = menu
        statusItem.button?.performClick(nil)
        statusItem.menu = nil
        menu.popUp(positioning: nil, at: NSEvent.mouseLocation, in: nil)
    }

    @objc private func sleepMenuAction() {
        performSleep()
    }

    @objc private func loginMenuAction(_ sender: NSMenuItem) {
        do {
            try loginService.setEnabled(!loginService.isEnabled)
            sender.state = loginService.isEnabled ? .on : .off
        } catch {
            NSLog("MonitorOff login item failed: \(error)")
            let alert = NSAlert()
            alert.messageText = "자동 실행 설정 실패"
            alert.informativeText = "\(error)"
            alert.runModal()
        }
    }

    @objc private func quitMenuAction() {
        NSApplication.shared.terminate(nil)
    }
}
```

비고: `showMenu`의 `performClick/menu/popUp` 3줄은 `NSStatusItem`에 프로그래밍 방식으로 메뉴를 띄우는 최소 트릭이다. 더블클릭 판별 때문에 `statusItem.menu`를 상시 세팅하지 않는다.

- [ ] **Step 2: AppDelegate에 연결**

```swift
import AppKit

final class AppDelegate: NSObject, NSApplicationDelegate {
    private var bar: StatusBarController?

    func applicationDidFinishLaunching(_ notification: Notification) {
        bar = StatusBarController()
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        return false
    }
}
```

- [ ] **Step 3: 빌드 + 전체 테스트**

Run: `swift build`
Expected: `Build complete!`

Run: `swift test`
Expected: PASS (8 tests: Sleep 2 + Discriminator 4 + Login 2)

- [ ] **Step 4: 수동 확인 (이 태스크의 테스트 사이클)**

Run: `swift run MonitorOff &`
Expected: 메뉴바에 달 아이콘 표시, 단일클릭 0.28s 후 메뉴 (지금 대기모드/로그인 시 자동 실행/종료), 더블클릭 시 화면 꺼짐, `종료`로 프로세스 종료. 확인 후 `kill %1` 또는 메뉴 종료.

- [ ] **Step 5: Commit**

```bash
git add Sources/MonitorOff/StatusBarController.swift Sources/MonitorOff/AppDelegate.swift
git commit -m "feat: wire status bar double-click sleep and menu"
```

---

### Task 6: .app 번들 + LSUIElement + E2E

**Files:**
- Create: `Resources/Info.plist`
- Create: `scripts/make-app.sh`

**Interfaces:**
- Consumes: Task 1~5의 실행파일.
- Produces: `build/MonitorOff.app` (더블클릭 실행 가능한 메뉴바 앱).

- [ ] **Step 1: Info.plist 작성**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key>
    <string>com.monitoroff.app</string>
    <key>CFBundleName</key>
    <string>MonitorOff</string>
    <key>CFBundleExecutable</key>
    <string>MonitorOff</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>LSUIElement</key>
    <true/>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
</dict>
</plist>
```

- [ ] **Step 2: make-app.sh 작성**

```bash
#!/bin/bash
set -euo pipefail
swift build -c release
APP="build/MonitorOff.app/Contents"
mkdir -p "$APP/MacOS" "$APP/Resources"
cp ".build/release/MonitorOff" "$APP/MacOS/MonitorOff"
cp "Resources/Info.plist" "$APP/Info.plist"
chmod +x scripts/make-app.sh
echo "Built build/MonitorOff.app"
```

- [ ] **Step 3: 번들 빌드 확인**

Run: `bash scripts/make-app.sh && ls build/MonitorOff.app/Contents/MacOS/MonitorOff`
Expected: 바이너리 존재, `Built build/MonitorOff.app` 출력

- [ ] **Step 4: E2E 수동 검증**

Run: `open build/MonitorOff.app`
Expected: (1) Dock에 안 뜸 (2) 메뉴바 아이콘 보임 (3) 더블클릭 5회 모두 1초 내 화면 꺼짐 (4) 단일클릭 메뉴 3항목 동작 (5) 자동실행 토글 후 `시스템 설정 > 일반 > 로그인 항목`에 표시 (6) 종료 동작. 실패 항목은 이슈로 기록하고 Task 5로 회귀.

- [ ] **Step 5: Commit**

```bash
git add Resources/Info.plist scripts/make-app.sh
git commit -m "feat: add app bundle packaging with LSUIElement"
```

---

## Self-Review

- Spec coverage: 디스플레이 절전(Task 2), 더블클릭/단일클릭(Task 3+5), Swift 메뉴바 + LSUIElement(Task 1+6), 메뉴 3항목(Task 5), 자동실행(Task 4), 에러 처리(Task 5 alert + 격리), 테스트(Task 2~4 자동 + Task 5~6 수동) — 전 항목에 태스크 존재.
- Placeholder scan: `TBD/TODO/적절히/나중에` 없음. 모든 코드·명령·메뉴 문구·번들 ID를 구체값으로 기재.
- Type consistency: `SleepService.sleep() throws` → `StatusBarController.onSleep: () throws -> Void` 일치. `ClickDiscriminator.click(at:) -> ClickOutcome?` + `timeout() -> ClickOutcome` → Controller 사용처 일치. `LoginItemService.setEnabled(_:)/isEnabled` → 메뉴 체크마크 사용처 일치. `AppServiceBackend` Fake/Real 시그니처 일치.
