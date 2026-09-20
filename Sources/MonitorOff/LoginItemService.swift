import Foundation
import ServiceManagement

protocol AppServiceBackend {
    var isRegistered: Bool { get }
    func register() throws
    func unregister() throws
}

struct SMAppServiceBackend: AppServiceBackend {
    var isRegistered: Bool {
        SMAppService.mainApp.status == .enabled
    }

    func register() throws {
        try SMAppService.mainApp.register()
    }

    func unregister() throws {
        try SMAppService.mainApp.unregister()
    }
}

struct LoginItemService {
    private let backend: AppServiceBackend

    init(backend: AppServiceBackend = SMAppServiceBackend()) {
        self.backend = backend
    }

    var isEnabled: Bool { backend.isRegistered }

    func setEnabled(_ enabled: Bool) throws {
        if enabled {
            try backend.register()
        } else {
            try backend.unregister()
        }
    }
}
