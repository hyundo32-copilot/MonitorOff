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
