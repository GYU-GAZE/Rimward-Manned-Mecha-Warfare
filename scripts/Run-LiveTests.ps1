param(
    [Parameter(Mandatory=$true)][string]$RimWorldDir,
    [string]$RunName = ('acceptance-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [string]$HarmonyDir,
    [switch]$WithVEF,
    [switch]$WithInstalledTrackers,
    [string]$CanaryDir
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$mode = if ($WithVEF) { 'WithVEF' } else { 'CoreOnly' }
& "$PSScriptRoot/Build.ps1" -RimWorldDir $RimWorldDir -PackageMode $mode
if ($LASTEXITCODE) { throw 'Build failed' }
dotnet build "$repo/Tests/Runtime/Runtime.csproj" -c Release "-p:RimWorldDir=$RimWorldDir" --nologo
if ($LASTEXITCODE) { throw 'Acceptance harness build failed' }
$steamApps = Split-Path (Split-Path $RimWorldDir -Parent) -Parent
$vefDir = Join-Path $steamApps 'workshop/content/294100/2023507013'
if ($WithVEF) {
    dotnet build "$repo/Tests/Runtime.VEF/Runtime.VEF.csproj" -c Release "-p:RimWorldDir=$RimWorldDir" "-p:VEFDir=$vefDir" --nologo
    if ($LASTEXITCODE) { throw 'Framework acceptance probe build failed' }
}

$runtime = Join-Path $repo 'artifacts/runtime'
New-Item -ItemType Directory -Force "$runtime/Data","$runtime/Mods" | Out-Null
if (!(Test-Path "$runtime/RimWorldWin64.exe")) {
    Copy-Item "$RimWorldDir/RimWorldWin64.exe","$RimWorldDir/UnityPlayer.dll","$RimWorldDir/Version.txt","$RimWorldDir/steam_appid.txt" $runtime
    Copy-Item "$RimWorldDir/MonoBleedingEdge","$RimWorldDir/RimWorldWin64_Data" $runtime -Recurse
    Copy-Item "$RimWorldDir/Data/Core" "$runtime/Data" -Recurse
}
$testMod = Join-Path $runtime 'Mods/ZenithTests'
$gameMod = Join-Path $runtime 'Mods/ZenithFrame'
$runtimeRoot = [IO.Path]::GetFullPath($runtime).TrimEnd('\') + '\'
foreach ($target in @($testMod, $gameMod)) {
    $fullTarget = [IO.Path]::GetFullPath($target)
    if (!$fullTarget.StartsWith($runtimeRoot, [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe mod target: $fullTarget" }
    if (Test-Path -LiteralPath $fullTarget) { Remove-Item -LiteralPath $fullTarget -Recurse -Force }
}
Copy-Item "$repo/artifacts/ZenithFrame" "$runtime/Mods" -Recurse -Force
if ([string]::IsNullOrWhiteSpace($HarmonyDir)) {
    $steamApps = Split-Path (Split-Path $RimWorldDir -Parent) -Parent
    $HarmonyDir = Join-Path $steamApps 'workshop/content/294100/2009463077'
}
if (!(Test-Path "$HarmonyDir/About/About.xml")) { throw "Harmony missing at $HarmonyDir" }
if (!(Test-Path "$runtime/Mods/Harmony/About/About.xml")) {
    Copy-Item $HarmonyDir "$runtime/Mods/Harmony" -Recurse -Force
}
New-Item -ItemType Directory -Force "$testMod/Assemblies" | Out-Null
Copy-Item "$repo/Tests/Runtime/About","$repo/Tests/Runtime/Defs" $testMod -Recurse -Force
Copy-Item "$repo/Tests/Runtime/bin/Release/net472/ZenithFrame.RuntimeTests.dll" "$testMod/Assemblies" -Force
if ($WithVEF) {
    Copy-Item "$repo/Tests/Runtime.VEF/bin/Release/net472/ZenithFrame.RuntimeTests.VEF.dll" "$testMod/Assemblies" -Force
    Copy-Item "$repo/Tests/Runtime.VEF/Defs/*" "$testMod/Defs" -Force
    if (!(Test-Path "$runtime/Mods/VEF/About/About.xml")) { Copy-Item $vefDir "$runtime/Mods/VEF" -Recurse -Force }
}
$active = @('brrainz.harmony', 'ludeon.rimworld')
if ($WithInstalledTrackers) {
    foreach ($dlc in @('Royalty','Biotech')) {
        if (Test-Path -LiteralPath "$RimWorldDir/Data/$dlc/About/About.xml") {
            if (!(Test-Path -LiteralPath "$runtime/Data/$dlc/About/About.xml")) { Copy-Item "$RimWorldDir/Data/$dlc" "$runtime/Data" -Recurse -Force }
            $active += "ludeon.rimworld.$($dlc.ToLowerInvariant())"
        }
    }
}
if ($WithVEF) { $active += 'OskarPotocki.VanillaFactionsExpanded.Core' }
if ($CanaryDir) {
    [xml]$canaryAbout = Get-Content -LiteralPath "$CanaryDir/About/About.xml" -Raw
    if (!(Test-Path -LiteralPath "$runtime/Mods/ArchitectureCanary/About/About.xml")) { Copy-Item $CanaryDir "$runtime/Mods/ArchitectureCanary" -Recurse -Force }
    $active += [string]$canaryAbout.ModMetaData.packageId
}
$active += @('gyugaze.zenithframe.rimward','gyugaze.zenithframe.tests')

$profile = Join-Path $repo "artifacts/$RunName"
if (Test-Path -LiteralPath $profile) { throw "Profile exists: $profile" }
New-Item -ItemType Directory -Force "$profile/Config" | Out-Null
Set-Content "$profile/acceptance-suite.txt" 'one-process'
if ($CanaryDir) { Set-Content -LiteralPath "$profile/architecture-canary.txt" 'unmodified external canary' }
$modList = ($active | ForEach-Object { '<li>' + [Security.SecurityElement]::Escape($_) + '</li>' }) -join ''
Set-Content -LiteralPath "$profile/Config/ModsConfig.xml" -Value ('<ModsConfigData><version>1.6.4871</version><activeMods>' + $modList + '</activeMods><knownExpansions /></ModsConfigData>')
@'
<PrefsData><devMode>True</devMode><logVerbose>False</logVerbose><pauseOnLoad>False</pauseOnLoad><runInBackground>True</runInBackground><screenWidth>1366</screenWidth><screenHeight>768</screenHeight><fullscreen>False</fullscreen><volumeGame>0</volumeGame><volumeMusic>0</volumeMusic></PrefsData>
'@ | Set-Content "$profile/Config/Prefs.xml"
$arguments = @(('-savedatafolder="' + $profile + '"'), '-logFile', ('"' + "$profile/Player.log" + '"'), '-quicktest')
$process = Start-Process -FilePath "$runtime/RimWorldWin64.exe" -ArgumentList $arguments -WorkingDirectory $runtime -WindowStyle Hidden -PassThru
$null = $process.Handle
Write-Host "Acceptance process PID $($process.Id) profile $profile"
$expires = (Get-Date).AddMinutes(8)
while (!$process.HasExited) {
    if ((Get-Date) -gt $expires) { Stop-Process -Id $process.Id; throw "Acceptance timed out: $profile" }
    Start-Sleep -Seconds 2
    $process.Refresh()
}
$process.WaitForExit()
$report = "$profile/acceptance.txt"
if (!(Test-Path $report)) { throw "No acceptance report: $profile/Player.log" }
Get-Content $report
Write-Host "Acceptance process exit: $($process.ExitCode)"
if ($process.ExitCode -ne 0 -or (Get-Content $report -Raw) -notmatch '(?m)^PASS acceptance complete\r?$' -or
    (Get-Content $report -Raw) -match '(?m)^FAIL ') { throw "Acceptance failed: $profile" }
$errors = Select-String -Path "$profile/Player.log" -Pattern 'Exception|Config error|Could not resolve|XML error|Error while|Error in|Could not execute|Could not load asset bundle|Rect height was too small'
if ($errors) { throw "Acceptance log contains errors: $($errors.Line -join [Environment]::NewLine)" }
Write-Host "PASS one-process acceptance: $profile"
