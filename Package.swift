// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "MonitorOff",
    platforms: [.macOS(.v13)],
    targets: [
        .executableTarget(
            name: "MonitorOff",
            path: "Sources/MonitorOff"
        ),
        .testTarget(
            name: "MonitorOffTests",
            dependencies: ["MonitorOff"],
            path: "Tests/MonitorOffTests"
        ),
    ]
)
