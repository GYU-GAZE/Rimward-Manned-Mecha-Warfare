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
[xml]$fleshTypes = Get-Content "$repo/Mod/Defs/FrameFleshTypes.xml" -Raw
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
Assert-Static ($frame.Defs.ThingDef[0].race.trainability -eq 'None') 'Frame must not expose animal training and must serialize a valid training tracker'
$frameThinkTree = @($frame.Defs.ThinkTreeDef) | Where-Object defName -EQ 'ZF_FrameIdle'
Assert-Static ($frameThinkTree.thinkRoot.GetAttribute('Class') -eq 'ThinkNode_Priority') 'Frame think tree must prioritize explicit queued jobs'
Assert-Static ($frameThinkTree.thinkRoot.subNodes.li[0].GetAttribute('Class') -eq 'ThinkNode_QueuedJob') 'Frame think tree must consume ordinary player-order jobs before idling'
Assert-Static ($frame.Defs.ThingDef[0].race.fleshType -eq 'ZF_FrameMechanical') 'Frame must use its own mechanical flesh type, not Biotech mechanoid classification'
$frameFlesh = $fleshTypes.Defs.FleshTypeDef | Where-Object defName -EQ 'ZF_FrameMechanical'
Assert-Static ($frameFlesh.corpseCategory -eq 'CorpsesMechanoid') 'Frame flesh type requires a valid mechanical corpse category'
Assert-Static ($frameFlesh.damageEffecter -eq 'Damage_HitMechanoid') 'Frame flesh type requires a valid mechanical damage effecter'
Assert-Static ($frameFlesh.isOrganic -eq 'false') 'Frame flesh type must remain non-organic'
Assert-Static (@($frameFlesh.genericWounds.li).Count -gt 0) 'Frame flesh type requires wound graphics'
[xml]$about = Get-Content "$repo/Mod/About/About.xml" -Raw
Assert-Static ($about.ModMetaData.packageId -eq 'gyugaze.zenithframe.rimward') 'Package ID mismatch'
Assert-Static ($about.ModMetaData.modDependencies.li.packageId -eq 'brrainz.harmony') 'Harmony dependency missing or incorrect'
[xml]$project = Get-Content "$repo/Source/ZenithFrame/ZenithFrame.csproj" -Raw
Assert-Static ($project.Project.ItemGroup.PackageReference.Include -contains 'Lib.Harmony.Ref') 'Harmony compile reference missing'
$code = (Get-ChildItem "$repo/Source" -Filter '*.cs' -Recurse | Where-Object FullName -NotMatch '\\obj\\' | Get-Content -Raw) -join "`n"
Assert-Static ($code -notmatch 'Coventech|System\.Reflection|GetField\(|GetMethod\(') 'Forbidden Coventech coupling or reflection in production source'
Assert-Static ($code -notmatch 'catch\s*(\([^)]*\))?\s*\{\s*\}') 'Swallowed exception'
Assert-Static ($code -notmatch 'MechanitorUtility|IsColonyMech|IsColonyMechPlayerControlled|GetMechControlGroup|OverseerSubject|OutOfCommandRange') 'Biotech mech-control semantics leaked into production Frame code'
Assert-Static ($code -match 'Patch_Pawn_CanTakeOrder_Frame' -and $code -match 'Patch_FloatMenuOptionProvider_SelectedPawnValid_Frame') 'Pawn-order Harmony bridge is incomplete'
Assert-Static ($code -match 'Patch_FloatMenuUtility_GetMeleeAttackAction_Frame' -and $code -match 'Patch_PawnAttackGizmoUtility_CanOrderPlayerPawn_Frame') 'Vanilla melee-control bridge is incomplete'
Assert-Static ($frame.Defs.ThingDef[0].tools.li.id -eq 'ZF_FrameStrike') 'Debug Frame requires a real melee verb for normal pawn attack semantics'
Assert-Static ($code -notmatch 'HarmonyPatch\(typeof\(Pawn\),\s*nameof\(Pawn\.IsPlayerControlled') 'Frame must not globally impersonate vanilla player-controlled pawn categories'
$dlls = @(Get-ChildItem "$repo/artifacts/ZenithFrame/Assemblies" -Filter '*.dll')
Assert-Static ($dlls.Count -eq 1 -and $dlls[0].Name -eq 'ZenithFrame.dll') 'Release contains unexpected runtime assemblies'
Write-Host "PASS: $checks XML/static/package checks"
