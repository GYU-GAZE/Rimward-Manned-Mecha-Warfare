# Standard humanoid Frame art template

Each visual part is a separate transparent **1024 x 1024 PNG** exported from the same complete composition. Keep the full canvas, its original center, and all transparent borders. Never crop, trim, recenter, or independently resize a part. The shared canvas carries alignment; Torso, intrinsic Head, Helmet, and Shoulder use one world-space draw size and zero positional offset.

Export three directions for each part: `South`, `Sides`, and `North`. The authored `Sides` view faces East (right) in the current Test Frame template. The game mirrors that same texture for West. Do not export mandatory East and West duplicates or bake a flipped file. Keep asymmetric details readable after mirroring.

The base chassis is always **Torso + intrinsic Head**. The intrinsic Head is part of chassis identity and corresponds to the mechanical Head Assembly body part. `Head` in the gameplay loadout is a removable **Helmet** that overlays the chassis Head. A Helmet is exported separately and never replaces the intrinsic Head. A Shoulder is another separate removable overlay. There is no visual arm, leg, Offhand, Utility, or Power export in this template. The primary weapon uses RimWorld's normal equipment drawing path.

For this development chassis, repository copies live under `Mod/Textures/ZenithFrame/Frames/Debug/TestFrame/` as `Torso_South.png`, `Torso_Sides.png`, `Torso_North.png`, and equivalent `Head`, `Helmet`, and `Shoulder` names. Each new chassis supplies its own Torso and intrinsic Head sets. Each new physical Helmet or Shoulder ThingDef supplies its own three-view set. The source exports in Downloads are originals; `test frame heada south.png` was copied as `Head_South.png` without modifying the original.

All twelve source files were found in `C:\Users\Gyu\Downloads`. The repository destination prefix for every row is `Mod/Textures/ZenithFrame/Frames/Debug/TestFrame/`. Copies retain byte-identical PNG contents and only the destination names change.

| Source file in Downloads | Repository file |
|---|---|
| `test frame torso south.png` | `Torso_South.png` |
| `test frame torso sides.png` | `Torso_Sides.png` |
| `test frame torso north.png` | `Torso_North.png` |
| `test frame heada south.png` | `Head_South.png` |
| `test frame head sides.png` | `Head_Sides.png` |
| `test frame head north.png` | `Head_North.png` |
| `test frame helmet south.png` | `Helmet_South.png` |
| `test frame helmet sides.png` | `Helmet_Sides.png` |
| `test frame helmet north.png` | `Helmet_North.png` |
| `test frame shoulder south.png` | `Shoulder_South.png` |
| `test frame shoulder sides.png` | `Shoulder_Sides.png` |
| `test frame shoulder north.png` | `Shoulder_North.png` |

For each paintable base part and facing, author a matching `<Part>_<Direction>_m.png` two-color mask. Red selects Primary, green selects Secondary, and black keeps the original detail. RimWorld's current `CutoutComplex` path does not offer a third independent color here. For visor or sensor pixels, optionally author a transparent `<Part>_<Direction>_v.png` overlay on the same 1024 x 1024 RGBA canvas. Its nontransparent pixels render with the chassis's Visor color through an independent `Cutout` layer. The visor overlay is optional for Torso, Head, Helmet, Shoulder, or future compatible body layers; it is not hard-coded to the head. `FrameChassisVisualExtension` and `FrameModuleVisualExtension` identify optional visor paths and the chassis defines Primary, Secondary, and Visor defaults. Keep fixed details out of both mask and visor alpha.

Every source, mask, and visor overlay for a facing uses the exact same full canvas and origin. Export South, Sides authored facing East, and North; the game mirrors Sides for West. Never crop, recenter, or offset one layer independently. Final masks and overlays are manually authored. `Generate-DebugLiveryMasks.ps1` and `Generate-DebugVisorOverlays.ps1` only create technical proofs for the current Debug Frame, with 12 base PNGs, 12 masks, and 9 visor overlays.

Before delivery, check dimensions, alpha, alignment, and independent recoloring in all four game rotations. Inspect installed/removed modules, missing/reconstructed Head, downing, and weapon overlap. Future damaged-state art may supply alternate sets; the current missing Head state simply omits the normal Head and Helmet layers.
