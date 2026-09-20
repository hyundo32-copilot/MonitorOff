import Foundation

public enum ClickOutcome: Equatable {
    case single
    case double
}

public struct ClickDiscriminator {
    public let interval: TimeInterval
    private var pendingDate: Date?

    public init(interval: TimeInterval = 0.28) {
        self.interval = interval
    }

    public mutating func click(at date: Date) -> ClickOutcome? {
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

    public mutating func timeout() -> ClickOutcome? {
        guard pendingDate != nil else { return nil }
        pendingDate = nil
        return .single
    }
}
