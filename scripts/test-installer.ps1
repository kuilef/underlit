# Installer smoke test for a disposable Windows GitHub Actions runner only.
param(
    [Parameter(Mandatory)][string]$InstallerPath,
    [Parameter(Mandatory)][string]$ExpectedExePath,
    [Parameter(Mandatory)][string]$ExpectedVersion,
    [string]$LogDirectory = "artifacts\installer-smoke",
    [switch]$EnableStartupTask
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
$scheduler = Join-Path $env:SystemRoot "System32\schtasks.exe"
function Test-UnderlitAutostartTask {
    $query = Start-Process -FilePath $scheduler -ArgumentList @("/Query", "/TN", "\Underlit\Autostart") -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logs "task-query.log") -RedirectStandardError (Join-Path $logs "task-query-error.log")
    return $query.ExitCode -eq 0
}
if (Test-UnderlitAutostartTask) { throw "Existing Underlit scheduled task; refusing to overwrite it." }
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
if (Get-ItemProperty -LiteralPath $runKey -Name Underlit -ErrorAction SilentlyContinue) {
    throw "Existing Underlit startup value; refusing to overwrite it."
}
[void](New-Item -ItemType Directory -Path $settingsRoot)
$settingsPath = Join-Path $settingsRoot "settings.json"
'{"BrightnessLevel":45,"WarmthKelvin":2734,"SmokeMarker":"preserve"}' |
    Set-Content -LiteralPath $settingsPath -Encoding utf8
$settingsHash = (Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash

$tasks = if ($EnableStartupTask) { "startwithwindows,desktopicon" } else { "desktopicon" }
$setupArguments = @(
    "/SP-", "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CURRENTUSER",
    "/DIR=`"$installDir`"", "/TASKS=$tasks",
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
if ($EnableStartupTask) {
    $runValue = (Get-ItemProperty -LiteralPath $runKey -Name Underlit).Underlit
    if ($runValue.Trim('"') -ne $installedExe) { throw "Incorrect startup registration." }
} else {
    if (Get-ItemProperty -LiteralPath $runKey -Name Underlit -ErrorAction SilentlyContinue) {
        throw "Installer enabled startup without selecting the task."
    }
    # AutoStart.Set creates this value when startup is enabled inside the app.
    [void](New-Item -Path $runKey -Force)
    New-ItemProperty -LiteralPath $runKey -Name Underlit -PropertyType String -Value "`"$installedExe`"" -Force | Out-Null
}
if ((Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash -ne $settingsHash) {
    throw "Installer changed existing settings."
}

# Reproduce the extra autostart mechanisms used by the application's current
# and older versions, without launching the tray application on the runner.
$legacyShortcut = Join-Path ([Environment]::GetFolderPath("Startup", "Create")) "Underlit.lnk"
if (Test-Path -LiteralPath $legacyShortcut) { throw "Existing legacy Startup shortcut." }
$legacyLink = $shell.CreateShortcut($legacyShortcut)
$legacyLink.TargetPath = $installedExe
$legacyLink.Save()
$taskUser = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$taskCommand = [Security.SecurityElement]::Escape($installedExe)
$taskXml = '<?xml version="1.0" encoding="UTF-16"?>' +
    '<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">' +
    '<Triggers><LogonTrigger><Enabled>true</Enabled></LogonTrigger></Triggers>' +
    "<Principals><Principal id=`"Author`"><UserId>$taskUser</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>" +
    "<Actions Context=`"Author`"><Exec><Command>$taskCommand</Command></Exec></Actions></Task>"
$taskXmlPath = Join-Path $logs "autostart-task.xml"
$taskXml | Set-Content -LiteralPath $taskXmlPath -Encoding Unicode
$register = Start-Process -FilePath $scheduler -ArgumentList @("/Create", "/TN", "\Underlit\Autostart", "/XML", "`"$taskXmlPath`"", "/F") -Wait -PassThru -WindowStyle Hidden
if ($register.ExitCode -ne 0 -or -not (Test-UnderlitAutostartTask)) { throw "Could not create the application autostart fixture." }

$uninstaller = Join-Path $installDir "unins000.exe"
$remove = Start-Process -FilePath $uninstaller -ArgumentList @(
    "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART",
    "/LOG=`"$(Join-Path $logs 'uninstall.log')`""
) -Wait -PassThru -WindowStyle Hidden
if ($remove.ExitCode -ne 0) { throw "Uninstaller exited with $($remove.ExitCode)." }
foreach ($removedPath in @($installedExe, $uninstallKey, $startMenuLink, $desktopLink, $legacyShortcut)) {
    if (Test-Path -LiteralPath $removedPath) { throw "Uninstall left an entry: $removedPath" }
}
if (Test-UnderlitAutostartTask) { throw "Uninstall left the application scheduled task." }
if (Get-ItemProperty -LiteralPath $runKey -Name Underlit -ErrorAction SilentlyContinue) {
    throw "Uninstall left the startup entry."
}
if ((Get-FileHash -LiteralPath $settingsPath -Algorithm SHA256).Hash -ne $settingsHash) {
    throw "Uninstaller changed existing settings."
}
Remove-Item -LiteralPath $settingsPath -Force
Remove-Item -LiteralPath $settingsRoot
Write-Host "PASS installer: files, version, shortcuts, startup task, settings preservation, uninstall"
