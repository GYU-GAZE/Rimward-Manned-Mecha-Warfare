# Modular Frame rendering

`ZF_DebugFrame` uses a dedicated native RimWorld 1.6 `PawnRenderTreeDef`. Its `Body` node is the chassis Torso; the next node is the intrinsic chassis Head; the optional Helmet and Shoulder nodes read the exact installed Things. The tree retains vanilla wound/firefoam overlays and `PawnRenderNodeWorker_Carried`, so the primary weapon stays in normal `Pawn_EquipmentTracker` drawing. No rendering Harmony patch or independent Frame draw loop is used. The PawnKind fallback graphic points to the Test Frame Torso South texture, never Lancer.

`FrameChassisVisualExtension` on the Frame ThingDef provides the Torso and Head `South/Sides/North` sets and one shared world draw size, currently 2. `FrameModuleVisualExtension` on the Sensor Helmet and Shoulder System ThingDefs provides the module's three-view set. `FrameRenderNode` resolves cached `Graphic_Single` materials through RimWorld's graphic database, and its worker selects the source for the current `Rot4`. East uses the authored Sides view. RimWorld's `GraphicMeshSet` already provides a flipped West mesh; toggling `FlipGraphic` a second time cancelled that mirror. Source textures and shared materials are never mutated. Every body node uses the same mesh footprint, scale, pivot, and origin; small render layers determine overlap order.

The render result is derived at draw time. Torso and intrinsic Head belong to the chassis Def. The real Head Assembly's health controls the normal intrinsic Head. The Head-slot Thing controls Helmet appearance, but a missing Head Assembly also suppresses Helmet while preserving that exact Thing. Reconstructing Head shows the same chassis Head and, if still installed, that same Helmet. The Shoulder node reads `InstalledShoulder` and `IsMountValid`. Mount loss suppresses an overlay without consuming the item. Power affects powered gameplay effects, never physical visual presence. Offhand, Utility, and Power have no body nodes.

The render node holds only temporary per-Pawn cache references and visibility for invalidation; it serializes no visual state. Every successful loadout install or removal explicitly calls the native renderer's `SetAllGraphicsDirty`, so a stationary Frame rebuilds its render tree and portrait without waiting for movement. Structural visibility also requests a recache. Save/load, service, hacking, map/world transfer, salvage, and rotation therefore resolve from current Def, health, and physical owners rather than a visual inventory copy. `FrameHeadVisualState` currently distinguishes present and missing, leaving room for later damaged art without changing ownership.

`scripts/Test-Static.ps1` validates the XML mapping, asset count, PNG format, dimensions, and alpha corners. Pure direction tests check all four rotations. Runtime lifecycle phases 45-46 inspect loaded Graphics, four-layer alignment, one Sides source with independent mirroring, exact Helmet/Shoulder installation, power deficit, Helmet removal, Head destruction/reconstruction, downing, fresh-process save/load, and corpse safety. Human inspection remains necessary for visual proportion, overlaps, shadows, and weapon placement in the real UI.

## Manual visual QA

1. Spawn a fresh Debug Frame and inspect Torso plus intrinsic Head facing South.
2. Rotate East, North, and West. East must show the authored Sides view; West must mirror it.
3. Cycle all four rotations repeatedly and look for gaps, scaling changes, or position drift.
4. Use real Construction service to install Sensor Helmet, inspect all rotations, and confirm it overlays the intrinsic Head.
5. Service-remove Sensor Helmet and confirm the intrinsic Head remains.
6. Install Shoulder System through service and inspect all rotations, then install Helmet and inspect the combined overlap.
7. Equip the Primary Weapon and inspect body/weapon composition in all directions. Confirm there is only one weapon image.
8. Create an addon power deficit. Helmet and Shoulder should remain visible while their powered effects deactivate.
9. Destroy the Head Assembly. Torso must remain; intrinsic Head and Helmet must disappear; the same physical Helmet Thing must remain in the Head owner.
10. Reconstruct Head. Intrinsic Head and that same Helmet must return automatically.
11. Down the Frame through locomotion damage only. Valid Torso, Head, Helmet, and Shoulder must remain visible.
12. Save and reload the configured Frame, then inspect each rotation and exact module identity.
13. Hack a configured hostile Frame. Its Helmet and Shoulder must survive faction transfer without visual reset.
14. Service-remove Helmet and Shoulder and confirm their overlays disappear with their exact physical Things.
15. Spawn two Frames facing East and West side by side. Their one shared Sides source must render independently, one mirrored.
16. Kill or destroy a configured Frame. Check for renderer errors and stale overlays; verify existing salvage behavior.
