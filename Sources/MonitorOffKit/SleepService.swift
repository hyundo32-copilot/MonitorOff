import Foundation

public enum SleepError: Error {
    case nonZeroExit(status: Int32)
}

public struct SleepService {
    public typealias ProcessRunner = (URL, [String]) throws -> Void

    private let runner: ProcessRunner

    public init(runner: @escaping ProcessRunner = SleepService.defaultRunner) {
        self.runner = runner
    }

    public func sleep() throws {
        try runner(URL(fileURLWithPath: "/usr/bin/pmset"), ["displaysleepnow"])
    }

    public static func defaultRunner(url: URL, args: [String]) throws {
        let process = Process()
        process.executableURL = url
        process.arguments = args
        try process.run()
        process.waitUntilExit()
        guard process.terminationStatus == 0 else {
            throw SleepError.nonZeroExit(status: process.terminationStatus)
        }
    }
}
