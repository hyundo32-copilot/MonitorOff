import Foundation
import ServiceManagement

public protocol AppServiceBackend {
    var isRegistered: Bool { get }
    func register() throws
    func unregister() throws
}

public struct SMAppServiceBackend: AppServiceBackend {
    public init() {}

    public var isRegistered: Bool {
        SMAppService.mainApp.status == .enabled
    }

    public func register() throws {
        try SMAppService.mainApp.register()
    }

    public func unregister() throws {
        try SMAppService.mainApp.unregister()
    }
}

public struct LoginItemService {
    private let backend: AppServiceBackend

    public init(backend: AppServiceBackend = SMAppServiceBackend()) {
        self.backend = backend
    }

    public var isEnabled: Bool { backend.isRegistered }

    public func setEnabled(_ enabled: Bool) throws {
        if enabled {
            try backend.register()
        } else {
            try backend.unregister()
        }
    }
}
