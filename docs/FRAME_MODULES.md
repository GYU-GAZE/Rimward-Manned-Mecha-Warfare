# Frame Modules

## Permanent vocabulary

The machine is displayed as `Chassis`, `Head`, `Shoulder`, `Primary Weapon`, `Offhand`, `Utility`, and `Power`. Chassis is the `FramePawn` itself and is read-only. The development chassis provides one slot for each of the other six categories. `Weapon=0`, `Utility=1`, and `Power=2` remain save-compatible enum values; `Head=3`, `Shoulder=4`, and `Offhand=5` are appended.

Weapon is owned only by the normal equipment tracker. Utility, Power, Head, Shoulder, and Offhand each use a dedicated `ThingOwner`. `FramePawn` explicitly includes the cockpit and loadout in its `IThingHolder` traversal, so maps, world pawns, save/load, cleanup, hacking, and destruction see every nested object exactly once. Empty slots have independent initialized state and are valid persistent configurations.

## Effects and mounts

The installed Thing is the only source of module identity. `installedHediff` and `poweredHediff` are disposable projections reconciled from ownership. Passive effects require an installed module and a live configured mount. Powered effects additionally require `IsModulePowered`.

`RequiredAddonPower` sums positive `powerDraw` from every installed non-Power addon. When output covers that total, all valid powered projections may run. During a deficit every positive-draw module is unpowered together. Modules with zero or negative draw remain active while installed on a valid mount. No active flag, charge, fuel, heat, battery, or priority order is saved.

The development fixtures prove distinct paths:

- Sensor Helmet: powered Sight multiplier which naturally changes Frame shooting accuracy
- Beam Sword Offhand: passive Manipulation penalty and powered vanilla Hediff melee verb
- Shield Offhand: passive Manipulation penalty and body-group-localized Sharp, Blunt, and Heat armor
- Shoulder System: module-owned Shoulder Burst ability

Destroying a mount removes the corresponding projections but leaves the physical item installed. Reconstructing the mount restores the projections. Modules do not receive combat damage in this milestone.

## Module-owned abilities

`CompFrameModuleAbility` deep-scribes one real RimWorld `Ability` inside the physical module. Installation binds the current Frame as the execution host. Removal clears the host without destroying cooldown state. Load and transfer rebind the same ability object to the currently installed Frame. It is never inserted into `Frame.abilities` or the pilot tracker.

The normal command gizmo is forwarded while the module is installed, powered, mounted, piloted, and outside service lock. Casting uses the Frame's real map, position, jobs, and verb caster. The cooldown remains with the Shoulder Thing through removal, storage, save/load, pilot swaps, and installation on another Frame. Hostile AI may choose an eligible AI-enabled module ability through the existing Frame ability job giver.

## Localized armor and weapons

Shield coverage uses explicit Zenith body groups. A thread-local context exists only while vanilla `ArmorUtility.GetPostArmorDamage` resolves a Frame hit. The armor StatPart reads the exact hit part and adds installed Shield armor only for a covered group. Vanilla performs the only armor roll and ordinary Pawns see no context or contribution.

Frame weapons remain normal physical equipment for storage and hauling. Narrow `EquipmentUtility.CanEquip` patches reject marked Frame weapons on ordinary Pawns and reject unmarked weapons on Frames. The restriction does not classify Frames as Biotech mechs.

## Rendering boundary

This milestone contains no module drawing. Head and Shoulder overlays, weapon anchors, per-rotation sockets, asymmetrical layers, color masks, and livery belong to Modular Frame Rendering and Frame Livery.

## Focused manual QA

1. Install Sensor Helmet through real service.
2. Inspect Health and Sight.
3. Verify ShootingAccuracy changes through Sight.
4. Remove Reactor or add draw to create a deficit.
5. Verify Helmet loses its powered bonus while the exact item remains installed.
6. Restore power and verify the same Helmet reactivates.
7. Install Beam Sword Offhand and inspect its Manipulation penalty.
8. Melee with sufficient power and confirm the beam sword attack is available.
9. Remove power and confirm the powered attack disappears while the passive penalty remains.
10. Restore power and confirm the attack returns without reinstallation.
11. Install Shield Offhand and compare covered Torso/offhand regions with an uncovered region.
12. Remove Shield and confirm its localized armor contribution disappears.
13. Install Shoulder System and cast Shoulder Burst.
14. Remove the Shoulder during cooldown and put that exact Thing in storage.
15. Reinstall it and confirm cooldown did not reset.
16. Move the same Shoulder to another Frame and confirm its state follows the module.
17. Swap pilots and confirm the module cooldown is unchanged.
18. Destroy Head Assembly with Helmet installed and verify the Helmet remains physical but inactive.
19. Reconstruct Head and confirm the same Helmet effect returns.
20. Destroy the tagged offhand mount and verify the Offhand remains installed but inactive.
21. Reconstruct the mount and confirm the same effects return.
22. Attempt to equip a marked Frame rifle on a human and confirm rejection.
23. Confirm the human can still haul and store that rifle.
24. Attempt to install an ordinary human rifle on a Frame and confirm rejection.
25. Service Head, Shoulder, Primary Weapon, Offhand, Utility, and Power through valid storage.
26. Confirm a matching item on random ground is excluded from service candidates.
27. Start each new-category service job with a pilot aboard and confirm drafting, movement, attacks, auto-fire, and every ability path are locked.
28. Save/load a full six-slot configuration and compare exact item identities.
29. Confirm Head, Shoulder, Offhand, Utility, and Power remain installed after ordinary Frame downing.
30. Hack a hostile configured Frame and verify its complete exact loadout remains.
31. Send a configured Frame into world ownership, save/load, and return it without duplicate child holders.
32. Destroy a configured Frame on map and inspect exact salvage.
33. Destroy a configured off-map Frame and confirm no dangling module owners.
34. Open the Frame tab and verify row order starts with read-only Chassis followed by all six physical categories.
35. Verify per-module Powered, UNPOWERED, Passive, and mount-damaged labels match live state.
36. Verify the tab summarizes Shoulder Burst but does not duplicate its cast control.
37. Combine Helmet, Shoulder, Heavy Rifle, Beam Sword, Mobility Booster, and Reactor on one piloted Frame.
38. Exercise sufficient and insufficient power configurations without reinstalling modules.
39. Verify Overdrive remains chassis-owned and pilot abilities remain pilot-owned beside the module command.
40. Save/load during module cooldown and service work.
41. Inspect logs for exceptions, XML errors, unresolved references, or missing textures.
