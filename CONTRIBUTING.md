# Contributing to Underlit

Thanks for your interest! Bug reports, feature ideas, and pull requests are all welcome.

## Reporting bugs

Please [open an issue](../../issues/new/choose) using the **Bug report** template. Include your Windows version, Underlit version, and a snippet from `%LOCALAPPDATA%\Underlit\underlit.log` if anything looked suspicious in there.

## Suggesting features

Use the **Feature request** template. If you're not sure whether an idea fits, open a [Discussion](../../discussions) first and we can talk about it.

## Building locally

You need:
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- (For installers) [Inno Setup 6](https://jrsoftware.org/isdl.php)

```powershell
dotnet publish src/Underlit/Underlit.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
# The portable single-file exe is now at: publish\Underlit.exe
```

For the installer:

```powershell
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer\Underlit.iss
# Output: installer\Output\UnderlitSetup-<version>.exe
```

CI runs both steps automatically when a `v*` tag is pushed — so contributors don't need Inno Setup installed.

## Code layout

See the **Architecture notes** section in the [README](README.md).

## Brightness regression checks

```powershell
dotnet run --project tests/Underlit.Engine.Tests -c Release
```

This dependency-free runner compiles the actual engine and settings sources with
test doubles only at the gamma, hardware and overlay output boundaries. It tests
mixed hardware/software monitors, failed writes and recovery, rejected gamma
writes, offsets, pause/boost, reconnects, OSD opacity and external WMI changes.
It does not change real monitor brightness. A failed check returns a nonzero exit code.

For a physical check, use two monitors with one lacking DDC/CI. Lower brightness
from 100 toward 0: both should dim, with software dimming on the unsupported
monitor. Continue below 0, return to 100, pause/resume, then reconnect a monitor.
The overlay must cover the entire monitor, including screens left of the primary
or using a different DPI. Build the main application separately to verify its
native display and WPF integration.

## Pull requests

Feel free to open a PR. Small, focused changes land fastest. For anything big, opening an issue first to agree on direction saves both of us time.
