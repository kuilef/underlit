# Underlit (kuilef fork)

A Windows tray app for brightness and color temperature. Dim below a monitor's
hardware minimum, adjust warmth with hotkeys, and schedule evening/morning changes.

This fork adds reliable dimming for secondary displays without hardware brightness
control and smaller Windows packages. The original project is
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

По-русски: для обычной установки скачайте `UnderlitSetup-0.7.3.exe` из
[Releases](https://github.com/kuilef/underlit/releases/latest). Отдельно ставить .NET
не нужно. Перед установкой завершите старую копию через значок в трее → Quit.

## Changes from DietervanDortmont/underlit:main

This release is based on upstream `main` at
[`1483629`](https://github.com/DietervanDortmont/underlit/tree/1483629718dc07b0788bbbc2b35835b2c120034d)
(v0.7.2), checked on 2026-10-18. Upstream provides the application, UI, warmth
schedule and Hue integration. This fork changes the display and packaging paths:

| Area | Upstream at that commit | This fork |
| --- | --- | --- |
| Monitor without WMI/DDC brightness | Software layers remain neutral above zero. | That monitor dims with an overlay across the positive slider range as well. Its backlight is not changed. |
| Hardware or gamma write failure | Hardware failures do not activate a per-monitor fallback; rejected gamma writes are ignored. | Failed hardware writes activate software dimming; rejected gamma brightness is compensated by that monitor's overlay. |
| Software brightness response | Identical gamma is written to each display on every ramp tick. | Identical successful writes are cached for up to 500 ms, with invalidation after display changes and resume. |
| Reconnects and native brightness changes | Reconnects may retain stale brightness cache; WMI sync assumes the primary display owns WMI. | Reconnects reapply the target. Native WMI changes are attributed to the internal panel and propagated to other displays. |
| Downloads | ReadyToRun is enabled for a self-contained build. | ReadyToRun is disabled, standalone libraries are compressed, and a smaller runtime-dependent profile is available. |
| Verification | No engine/display regression runners. | 23 automated engine/gamma checks, plus installer installation/removal checks on Windows CI. |

Local pre-release builds measured about **25 MiB** for the runtime-dependent EXE
and **75 MiB** for the standalone EXE. Download sizes vary with the SDK and runtime.
The standalone package can take longer to start while libraries are decompressed.
This does not add a delay to brightness changes once the application is running.

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

For smaller downloads, use a publish profile:

```powershell
# Compressed standalone EXE, including .NET Desktop Runtime.
dotnet publish src/Underlit/Underlit.csproj -c Release -p:PublishProfile=PortableCompact

# Smaller EXE using an installed .NET 8 Desktop Runtime (x64).
dotnet publish src/Underlit/Underlit.csproj -c Release -p:PublishProfile=DesktopRuntime
```

The profiles write to `artifacts/portable-compact` and `artifacts/desktop-runtime`.
Both contain the same application features. ReadyToRun is disabled to avoid a
large extra precompiled image. The standalone profile compresses its embedded
libraries and may take longer to start; this does not add a fade delay when
adjusting brightness. Native libraries are bundled and extracted on first use.
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
