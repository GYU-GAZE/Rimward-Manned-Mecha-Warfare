param(
    [Parameter(Mandatory=$true)][string]$RimWorldDir,
    [string]$RunName = ('abilities-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [switch]$WithInstalledExpansions,
    [string]$HarmonyDir,
    [string]$VEFDir,
    [string]$VPEDir
)
throw 'This historical ability matrix is retired. Use Run-LiveTests.ps1 once for the completed candidate.'
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent

$buildArgs = @{ RimWorldDir = $RimWorldDir }
if (![string]::IsNullOrWhiteSpace($VEFDir)) { $buildArgs.VEFDir = $VEFDir }
& "$PSScriptRoot/Build.ps1" @buildArgs
dotnet build "$repo/Tests/Abilities/Abilities.csproj" -c Release "-p:RimWorldDir=$RimWorldDir" --nologo
if ($LASTEXITCODE) { throw 'Ability harness build failed' }
if (![string]::IsNullOrWhiteSpace($VEFDir)) {
    dotnet build "$repo/Tests/Abilities.VEF/Abilities.VEF.csproj" -c Release "-p:RimWorldDir=$RimWorldDir" "-p:VEFDir=$VEFDir" --nologo
    if ($LASTEXITCODE) { throw 'VEF ability harness build failed' }
}

$runtime = Join-Path $repo 'artifacts/runtime'
New-Item -ItemType Directory -Force "$runtime/Data","$runtime/Mods" | Out-Null
if (!(Test-Path "$runtime/RimWorldWin64.exe")) {
    Copy-Item "$RimWorldDir/RimWorldWin64.exe","$RimWorldDir/UnityPlayer.dll","$RimWorldDir/Version.txt","$RimWorldDir/steam_appid.txt" $runtime
    Copy-Item "$RimWorldDir/MonoBleedingEdge","$RimWorldDir/RimWorldWin64_Data" $runtime -Recurse
    Copy-Item "$RimWorldDir/Data/Core" "$runtime/Data" -Recurse
}

foreach ($name in @('ZenithFrame','ZenithAbilityTests','VEF','VPE')) {
    if (Test-Path "$runtime/Mods/$name") { Remove-Item -LiteralPath "$runtime/Mods/$name" -Recurse -Force }
}
Copy-Item "$repo/artifacts/ZenithFrame" "$runtime/Mods/ZenithFrame" -Recurse -Force
New-Item -ItemType Directory -Force "$runtime/Mods/ZenithAbilityTests/Assemblies" | Out-Null
Copy-Item "$repo/Tests/Abilities/About","$repo/Tests/Abilities/Defs" "$runtime/Mods/ZenithAbilityTests" -Recurse -Force
Copy-Item "$repo/Tests/Abilities/bin/Release/net472/ZenithFrame.AbilityTests.dll" "$runtime/Mods/ZenithAbilityTests/Assemblies" -Force
Copy-Item "$repo/Tests/Abilities/loadFolders.xml" "$runtime/Mods/ZenithAbilityTests" -Force

if ([string]::IsNullOrWhiteSpace($HarmonyDir)) {
    $steamApps = Split-Path (Split-Path $RimWorldDir -Parent) -Parent
    $HarmonyDir = Join-Path $steamApps 'workshop/content/294100/2009463077'
}
if (!(Test-Path "$HarmonyDir/About/About.xml")) { throw 'Harmony missing' }
if (!(Test-Path "$runtime/Mods/Harmony/About/About.xml")) { Copy-Item $HarmonyDir "$runtime/Mods/Harmony" -Recurse -Force }

$thirdPartyMods = ''
if (![string]::IsNullOrWhiteSpace($VEFDir)) {
    if (!(Test-Path "$VEFDir/About/About.xml")) { throw "VEF missing: $VEFDir" }
    Copy-Item $VEFDir "$runtime/Mods/VEF" -Recurse -Force
    New-Item -ItemType Directory -Force "$runtime/Mods/ZenithAbilityTests/Compatibility/VEF/Assemblies" | Out-Null
    Copy-Item "$repo/Tests/Abilities/Compatibility/VEF/Defs" "$runtime/Mods/ZenithAbilityTests/Compatibility/VEF" -Recurse -Force
    Copy-Item "$repo/Tests/Abilities.VEF/bin/Release/net472/ZenithFrame.AbilityTests.VEF.dll" "$runtime/Mods/ZenithAbilityTests/Compatibility/VEF/Assemblies" -Force
    $thirdPartyMods += '<li>oskarpotocki.vanillafactionsexpanded.core</li>'
}
if (![string]::IsNullOrWhiteSpace($VPEDir)) {
    if ([string]::IsNullOrWhiteSpace($VEFDir)) { throw 'VPE configuration requires -VEFDir' }
    if (!(Test-Path "$VPEDir/About/About.xml")) { throw "VPE missing: $VPEDir" }
    Copy-Item $VPEDir "$runtime/Mods/VPE" -Recurse -Force
    $thirdPartyMods += '<li>vanillaexpanded.vpsycastse</li>'
}

$expansionMods = ''
if ($WithInstalledExpansions -or ![string]::IsNullOrWhiteSpace($VPEDir)) {
    foreach ($name in @('Royalty','Ideology','Biotech','Anomaly','Odyssey')) {
        if (Test-Path "$RimWorldDir/Data/$name/About/About.xml") {
            if (!(Test-Path "$runtime/Data/$name/About/About.xml")) { Copy-Item "$RimWorldDir/Data/$name" "$runtime/Data" -Recurse }
            [xml]$metadata = Get-Content "$RimWorldDir/Data/$name/About/About.xml" -Raw
            $expansionMods += '<li>' + $metadata.ModMetaData.packageId.ToLowerInvariant() + '</li>'
        }
    }
}
else {
    foreach ($directory in Get-ChildItem "$runtime/Data" -Directory | Where-Object Name -NE 'Core') {
        Get-ChildItem $directory.FullName -Recurse -Force -File | ForEach-Object { $_.Attributes = $_.Attributes -band (-bnot [System.IO.FileAttributes]::ReadOnly) }
        Remove-Item -LiteralPath $directory.FullName -Recurse -Force
    }
}

$knownExpansionMods = ''
foreach ($expansion in Get-ChildItem "$runtime/Data" -Directory | Where-Object Name -NE 'Core') {
    [xml]$metadata = Get-Content "$($expansion.FullName)/About/About.xml" -Raw
    $knownExpansionMods += '<li>' + $metadata.ModMetaData.packageId.ToLowerInvariant() + '</li>'
}

$profile = Join-Path $repo "artifacts/$RunName"
if (Test-Path $profile) { throw "Use a new RunName: $profile" }
New-Item -ItemType Directory -Force "$profile/Config","$profile/Saves" | Out-Null
$active = '<li>brrainz.harmony</li><li>ludeon.rimworld</li>' + $expansionMods + $thirdPartyMods + '<li>gyugaze.zenithframe.rimward</li><li>gyugaze.zenithframe.abilitytests</li>'
('<ModsConfigData><version>1.6.4871</version><activeMods>' + $active + '</activeMods><knownExpansions>' + $knownExpansionMods + '</knownExpansions></ModsConfigData>') | Set-Content "$profile/Config/ModsConfig.xml"
'<PrefsData><devMode>True</devMode><logVerbose>False</logVerbose><pauseOnLoad>False</pauseOnLoad><runInBackground>True</runInBackground><screenWidth>1024</screenWidth><screenHeight>768</screenHeight><fullscreen>False</fullscreen><volumeGame>0</volumeGame><volumeMusic>0</volumeMusic></PrefsData>' | Set-Content "$profile/Config/Prefs.xml"

foreach ($phase in 0..3) {
    if ($phase -gt 0) { Copy-Item "$profile/Saves/ZenithAbilities.rws" "$profile/Saves/Autostart.rws" -Force }
    $arguments = @(('-savedatafolder="' + $profile + '"'), '-logFile', ('"' + "$profile/Player-$phase.log" + '"'))
    if ($phase -eq 0) { $arguments += '-quicktest' }
    $process = Start-Process -FilePath "$runtime/RimWorldWin64.exe" -ArgumentList $arguments -WorkingDirectory $runtime -WindowStyle Minimized -PassThru
    $null = $process.Handle
    Write-Host "Ability phase $phase PID $($process.Id) profile $profile"
    $expires = (Get-Date).AddMinutes(6)
    while (!$process.HasExited) {
        if ((Get-Date) -gt $expires) { Stop-Process -Id $process.Id; throw "Ability runtime timeout; inspect $profile/Player-$phase.log" }
        Start-Sleep -Seconds 2
        $process.Refresh()
    }
    $process.WaitForExit()
    $report = "$profile/ability-phase-$phase.txt"
    if (!(Test-Path $report)) { throw "Runtime exited without report; inspect $profile/Player-$phase.log" }
    Get-Content $report
    if ((Get-Content $report -Raw) -match '(?m)^FAIL ' -or $process.ExitCode -ne 0) { throw "Ability phase $phase failed" }
    $runtimeErrors = Select-String -Path "$profile/Player-$phase.log" -Pattern 'Exception|Config error|Could not resolve|XML error|Error while|Error in|Could not execute|Could not load asset bundle'
    if ($runtimeErrors) { throw "Runtime log contains errors: $($runtimeErrors.Line -join [Environment]::NewLine)" }
}
Write-Host "PASS embarked ability suite: $profile"
