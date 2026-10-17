# Underlit (kuilef fork)

A Windows tray app for brightness and color temperature. Dim below a monitor's
hardware minimum, adjust warmth with hotkeys, and schedule evening/morning changes.

This fork adds reliable dimming for secondary displays without hardware brightness
control, lower memory use and a choice of Windows build profiles. The original project is
[DietervanDortmont/underlit](https://github.com/DietervanDortmont/underlit).

## Install

Download **UnderlitSetup-0.7.3.exe** from the
[latest release](https://github.com/kuilef/underlit/releases/latest). It includes
.NET and installs Underlit with a Start menu shortcut and a Windows uninstall
entry. Start with Windows and a desktop shortcut are optional. Quit any running
Underlit, including a portable copy, before installing. Existing settings in
`%APPDATA%\Underlit` are retained.

Release downloads:

| File | Use |
| --- | --- |
| `UnderlitSetup-0.7.3.exe` | Recommended installer; includes .NET Desktop Runtime. |
| `Underlit-0.7.3-portable-win-x64.zip` | Standalone portable EXE; extract and run without installing. |
| `Underlit-0.7.3-desktop-runtime-win-x64.zip` | Smaller portable EXE; requires .NET 8 Desktop Runtime x64 already installed. |
| `SHA256SUMS.txt` | SHA-256 checksums for the downloads above. |

Windows 10 (2004+) or Windows 11, x64. The installer supports per-user installation
and removal through Windows Settings. These community builds are not code-signed.
Uninstall also removes Underlit's startup registry entry, scheduled autostart task
and legacy Startup shortcut, while keeping the settings file.

По-русски: для обычной установки скачайте `UnderlitSetup-0.7.3.exe` из
[Releases](https://github.com/kuilef/underlit/releases/latest). Отдельно ставить .NET
не нужно. Перед установкой завершите старую копию через значок в трее → Quit.

## Changes from DietervanDortmont/underlit:main

This fork is based on upstream `main` at
[`1483629`](https://github.com/DietervanDortmont/underlit/tree/1483629718dc07b0788bbbc2b35835b2c120034d)
(v0.7.2), checked on 2026-10-18. Upstream provides the application, UI, warmth
schedule and Hue integration. This fork changes display handling, resource lifetimes
and packaging:

| Area | Upstream at that commit | This fork: change and purpose |
| --- | --- | --- |
| Monitor without WMI/DDC brightness | Software layers remain neutral above zero. | That monitor dims with an overlay across the positive slider range as well. Its backlight is not changed. |
| Hardware or gamma write failure | Hardware failures do not activate a per-monitor fallback; rejected gamma writes are ignored. | Failed hardware writes activate software dimming; rejected gamma brightness is compensated by that monitor's overlay. |
| Software brightness response | Identical gamma is written to each display on every ramp tick. | Identical successful writes are cached for up to 500 ms, with invalidation after display changes and resume. |
| Reconnects and native brightness changes | Reconnects may retain stale brightness cache; WMI sync assumes the primary display owns WMI. | Reconnects reapply the target. Native WMI changes are attributed to the internal panel and propagated to other displays. |
| Background WMI polling | Polls laptop brightness even on desktops without WMI brightness support. | Polls only while a display has a WMI brightness path, avoiding repeated unsupported queries and exception logging. Rechecks support on display changes and resume; temporary probe errors preserve known laptop support. |
| WMI object lifetime | Query results and returned management objects are not explicitly disposed. | Disposes both immediately after use to release their WMI/COM resources. |
| Overlay memory | Creates a full-screen transparent WPF window for every monitor, including monitors needing no overlay. | Creates windows only while their dimming opacity is positive and closes them at zero, avoiding unused full-screen rendering surfaces. |
| Liquid Glass capture memory | Stopping capture retains the last full-monitor frame and staging texture. | Releases both when the OSD capture stops, with synchronization against frame processing; keeps the graphics device for quick reopening. |
| Publish profiles | ReadyToRun is enabled for a self-contained build. | Disables ReadyToRun to reduce EXE size. `PortableCompact` compresses bundled libraries for smaller downloads; `DesktopRuntime` uses an installed runtime. `PortableLowMemory` keeps standalone libraries uncompressed to reduce RAM use at the cost of a larger EXE. |
| Verification | No engine/display/resource regression runners. | 31 automated engine, gamma and resource checks cover dimming, WMI availability, overlay lifetimes and capture cleanup; Windows CI also checks installer installation/removal. |

Local pre-release builds measured about **25 MiB** for the runtime-dependent EXE
and **75 MiB** for the standalone EXE. Download sizes vary with the SDK and runtime.
The standalone package can take longer to start while libraries are decompressed.
This does not add a delay to brightness changes once the application is running.

On one Windows PC with two monitors, these memory changes reduced the compact
standalone build's private working set from about **228 to 173 MiB** and the
runtime-dependent build's from **154 to 125 MiB**. The uncompressed
`PortableLowMemory` build measured **116 MiB**, with a **179 MiB** EXE. Each value
is the median of five idle samples after a 12-second startup warmup, using the
same saved settings. These are local measurements within this fork; memory use
varies with configuration and activity.

Software dimming follows **Software fade duration** in settings (80 ms in the
tested configuration); hardware brightness snaps directly. Set the duration to
0 for an immediate software change. HDR and exclusive fullscreen limitations
still apply. Gamma failure compensation restores brightness, not color temperature.

## What it does

- **Brightness past zero.** When your monitor's hardware brightness bottoms out, Underlit keeps dimming via gamma-ramp + a transparent black overlay on every monitor. You just keep pressing the brightness-down key.
- **Color temperature with its own hotkey axis.** A second keyboard pair (default `Ctrl+Alt+Left/Right`) shifts warmth between 1500K and 6500K.
- **Windows-style OSD.** Rounded, centered flyout matching the Win11 look, so it feels native.
- **Multi-monitor aware.** One overlay per monitor, per-display gamma, DDC/CI for external monitors that support it. Displays without usable hardware brightness follow the full slider using a dim overlay, including above zero. This adjusts their visible brightness rather than their backlight.
- **Schedule (optional).** Drifts toward a warmer/underlit baseline between your bedtime and wakeup times and back.
- **Boost.** Toggle full brightness and neutral warmth, then return to the previous levels.
- **Per-monitor offsets.** Dim your secondary display relative to the main one.
- **App exclusion list.** Auto-suspends dimming while, say, Photoshop is focused.

## Requirements

- Windows 10 (2004+) or Windows 11
- .NET 8 desktop runtime (bundled by the installer if you build the self-contained publish)

## Build

```powershell
# From repo root
dotnet restore
dotnet publish src/Underlit/Underlit.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

The single-file `publish\Underlit.exe` is ready to run. To build a proper installer, install [Inno Setup 6](https://jrsoftware.org/isdl.php) and compile `installer/Underlit.iss`.

Choose a publish profile to prioritize download size or memory use:

```powershell
# Compressed standalone EXE, including .NET Desktop Runtime.
dotnet publish src/Underlit/Underlit.csproj -c Release -p:PublishProfile=PortableCompact

# Standalone EXE with lower RAM use and a larger file, including .NET Desktop Runtime.
dotnet publish src/Underlit/Underlit.csproj -c Release -p:PublishProfile=PortableLowMemory

# Smaller EXE using an installed .NET 8 Desktop Runtime (x64).
dotnet publish src/Underlit/Underlit.csproj -c Release -p:PublishProfile=DesktopRuntime
```

The profiles write to `artifacts/portable-compact`, `artifacts/portable-low-memory`
and `artifacts/desktop-runtime`. All three contain the same application features.
ReadyToRun is disabled to avoid a large extra precompiled image. `PortableCompact`
compresses its embedded libraries and decompresses them into memory at startup;
`PortableLowMemory` leaves them uncompressed. Compressed builds may take longer
to start; this does not add a fade delay when adjusting brightness. Native libraries are
bundled and extracted on first use.
The DesktopRuntime profile requires `Microsoft.WindowsDesktop.App 8.x` (x64);
the base .NET Runtime alone is insufficient. WPF is not trimmed.

## Run

Launch `Underlit.exe`. It lives in the system tray — left-click opens settings, right-click pauses or quits.

Default hotkeys (re-bindable in Settings → Hotkeys):

- `Ctrl+Alt+Down / Up` — brightness
- `Ctrl+Alt+Left / Right` — warmth
- `Ctrl+Alt+B` — toggle boost to full and back
- `Ctrl+Alt+Shift+D` — toggle Underlit on/off

Use Underlit's custom brightness hotkeys for the full range below the hardware
minimum. Native laptop brightness changes are synchronized when WMI is available;
direct interception of OEM Fn brightness keys is currently disabled.

## Architecture notes

```
UnderlitHost             — top-level wiring, lifecycle
  UnderlitEngine         — state machine; splits a single level into hw / gamma / overlay layers
  OverlayManager       — per-monitor click-through transparent windows
  GammaRampApplier     — SetDeviceGammaRamp per monitor, brightness + warmth
  HardwareBrightness   — WMI (internal panel) + DDC/CI (externals) unified
  Scheduler            — bedtime/wakeup baseline curve
  HotkeyManager        — RegisterHotKey for user-bound combos
  LowLevelKeyboardHook — available in source, currently not installed by the host
  TrayIcon, OsdWindow, SettingsWindow
```

## Known limitations

- **HDR.** Windows rejects gamma-ramp writes when a display is in HDR mode; on those displays Underlit falls back to overlay-only (still works, just less smooth).
- **Fullscreen exclusive games.** Overlays don't cover DX exclusive fullscreen — Underlit is a no-op there. Most modern games use borderless windowed, which works fine.
- **DDC/CI compatibility** varies by monitor. Some lie about their range; some refuse writes faster than ~5Hz. The engine debounces hardware writes to once per ramp tick.
- **Native Fn brightness keys** remain handled by Windows or the OEM software. Use Underlit's custom hotkeys for its full brightness range.
- **Windows Night Light** will conflict with Underlit's warmth. Underlit disables it at startup when configured (default on).

## Files on disk

- Settings: `%APPDATA%\Underlit\settings.json`
- Log: `%LOCALAPPDATA%\Underlit\underlit.log`

## Not included yet (planned)

- Integration with LAN-addressable lights beyond the existing Philips Hue support.
- Per-application brightness profiles (not just exclusion).
- Toast-style quick-adjust popup when you mouse over the tray icon.

## License

MIT. See LICENSE.
