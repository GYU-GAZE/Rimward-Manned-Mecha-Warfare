param([Parameter(Mandatory=$true)][string]$RimWorldDir, [string]$RunName = ('run-' + (Get-Date -Format 'yyyyMMdd-HHmmss')), [switch]$WithInstalledExpansions, [string]$HarmonyDir)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
& "$PSScriptRoot/Build.ps1" -RimWorldDir $RimWorldDir
dotnet build "$repo/Tests/Runtime/Runtime.csproj" -c Release "-p:RimWorldDir=$RimWorldDir" --nologo
if ($LASTEXITCODE) { throw 'Harness build failed' }
$runtime = Join-Path $repo 'artifacts/runtime'
New-Item -ItemType Directory -Force "$runtime/Data","$runtime/Mods" | Out-Null
if (!(Test-Path "$runtime/RimWorldWin64.exe")) {
    Copy-Item "$RimWorldDir/RimWorldWin64.exe","$RimWorldDir/UnityPlayer.dll","$RimWorldDir/Version.txt","$RimWorldDir/steam_appid.txt" $runtime
    Copy-Item "$RimWorldDir/MonoBleedingEdge","$RimWorldDir/RimWorldWin64_Data" $runtime -Recurse
    Copy-Item "$RimWorldDir/Data/Core" "$runtime/Data" -Recurse
}
if (Test-Path "$runtime/Mods/ZenithFrame") { Remove-Item "$runtime/Mods/ZenithFrame" -Recurse -Force }
Copy-Item "$repo/artifacts/ZenithFrame" "$runtime/Mods" -Recurse -Force
if ([string]::IsNullOrWhiteSpace($HarmonyDir)) {
    $steamApps = Split-Path (Split-Path $RimWorldDir -Parent) -Parent
    $HarmonyDir = Join-Path $steamApps "workshop/content/294100/2009463077"
}
if (!(Test-Path "$HarmonyDir/About/About.xml")) { throw "Harmony missing. Install Workshop item 2009463077 or pass -HarmonyDir PATH." }
if (!(Test-Path "$runtime/Mods/Harmony/About/About.xml")) {
    if (Test-Path "$runtime/Mods/Harmony") {
        New-Item -ItemType Directory -Force "$runtime/Mods/Harmony/About" | Out-Null
        Copy-Item "$HarmonyDir/About/About.xml" "$runtime/Mods/Harmony/About/About.xml" -Force
    }
    else {
        Copy-Item $HarmonyDir "$runtime/Mods/Harmony" -Recurse -Force
    }
}
New-Item -ItemType Directory -Force "$runtime/Mods/ZenithTests/Assemblies" | Out-Null
Copy-Item "$repo/Tests/Runtime/About" "$runtime/Mods/ZenithTests" -Recurse -Force
Copy-Item "$repo/Tests/Runtime/bin/Release/net472/ZenithFrame.RuntimeTests.dll" "$runtime/Mods/ZenithTests/Assemblies" -Force
$expansionMods = ''
if ($WithInstalledExpansions) {
    foreach ($name in @('Royalty','Ideology','Biotech','Anomaly','Odyssey')) {
        if (Test-Path "$RimWorldDir/Data/$name/About/About.xml") {
            if (!(Test-Path "$runtime/Data/$name/About/About.xml")) {
                Copy-Item "$RimWorldDir/Data/$name" "$runtime/Data" -Recurse -Force
            }
            [xml]$metadata = Get-Content "$RimWorldDir/Data/$name/About/About.xml" -Raw
            $expansionMods += '<li>' + $metadata.ModMetaData.packageId.ToLowerInvariant() + '</li>'
        }
    }
}
else {
    foreach ($directory in Get-ChildItem "$runtime/Data" -Directory | Where-Object Name -NE 'Core') {
        Get-ChildItem $directory.FullName -Recurse -Force -File | ForEach-Object {
            $_.Attributes = $_.Attributes -band (-bnot [System.IO.FileAttributes]::ReadOnly)
        }
        Remove-Item -LiteralPath $directory.FullName -Recurse -Force
    }
}
$knownExpansionMods = ''
foreach ($expansion in Get-ChildItem "$runtime/Data" -Directory | Where-Object Name -NE 'Core') {
    [xml]$metadata = Get-Content "$($expansion.FullName)/About/About.xml" -Raw
    $knownExpansionMods += '<li>' + $metadata.ModMetaData.packageId.ToLowerInvariant() + '</li>'
}
$profile = Join-Path $repo "artifacts/$RunName"
if (Test-Path $profile) { throw "Use a new RunName; profile already exists: $profile" }
New-Item -ItemType Directory -Force "$profile/Config","$profile/Saves" | Out-Null
@'
<ModsConfigData><version>1.6.4871</version><activeMods><li>brrainz.harmony</li><li>ludeon.rimworld</li><li>gyugaze.zenithframe.rimward</li><li>gyugaze.zenithframe.tests</li></activeMods><knownExpansions /></ModsConfigData>
'@ | ForEach-Object { $_.Replace('<li>gyugaze.zenithframe.rimward</li>', $expansionMods + '<li>gyugaze.zenithframe.rimward</li>').Replace('<knownExpansions />', '<knownExpansions>' + $knownExpansionMods + '</knownExpansions>') } | Set-Content "$profile/Config/ModsConfig.xml"
@'
<PrefsData><devMode>True</devMode><logVerbose>False</logVerbose><pauseOnLoad>False</pauseOnLoad><runInBackground>True</runInBackground><screenWidth>1024</screenWidth><screenHeight>768</screenHeight><fullscreen>False</fullscreen><volumeGame>0</volumeGame><volumeMusic>0</volumeMusic></PrefsData>
'@ | Set-Content "$profile/Config/Prefs.xml"
foreach ($phase in 0..10) {
    if ($phase -gt 0) { Copy-Item "$profile/Saves/ZenithLifecycle.rws" "$profile/Saves/Autostart.rws" -Force }
    # Keep Unity's GUI callbacks active: the suite exercises RimWorld's real
    # FloatMenuOption pipeline, whose text measurement is only valid in OnGUI.
    # A minimized window keeps a real Unity window/event loop without interrupting
    # the desktop; WindowStyle Hidden creates no handle and suppresses OnGUI.
    $arguments = @(('-savedatafolder="' + $profile + '"'), '-logFile', ('"' + "$profile/Player-$phase.log" + '"'))
    if ($phase -eq 0) { $arguments += '-quicktest' }
    $process = Start-Process -FilePath "$runtime/RimWorldWin64.exe" -ArgumentList $arguments -WorkingDirectory $runtime -WindowStyle Minimized -PassThru
    $null = $process.Handle
    Write-Host "Live phase $phase PID $($process.Id) profile $profile"
    $expires = (Get-Date).AddMinutes(6)
    while (!$process.HasExited) {
        if ((Get-Date) -gt $expires) { Stop-Process -Id $process.Id; throw "Runtime timeout; inspect $profile/Player-$phase.log" }
        Start-Sleep -Seconds 2
        $process.Refresh()
    }
    $process.WaitForExit()
    Write-Host "Phase $phase process exit: $($process.ExitCode)"
    $report = "$profile/phase-$phase.txt"
    if (!(Test-Path $report)) { throw "Runtime exited without a report; inspect $profile/Player-$phase.log" }
    Get-Content $report
    if ((Get-Content $report -Raw) -match '(?m)^FAIL ' -or $process.ExitCode -ne 0) { throw "Phase $phase failed" }
    $runtimeErrors = Select-String -Path "$profile/Player-$phase.log" -Pattern 'Exception|Config error|Could not resolve|XML error|Error while|Error in|Could not execute|Could not load asset bundle'
    if ($runtimeErrors) { throw "Runtime log contains errors: $($runtimeErrors.Line -join [Environment]::NewLine)" }
}
Write-Host "PASS live lifecycle suite: $profile"



