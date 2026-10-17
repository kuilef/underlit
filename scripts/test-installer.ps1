# Installer smoke test for a disposable Windows GitHub Actions runner only.
param(
    [Parameter(Mandatory)][string]$InstallerPath,
    [Parameter(Mandatory)][string]$ExpectedExePath,
    [Parameter(Mandatory)][string]$ExpectedVersion,
    [string]$LogDirectory = "artifacts\installer-smoke"
)

$ErrorActionPreference = "Stop"
if ($env:GITHUB_ACTIONS -ne "true" -or $env:RUNNER_OS -ne "Windows") {
    throw "Run this installer smoke test only on a disposable Windows GitHub Actions runner."
}

$appId = "{A4F9E6D2-0001-4F40-8F2B-3EA7D9E8F111}_is1"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appId"
$uninstallKeys = @(
    $uninstallKey,
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appId",
    "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\$appId"
)
foreach ($key in $uninstallKeys) {
    if (Test-Path -LiteralPath $key) { throw "Existing Underlit installation: $key" }
}
if (Get-Process Underlit -ErrorAction SilentlyContinue) { throw "Underlit is already running." }

$settingsRoot = Join-Path $env:APPDATA "Underlit"
if (Test-Path -LiteralPath $settingsRoot) { throw "Existing Underlit settings; refusing to overwrite them." }
$installer = (Resolve-Path -LiteralPath $InstallerPath).Path
$expectedHash = (Get-FileHash -LiteralPath $ExpectedExePath -Algorithm SHA256).Hash
$installDir = Join-Path $env:RUNNER_TEMP ("UnderlitSmoke-" + [Guid]::NewGuid().ToString("N"))
$logs = (New-Item -ItemType Directory -Path $LogDirectory -Force).FullName
[void](New-Item -ItemType Directory -Path $settingsRoot)
$settingsPath = Join-Path $settingsRoot "settings.json"
'{"BrightnessLevel":45,"WarmthKelvin":2734,"SmokeMarker":"preserve"}' |
    Set-Content -LiteralPath $settingsPath -Encoding utf8
$settingsHash = (Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash

$setupArguments = @(
    "/SP-", "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CURRENTUSER",
    "/DIR=`"$installDir`"", "/TASKS=startwithwindows,desktopicon",
    "/LOG=`"$(Join-Path $logs 'install.log')`""
)
$setup = Start-Process -FilePath $installer -ArgumentList $setupArguments -Wait -PassThru -WindowStyle Hidden
if ($setup.ExitCode -ne 0) { throw "Installer exited with $($setup.ExitCode)." }
$installedExe = Join-Path $installDir "Underlit.exe"
if ((Get-FileHash -LiteralPath $installedExe -Algorithm SHA256).Hash -ne $expectedHash) {
    throw "Installed EXE differs from the published application."
}
$registration = Get-ItemProperty -LiteralPath $uninstallKey
if ($registration.DisplayVersion -ne $ExpectedVersion) { throw "Incorrect installed version." }
$startMenuLink = Join-Path ([Environment]::GetFolderPath("Programs")) "Underlit\Underlit.lnk"
$desktopLink = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "Underlit.lnk"
foreach ($shortcut in @($startMenuLink, $desktopLink)) {
    if (-not (Test-Path -LiteralPath $shortcut)) { throw "Missing shortcut: $shortcut" }
    $shell = New-Object -ComObject WScript.Shell
    if ($shell.CreateShortcut($shortcut).TargetPath -ne $installedExe) { throw "Incorrect shortcut target." }
}
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$runValue = (Get-ItemProperty -LiteralPath $runKey -Name Underlit).Underlit
if ($runValue.Trim('"') -ne $installedExe) { throw "Incorrect startup registration." }
if ((Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash -ne $settingsHash) {
    throw "Installer changed existing settings."
}

$uninstaller = Join-Path $installDir "unins000.exe"
$remove = Start-Process -FilePath $uninstaller -ArgumentList @(
    "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART",
    "/LOG=`"$(Join-Path $logs 'uninstall.log')`""
) -Wait -PassThru -WindowStyle Hidden
if ($remove.ExitCode -ne 0) { throw "Uninstaller exited with $($remove.ExitCode)." }
foreach ($removedPath in @($installedExe, $uninstallKey, $startMenuLink, $desktopLink)) {
    if (Test-Path -LiteralPath $removedPath) { throw "Uninstall left an entry: $removedPath" }
}
if (Get-ItemProperty -LiteralPath $runKey -Name Underlit -ErrorAction SilentlyContinue) {
    throw "Uninstall left the startup entry."
}
if ((Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash -ne $settingsHash) {
    throw "Uninstaller changed existing settings."
}
Remove-Item -LiteralPath $settingsPath -Force
Remove-Item -LiteralPath $settingsRoot
Write-Host "PASS installer: files, version, shortcuts, startup, settings preservation, uninstall"
