# MonitorOff

A tiny tray app that puts your display to sleep — on macOS and Windows.

Made for machines that run 24/7 as an agent server while doubling as a personal
PC: when you're done using it as a PC, double-click the icon and only the display
sleeps — background agent work keeps running.

## Usage

Identical on both platforms:

- **Double-click** the tray icon → display sleeps immediately (system keeps running)
- **Single-click** → menu with:
  - Sleep Display Now (`Cmd+D` / `Ctrl+D`)
  - Open at Login (toggle)
  - Language submenu (System Default / 한국어 / English)
  - Quit (`Cmd+Q` / `Ctrl+Q`)

## Build & Run

### macOS

Requirements: macOS 13+, Swift toolchain (Xcode or Command Line Tools).

```bash
bash scripts/make-app.sh
open build/MonitorOff.app
```

### Windows

Requirements: Windows 10+, [.NET SDK 9](https://dotnet.microsoft.com/download/dotnet/9.0).

```powershell
pwsh scripts/build-windows.ps1
build\windows\MonitorOff.exe
```

Produces a single self-contained `MonitorOff.exe` (~48 MB) that runs on any
Windows 10+ x64 machine without a .NET install.

## How it works

| | macOS | Windows |
| --- | --- | --- |
| Sleep the display | `pmset displaysleepnow` | `SendMessageTimeout(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, -1)` |
| Keep the system awake | user power settings | `SetThreadExecutionState(ES_CONTINUOUS \| ES_SYSTEM_REQUIRED)` |
| Tray icon | `NSStatusItem` | `NotifyIcon` (icon drawn in code — see `MoonIcon.cs`) |
| Open at Login | `SMAppService` | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |
| Language prefs | `UserDefaults` | `%LOCALAPPDATA%\MonitorOff\settings.json` |
| No Dock / taskbar icon | `LSUIElement` | no top-level window is ever created |

Turning off the display never suspends the machine: processes, downloads and
agent jobs keep running.

On Windows the app additionally holds an `ES_SYSTEM_REQUIRED` power request for
as long as it is running, so the machine cannot drift into standby on its own
once the display is off (relevant on battery, where the default standby timer is
10 minutes). `ES_DISPLAY_REQUIRED` is deliberately not set, so the display still
turns off. Verify with `powercfg /requests` — you should see `MonitorOff.exe`
listed under `SYSTEM`. Quitting the app releases the request.

## Tests

```bash
swift test   # requires full Xcode (XCTest)
```

## Layout

- `Sources/MonitorOff/` — AppDelegate, StatusBarController (macOS menu bar UI)
- `Sources/MonitorOffKit/` — SleepService, ClickDiscriminator, LoginItemService, L10n, LanguageService
- `Tests/MonitorOffTests/` — unit tests
- `Resources/Info.plist` — bundle config (`LSUIElement`, no Dock icon)
- `Windows/MonitorOff.Windows/` — Windows port (`Kit/` mirrors `Sources/MonitorOffKit/`)
- `Windows/MonitorOff.Windows/Kit/SystemAwakeService.cs` — Windows-only: keeps the system out of standby
- `scripts/make-app.sh`, `scripts/build-windows.ps1` — build scripts
