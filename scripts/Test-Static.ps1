param([Parameter(Mandatory=$true)][string]$RimWorldDir)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$checks = 0
function Assert-Static([bool]$condition, [string]$message) {
    if (!$condition) { throw $message }
    $script:checks++
}
$defs = @{}
$parents = @{}
foreach ($file in Get-ChildItem "$RimWorldDir/Data/Core/Defs" -Filter '*.xml' -Recurse) {
    [xml]$doc = Get-Content $file.FullName -Raw
    foreach ($node in $doc.Defs.ChildNodes) {
        if ($node.NodeType -ne 'Element') { continue }
        if ($node.Name -and $node.GetAttribute('Name')) { $parents[$node.GetAttribute('Name')] = $true }
        if ($node.defName) { $defs[($node.LocalName + ':' + $node.defName)] = $true }
    }
}
$modNames = @{}
foreach ($file in Get-ChildItem "$repo/Mod/Defs" -Filter '*.xml' -Recurse) {
    [xml]$doc = Get-Content $file.FullName -Raw
    Assert-Static ($doc.DocumentElement.Name -eq 'Defs') "Invalid root: $file"
    foreach ($node in $doc.Defs.ChildNodes) {
        if ($node.NodeType -ne 'Element') { continue }
        $key = $node.LocalName + ':' + $node.defName
        Assert-Static (![string]::IsNullOrWhiteSpace($node.defName)) "Unnamed def: $file"
        Assert-Static (!$modNames.ContainsKey($key)) "Duplicate typed def: $key"
        $modNames[$key] = $true
        $defs[$key] = $true
        if ($node.GetAttribute('ParentName')) { Assert-Static ($parents.ContainsKey($node.GetAttribute('ParentName'))) "Unknown XML parent: $key" }
    }
}
[xml]$frame = Get-Content "$repo/Mod/Defs/Frame.xml" -Raw
$refs = @{
    BodyDef = $frame.Defs.ThingDef[0].race.body
    FleshTypeDef = $frame.Defs.ThingDef[0].race.fleshType
    LifeStageDef = $frame.Defs.ThingDef[0].race.lifeStageAges.li.def
    ThingDef = $frame.Defs.ThingDef[1].verbs.li.defaultProjectile
    SoundDef = $frame.Defs.ThingDef[1].verbs.li.soundCast
}
foreach ($entry in $refs.GetEnumerator()) { Assert-Static ($defs.ContainsKey($entry.Key + ':' + $entry.Value)) "Missing game def reference: $($entry.Key):$($entry.Value)" }
Assert-Static ($frame.Defs.ThingDef[1].verbs.li.onlyManualCast -eq 'true') 'Weapon must be manual only'
Assert-Static ($frame.Defs.ThingDef[0].race.thinkTreeMain -eq 'ZF_FrameIdle') 'Frame must not inherit autonomous AI'
Assert-Static ($frame.Defs.ThingDef[0].race.fleshType -eq 'ZF_FrameMechanical') 'Frame must use its own mechanical flesh type, not Biotech mechanoid classification'
[xml]$about = Get-Content "$repo/Mod/About/About.xml" -Raw
Assert-Static ($about.ModMetaData.packageId -eq 'gyugaze.zenithframe.rimward') 'Package ID mismatch'
Assert-Static ($null -eq $about.ModMetaData.modDependencies) 'Unexpected release dependency'
$code = (Get-ChildItem "$repo/Source" -Filter '*.cs' -Recurse | Where-Object FullName -NotMatch '\\obj\\' | Get-Content -Raw) -join "`n"
Assert-Static ($code -notmatch 'Coventech|Harmony|System\.Reflection|GetField\(|GetMethod\(') 'Forbidden coupling, patch or reflection in production source'
Assert-Static ($code -notmatch 'catch\s*(\([^)]*\))?\s*\{\s*\}') 'Swallowed exception'
Assert-Static ($code -notmatch 'MechanitorUtility|IsColonyMech|IsColonyMechPlayerControlled|GetMechControlGroup|OverseerSubject|OutOfCommandRange') 'Biotech mech-control semantics leaked into production Frame code'
$dlls = @(Get-ChildItem "$repo/artifacts/ZenithFrame/Assemblies" -Filter '*.dll')
Assert-Static ($dlls.Count -eq 1 -and $dlls[0].Name -eq 'ZenithFrame.dll') 'Release contains unexpected runtime assemblies'
Write-Host "PASS: $checks XML/static/package checks"
