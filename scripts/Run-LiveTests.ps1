param([Parameter(Mandatory=$true)][string]$RimWorldDir, [string]$RunName = ('run-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
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
Copy-Item "$repo/artifacts/ZenithFrame" "$runtime/Mods" -Recurse -Force
New-Item -ItemType Directory -Force "$runtime/Mods/ZenithTests/Assemblies" | Out-Null
Copy-Item "$repo/Tests/Runtime/About" "$runtime/Mods/ZenithTests" -Recurse -Force
Copy-Item "$repo/Tests/Runtime/bin/Release/net472/ZenithFrame.RuntimeTests.dll" "$runtime/Mods/ZenithTests/Assemblies" -Force
$profile = Join-Path $repo "artifacts/$RunName"
if (Test-Path $profile) { throw "Use a new RunName; profile already exists: $profile" }
New-Item -ItemType Directory -Force "$profile/Config","$profile/Saves" | Out-Null
@'
<ModsConfigData><version>1.6.4871</version><activeMods><li>ludeon.rimworld</li><li>gyugaze.zenithframe.rimward</li><li>gyugaze.zenithframe.tests</li></activeMods><knownExpansions /></ModsConfigData>
'@ | Set-Content "$profile/Config/ModsConfig.xml"
@'
<PrefsData><devMode>True</devMode><logVerbose>False</logVerbose><pauseOnLoad>False</pauseOnLoad><runInBackground>True</runInBackground><screenWidth>1024</screenWidth><screenHeight>768</screenHeight><fullscreen>False</fullscreen><volumeGame>0</volumeGame><volumeMusic>0</volumeMusic></PrefsData>
'@ | Set-Content "$profile/Config/Prefs.xml"
foreach ($phase in 0..2) {
    if ($phase -gt 0) { Copy-Item "$profile/Saves/ZenithLifecycle.rws" "$profile/Saves/Autostart.rws" -Force }
    $arguments = @('-batchmode', ('-savedatafolder="' + $profile + '"'), '-logFile', ('"' + "$profile/Player-$phase.log" + '"'))
    if ($phase -eq 0) { $arguments += '-quicktest' }
    $process = Start-Process -FilePath "$runtime/RimWorldWin64.exe" -ArgumentList $arguments -WorkingDirectory $runtime -WindowStyle Hidden -PassThru
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
}
Write-Host "PASS live lifecycle suite: $profile"



