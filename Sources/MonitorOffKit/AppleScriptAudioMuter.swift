import Foundation

public enum AudioMuteError: Error {
    case nonZeroExit(status: Int32)
    case unexpectedOutput(String)
}

/// `osascript` 로 시스템 출력 음소거를 읽고 쓴다.
/// `set volume` 은 Standard Additions 명령이라 접근성 권한이 필요 없다.
public struct AppleScriptAudioMuter: AudioMuting {
    /// (실행 파일, 인자) → 표준 출력 문자열
    public typealias ProcessRunner = (URL, [String]) throws -> String

    private static let osascript = URL(fileURLWithPath: "/usr/bin/osascript")
    private let runner: ProcessRunner

    public init(runner: @escaping ProcessRunner = AppleScriptAudioMuter.defaultRunner) {
        self.runner = runner
    }

    public func isMuted() throws -> Bool {
        let out = try runner(Self.osascript, ["-e", "output muted of (get volume settings)"])
        switch out.trimmingCharacters(in: .whitespacesAndNewlines) {
        case "true": return true
        case "false": return false
        default: throw AudioMuteError.unexpectedOutput(out)  // 예: "missing value" (출력 장치가 음소거 미지원)
        }
    }

    public func setMuted(_ muted: Bool) throws {
        _ = try runner(Self.osascript, ["-e", "set volume output muted \(muted ? "true" : "false")"])
    }

    public static func defaultRunner(url: URL, args: [String]) throws -> String {
        let process = Process()
        process.executableURL = url
        process.arguments = args
        let pipe = Pipe()
        process.standardOutput = pipe
        process.standardError = FileHandle.nullDevice
        try process.run()
        // waitUntilExit 전에 읽어야 파이프가 차서 막히는 일이 없다.
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        process.waitUntilExit()
        guard process.terminationStatus == 0 else {
            throw AudioMuteError.nonZeroExit(status: process.terminationStatus)
        }
        return String(decoding: data, as: UTF8.self)
    }
}
