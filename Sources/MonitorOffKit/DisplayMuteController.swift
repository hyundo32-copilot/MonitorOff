import Foundation

public protocol AudioMuting {
    func isMuted() throws -> Bool
    func setMuted(_ muted: Bool) throws
}

/// 화면이 꺼져 있는 동안 시스템 음소거를 거는 순수 상태 기계.
/// 우리가 건 음소거(`mutedByUs`)만 되돌린다. 오디오 입출력은 `AudioMuting` 으로 주입한다.
/// 오디오 호출 실패는 조용히 `logger` 로만 남긴다 (사용자 알림 없음).
public final class DisplayMuteController {
    private let audio: AudioMuting
    private let logger: (String) -> Void
    public private(set) var enabled: Bool
    public private(set) var mutedByUs = false

    public init(audio: AudioMuting, enabled: Bool = true, logger: @escaping (String) -> Void = { _ in }) {
        self.audio = audio
        self.enabled = enabled
        self.logger = logger
    }

    public func onDisplaySleep() {
        guard enabled else { return }
        // 이미 우리가 음소거해 둔 상태에서 중복 호출되면 그대로 둔다.
        guard !mutedByUs else { return }
        do {
            if try audio.isMuted() { return }  // 이미 음소거 → 건드리지 않음
            try audio.setMuted(true)
            mutedByUs = true
        } catch {
            logger("MonitorOff mute failed: \(error)")
        }
    }

    public func onDisplayWake() {
        restoreIfMutedByUs()
    }

    public func setEnabled(_ value: Bool) {
        enabled = value
        if !value { restoreIfMutedByUs() }
    }

    public func restoreOnExit() {
        restoreIfMutedByUs()
    }

    private func restoreIfMutedByUs() {
        guard mutedByUs else { return }
        do {
            try audio.setMuted(false)
        } catch {
            logger("MonitorOff unmute failed: \(error)")
        }
        mutedByUs = false
    }
}
