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
