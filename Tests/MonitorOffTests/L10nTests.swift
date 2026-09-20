import XCTest
@testable import MonitorOffKit

final class L10nTests: XCTestCase {
    func test_table_covers_all_keys_in_both_languages() {
        for key in L10nKey.allCases {
            let ko = L10n.string(key, language: .korean)
            let en = L10n.string(key, language: .english)
            XCTAssertFalse(ko.isEmpty, "\(key)")
            XCTAssertFalse(en.isEmpty, "\(key)")
            XCTAssertNotEqual(ko, en, "\(key)")
        }
    }

    func test_table_matches_spec_verbatim() {
        XCTAssertEqual(L10n.string(.sleepNow, language: .korean), "지금 대기모드")
        XCTAssertEqual(L10n.string(.sleepNow, language: .english), "Sleep Display Now")
        XCTAssertEqual(L10n.string(.launchAtLogin, language: .korean), "로그인 시 자동 실행")
        XCTAssertEqual(L10n.string(.launchAtLogin, language: .english), "Open at Login")
        XCTAssertEqual(L10n.string(.quit, language: .korean), "종료")
        XCTAssertEqual(L10n.string(.quit, language: .english), "Quit")
        XCTAssertEqual(L10n.string(.sleepFailedTitle, language: .korean), "대기모드 전환 실패")
        XCTAssertEqual(L10n.string(.sleepFailedTitle, language: .english), "Failed to Sleep Display")
        XCTAssertEqual(L10n.string(.loginFailedTitle, language: .korean), "자동 실행 설정 실패")
        XCTAssertEqual(L10n.string(.loginFailedTitle, language: .english), "Failed to Set Login Item")
    }

    private func service(languages: [String], stored: String? = nil) -> LanguageService {
        let defaults = UserDefaults(suiteName: #function)!
        defaults.removeObject(forKey: LanguageService.storageKey)
        if let stored { defaults.set(stored, forKey: LanguageService.storageKey) }
        return LanguageService(defaults: defaults, preferredLanguages: languages)
    }

    func test_system_korean_locale_resolves_korean() {
        XCTAssertEqual(service(languages: ["ko-KR", "en-US"]).effectiveLanguage(), .korean)
    }

    func test_system_english_locale_resolves_english() {
        XCTAssertEqual(service(languages: ["en-US"]).effectiveLanguage(), .english)
    }

    func test_system_empty_locale_defaults_english() {
        XCTAssertEqual(service(languages: []).effectiveLanguage(), .english)
    }

    func test_explicit_selection_overrides_locale() {
        XCTAssertEqual(service(languages: ["en-US"], stored: "korean").effectiveLanguage(), .korean)
        XCTAssertEqual(service(languages: ["ko-KR"], stored: "english").effectiveLanguage(), .english)
        XCTAssertEqual(service(languages: ["ko-KR"], stored: "system").effectiveLanguage(), .korean)
    }

    func test_setSelection_roundtrip() {
        let defaults = UserDefaults(suiteName: #function)!
        defaults.removeObject(forKey: LanguageService.storageKey)
        let service = LanguageService(defaults: defaults, preferredLanguages: ["en-US"])
        XCTAssertEqual(service.selection, .system)
        service.setSelection(.english)
        XCTAssertEqual(service.selection, .english)
        XCTAssertEqual(defaults.string(forKey: LanguageService.storageKey), "english")
    }
}
