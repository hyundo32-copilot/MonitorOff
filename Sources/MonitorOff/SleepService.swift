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
