# MonitorOff

A tiny tray app that puts your display to sleep — on macOS and Windows.

Made for machines that run 24/7 as an agent server while doubling as a personal
PC: when you're done using it as a PC, double-click the icon and only the display
sleeps — background agent work keeps running.

## Download

Built files are published on the [Releases](https://github.com/hyundo32-copilot/MonitorOff/releases) page: `MonitorOff-macOS.zip` (`.app` inside) and `MonitorOff-Windows.zip` (`MonitorOff.exe` inside). They are built by GitHub Actions (`.github/workflows/release.yml`) whenever a `v*` tag is pushed.

To cut a release:

```bash
git tag v0.1.0
git push origin v0.1.0
```

Note: the macOS app is not signed or notarized, so after downloading it macOS may refuse to open it with "'MonitorOff' is damaged and can't be opened". Fix it once in Terminal:

```bash
xattr -cr ~/Downloads/MonitorOff.app
```

then double-click the app. (If you download the zip with `curl` instead of a browser, macOS does not quarantine it and it opens directly.)

## Usage

Identical on both platforms:

- **Double-click** the tray icon → display sleeps immediately (system keeps running)
- **Single-click** → menu with:
  - Sleep Display Now (`Cmd+D` / `Ctrl+D`)
  - Open at Login (toggle)
  - Mute sound when display sleeps (toggle, on by default)
  - Language submenu (System Default / 한국어 / English)
  - Quit (`Cmd+Q` / `Ctrl+Q`)

## Mute while the display is off

With **Mute sound when display sleeps** enabled (the default), the system output
is muted as soon as the display turns off and unmuted when it turns back on, so a
push notification can't make noise while the screen stays dark. It reacts to the
OS display-sleep/wake notification, so it applies whether MonitorOff or the
idle timer turned the display off, and moving the mouse to wake the screen
brings the sound back. The app remembers the previous state: if the output was
already muted before the display went off, it is left alone and not unmuted
afterwards — only a mute MonitorOff applied is undone (also when you turn the
toggle off, or quit the app). Failures to mute are logged silently, never shown.

**Known limitation:** this is a full system mute, so music and phone ringtones
are silenced too. If a call wakes the display, sound returns from that moment.

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
| Display sleep/wake detection | `NSWorkspace.screensDidSleep/WakeNotification` | `RegisterPowerSettingNotification(GUID_MONITOR_POWER_ON)` |
| Mute output | `osascript` (`set volume output muted`) | Core Audio `IAudioEndpointVolume::SetMute` |
| Settings (language, mute toggle) | `UserDefaults` | `%LOCALAPPDATA%\MonitorOff\settings.json` |
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
- `Sources/MonitorOffKit/` — SleepService, ClickDiscriminator, LoginItemService, DisplayMuteController, AppleScriptAudioMuter, L10n, LanguageService
- `Tests/MonitorOffTests/` — unit tests
- `Resources/Info.plist` — bundle config (`LSUIElement`, no Dock icon)
- `Windows/MonitorOff.Windows/` — Windows port (`Kit/` mirrors `Sources/MonitorOffKit/`)
- `Windows/MonitorOff.Windows/Kit/SystemAwakeService.cs` — Windows-only: keeps the system out of standby
- `scripts/make-app.sh`, `scripts/build-windows.ps1` — build scripts
