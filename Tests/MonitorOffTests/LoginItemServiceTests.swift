import XCTest
@testable import MonitorOffKit

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
