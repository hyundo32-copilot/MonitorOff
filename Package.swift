// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "MonitorOff",
    platforms: [.macOS(.v13)],
    targets: [
        .target(
            name: "MonitorOffKit",
            path: "Sources/MonitorOffKit"
        ),
        .executableTarget(
            name: "MonitorOff",
            dependencies: ["MonitorOffKit"],
            path: "Sources/MonitorOff"
        ),
        .testTarget(
            name: "MonitorOffTests",
            dependencies: ["MonitorOffKit"],
            path: "Tests/MonitorOffTests"
        ),
    ]
)
