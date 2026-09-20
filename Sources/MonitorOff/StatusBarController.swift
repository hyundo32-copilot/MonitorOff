import AppKit
import MonitorOffKit

@MainActor
final class StatusBarController: NSObject {
    static let doubleClickInterval: TimeInterval = 0.28

    private let statusItem: NSStatusItem
    private let onSleep: () throws -> Void
    private let loginService: LoginItemService
    private let languageService: LanguageService
    private var discriminator = ClickDiscriminator(interval: doubleClickInterval)
    private var pendingTimer: Timer?
    private var lastSleepAt = Date.distantPast

    init(
        statusItem: NSStatusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength),
        sleepService: SleepService = SleepService(),
        loginService: LoginItemService = LoginItemService(),
        languageService: LanguageService = LanguageService()
    ) {
        self.statusItem = statusItem
        let service = sleepService
        self.onSleep = { try service.sleep() }
        self.loginService = loginService
        self.languageService = languageService
        super.init()
        configureButton()
    }

    private func t(_ key: L10nKey) -> String {
        L10n.string(key, language: languageService.effectiveLanguage())
    }

    private func configureButton() {
        guard let button = statusItem.button else { return }
        button.image = NSImage(systemSymbolName: "moon.fill", accessibilityDescription: "Monitor Off")
        button.image?.isTemplate = true
        button.target = self
        button.action = #selector(handleStatusButtonAction(_:))
        button.sendAction(on: [.leftMouseUp])
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
                if result == .some(.single) {
                    self.showMenu()
                }
            }
        }
    }

    private func performSleep() {
        if Date().timeIntervalSince(lastSleepAt) < 1.0 { return }
        do {
            try onSleep()
            lastSleepAt = Date()
        } catch {
            NSLog("MonitorOff sleep failed: \(error)")
            let alert = NSAlert()
            alert.messageText = t(.sleepFailedTitle)
            alert.informativeText = "\(error)"
            alert.runModal()
        }
    }

    private func showMenu() {
        let menu = NSMenu()
        let sleepItem = NSMenuItem(title: t(.sleepNow), action: #selector(sleepMenuAction), keyEquivalent: "d")
        sleepItem.target = self
        menu.addItem(sleepItem)
        menu.addItem(.separator())
        let loginItem = NSMenuItem(title: t(.launchAtLogin), action: #selector(loginMenuAction), keyEquivalent: "")
        loginItem.target = self
        loginItem.state = loginService.isEnabled ? .on : .off
        menu.addItem(loginItem)
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
        menu.addItem(.separator())
        let quitItem = NSMenuItem(title: t(.quit), action: #selector(quitMenuAction), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)
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
            alert.messageText = t(.loginFailedTitle)
            alert.informativeText = "\(error)"
            alert.runModal()
        }
    }

    @objc private func languageMenuAction(_ sender: NSMenuItem) {
        guard let raw = sender.representedObject as? String,
              let language = AppLanguage(rawValue: raw) else { return }
        languageService.setSelection(language)
    }

    @objc private func quitMenuAction() {
        NSApplication.shared.terminate(nil)
    }
}
