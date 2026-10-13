# Frame Service and Docking

## Production contract

Frame maintenance is Construction work against the existing `FramePawn` and its normal health tracker. There is no service HP, reconstructed-body mirror, saved disable flag, or free production recovery path.

Ordinary damage costs work only. `WorkGiver_RepairFrame` automatically considers living idle player Frames in Home Area; forced work may operate outside Home Area. It chooses the highest-severity real injury with body order as a stable tiebreaker, applies `ConstructionSpeed`, awards Construction XP, and reduces that injury's actual severity. Frames have zero biological natural-healing factor.

Missing structure costs work and physical material. `GetMissingPartsCommonAncestors()` prevents charging separately for descendants of one missing parent. The chassis extension defines material, cost per part HP, work per part HP, ordinary repair work, module service work, hacking defence, boarding time, Dock multiplier, and storage radius. The Debug Frame currently uses Steel and provisional development values.

Reconstruction reserves the Frame and exact material stack, carries the material through the job, persists the intended body-part index and remaining work, and consumes the required count only at successful completion. `Pawn_HealthTracker.RestorePart` restores the exact common ancestor. Interruption leaves the missing part missing and the carried material recoverable. Torso and Core never qualify because their destruction already kills the chassis.

## Hacking and boarding

`CompFrameHacking` stores progress on the physical chassis. Core `StatDefOf.HackingSpeed` supplies deterministic Intellectual scaling, active work awards Intellectual XP, and interruption or save/load preserves progress. Core hacking effects and sounds are used only after Core-only runtime verification. Completion transfers the existing empty hostile or factionless Frame exactly once without repairing it, replacing it, creating a pilot, or resetting its loadout.

Boarding reserves and approaches the Frame, waits for the definition-driven duration, then boards the exact Pawn on the final tick. Normal boarding is 300 ticks. A correctly positioned Dock reduces it to 60 ticks. Interruption leaves the Pawn spawned and outside.

## Docking Bay

`ZF_FrameDockingBay` is a 3x3 standable building. The Frame remains a spawned Pawn on the map. `DockingBayUtility` derives docking from the building under the Frame every time it is queried; no `isDocked` value is saved.

Docked service work runs at five times normal speed for repair, reconstruction, module installation, replacement, and removal. Walking and hauling remain normal. Hacking receives no Dock bonus. Destroying the Bay or moving the Frame immediately removes the derived bonus.

## Physical module service

The service categories are Head, Shoulder, Weapon, Offhand, Utility, and Power. Chassis remains the permanent read-only Frame identity. The vocabulary is fixed and Zenith-specific; there is no generic slot registry or arbitrary slot grid.

The Frame tab builds Equip and Replace menus only from compatible stack-of-one Things in valid map storage. A ground item outside storage, a wrong-category addon, a forbidden item, or an ordinary unmarked weapon is excluded. Selecting one saves the exact `Thing` reference in `CompFrameService`; the UI performs no transfer.

The dedicated Construction job reserves the idle player Frame and exact incoming module, fetches it, starts a worker-specific service lock, performs work, and delegates final ownership to `CompFrameLoadout`. Weapon enters only `Pawn_EquipmentTracker`; Utility, Power, Head, Shoulder, and Offhand enter their dedicated `ThingOwner`s. Replacement and removal return the exact outgoing object to the map. If the Frame is docked and a valid empty storage cell exists within the chassis radius, the same worker receives a normal `HaulToCell` job for that exact outgoing object. Otherwise it remains safely dropped near the Frame.

The lock is derived from the saved exact worker and that worker's real active Frame-service job. It does not set `CanOperate` false or alter the pilot. While active it blocks player orders, hostile combat AI, intrinsic Frame abilities, forwarded vanilla/Ideology abilities, VEF abilities, and VPE psycasts. Completion, interruption, invalidation, and reload reconcile or clear the lock without changing pilot ability ownership or cooldowns.

The visual tab offers six fixed 80 px slot boxes. A click opens compatible stored Things, removal, and per-slot cancellation. `CompFrameService` persists an ordered list of at most six `FrameServiceOrder` objects, one per category. Each stores its category, kind, exact incoming `Thing`, and exact installed `Thing` expected at execution. A new pending intent for the same category replaces that intent in place; active intent must be cancelled before changing it. A stale exact reference invalidates only its own order. The WorkGiver selects the earliest valid order; one normal job completes that order and enqueues another service job for the same worker if possible. The exact outgoing module remains physical and may be hauled by that worker.

Cancelling an active install ends the worker's job and clears the lock. If the worker already carries the incoming module, it is dropped safely nearby before ending the job. No installed Thing changes until the completion transaction succeeds. All other queued categories remain. Legacy saves with one pending order migrate that exact reference to the new list on load.

An operational piloted player Frame accepts selected right-click movement and attack plus manual Haul, vanilla Rescue of ordinary downed Pawns, vanilla Capture of ordinary hostile downed Pawns, and self right-click Disembark. Haul uses a single explicit vanilla storage job; the Frame never joins colonist work scanning. Downed Frames remain mechanical and are excluded as biological Rescue/Capture patients.

## Manual QA

1. Spawn a player Debug Frame in Home Area and injure a forearm without destroying it.
2. Enable Construction and confirm a worker naturally repairs the real Health-tab injury and gains Construction XP.
3. Repeat outside Home Area; confirm automatic work stays away and forced Prioritize repair works.
4. Leave the injury alone and confirm the Frame does not biologically heal.
5. Destroy one Digit, remove Steel, and confirm reconstruction cannot start.
6. Supply the displayed Steel count, reconstruct it, and confirm only successful completion consumes material.
7. Interrupt reconstruction and confirm the part stays missing and carried Steel remains recoverable.
8. Destroy a whole Arm and confirm only the Arm common ancestor is charged, then reconstruct it.
9. Destroy Head Assembly or Cockpit, confirm disable/ejection, reconstruct it, and confirm normal health evaluation restores operation when otherwise valid.
10. Destroy Torso or Core on a disposable fixture and confirm there is no resurrection path.
11. Spawn an empty healthy hostile Frame and compare hacking speed with Intellectual 0, 10, and 20 pawns.
12. Interrupt and save/load mid-hack; confirm chassis progress persists and ownership has not changed.
13. Finish hacking and confirm one faction transfer with the same damage, name, and loadout and no generated pilot.
14. Board normally, observe the progress bar, interrupt it, and confirm the exact Pawn stays spawned outside.
15. Build a Docking Bay, stand the Frame on it, and confirm boarding changes from 300 to 60 ticks.
16. Damage and reconstruct a docked Frame and compare actual work speed with the undocked case.
17. Put a Heavy Rifle in valid storage and an identical rifle on random ground.
18. Open Frame tab > Weapon > Equip/Replace and confirm only the stored compatible exact item appears.
19. Queue it and confirm a Construction worker fetches that exact object while a valid pilot may remain inside.
20. During active service confirm draft, movement, attacks, Overdrive, pilot abilities, and psycasts are blocked.
21. Save/load mid-service and confirm the same worker, same carried module, same pilot, and same ability instances resume.
22. Complete the replacement and confirm the exact incoming Weapon is equipment and the exact outgoing Weapon still exists.
23. Remove it away from the Dock and confirm the exact item drops safely near the Frame.
24. Repeat on the Dock with an empty valid stockpile cell in range and confirm the same worker stores the exact outgoing item.
25. Remove nearby storage and repeat; confirm a safe ground drop.
26. Repeat install, replace, and removal with Utility and Power; inspect their dedicated owners and addon power state.
27. Queue a stored module then destroy it before pickup; confirm the request invalidates without substituting an identical item.
28. Interrupt a live service job and confirm the lock clears with pilot, body, loadout, abilities, and cooldowns unchanged.

## Bound target and executable queue contracts

Reconstruction jobs bind their immutable chassis BodyDef AllParts index in job.countQueue and exact material count in job.count at creation. The driver validates that same missing target and cost; it never chooses another part at work start. Old debug jobs lacking metadata fail safely. Queue selection preserves enqueue preference, skips temporarily unexecutable requests and retains their exact references. The UI distinguishes Queued, Blocked and Servicing. Cancellation and failure restore detached incoming/outgoing Things to explicit custody. CanOperate stays true for a valid contained pilot during service; CanExecuteActions is false until the worker lock releases.
