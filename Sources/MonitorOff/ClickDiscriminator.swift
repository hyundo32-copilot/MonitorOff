import Foundation

enum ClickOutcome: Equatable {
    case single
    case double
}

struct ClickDiscriminator {
    let interval: TimeInterval
    private var pendingDate: Date?

    init(interval: TimeInterval = 0.28) {
        self.interval = interval
    }

    mutating func click(at date: Date) -> ClickOutcome? {
        guard let pending = pendingDate else {
            pendingDate = date
            return nil
        }
        if date.timeIntervalSince(pending) <= interval {
            pendingDate = nil
            return .double
        }
        pendingDate = date
        return nil
    }

    mutating func timeout() -> ClickOutcome {
        pendingDate = nil
        return .single
    }
}
