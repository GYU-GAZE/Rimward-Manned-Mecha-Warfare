# Implemented MVP architecture

`FramePawn : Pawn` is a real ToolUser, Mechanoid-flesh pawn with a vanilla lancer body and placeholder graphic. It uses its own idle think trees, not mechanoid combat/work AI. A rifle is created on first spawn; it is ordinary equipment and serializes normally.

`CompPilotableMech : ThingComp, IThingHolder` owns exactly one `ThingOwner<Pawn>`. That container is the sole source of pilot identity, deep-scribed under `zenithPilot`. There is no second saved pilot object or speculative registry. Verse discovers the component holder during recursive map enumeration. RimWorld 1.6 also gives spawned pawns a map-owned ThingOwner; eligibility explicitly accepts that ownership and rejects other containers.

`JobDriver_BoardFrame` reserves the Frame, walks to touch range, revalidates and despawns/transfers the pilot. Failed eligibility never mutates ownership. A failed/throwing insertion restores an unowned pawn to its original map in a finally block; exceptions propagate. Disembarking uses Verse's own TryDrop contract, which retains the container on failure. Pilots carrying another thing/pawn, slaves, prisoners and incapacitated/mental-state pawns cannot board. Both factions must be the player.

Controls use small Frame gizmos: Board, Disembark, Draft, Move and Fire rifle. Movement uses the real Goto job and pathfinder. Fire uses AttackStatic limited to one burst with Verb_Shoot, normal projectile/range/LOS/accuracy/cooldown. A Frame-only Verb checks the cockpit and draft state on every shot. No automatic firing, aim forwarding, pawn-stat forwarding or loadout system is implemented. Right-click vanilla mech orders are not used, since they require Biotech overseer control. This avoids patching vanilla player-control predicates globally.

The normal draft controller is used, with an additional serialized draft intent because vanilla removes/recreates a non-Biotech mech's drafter while loading. The Frame disables fire-at-will and stops invalid jobs before base ticking when its pilot cannot operate it.

`Notify_Downed` ejects through the comp. FramePawn overrides `Kill` and `Destroy` to detach the pilot BEFORE the base destruction path. Ordinary destruction never owns a pilot by the time it reaches vanilla cleanup. If there is no map or a normal drop fails, ownership transfers to `PilotRecovery`, an independently deep-scribed GameComponent owner. This owner retries delivery near the home map's center once per second. With no home map, it retains the pilot across saves. If even the rescue owner is unavailable, destruction aborts loudly rather than destroying the pilot.

The vanilla game-over check only recognizes a hard-coded set of pod types. PilotRecovery clears its public game-ending countdown flag on a tick when a live free colonist is held by a spawned Frame or the rescue owner. This is explicit ownership-based presence checking, not a fake Map/Position and not a Harmony patch. Regression tests let the 400-tick vanilla countdown duration elapse with the sole colonist embarked.

## Deliberate MVP boundaries

- A stored pilot is not ticked: needs, injuries and aging are paused while embarked. Life-support, pilot damage and ongoing needs are future gameplay work.
- No boarding of hostile/neutral Frames and no faction conversion. Invalid faction changes eject on the next tick.
- One fixed weapon. No reload/ammunition, repair, energy, research, chassis variants or final art.
- No caravan, transport-pod/gravship or temporary-map removal workflow is supported. Off-map destruction rescue is a safety fallback, not travel support.
- No broad compatibility guarantee, Combat Extended support, multiplayer, autonomy or enemy raids.
- The rescue owner is visible through a recovery message on delivery; there is no rescue management UI. A game with no home map retains rescue pawns until a home map exists.

## Patch and dependency audit

Production has **zero Harmony patches, zero reflection and zero third-party runtime dependencies**. Only installed Verse/RimWorld/Unity assemblies are compile references, all with Copy Local disabled. The release contains only ZenithFrame.dll. No proprietary game assemblies, testing assemblies or decompiled game source are distributed.
