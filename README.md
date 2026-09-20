# MonitorOff

A tiny macOS menu bar app that puts your display to sleep.

Made for Macs that run 24/7 as an agent server while doubling as a personal PC:
when you're done using it as a PC, double-click the icon and only the display
sleeps — background agent work keeps running.

## Usage

- **Double-click** the menu bar icon → display sleeps immediately (system keeps running)
- **Single-click** → menu with:
  - Sleep Display Now (`Cmd+D`)
  - Open at Login (toggle)
  - Language submenu (System Default / 한국어 / English)
  - Quit (`Cmd+Q`)

## Requirements

- macOS 13+
- Swift toolchain (Xcode or Command Line Tools)

## Build & Run

```bash
bash scripts/make-app.sh
open build/MonitorOff.app
```

## Tests

```bash
swift test   # requires full Xcode (XCTest)
```

## Layout

- `Sources/MonitorOff/` — AppDelegate, StatusBarController (menu bar UI)
- `Sources/MonitorOffKit/` — SleepService, ClickDiscriminator, LoginItemService, L10n, LanguageService
- `Tests/MonitorOffTests/` — unit tests
- `Resources/Info.plist` — bundle config (`LSUIElement`, no Dock icon)
- `scripts/make-app.sh` — packages the `.app` bundle
