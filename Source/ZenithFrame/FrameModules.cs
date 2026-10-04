using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ZenithFrame
{
    public sealed class CompProperties_FrameModuleAbility : CompProperties
    {
        public AbilityDef ability;
        public CompProperties_FrameModuleAbility() { compClass = typeof(CompFrameModuleAbility); }
    }

    // The physical module deep-owns the vanilla Ability object. The current
    // installed Frame is only a rebound execution host and is never its owner.
    public sealed class CompFrameModuleAbility : ThingComp
    {
        private Ability_FrameModule ability;

        public CompProperties_FrameModuleAbility Props => (CompProperties_FrameModuleAbility)props;
        public Ability_FrameModule Ability
        {
            get
            {
                EnsureAbility(InstalledFrame());
                return ability;
            }
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            EnsureAbility(null);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref ability, "frameModuleAbility");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureAbility(InstalledFrame());
                Bind(InstalledFrame());
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            FramePawn frame = InstalledFrame();
            if (frame?.CurJob?.ability == ability)
                frame.jobs.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
            if (Find.Targeter?.IsTargeting == true && ability?.pawn == frame)
                Find.Targeter.StopTargeting();
            Bind(null);
            base.PostDestroy(mode, previousMap);
        }

        public void Bind(FramePawn frame)
        {
            EnsureAbility(frame);
            ability.Module = parent;
            ability.pawn = frame;
            if (ability.verb != null) ability.verb.caster = frame;
        }

        private void EnsureAbility(FramePawn frame)
        {
            if (ability == null)
            {
                if (Props.ability == null) return;
                ability = new Ability_FrameModule(frame, Props.ability);
            }
            ability.Module = parent;
        }

        private FramePawn InstalledFrame()
        {
            if (parent?.holdingOwner?.Owner is CompFrameLoadout.SlotHolder holder &&
                ReferenceEquals(holder.loadout.Installed(holder.category), parent))
                return holder.loadout.Frame;
            return null;
        }
    }

    public sealed class Ability_FrameModule : Ability
    {
        public Thing Module;

        public Ability_FrameModule() { }
        public Ability_FrameModule(Pawn pawn) : base(pawn) { }
        public Ability_FrameModule(Pawn pawn, AbilityDef def) : base(pawn, def) { }
        public Ability_FrameModule(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def) { }

        public override AcceptanceReport CanCast
        {
            get
            {
                if (!(pawn is FramePawn frame) || Module == null || Module.Destroyed ||
                    frame.Loadout?.IsInstalled(Module) != true || frame.Loadout.IsModulePowered(Module) != true ||
                    frame.Cockpit?.CanOperate != true || frame.Service?.IsServiceLocked == true)
                    return "Module ability requires its powered physical module and a valid operating pilot.";
                return base.CanCast;
            }
        }

    }

    public static class FrameModuleArmorContext
    {
        [ThreadStatic] private static FramePawn frame;
        [ThreadStatic] private static BodyPartRecord part;

        public static bool TryGet(FramePawn candidate, out BodyPartRecord hitPart)
        {
            hitPart = part;
            return candidate != null && ReferenceEquals(candidate, frame) && hitPart != null;
        }

        internal static State Enter(Pawn pawn, BodyPartRecord hitPart)
        {
            State state = new State(frame, part);
            frame = pawn as FramePawn;
            part = frame == null ? null : hitPart;
            return state;
        }

        internal static void Exit(State state)
        {
            frame = state.Frame;
            part = state.Part;
        }

        internal struct State
        {
            internal readonly FramePawn Frame;
            internal readonly BodyPartRecord Part;
            internal State(FramePawn frame, BodyPartRecord part) { Frame = frame; Part = part; }
        }
    }

    // ArmorUtility still performs the one and only random armor roll. This
    // StatPart contributes only while vanilla is evaluating a covered Frame hit.
    public sealed class StatPart_FrameModuleArmor : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!(req.Thing is FramePawn frame) ||
                !FrameModuleArmorContext.TryGet(frame, out BodyPartRecord part) || frame.Loadout == null)
                return;
            val += FrameModuleArmorUtility.Contribution(frame, part, parentStat);
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is FramePawn frame) ||
                !FrameModuleArmorContext.TryGet(frame, out BodyPartRecord part) || frame.Loadout == null)
                return null;
            float offset = FrameModuleArmorUtility.Contribution(frame, part, parentStat);
            return offset == 0f ? null : "Frame module armor (" + part.Label + "): " + offset.ToStringPercent();
        }
    }

    public static class FrameModuleArmorUtility
    {
        public static float Contribution(FramePawn frame, BodyPartRecord part, StatDef armorStat)
        {
            if (frame?.Loadout == null || part == null || armorStat == null) return 0f;
            float result = 0f;
            foreach (Thing module in frame.Loadout.InstalledModules)
                if (frame.Loadout.CoversArmorPart(module, part))
                    result += frame.Loadout.ArmorOffset(module, armorStat);
            return result;
        }
    }

    [HarmonyPatch(typeof(ArmorUtility), nameof(ArmorUtility.GetPostArmorDamage))]
    internal static class Patch_ArmorUtility_GetPostArmorDamage_FrameModule
    {
        private static void Prefix(Pawn pawn, BodyPartRecord part, out FrameModuleArmorContext.State __state) =>
            __state = FrameModuleArmorContext.Enter(pawn, part);

        private static Exception Finalizer(Exception __exception, FrameModuleArmorContext.State __state)
        {
            FrameModuleArmorContext.Exit(__state);
            return __exception;
        }
    }

    internal static class FrameWeaponRestriction
    {
        internal static void Apply(Thing thing, Pawn pawn, ref bool result, ref string reason)
        {
            FrameAddonExtension extension = thing?.def?.GetModExtension<FrameAddonExtension>();
            if (extension?.category == FrameAddonCategory.Weapon && !(pawn is FramePawn))
            {
                result = false;
                reason = "Zenith Frame weapons require a Frame chassis.";
            }
            else if (pawn is FramePawn && extension?.category != FrameAddonCategory.Weapon)
            {
                result = false;
                reason = "Frames can equip only marked Zenith Frame weapons.";
            }
        }
    }

    [HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip), new[] { typeof(Thing), typeof(Pawn) })]
    internal static class Patch_EquipmentUtility_CanEquipSimple_FrameWeapon
    {
        private static void Postfix(Thing thing, Pawn pawn, ref bool __result)
        {
            string reason = null;
            FrameWeaponRestriction.Apply(thing, pawn, ref __result, ref reason);
        }
    }

    [HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip),
        new[] { typeof(Thing), typeof(Pawn), typeof(string), typeof(bool) },
        new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal })]
    internal static class Patch_EquipmentUtility_CanEquipReason_FrameWeapon
    {
        private static void Postfix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result) =>
            FrameWeaponRestriction.Apply(thing, pawn, ref __result, ref cantReason);
    }
}
