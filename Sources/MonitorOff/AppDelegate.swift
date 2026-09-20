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
