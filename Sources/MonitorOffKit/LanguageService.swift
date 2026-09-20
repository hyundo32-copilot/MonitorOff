import Foundation

public struct LanguageService {
    public static let storageKey = "appLanguage"

    private let defaults: UserDefaults
    private let preferredLanguages: [String]

    public init(defaults: UserDefaults = .standard, preferredLanguages: [String] = Locale.preferredLanguages) {
        self.defaults = defaults
        self.preferredLanguages = preferredLanguages
    }

    public var selection: AppLanguage {
        get {
            guard let raw = defaults.string(forKey: Self.storageKey) else { return .system }
            return AppLanguage(rawValue: raw) ?? .system
        }
    }

    public func setSelection(_ language: AppLanguage) {
        defaults.set(language.rawValue, forKey: Self.storageKey)
    }

    public func effectiveLanguage() -> AppLanguage.Resolved {
        switch selection {
        case .korean: return .korean
        case .english: return .english
        case .system:
            guard let first = preferredLanguages.first else { return .english }
            return first.hasPrefix("ko") ? .korean : .english
        }
    }
}
