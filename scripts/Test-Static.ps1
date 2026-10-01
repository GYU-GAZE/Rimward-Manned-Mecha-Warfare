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
    ThingDef = $frame.Defs.ThingDef[1].verbs.li[0].defaultProjectile
    SoundDef = $frame.Defs.ThingDef[1].verbs.li[0].soundCast
}
foreach ($entry in $refs.GetEnumerator()) { Assert-Static ($defs.ContainsKey($entry.Key + ':' + $entry.Value)) "Missing game def reference: $($entry.Key):$($entry.Value)" }
$weaponVerbs = @($frame.Defs.ThingDef[1].verbs.li)
Assert-Static ($weaponVerbs[0].verbClass -eq 'ZenithFrame.Verb_HostileFrameShot' -and $weaponVerbs[0].onlyManualCast -eq 'false' -and $weaponVerbs[0].hasStandardCommand -eq 'false') 'Hostile Frame requires a primary AI-only weapon verb'
Assert-Static ($weaponVerbs[1].verbClass -eq 'ZenithFrame.Verb_FrameShot' -and $weaponVerbs[1].onlyManualCast -eq 'true') 'Player Frame weapon verb must remain manually targeted'
Assert-Static ($weaponVerbs[1].hasStandardCommand -eq 'true') 'Debug rifle must expose vanilla Command_VerbTarget / Misc1'
Assert-Static ($frame.Defs.ThingDef[0].race.thinkTreeMain -eq 'ZF_FrameIdle') 'Frame must not inherit autonomous AI'
Assert-Static ($frame.Defs.ThingDef[0].race.trainability -eq 'None') 'Frame must not expose animal training and must serialize a valid training tracker'
$frameThinkTree = @($frame.Defs.ThinkTreeDef) | Where-Object defName -EQ 'ZF_FrameIdle'
Assert-Static ($frameThinkTree.thinkRoot.GetAttribute('Class') -eq 'ThinkNode_Priority') 'Frame think tree must prioritize explicit queued jobs'
Assert-Static ($frameThinkTree.thinkRoot.subNodes.li[0].GetAttribute('Class') -eq 'ThinkNode_QueuedJob') 'Frame think tree must consume ordinary player-order jobs before idling'
Assert-Static ($frameThinkTree.thinkRoot.subNodes.li[1].GetAttribute('Class') -eq 'ZenithFrame.ThinkNode_Conditional_OperationalHostileFrame') 'Frame think tree requires a narrowly gated hostile combat branch'
Assert-Static ($frameThinkTree.thinkRoot.subNodes.li[1].subNodes.li.GetAttribute('Class') -eq 'JobGiver_AIFightEnemies') 'Hostile piloted Frames must use vanilla enemy combat jobs'
Assert-Static (@($frame.Defs.JobDef | Where-Object defName -EQ 'ZF_FrameIdleJob').driverClass -eq 'ZenithFrame.JobDriver_FrameIdle') 'Frame inert idle JobDef is missing or miswired'
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
Assert-Static ($code -match 'Patch_HealthAIUtility_CanRescueNow_Frame' -and $code -match 'patient is FramePawn') 'Frame rescue/capture exclusion is missing or overbroad'
Assert-Static ($code -match 'class CompFrameHacking' -and $code -match 'class JobDriver_HackFrame' -and $code -match 'class FloatMenuOptionProvider_HackFrame') 'Frame hacking component/job/menu flow is incomplete'
Assert-Static ($code -match 'IsHackableDisabledChassis' -and $code -match 'Frame\.Faction == null \|\|' -and $code -match 'Frame\.Faction\.HostileTo\(Faction\.OfPlayer\)') 'Hacking must allow factionless or hostile disabled Frames without allowing neutral ownership'
Assert-Static ($code -match 'class JobDriver_FrameIdle' -and $code -match 'ZF_FrameIdleJob' -and $code -notmatch 'JobMaker\.MakeJob\(RimWorld\.JobDefOf\.Wait') 'Empty Frames must use a Frame-specific idle job without vanilla Wait auto-attack'
Assert-Static ($code -match 'Patch_Pawn_MeleeVerbs_TryMeleeAttack_Frame' -and $code -match '!frame\.Cockpit\.CanOperate') 'Empty Frames must reject melee at the verb execution boundary'
Assert-Static ($code -match 'CanTakePlayerOrders' -and $code -match 'CanUseHostileCombatAI') 'Machine operation, player orders, and hostile combat AI must remain separate policies'
Assert-Static ($code -match 'TryInstallPilot' -and $code -match 'SpawnHostilePilotedFrame' -and $code -match 'Spawn hostile piloted Frame') 'Explicit hostile Frame fixture and pilot installation path are missing'
Assert-Static ($code -match 'IncidentWorker_DevelopmentEnemyFrameRaid\s*:\s*IncidentWorker_RaidEnemy' -and
    $code -match 'PawnsArrivalModeDefOf\.EdgeWalkIn' -and $code -match 'RaidStrategyDefOf\.ImmediateAttack' -and
    $code -match 'MakeLords\(parms, raiders\)') 'Development mixed Frame raid must use vanilla incident, arrival, strategy, and Lord machinery'
Assert-Static ($code -match 'TryInstallGeneratedPilot' -and $code -match 'new List<Pawn> \{ infantry, frame \}') 'Generated raid pilot must be contained before arrival and excluded from direct Lord membership'
Assert-Static ($code -match 'TransferRaidControlToEjectedPilot' -and $code -match 'lord\.RemovePawn\(Frame\)' -and $code -match 'lord\.AddPawn\(pilot\)') 'Raid ejection must remove the empty chassis and transfer the exact pilot to its Lord'
Assert-Static ($code -match 'hostileLord\.RemovePawn\(Frame\)' -and $code -match 'Frame\.SetFaction\(Faction\.OfPlayer\)') 'Hacking must remove stale hostile Lord control before faction conversion'
Assert-Static ($code -match 'TryFollowLordExitDuty' -and $code -match 'new JobGiver_ExitMapBest\(\)\.TryIssueJobPackage') 'Operational hostile Frame retreat must execute the vanilla exit-map JobGiver'
Assert-Static ($code -match 'executingPlayerAttack' -and $code -match 'CurJob\?\.playerForced == true' -and $code -match 'CurJob\.verbToUse is Verb_FrameShot') 'Player firing permission must be scoped to an explicit player-forced manual attack job'
Assert-Static ($code -match 'CanUseHostileCombatAI \|\| executingPlayerAttack') 'Primary Frame combat verb must be gated to hostile AI or an explicit manual player job'
Assert-Static ($code -match 'Frame\.SetFaction\(Faction\.OfPlayer\)' -and $code -notmatch 'ThingMaker\.MakeThing\([^\r\n]*ZF_DebugFrame') 'Hacking must convert the existing Frame through the normal faction API'
Assert-Static (@($frame.Defs.ThingDef[0].comps.li | Where-Object Class -EQ 'ZenithFrame.CompProperties_FrameHacking').Count -eq 1) 'Debug Frame requires exactly one hacking component'
Assert-Static (@($frame.Defs.JobDef | Where-Object defName -EQ 'ZF_HackFrame').driverClass -eq 'ZenithFrame.JobDriver_HackFrame') 'Hack Frame JobDef is missing or miswired'
Assert-Static ($code -match 'Patch_FloatMenuUtility_GetMeleeAttackAction_Frame' -and $code -match 'Patch_PawnAttackGizmoUtility_CanOrderPlayerPawn_Frame') 'Vanilla melee-control bridge is incomplete'
Assert-Static ($code -match 'job\.verbToUse\s*=\s*verb' -and $code -match 'job\.playerForced\s*=\s*true') 'Manual ranged order must bind its selected verb and player-forced intent'
Assert-Static ($frame.Defs.ThingDef[0].tools.li.id -eq 'ZF_FrameStrike') 'Debug Frame requires a real melee verb for normal pawn attack semantics'
Assert-Static ($code -notmatch 'HarmonyPatch\(typeof\(Pawn\),\s*nameof\(Pawn\.IsPlayerControlled') 'Frame must not globally impersonate vanilla player-controlled pawn categories'
$frameCompClasses = @($frame.Defs.ThingDef[0].comps.li | ForEach-Object { $_.GetAttribute('Class') })
Assert-Static ($frameCompClasses -contains 'ZenithFrame.CompProperties_FrameRecovery') 'Frame must include its focused recovery component'
Assert-Static ($code -match 'IsRecoverablyDisabled\s*=>' -and $code -match 'Frame\.Downed' -and $code -match '!Frame\.Dead' -and $code -match '!Frame\.Destroyed') 'Recoverable state must derive from actual living downed Frame health'
Assert-Static ($code -notmatch 'isDisabled\s*[;=]') 'Recovery must not persist a second disabled-state boolean'
$recoveryCode = Get-Content "$repo/Source/ZenithFrame/CompFrameRecovery.cs" -Raw
Assert-Static ($recoveryCode -notmatch 'SetFaction\s*\(') 'Recovery must not convert hostile Frame ownership'
Assert-Static ($recoveryCode -notmatch 'PawnGenerator|ThingMaker\.MakeThing') 'Recovery must not construct or replace the existing Frame Pawn'
$dlls = @(Get-ChildItem "$repo/artifacts/ZenithFrame/Assemblies" -Filter '*.dll')
Assert-Static ($dlls.Count -eq 1 -and $dlls[0].Name -eq 'ZenithFrame.dll') 'Release contains unexpected runtime assemblies'
Write-Host "PASS: $checks XML/static/package checks"
