import XCTest
@testable import MonitorOffKit

private final class FakeAudio: AudioMuting {
    var muted: Bool
    var setCalls: [Bool] = []
    var failSet = false
    init(muted: Bool = false) { self.muted = muted }
    func isMuted() throws -> Bool { muted }
    func setMuted(_ value: Bool) throws {
        setCalls.append(value)
        if failSet { throw AudioMuteError.nonZeroExit(status: 1) }
        muted = value
    }
}

final class DisplayMuteControllerTests: XCTestCase {
    func test_sleep_mutes_and_wake_restores() {
        let audio = FakeAudio()
        let c = DisplayMuteController(audio: audio)
        c.onDisplaySleep()
        XCTAssertTrue(audio.muted)
        XCTAssertTrue(c.mutedByUs)
        c.onDisplayWake()
        XCTAssertFalse(audio.muted)
        XCTAssertFalse(c.mutedByUs)
    }

    func test_already_muted_is_left_alone_on_sleep_and_wake() {
        let audio = FakeAudio(muted: true)
        let c = DisplayMuteController(audio: audio)
        c.onDisplaySleep()
        XCTAssertFalse(c.mutedByUs)
        c.onDisplayWake()
        XCTAssertTrue(audio.muted)
        XCTAssertTrue(audio.setCalls.isEmpty)
    }

    func test_disabled_does_nothing() {
        let audio = FakeAudio()
        let c = DisplayMuteController(audio: audio, enabled: false)
        c.onDisplaySleep()
        XCTAssertFalse(audio.muted)
        XCTAssertTrue(audio.setCalls.isEmpty)
    }

    func test_disabling_while_mutedByUs_restores_immediately() {
        let audio = FakeAudio()
        let c = DisplayMuteController(audio: audio)
        c.onDisplaySleep()
        c.setEnabled(false)
        XCTAssertFalse(audio.muted)
        XCTAssertFalse(c.mutedByUs)
    }

    func test_disabling_when_not_mutedByUs_does_not_touch_audio() {
        let audio = FakeAudio(muted: true)
        let c = DisplayMuteController(audio: audio)
        c.setEnabled(false)
        XCTAssertTrue(audio.muted)
        XCTAssertTrue(audio.setCalls.isEmpty)
    }

    func test_wake_without_mutedByUs_does_not_unmute() {
        let audio = FakeAudio(muted: true)  // 사용자가 직접 음소거
        let c = DisplayMuteController(audio: audio)
        c.onDisplayWake()
        XCTAssertTrue(audio.muted)
        XCTAssertTrue(audio.setCalls.isEmpty)
    }

    func test_restoreOnExit_restores_only_when_mutedByUs() {
        let audio = FakeAudio()
        let c = DisplayMuteController(audio: audio)
        c.onDisplaySleep()
        c.restoreOnExit()
        XCTAssertFalse(audio.muted)

        let other = FakeAudio(muted: true)
        let c2 = DisplayMuteController(audio: other)
        c2.onDisplaySleep()
        c2.restoreOnExit()
        XCTAssertTrue(other.muted)
    }

    func test_mute_failure_is_logged_and_does_not_set_mutedByUs() {
        let audio = FakeAudio()
        audio.failSet = true
        var logs: [String] = []
        let c = DisplayMuteController(audio: audio, logger: { logs.append($0) })
        c.onDisplaySleep()
        XCTAssertFalse(c.mutedByUs)
        XCTAssertEqual(logs.count, 1)
    }

    func test_repeated_sleep_does_not_lose_mutedByUs() {
        let audio = FakeAudio()
        let c = DisplayMuteController(audio: audio)
        c.onDisplaySleep()
        c.onDisplaySleep()  // 이미 음소거(우리가 건 것)여도 mutedByUs 유지
        XCTAssertTrue(c.mutedByUs)
        c.onDisplayWake()
        XCTAssertFalse(audio.muted)
    }
}

final class AppleScriptAudioMuterTests: XCTestCase {
    func test_isMuted_parses_true_false_and_rejects_other() throws {
        var out = "true\n"
        let m = AppleScriptAudioMuter { _, _ in out }
        XCTAssertTrue(try m.isMuted())
        out = "false\n"
        XCTAssertFalse(try m.isMuted())
        out = "missing value\n"
        XCTAssertThrowsError(try m.isMuted())
    }

    func test_commands_use_osascript() throws {
        var calls: [(String, [String])] = []
        let m = AppleScriptAudioMuter { url, args in calls.append((url.path, args)); return "" }
        try m.setMuted(true)
        try m.setMuted(false)
        XCTAssertEqual(calls[0].0, "/usr/bin/osascript")
        XCTAssertEqual(calls[0].1, ["-e", "set volume output muted true"])
        XCTAssertEqual(calls[1].1, ["-e", "set volume output muted false"])
    }
}
