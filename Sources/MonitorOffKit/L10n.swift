import Foundation

public enum L10nKey: String, CaseIterable {
    case sleepNow
    case launchAtLogin
    case quit
    case sleepFailedTitle
    case loginFailedTitle
}

public enum AppLanguage: String {
    case system
    case korean
    case english

    public enum Resolved: Equatable {
        case korean
        case english
    }
}

public enum L10n {
    public static func string(_ key: L10nKey, language: AppLanguage.Resolved) -> String {
        switch (key, language) {
        case (.sleepNow, .korean): return "지금 대기모드"
        case (.sleepNow, .english): return "Sleep Display Now"
        case (.launchAtLogin, .korean): return "로그인 시 자동 실행"
        case (.launchAtLogin, .english): return "Open at Login"
        case (.quit, .korean): return "종료"
        case (.quit, .english): return "Quit"
        case (.sleepFailedTitle, .korean): return "대기모드 전환 실패"
        case (.sleepFailedTitle, .english): return "Failed to Sleep Display"
        case (.loginFailedTitle, .korean): return "자동 실행 설정 실패"
        case (.loginFailedTitle, .english): return "Failed to Set Login Item"
        }
    }
}
