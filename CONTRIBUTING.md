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

CI runs the regression checks, publishes both compact packages, and tests the
installer on each push to `main`. A matching `vX.Y.Z` tag publishes a Release
after those checks pass. The tag must match `<Version>` in the project and have
release notes at `docs/releases/X.Y.Z.md`. Contributors do not need Inno Setup
installed locally. The installer smoke test runs only on a disposable Windows
GitHub Actions runner and refuses to overwrite an existing installation or settings.
The workflow can also be started manually: leave the tag input empty to only
build and test, or provide an existing release tag to publish its checked source.

## Code layout

See the **Architecture notes** section in the [README](README.md).

## Brightness regression checks

```powershell
dotnet run --project tests/Underlit.Engine.Tests -c Release
dotnet run --project tests/Underlit.Display.Tests -c Release
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

The display runner compiles the real gamma applier with a replacement for native
GDI calls. It checks that unchanged gamma avoids driver writes while changed
warmth, extended dimming, failed writes, display changes and resume still reach
the driver. Identical targets are reasserted after 500 ms to recover from external
gamma changes. Software brightness follows the configured smooth ramp; normal
hardware brightness is sent immediately, so a small difference in response is
expected when smooth ramping is enabled.

## Pull requests

Feel free to open a PR. Small, focused changes land fastest. For anything big, opening an issue first to agree on direction saves both of us time.
