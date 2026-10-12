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
        if ($node.GetAttribute('Name')) { $parents[$node.GetAttribute('Name')] = $true }
        if ($node.GetAttribute('Abstract') -eq 'True') { continue }
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
$frameDef = @($frame.Defs.ThingDef | Where-Object defName -EQ 'ZF_DebugFrame')[0]
$rifleDef = @($frame.Defs.ThingDef | Where-Object defName -EQ 'ZF_DebugRifle')[0]
$visualExtension = @($frameDef.modExtensions.li | Where-Object { $_.GetAttribute('Class') -eq 'ZenithFrame.FrameChassisVisualExtension' })[0]
Assert-Static ($visualExtension.worldDrawSize -eq '3.5' -and $frame.Defs.PawnKindDef.lifeStages.li.bodyGraphicData.drawSize -eq '3.5') 'Frame chassis and PawnKind fallback must use the 3.5 visual draw size'
Assert-Static ($frameDef.race.baseBodySize -eq '1.5') 'Visual scale must not alter mechanical body size'
Assert-Static (!$rifleDef.graphicData.drawSize) 'Assault rifle control must retain vanilla graphic draw size'
$refs = @{
    BodyDef = $frameDef.race.body
    FleshTypeDef = $frameDef.race.fleshType
    LifeStageDef = $frameDef.race.lifeStageAges.li.def
    ThingDef = $rifleDef.verbs.li[0].defaultProjectile
    SoundDef = $rifleDef.verbs.li[0].soundCast
}
foreach ($entry in $refs.GetEnumerator()) { Assert-Static ($defs.ContainsKey($entry.Key + ':' + $entry.Value)) "Missing game def reference: $($entry.Key):$($entry.Value)" }
$weaponVerbs = @($rifleDef.verbs.li)
Assert-Static ($weaponVerbs[0].verbClass -eq 'ZenithFrame.Verb_HostileFrameShot' -and $weaponVerbs[0].onlyManualCast -eq 'false' -and $weaponVerbs[0].hasStandardCommand -eq 'false') 'Hostile Frame requires a primary AI-only weapon verb'
Assert-Static ($weaponVerbs[1].verbClass -eq 'ZenithFrame.Verb_FrameShot' -and $weaponVerbs[1].onlyManualCast -eq 'true') 'Player Frame weapon verb must remain manually targeted'
$heavyWeapon = @($frame.Defs.ThingDef | Where-Object defName -EQ 'ZF_DebugHeavyRifle')
Assert-Static ($heavyWeapon.Count -eq 1 -and $heavyWeapon.verbs.li[0].range -eq '40' -and $heavyWeapon.verbs.li[0].burstShotCount -eq '1' -and $heavyWeapon.statBases.RangedWeapon_Cooldown -eq '2.5') 'Heavy Rifle must remain a mechanically distinct single-shot development Weapon'
Assert-Static ($heavyWeapon[0].graphicData.drawSize -eq '(2.5,2.5)') 'Oversized debug Heavy Rifle must specify graphic draw size through ordinary equipment data'
Assert-Static ($weaponVerbs[1].hasStandardCommand -eq 'true') 'Debug rifle must expose vanilla Command_VerbTarget / Misc1'
Assert-Static ($frameDef.race.thinkTreeMain -eq 'ZF_FrameIdle') 'Frame must not inherit autonomous AI'
Assert-Static ($frameDef.race.trainability -eq 'None') 'Frame must not expose animal training and must serialize a valid training tracker'
$frameThinkTree = @($frame.Defs.ThinkTreeDef) | Where-Object defName -EQ 'ZF_FrameIdle'
Assert-Static ($frameThinkTree.thinkRoot.GetAttribute('Class') -eq 'ThinkNode_Priority') 'Frame think tree must prioritize explicit queued jobs'
Assert-Static ($frameThinkTree.thinkRoot.subNodes.li[0].GetAttribute('Class') -eq 'ThinkNode_QueuedJob') 'Frame think tree must consume ordinary player-order jobs before idling'
Assert-Static ($frameThinkTree.thinkRoot.subNodes.li[1].GetAttribute('Class') -eq 'ZenithFrame.ThinkNode_Conditional_OperationalPlayerFrame') 'Frame think tree requires a narrowly gated drafted-player combat-wait branch'
Assert-Static ($frameThinkTree.thinkRoot.subNodes.li[2].GetAttribute('Class') -eq 'ZenithFrame.ThinkNode_Conditional_OperationalHostileFrame') 'Frame think tree requires a narrowly gated hostile combat branch'
$hostileNodes = @($frameThinkTree.thinkRoot.subNodes.li[2].subNodes.li)
Assert-Static ($hostileNodes[0].GetAttribute('Class') -eq 'ThinkNode_Duty' -and
    $hostileNodes[1].GetAttribute('Class') -eq 'ZenithFrame.JobGiver_FramePilotAbility' -and
    $hostileNodes[2].GetAttribute('Class') -eq 'JobGiver_AIFightEnemies') 'Hostile raid Frames must consume Lord duty then pilot ability AI before the tactical combat fallback'
Assert-Static (@($frame.Defs.JobDef | Where-Object defName -EQ 'ZF_FrameIdleJob').driverClass -eq 'ZenithFrame.JobDriver_FrameIdle') 'Frame inert idle JobDef is missing or miswired'
Assert-Static ($frameDef.race.fleshType -eq 'ZF_FrameMechanical') 'Frame must use its own mechanical flesh type, not Biotech mechanoid classification'
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
$coreCode = (Get-ChildItem "$repo/Source/ZenithFrame" -Filter '*.cs' -Recurse | Where-Object FullName -NotMatch '\\obj\\' | Get-Content -Raw) -join "`n"
Assert-Static ($code -notmatch 'Coventech') 'Forbidden Coventech coupling in production source'
Assert-Static ($coreCode -notmatch 'System\.Reflection|\bGetField\(|\bGetMethod\(') 'Reflection leaked outside the isolated optional compatibility boundary'
Assert-Static ([regex]::Matches($code, 'System\.Reflection').Count -le 2 -and $code -notmatch '\bGetField\(|\bGetMethod\(') 'Optional compatibility reflection must remain cached and isolated to the VPE transpiler'
Assert-Static ($code -notmatch 'HarmonyPatch\(typeof\(ThingWithComps\).*GetComp' -and $code -match 'JobDriver_CastAbilityOnce.*CompAbilities.*MethodType\.Getter') 'VEF routing must patch the exact cast-driver getter and never the shared generic GetComp body'
Assert-Static ($code -match 'Patch_VPEPsycastWarmup_CompAbilities' -and $code -match '<WarmupToil>b__0') 'VPE warmup requires its narrow generated-method transpiler'
Assert-Static ($code -match 'Patch_FramePilotAbility_VEFHostileAI' -and $code -match 'ability\.def\.range') 'VEF hostile AI must select validated targets within each ability range'
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
Assert-Static ($code -match 'playerAutomaticFire' -and $code -match 'drafter\.FireAtWill' -and $code -match 'CurJob\?\.playerForced == true') 'Primary Frame combat verb must support vanilla Fire At Will plus explicit manual player jobs'
Assert-Static ($code -match 'KeyBindingDefOf\.Misc6' -and $code -match 'TexCommand\.FireAtWill') 'Drafted player Frames must expose vanilla Fire At Will UI semantics'
Assert-Static ($code -match 'ColonistBar\.ColonistOrCorpseAt' -and $code -match 'holdingOwner\?\.Owner is CompPilotableMech') 'Colonist-bar pilot selection must redirect through explicit cockpit containment'
Assert-Static ($code -match 'override void DrawGUIOverlay' -and $code -match 'GenMapUI\.DrawThingLabel' -and $code -match 'LabelShort') 'Spawned Frames must draw their LabelShort through GenMapUI'
Assert-Static ($code -match 'Patch_MapDeiniter_Deinit_FramePilots' -and $code -match 'HarmonyPatch\(typeof\(MapDeiniter\), nameof\(MapDeiniter\.Deinit\)\)' -and $code -match 'PrepareForMapRemoval') 'Map removal must detach cockpit pilots through the narrow MapDeiniter boundary'
Assert-Static ($code -notmatch 'HarmonyPatch\(typeof\(MapPawns\)' -and $code -notmatch 'HarmonyPatch\(typeof\(Map\)') 'Frame map cleanup must not patch broad Map or MapPawns behavior'
Assert-Static ($code -match 'IsBeingDiscarded\(Frame\)' -and $code -match 'DiscardEscapedHostilePilot' -and $code -match 'RemovePawn\(exactPilot\)') 'Hostile world GC must discard the nested pilot without leaving a second world-pawn owner'
Assert-Static ($code -match 'ForcefullyKeptPawns\.Add\(this\)' -and $code -match 'Find\.WorldPawns\.Contains\(this\)') 'A player pilot contained in an exited world Frame must keep the outer Frame alive'
Assert-Static ($code -match 'recovery\.Accept\(exactPilot\)' -and $code -match 'Find\.WorldPawns\.PassToWorld\(exactPilot\)') 'Map removal must route player and non-player pilots into explicit independent owners'
$skillCode = Get-Content "$repo/Source/ZenithFrame/PilotCombatSkills.cs" -Raw
[xml]$skillPatch = Get-Content "$repo/Mod/Patches/PilotCombatSkills.xml" -Raw
$pilotStatParts = @($skillPatch.SelectNodes('//li[@Class="ZenithFrame.StatPart_PilotSkill"]'))
Assert-Static ($pilotStatParts.Count -eq 4 -and $skillPatch.OuterXml -match 'ShootingAccuracyPawn' -and $skillPatch.OuterXml -match 'MeleeHitChance') 'Pilot combat StatParts must cover Core-only and DLC XML shapes for ShootingAccuracyPawn and MeleeHitChance'
Assert-Static ($skillCode -match 'need\.ValueFor\(pilot\)' -and $skillCode -match 'parentStat\.noSkillOffset' -and $skillCode -match 'frame\.Cockpit\?\.CanOperate') 'Pilot stat bridge must replace only the vanilla no-skill term with the cockpit pilot SkillNeed contribution'
Assert-Static ($skillCode -match 'SkillDefOf\.Shooting' -and $skillCode -match '170f : 20f' -and $skillCode -match 'AdjustedFullCycleTime') 'Frame ranged XP must reuse vanilla hostile/friendly cycle-time semantics on the pilot'
Assert-Static ($skillCode -match 'SkillDefOf\.Melee' -and $skillCode -match '200f \* verb\.verbProps\.AdjustedFullCycleTime') 'Frame melee XP must reuse vanilla cycle-time semantics on the pilot'
Assert-Static ($skillCode -match 'pilot\.skills\.Learn' -and $skillCode -notmatch 'frame\.skills\.Learn|Frame\.skills\.Learn') 'Frame combat experience must belong exclusively to the exact cockpit pilot'
Assert-Static ($skillCode -notmatch 'Pawn_SkillTracker|new\s+SkillTracker|Scribe_' -and $code -notmatch 'Scribe_.*Skill') 'Frame must not create or serialize mirrored pilot skill state'
Assert-Static ($skillCode -notmatch 'pilot\.(Map|Position)' -and $skillCode -notmatch 'DamageInfo|MeleeDamage') 'Skill bridge must not fake contained-pilot map state or modify physical melee damage'
Assert-Static ($code -notmatch 'HarmonyPatch\(typeof\(Pawn\),\s*nameof\(Pawn\.GetStatValue') 'Pilot skills must not patch the global Pawn stat lookup path'
$abilityCode = Get-Content "$repo/Source/ZenithFrame/FrameAbilities.cs" -Raw
[xml]$abilityDefs = Get-Content "$repo/Mod/Defs/FrameAbilities.xml" -Raw
$overdrive = @($abilityDefs.Defs.AbilityDef | Where-Object defName -EQ 'ZF_DebugFrameOverdrive')
$overdriveHediff = @($abilityDefs.Defs.HediffDef | Where-Object defName -EQ 'ZF_FrameOverdrive')
Assert-Static (@($frame.Defs.PawnKindDef.abilities.li | Where-Object { $_ -eq 'ZF_DebugFrameOverdrive' }).Count -eq 1) 'Debug Frame PawnKind must own exactly one intrinsic Overdrive ability'
Assert-Static ($overdrive.Count -eq 1 -and $overdrive.abilityClass -eq 'ZenithFrame.Ability_FrameOverdrive' -and $overdrive.cooldownTicksRange -eq '900') 'Frame Overdrive must use the Frame-gated vanilla Ability with a 900-tick cooldown'
Assert-Static ($overdrive.verbProperties.verbClass -eq 'Verb_CastAbility' -and $overdrive.targetRequired -eq 'false' -and $overdrive.verbProperties.targetParams.canTargetSelf -eq 'true') 'Frame Overdrive must remain a vanilla self-cast ability'
Assert-Static ($overdrive.comps.li.GetAttribute('Class') -eq 'CompProperties_AbilityGiveHediff' -and $overdrive.comps.li.hediffDef -eq 'ZF_FrameOverdrive' -and $overdrive.comps.li.onlyApplyToSelf -eq 'true') 'Frame Overdrive must apply its effect only to its Frame caster through the vanilla comp'
Assert-Static ($overdriveHediff.Count -eq 1 -and $overdriveHediff.comps.li.disappearsAfterTicks -eq '300' -and $overdriveHediff.stages.li.statOffsets.MoveSpeed -eq '6') 'Frame Overdrive requires the deterministic 300-tick chassis MoveSpeed effect'
Assert-Static ($abilityCode -match 'frame\.Cockpit\?\.CanOperate' -and $abilityCode -match 'base\.CanCast') 'Frame ability casting must add only the faction-agnostic cockpit operation gate to vanilla CanCast'
Assert-Static ($code -match 'gizmo is Command_Ability' -and $code -match 'ability\.GetGizmos\(\)' -and $code -notmatch 'Use Overdrive') 'Player Frame ability UI must expose normal Command_Ability gizmos without a custom action surrogate'
Assert-Static ($abilityCode -notmatch 'Scribe_|cooldownEndTick|FrameAbilityManager|FrameAbilityTracker|FrameCooldownManager') 'Frame abilities must use vanilla tracker cooldown and serialization state'
Assert-Static ($code -notmatch '(Cockpit\.Pilot|\bpilot)\.abilities\.(GainAbility|abilities\.Add)|frame\.abilities\s*=\s*pilot\.abilities') 'Frame abilities must never be copied or granted to the cockpit pilot'
Assert-Static ($code -match 'Frame\.SetFaction\(Faction\.OfPlayer\)' -and $code -notmatch 'ThingMaker\.MakeThing\([^\r\n]*ZF_DebugFrame') 'Hacking must convert the existing Frame through the normal faction API'
Assert-Static (@($frameDef.comps.li | Where-Object Class -EQ 'ZenithFrame.CompProperties_FrameHacking').Count -eq 1) 'Debug Frame requires exactly one hacking component'
Assert-Static (@($frame.Defs.JobDef | Where-Object defName -EQ 'ZF_HackFrame').driverClass -eq 'ZenithFrame.JobDriver_HackFrame') 'Hack Frame JobDef is missing or miswired'
Assert-Static ($code -match 'Patch_FloatMenuUtility_GetMeleeAttackAction_Frame' -and $code -match 'Patch_PawnAttackGizmoUtility_CanOrderPlayerPawn_Frame') 'Vanilla melee-control bridge is incomplete'
Assert-Static ($code -match 'job\.verbToUse\s*=\s*verb' -and $code -match 'job\.playerForced\s*=\s*true') 'Manual ranged order must bind its selected verb and player-forced intent'
Assert-Static ($frameDef.tools.li.id -eq 'ZF_FrameStrike') 'Debug Frame requires a real melee verb for normal pawn attack semantics'
Assert-Static ($code -notmatch 'HarmonyPatch\(typeof\(Pawn\),\s*nameof\(Pawn\.IsPlayerControlled') 'Frame must not globally impersonate vanilla player-controlled pawn categories'
$frameCompClasses = @($frameDef.comps.li | ForEach-Object { $_.GetAttribute('Class') })
Assert-Static ($frameCompClasses -contains 'ZenithFrame.CompProperties_FrameRecovery') 'Frame must include its focused recovery component'
Assert-Static ($code -match 'IsRecoverablyDisabled\s*=>' -and $code -match 'Frame\.Downed' -and $code -match '!Frame\.Dead' -and $code -match '!Frame\.Destroyed') 'Recoverable state must derive from actual living downed Frame health'
Assert-Static ($code -notmatch 'isDisabled\s*[;=]') 'Recovery must not persist a second disabled-state boolean'
$recoveryCode = Get-Content "$repo/Source/ZenithFrame/CompFrameRecovery.cs" -Raw
Assert-Static ($recoveryCode -notmatch 'SetFaction\s*\(') 'Recovery must not convert hostile Frame ownership'
Assert-Static ($recoveryCode -notmatch 'PawnGenerator|ThingMaker\.MakeThing') 'Recovery must not construct or replace the existing Frame Pawn'
$loadoutCode = Get-Content "$repo/Source/ZenithFrame/FrameLoadout.cs" -Raw
$controlCode = Get-Content "$repo/Source/ZenithFrame/FrameControls.cs" -Raw
$loadoutComp = @($frameDef.comps.li | Where-Object Class -EQ 'ZenithFrame.CompProperties_FrameLoadout')
Assert-Static ($loadoutComp.Count -eq 1 -and @('weaponCapacity','utilityCapacity','powerCapacity','headCapacity','shoulderCapacity','offhandCapacity').Where({$loadoutComp.$_ -eq '1'}).Count -eq 6) 'Debug Frame must define one explicit slot for every canonical addon category'
Assert-Static ($loadoutComp.defaultWeapon -eq 'ZF_DebugRifle' -and $loadoutComp.defaultPower -eq 'ZF_DebugReactor' -and !$loadoutComp.defaultUtility) 'Definition defaults must provide Weapon and Power once while Utility starts empty'
Assert-Static (@($frame.Defs.ThingDef | Where-Object { $_.defName -in @('ZF_DebugRifle','ZF_DebugHeavyRifle') -and $_.modExtensions.li.category -eq 'Weapon' }).Count -eq 2) 'Both development weapons require explicit Frame Weapon metadata'
Assert-Static (@($frame.Defs.ThingDef | Where-Object { $_.defName -eq 'ZF_DebugMobilityBooster' -and $_.modExtensions.li.category -eq 'Utility' -and $_.modExtensions.li.powerDraw -eq '2' -and $_.modExtensions.li.moveSpeedOffset -eq '1' }).Count -eq 1) 'Mobility Booster requires deterministic Utility metadata'
Assert-Static (@($frame.Defs.ThingDef | Where-Object { $_.defName -eq 'ZF_DebugReactor' -and $_.modExtensions.li.category -eq 'Power' -and $_.modExtensions.li.powerOutput -eq '2' }).Count -eq 1) 'Debug Reactor requires two static addon power points'
Assert-Static (@($frame.Defs.ThingDef | Where-Object { $_.defName -eq 'ZF_DebugWeakPowerCell' -and $_.modExtensions.li.category -eq 'Power' -and $_.modExtensions.li.powerOutput -eq '1' }).Count -eq 1) 'Weak Power Cell requires one static addon power point'
Assert-Static ($controlCode -notmatch 'ZF_DebugRifle|ThingMaker\.MakeThing') 'Frame SpawnSetup must not hardcode or manufacture its default weapon'
Assert-Static ($recoveryCode -notmatch 'ZF_DebugRifle|ReclaimDroppedDebugWeapon|GenRadial|ThingMaker\.MakeThing') 'Recovery must reclaim only the generic exact remembered weapon reference'
Assert-Static ($loadoutCode -match 'mindState\?\.droppedWeapon' -and $loadoutCode -match 'rememberedDroppedWeapon' -and $loadoutCode -match 'Patch_PawnHealthTracker_MakeDowned_FrameWeapon') 'Loadout recovery must prefer vanilla remembered primary and narrowly retain the exact Frame drop reference'
Assert-Static ($loadoutCode -match 'Weapon\s*=\s*0' -and $loadoutCode -match 'Utility\s*=\s*1' -and $loadoutCode -match 'Power\s*=\s*2' -and $loadoutCode -match 'Head\s*=\s*3' -and $loadoutCode -match 'Shoulder\s*=\s*4' -and $loadoutCode -match 'Offhand\s*=\s*5') 'Serialized addon enum values must preserve Weapon Utility Power and append the three new categories'
Assert-Static ([regex]::Matches($loadoutCode, 'Scribe_References\.Look').Count -eq 1 -and [regex]::Matches($loadoutCode, 'Scribe_Deep\.Look').Count -eq 5) 'Loadout must shallow-scribe only the dropped Weapon and deep-scribe all five contained physical slot owners'
Assert-Static (@('utilities','powerSources','heads','shoulders','offhands').Where({$loadoutCode -match ('ThingOwner<Thing>\s+' + $_)}).Count -eq 5 -and $loadoutCode -match 'class CompFrameLoadout : ThingComp, IThingHolder') 'Every non-equipment module slot requires physical ThingOwner ownership with Verse traversal'
Assert-Static ($loadoutCode -match 'GetDirectlyHeldThings\(\) => utilities' -and @('powerHolder','headHolder','shoulderHolder','offhandHolder').Where({$loadoutCode -match ('children\.Add\(' + $_ + '\)')}).Count -eq 4 -and $code -match 'class FramePawn : Pawn' -and $code -notmatch 'new void GetChildHolders') 'Comp-owned modules traverse once through vanilla ThingWithComps holders'
Assert-Static ($loadoutCode -notmatch 'System\.Reflection|GetField\(|GetMethod\(') 'Frame loadout must use no reflection'
Assert-Static ($loadoutCode -match 'GetModExtension<FrameAddonExtension>' -and $loadoutCode -match 'Extension\(item\)\?\.category != category') 'Every slot must reject ordinary Things and mismatched addon categories explicitly'
Assert-Static (@('weapon','utility','power','head','shoulder','offhand').Where({$loadoutCode -match ($_ + 'DefaultInitialized')}).Count -eq 6) 'Every category default requires independent once-only initialization state'
Assert-Static ($loadoutCode -match 'AvailableAddonPower' -and $loadoutCode -match 'RequiredAddonPower' -and $loadoutCode -match 'InstalledModules\.Where\(IsModulePowered\)' -and $loadoutCode -match 'HasSufficientAddonPower') 'Generalized static addon power API is incomplete'
Assert-Static ($loadoutCode -match 'override float GetStatOffset\(StatDef stat\)' -and $loadoutCode -match 'StatDefOf\.MoveSpeed' -and $code -notmatch 'HarmonyPatch\(typeof\((Thing|Pawn)\).*GetStatValue') 'Powered module movement must use a local stat hook without global stat lookup patches'
Assert-Static ($loadoutCode -notmatch 'battery|recharge|fuel|temperature|heatPerTick|heatCapacity') 'Static addon power must not introduce a fuel heat recharge or battery simulation'
Assert-Static ($loadoutCode -match 'PrepareForFrameDestruction' -and $loadoutCode -match 'Frame\.Spawned.*TryDrop' -and $loadoutCode -match 'exact\.Destroy\(\)') 'Destruction requires explicit spawned salvage and off-map destruction policies'
Assert-Static ($loadoutCode -notmatch 'InstalledUtilityDef|InstalledPowerDef|InstalledHeadDef|InstalledShoulderDef|InstalledOffhandDef|utilityActive|powerActive|Scribe_Values\.Look\(ref\s+\w*Active') 'Installed addon identity and active state must not be serialized as parallel fake truth'
$moduleCode = Get-Content "$repo/Source/ZenithFrame/FrameModules.cs" -Raw
[xml]$moduleDefs = Get-Content "$repo/Mod/Defs/FrameModules.xml" -Raw
[xml]$armorPatch = Get-Content "$repo/Mod/Patches/FrameModuleArmor.xml" -Raw
Assert-Static ($loadoutCode -match 'installedHediff' -and $loadoutCode -match 'poweredHediff' -and $loadoutCode -match 'IsMountValid' -and $loadoutCode -match 'PartIsMissing' -and $loadoutCode -match 'GetPartHealth') 'Passive and powered projections must derive from installed physical modules and live mount health'
Assert-Static ($loadoutCode -notmatch 'Hediff_AddedPart' -and $moduleCode -notmatch 'Hediff_AddedPart') 'Modular anatomy must not replace or fabricate chassis body parts'
Assert-Static ($moduleCode -match 'Scribe_Deep\.Look\(ref ability, "frameModuleAbility"\)' -and $moduleCode -match 'ability\.Module = parent' -and $moduleCode -match 'ability\.pawn = frame') 'The physical Shoulder module must deep-own and rebind its exact vanilla Ability state'
Assert-Static ($moduleCode -match '!\(pawn is FramePawn frame\)' -and $moduleCode -match 'frame\.Loadout\?\.IsInstalled\(Module\)' -and $moduleCode -match 'frame\.Service\?\.IsServiceLocked') 'Module Ability execution must require its exact powered installed module and normal Frame control gates'
Assert-Static ($moduleCode -match 'HarmonyPatch\(typeof\(ArmorUtility\), nameof\(ArmorUtility\.GetPostArmorDamage\)\)' -and $moduleCode -match '\[ThreadStatic\]' -and $moduleCode -match 'StatPart_FrameModuleArmor') 'Localized module armor must add a narrow per-hit context around vanilla ArmorUtility'
Assert-Static (@($armorPatch.SelectNodes('//li[@Class="ZenithFrame.StatPart_FrameModuleArmor"]')).Count -eq 6 -and $armorPatch.OuterXml -match 'ArmorRating_Sharp' -and $armorPatch.OuterXml -match 'ArmorRating_Blunt' -and $armorPatch.OuterXml -match 'ArmorRating_Heat') 'Localized module armor StatPart must cover vanilla sharp blunt and heat armor stats'
Assert-Static ($moduleCode -match 'FrameAddonCategory\.Weapon.*!\(pawn is FramePawn\)' -and $moduleCode -match 'pawn is FramePawn.*FrameAddonCategory\.Weapon') 'Frame weapons must reject human users and Frames must reject unmarked human weapons'
Assert-Static (@($moduleDefs.Defs.HediffDef | Where-Object defName -EQ 'ZF_BeamSwordPowered').comps.li.GetAttribute('Class') -eq 'HediffCompProperties_VerbGiver') 'Powered Beam Sword must supply its melee attack through a normal HediffComp verb giver'
Assert-Static ($coreCode -notmatch 'FrameAbilityManager|FrameModuleAbilityManager|moduleCooldown|Scribe_Values\.Look\(ref.*cooldown') 'Module abilities must not introduce a parallel cooldown manager or serialized cooldown truth'
$uiCode = Get-Content "$repo/Source/ZenithFrame/FrameUI.cs" -Raw
$visualUi = Get-Content "$repo/Source/ZenithFrame/FrameUI.Visual.cs" -Raw
Assert-Static ($frameDef.inspectorTabs.li -eq 'ZenithFrame.ITab_FrameVisual' -and $visualUi -match 'class ITab_FrameVisual : ITab') 'Frame UI must use its native visual inspect tab'
Assert-Static ($uiCode -match 'class FrameUiSnapshot' -and $uiCode -match 'Pilot = frame\?\.Cockpit\?\.Pilot' -and @('InstalledWeapon','InstalledUtility','InstalledPower','InstalledHead','InstalledShoulder','InstalledOffhand').Where({$uiCode -match $_}).Count -eq 6) 'Frame UI presentation must read exact cockpit and all physical loadout objects'
Assert-Static ($uiCode -match 'AvailableAddonPower' -and $uiCode -match 'RequiredAddonPower' -and $visualUi -match 'IsModulePowered') 'Frame UI power display must derive from the live generalized static addon budget'
Assert-Static ($uiCode -match 'summaryHealth.*SummaryHealthPercent' -and $uiCode -match 'IsRecoverablyDisabled' -and $uiCode -match 'IsHackableDisabledChassis') 'Frame UI condition recovery and hacking state must derive from existing authoritative components'
Assert-Static ($uiCode -match 'Dialog_Rename<Dialog_RenameFrame\.Target>' -and $uiCode -match 'frame\.Name = new NameSingle' -and $uiCode -notmatch 'customFrameName|Scribe_') 'Frame naming must use normal Pawn.Name persistence without parallel UI state'
Assert-Static ($visualUi -match 'PortraitsCache\.Get\(pilot' -and $uiCode -match 'CharacterCardUtility\.DrawCharacterCard' -and $uiCode -match 'HealthCardUtility\.DrawPawnHealthCard') 'Contained pilot inspection must reuse vanilla portrait character and health presentation'
Assert-Static (($uiCode + $visualUi) -notmatch 'GenSpawn|\.DeSpawn\(|ThingMaker|PassToWorld') 'Frame UI must never temporarily spawn move or recreate the contained pilot or loadout items'
Assert-Static ($uiCode -notmatch 'ZF_DebugRifle|ZF_DebugReactor|ZF_DebugMobilityBooster|ZF_DebugFrameOverdrive|ZF_DebugFrame') 'Production Frame UI must remain data-driven with no debug content DefNames'
Assert-Static ($uiCode -notmatch 'System\.Reflection|GetField\(|GetMethod\(|Coventech|MechanitorUtility|IsColonyMech|Overseer') 'Frame UI must use stable local APIs without reflection Coventech or Biotech control semantics'
Assert-Static ($uiCode -notmatch 'GainAbility|GiveAbility|StartAbilityJob|QueueCastingJob|GetGizmos\(') 'Frame UI must display ability summaries without recreating commands or entering targeting'
Assert-Static ($uiCode -match 'IsReadOnly = frame\?\.Faction != Faction\.OfPlayer' -and $uiCode -match 'CanRename = .*Faction == Faction\.OfPlayer' -and $uiCode -match 'CanDisembark = .*Faction == Faction\.OfPlayer') 'Hostile and factionless Frame UI must remain read-only'
Assert-Static ($visualUi -match 'new Rect\(x \+ 10f, y \+ 21f, slotSize, slotSize\)' -and $visualUi -match 'new Rect\(area\.x, rowY, area\.width, RowHeight\)' -and $visualUi -notmatch 'HealthRowHeight|BeginScrollView' -and $visualUi -match 'issues\.Count - shown') 'Visual Frame UI must use square slots and fixed compact health rows without a main scroll view'
Assert-Static ($visualUi -match 'public override Vector2 InitialSize => new Vector2\(520f, 420f\)' -and $visualUi -match 'FramePaintPresets' -and $visualUi -match 'GUIUtility\.systemCopyBuffer' -and $visualUi -match 'valueBar' -and $visualUi -match 'KeyCode\.Return' -and $visualUi -match 'defaultVisor' -and $visualUi -notmatch '"Current " \+ channel|"Apply"') 'Compact paint editor must expose vertical value, Enter-to-commit hex, presets, favorites and defaults'
$popupPlacement = Get-Content "$repo/Source/ZenithFrame/FramePopupPlacement.cs" -Raw
Assert-Static ($popupPlacement -match 'rightSpace >= width' -and $popupPlacement -match 'leftSpace >= width' -and $visualUi -match 'FramePopupPlacement\.Beside' -and $uiCode -match 'FramePopupPlacement\.Beside') 'Paint and health information dialogs must share right-first side placement'
Assert-Static ($visualUi -match 'Widgets\.ButtonInvisible\(panel\)' -and $visualUi -match 'Widgets\.ButtonInvisible\(health\)' -and $visualUi -notmatch '"Frame condition"|"Pilot health"') 'Exact Frame and pilot health regions must be clickable without redundant headings'
$healthUi = Get-Content "$repo/Source/ZenithFrame/FrameHealthSummary.cs" -Raw
Assert-Static ($healthUi -match 'GetMissingPartsCommonAncestors' -and $healthUi -match 'UIGroupKey' -and $healthUi -match 'hediff\.LabelCap' -and $healthUi -match 'hediff\.LabelColor' -and $healthUi -match 'hediff\.StateIcon' -and $healthUi -match 'hediff\.GetTooltip' -and $healthUi -match 'BleedRateTotal') 'Compact health rows must derive from live vanilla Hediff semantics and pin bleed rate'
$bodyCode = Get-Content "$repo/Source/ZenithFrame/FrameDamage.cs" -Raw
$pilotHealthCode = Get-Content "$repo/Source/ZenithFrame/FramePilotHealth.cs" -Raw
[xml]$bodyDefs = Get-Content "$repo/Mod/Defs/FrameBodies.xml" -Raw
$body = @($bodyDefs.Defs.BodyDef | Where-Object defName -EQ 'ZF_HumanoidFrameBody')
$bodyPartRecords = @($body.SelectNodes('.//corePart | .//parts/li'))
Assert-Static ($frameDef.race.body -eq 'ZF_HumanoidFrameBody' -and $frameDef.race.body -ne 'Lancer') 'Debug Frame must use the original Zenith body instead of Lancer'
Assert-Static ($body.Count -eq 1 -and $bodyPartRecords.Count -eq 25) 'Zenith humanoid body must contain exactly 25 mechanical body records'
Assert-Static (@($body.SelectNodes('.//li[def="ZF_FrameCore" and depth="Inside"]')).Count -eq 1 -and @($body.SelectNodes('.//li[def="ZF_FrameCockpit" and depth="Inside"]')).Count -eq 1) 'Core and Cockpit must be internal Frame body parts'
Assert-Static ($bodyDefs.Defs.BodyPartDef[0].GetAttribute('Name') -eq 'ZF_FramePartBase' -and $bodyDefs.Defs.BodyPartDef[0].bleedRate -eq '0' -and
    @($bodyDefs.Defs.BodyPartDef | Where-Object { $_.GetAttribute('Abstract') -ne 'True' -and $_.GetAttribute('ParentName') -eq 'ZF_FramePartBase' }).Count -eq 13) 'Every concrete Frame body part must inherit zero-bleed mechanical metadata'
Assert-Static ($bodyCode -match 'class FrameDamageRules' -and $bodyCode -match 'ZF_FrameTorso' -and $bodyCode -match 'ZF_FrameCore' -and $bodyCode -match 'ZF_FrameHeadAssembly' -and $bodyCode -match 'ZF_FrameCockpit') 'Frame damage semantics must remain centralized around explicit structural parts'
Assert-Static ($bodyCode -match 'ShouldBeDeadFromLethalDamageThreshold' -and $bodyCode -match 'ShouldBeDeadFromRequiredCapacity' -and $bodyCode -match 'ShouldBeDowned' -and $bodyCode -match '___pawn is FramePawn') 'Frame-only health guards are incomplete or insufficiently gated'
Assert-Static ($bodyCode -match 'HarmonyPatch\(typeof\(Thing\), nameof\(Thing\.TakeDamage\)\)' -and $bodyCode -match '__result\?\.hediffs' -and $bodyCode -match 'ReferenceEquals\(injury\.Part, __state\.Cockpit\)' -and $bodyCode -match '__state\.Pilot\.TakeDamage') 'Cockpit trauma must derive from the resolved exact Cockpit injury and damage the exact pilot normally'
Assert-Static ($bodyCode -notmatch 'Rand\.|Chance\(|ZF_DebugRifle|ZF_DebugHeavyRifle' -and $bodyCode -match 'pilotTraumaFraction') 'Pilot trauma must be definition-driven without random chance or weapon DefName gates'
Assert-Static ($bodyCode -match 'Patch_Pawn_Kill_ContainedFramePilot' -and $bodyCode -match 'cockpit\.EmergencyEject\(\)') 'Contained pilot death must detach before vanilla corpse creation'
Assert-Static ($coreCode -match 'exactPilot\.health\.HealthTick\(\)' -and $coreCode -match 'exactPilot\.health\.HealthTickInterval\(1\)' -and $coreCode -notmatch '\bPilot\?*\.DoTick\(|\bpilot\.DoTick\(') 'Contained pilot must receive per-tick and interval health simulation and never full Pawn.DoTick'
Assert-Static ($pilotHealthCode -match 'MedicalTendSpeed' -and $pilotHealthCode -match 'SkillDefOf\.Medicine' -and $pilotHealthCode -match 'TendUtility\.DoTend\(ExactPilot, ExactPilot, Medicine\)') 'Embarked self-tend must use exact pilot speed skill XP and normal TendUtility'
Assert-Static ($code -match 'class FloatMenuOptionProvider_FramePilotSelfTend' -and $code -notmatch 'defaultLabel = "Pilot self-tend"' -and $code -match 'class FloatMenuOptionProvider_DockFrame') 'Self-tend and docking must be narrow right-click orders without a self-tend gizmo'
Assert-Static ($frameDef.race.hideTrainingTab -eq 'true' -and $frameDef.race.trainability -eq 'None') 'Frame training tab must be hidden without changing trainability'
Assert-Static ($code -match 'inventory\?\.innerContainer\s*\.OfType<Medicine>' -and $pilotHealthCode -notmatch 'FindBestMedicine|listerThings|StoreUtility') 'Embarked self-tend may select only exact medicine in the pilot inventory'
Assert-Static ($recoveryCode -notmatch 'RestorePart\(') 'Frame recovery must never reconstruct missing mechanical parts'
Assert-Static ($code -notmatch '\b(FrameHP|PilotHP)\b|Scribe_Values\.Look\(ref\s+(isDisabled|isDestroyed|headDestroyed|legsDestroyed)') 'No parallel HP or serialized derived damage-state truth is allowed'
$uiCode = Get-Content "$repo/Source/ZenithFrame/FrameUI.cs" -Raw
Assert-Static ($uiCode -match 'FrameMoving' -and $uiCode -match 'FrameManipulation' -and $uiCode -match 'FrameSight' -and $uiCode -match 'PilotHealth' -and $uiCode -match 'PilotConsciousness' -and $uiCode -match 'PilotBleedRate') 'Frame UI snapshot must expose live chassis capacities and pilot vitals'
$serviceCode = Get-Content "$repo/Source/ZenithFrame/FrameService.cs" -Raw
$hackingCode = Get-Content "$repo/Source/ZenithFrame/FrameHacking.cs" -Raw
$boardingCode = Get-Content "$repo/Source/ZenithFrame/Boarding.cs" -Raw
$productionCode = (Get-ChildItem "$repo/Source/ZenithFrame" -Filter '*.cs' | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$releaseContent = (Get-ChildItem "$repo/Mod" -File -Recurse | Where-Object Extension -In '.xml','.txt' | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
$dockDef = @($frame.Defs.ThingDef | Where-Object defName -EQ 'ZF_FrameDockingBay')[0]
Assert-Static ($hackingCode -notmatch 'WorkTicks' -and $hackingCode -match 'hackProgress' -and $hackingCode -match 'StatDefOf\.HackingSpeed') 'Production hacking must use persistent HackingSpeed progress without the old fixed WorkTicks path'
Assert-Static ($hackingCode -match 'SkillDefOf\.Intellectual' -and $hackingCode -match 'EffecterDefOf\.Hacking' -and $hackingCode -match 'Hacking_Started' -and $hackingCode -match 'Hacking_Completed') 'Frame hacking must use Core-safe Intellectual XP and verified vanilla hacking feedback'
Assert-Static ($productionCode -notmatch 'Odyssey' -and $releaseContent -notmatch 'ludeon\.rimworld\.odyssey|Odyssey') 'Core production package must not depend on or copy Odyssey content'
Assert-Static ($serviceCode -match 'GetMissingPartsCommonAncestors' -and $serviceCode -match 'material\.stackCount.*materialCount' -and $serviceCode -match 'material\.Destroy\(\).*RestorePart|material\.stackCount -= materialCount;\s*Frame\.health\.RestorePart') 'Missing-part restoration must consume exact physical material at successful reconstruction completion'
Assert-Static ($serviceCode -match 'ZF_FrameTorso.*ZF_FrameCore|IsRecoverablePart' -or $bodyCode -match 'IsRecoverablePart.*ZF_FrameTorso.*ZF_FrameCore') 'Torso and Core must remain excluded from living-chassis reconstruction'
Assert-Static ($visualUi -match 'QueueInstall\(category, exact\)' -and $visualUi -match 'QueueRemove\(category\)' -and $visualUi -notmatch 'TryInstallWeapon|TryInstallUtility|TryInstallPower') 'Frame UI must queue work against exact Things instead of teleporting modules'
Assert-Static ($serviceCode -match 'Scribe_Collections\.Look\(ref orders' -and $serviceCode -match 'Scribe_References\.Look\(ref incoming' -and $serviceCode -match 'Scribe_References\.Look\(ref expectedInstalled' -and $serviceCode -notmatch 'pendingModuleDef|GetNamed\(.*pending') 'Service orders must persist exact incoming and expected installed Thing references'
Assert-Static ($serviceCode -match 'Frame\.Loadout\?\.TryInstall\(category, thing\)' -and $serviceCode -match 'Frame\.Loadout\?\.TryRemove\(category, out removed\)' -and $serviceCode -notmatch 'AddEquipment\(') 'Service must delegate every module category to the canonical generic loadout ownership API'
Assert-Static ($serviceCode -notmatch 'Scribe_Values\.Look\(ref\s+isDocked|(?:private|public|protected)\s+bool\s+isDocked\s*[;=]' -and $serviceCode -match 'DockingBayFor\(FramePawn frame\)') 'Docking must remain position-derived without a saved isDocked flag'
Assert-Static ($dockDef.thingClass -eq 'Building' -and $dockDef.passability -eq 'Standable' -and $dockDef.size -eq '(3,3)' -and $serviceCode -notmatch 'ThingOwner<FramePawn>|DeSpawn.*Frame') 'Docking Bay must be a standable building and never a Frame container'
Assert-Static ($serviceCode -notmatch 'CanOperate\s*=' -and $productionCode -match 'IsServiceLocked') 'Service lock must gate controls without mutating CanOperate truth'
Assert-Static ($serviceCode -match 'Frame\.Faction == Faction\.OfPlayer' -and $serviceCode -match 'HomeAreaAllows') 'Automatic Construction service must remain player-owned and Home Area aware'
Assert-Static ($productionCode -notmatch 'Coventech|System\.Reflection|BindingFlags|GetMethod\(') 'Production service slice must contain no Coventech or reflection hacks'
Assert-Static ($productionCode -notmatch 'catch\s*(\([^)]*\))?\s*\{\s*\}') 'Production code must not swallow exceptions'
Assert-Static ($productionCode -notmatch 'IFrameSlotProvider|RegisterSlot|ThirdParty|GenericFrameService') 'Service architecture must stay Zenith-specific instead of becoming a generic third-party slot framework'
$boardingDuration = [int]$frameDef.modExtensions.li.Where({$_.GetAttribute('Class') -eq 'ZenithFrame.FrameServiceExtension'}).normalBoardingTicks
Assert-Static ($boardingDuration -eq 300 -and $serviceCode -match 'dockingSpeedMultiplier = 5f' -and $boardingCode -match 'DockingBayUtility\.BoardingTicks') 'Timed boarding and x5 derived docking acceleration must remain definition-driven'
$dlls = @(Get-ChildItem "$repo/artifacts/ZenithFrame/Assemblies" -Filter '*.dll')
Assert-Static ($dlls.Count -eq 1 -and $dlls[0].Name -eq 'ZenithFrame.dll') 'Release contains unexpected runtime assemblies'
$renderCode = Get-Content "$repo/Source/ZenithFrame/FrameRendering.cs" -Raw
$liveryCode = Get-Content "$repo/Source/ZenithFrame/FrameLivery.cs" -Raw
$renderDef = [xml](Get-Content "$repo/Mod/Defs/FrameRendering.xml" -Raw)
Assert-Static ($frameDef.race.renderTree -eq 'ZF_FrameModular' -and $renderDef.Defs.PawnRenderTreeDef.defName -eq 'ZF_FrameModular') 'Debug Frame must use its own native Pawn render tree'
Assert-Static ($frame.Defs.PawnKindDef.lifeStages.li.bodyGraphicData.texPath -ne 'Things/Pawn/Mechanoid/Lancer' -and $frameDef.description -notmatch 'lancer') 'Debug Frame must not retain the visible Lancer placeholder'
Assert-Static ($renderCode -match 'InstalledHead' -and $renderCode -match 'InstalledShoulder' -and $renderCode -match 'IsMountValid' -and $renderCode -match 'HeadState' -and $renderCode -notmatch 'IsModulePowered|HasSufficientAddonPower') 'Body overlays must follow exact physical modules and structural mounts rather than power'
Assert-Static ($renderCode -notmatch 'Scribe_|OffsetForChassis|OffsetForHelmet|Texture2D\s*\(' -and $renderCode -match 'PawnRenderNodeWorker_Body') 'Frame rendering must remain derived, layer-based and native'
Assert-Static (@($frameDef.comps.li | Where-Object Class -EQ 'ZenithFrame.CompProperties_FrameLivery').Count -eq 1 -and
    $liveryCode -match 'Scribe_Values\.Look\(ref primary' -and $liveryCode -match 'Scribe_Values\.Look\(ref secondary' -and
    $liveryCode -match 'Scribe_Values\.Look\(ref visor' -and $liveryCode -match 'SetAllGraphicsDirty\(\)') 'One physical Frame comp must own all three saved colors and invalidate native graphics'
Assert-Static ($renderCode -match 'ShaderDatabase\.CutoutComplex' -and $renderCode -match 'GraphicDatabase\.Get<Graphic_Single>' -and
    $renderCode -match 'frame\?\.Livery\?\.Primary' -and $renderCode -match 'frame\?\.Livery\?\.Secondary' -and
    $renderCode -match 'frame\?\.Livery\?\.Visor' -and $renderCode -match 'ShaderDatabase\.Cutout' -and
    $renderCode -notmatch '\.MatSingle\.color\s*=|\.colorTwo\s*=') 'Body layers must request native per-color masked Graphics without mutating shared materials'
Assert-Static ($heavyWeapon[0].graphicData.drawSize -eq '(2.5,2.5)' -and !$rifleDef.graphicData.drawSize) 'Livery must not change the normal or oversized weapon scale'
$parts = @('Torso','Head','Helmet','Shoulder')
$directions = @('South','Sides','North')
$textures = "$repo/Mod/Textures/ZenithFrame/Frames/Debug/TestFrame"
Assert-Static (@(Get-ChildItem $textures -File -Filter '*.png' | Where-Object BaseName -NotMatch '_(m|v)$').Count -eq 12 -and
    @(Get-ChildItem $textures -File -Filter '*_m.png').Count -eq 12 -and
    @(Get-ChildItem $textures -File -Filter '*_v.png').Count -eq 9) 'Test Frame must contain twelve original canvases, twelve paint masks and nine visor overlays'
Add-Type -AssemblyName System.Drawing
foreach ($part in $parts) {
    foreach ($direction in $directions) {
        $file = Join-Path $textures "${part}_${direction}.png"
        Assert-Static (Test-Path -LiteralPath $file) "Missing Test Frame texture: $file"
        $bytes = [System.IO.File]::ReadAllBytes($file)
        Assert-Static ($bytes.Length -gt 33 -and [Convert]::ToHexString($bytes[0..7]) -eq '89504E470D0A1A0A' -and $bytes[25] -eq 6) "Invalid RGBA PNG: $file"
        $bitmap = [System.Drawing.Bitmap]::new($file)
        $maskFile = Join-Path $textures "${part}_${direction}_m.png"
        Assert-Static (Test-Path -LiteralPath $maskFile) "Missing Test Frame mask: $maskFile"
        $maskBytes = [System.IO.File]::ReadAllBytes($maskFile)
        Assert-Static ($maskBytes.Length -gt 33 -and [Convert]::ToHexString($maskBytes[0..7]) -eq '89504E470D0A1A0A' -and $maskBytes[25] -eq 6) "Invalid RGBA mask: $maskFile"
        $mask = [System.Drawing.Bitmap]::new($maskFile)
        try {
            Assert-Static ($bitmap.Width -eq 1024 -and $bitmap.Height -eq 1024) "Wrong full-canvas size: $file"
            Assert-Static ($bitmap.GetPixel(0,0).A -eq 0 -and $bitmap.GetPixel(1023,0).A -eq 0 -and $bitmap.GetPixel(0,1023).A -eq 0 -and $bitmap.GetPixel(1023,1023).A -eq 0) "Opaque PNG background: $file"
            Assert-Static ($mask.Width -eq 1024 -and $mask.Height -eq 1024) "Wrong mask canvas size: $maskFile"
            $red = 0; $green = 0; $alphaAligned = $true
            for ($y=0; $y -lt 1024 -and $alphaAligned; $y+=8) {
                for ($x=0; $x -lt 1024; $x+=8) {
                    $sourcePixel = $bitmap.GetPixel($x,$y)
                    $maskPixel = $mask.GetPixel($x,$y)
                    if ($sourcePixel.A -ne $maskPixel.A) { $alphaAligned = $false; break }
                    if ($maskPixel.R -eq 255 -and $maskPixel.G -eq 0 -and $maskPixel.B -eq 0) { $red++ }
                    if ($maskPixel.R -eq 0 -and $maskPixel.G -eq 255 -and $maskPixel.B -eq 0) { $green++ }
                }
            }
            Assert-Static $alphaAligned "Mask alpha drift in ${part}_${direction}"
            Assert-Static ($red -gt 0 -and $green -gt 0) "Both paint channels must appear in ${part}_${direction}"
        } finally { $bitmap.Dispose(); $mask.Dispose() }
        $releaseFile = Join-Path "$repo/artifacts/ZenithFrame/Textures/ZenithFrame/Frames/Debug/TestFrame" "${part}_${direction}.png"
        $releaseMask = Join-Path "$repo/artifacts/ZenithFrame/Textures/ZenithFrame/Frames/Debug/TestFrame" "${part}_${direction}_m.png"
        Assert-Static ((Test-Path -LiteralPath $releaseFile) -and
            (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $releaseFile -Algorithm SHA256).Hash) "Release texture differs from source copy: $file"
        Assert-Static ((Test-Path -LiteralPath $releaseMask) -and
            (Get-FileHash -LiteralPath $maskFile -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $releaseMask -Algorithm SHA256).Hash) "Release mask differs from source copy: $maskFile"
        if ($part -ne 'Torso') {
            $visorFile = Join-Path $textures "${part}_${direction}_v.png"
            $releaseVisor = Join-Path "$repo/artifacts/ZenithFrame/Textures/ZenithFrame/Frames/Debug/TestFrame" "${part}_${direction}_v.png"
            Assert-Static ((Test-Path -LiteralPath $visorFile) -and (Test-Path -LiteralPath $releaseVisor) -and
                (Get-FileHash -LiteralPath $visorFile -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $releaseVisor -Algorithm SHA256).Hash) "Release visor overlay differs from source copy: $visorFile"
            $visorBitmap = [System.Drawing.Bitmap]::new($visorFile)
            $sourceBitmap = [System.Drawing.Bitmap]::new($file)
            try {
                Assert-Static ($visorBitmap.Width -eq 1024 -and $visorBitmap.Height -eq 1024) "Wrong visor canvas: $visorFile"
                $visible = 0; $escaped = $false
                for ($y=0; $y -lt 1024; $y+=4) {
                    for ($x=0; $x -lt 1024; $x+=4) {
                        $visorPixel = $visorBitmap.GetPixel($x,$y)
                        if ($visorPixel.A -gt 0) {
                            $visible++
                            if ($sourceBitmap.GetPixel($x,$y).A -eq 0) { $escaped = $true }
                        }
                    }
                }
                Assert-Static ($visible -gt 0 -and !$escaped) "Visor overlay must tint visible source pixels only: $visorFile"
            } finally { $visorBitmap.Dispose(); $sourceBitmap.Dispose() }
        }
    }
}
Write-Host "PASS: $checks XML/static/package checks"
