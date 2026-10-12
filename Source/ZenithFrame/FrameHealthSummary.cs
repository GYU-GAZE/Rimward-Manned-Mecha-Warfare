using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    // Presentation data only: the exact Hediff and Pawn still own every health fact.
    public sealed class FrameHealthIssue
    {
        public readonly int Priority;
        public readonly string PartLabel;
        public readonly string ConditionLabel;
        public readonly Color PartColor;
        public readonly Color ConditionColor;
        public readonly Pawn Pawn;
        public readonly Hediff Hediff;
        public readonly bool IsBleedSummary;
        public readonly bool Bleeding;
        public readonly TextureAndColor StateIcon;

        public string Tooltip => Hediff != null ? Hediff.GetTooltip(Pawn, false) :
            PartLabel + ": " + ConditionLabel;

        public FrameHealthIssue(int priority, string partLabel, string conditionLabel, Color partColor,
            Color conditionColor, Pawn pawn, Hediff hediff = null, bool bleedSummary = false,
            bool bleeding = false, TextureAndColor stateIcon = default)
        {
            Priority = priority;
            PartLabel = partLabel;
            ConditionLabel = conditionLabel;
            PartColor = partColor;
            ConditionColor = conditionColor;
            Pawn = pawn;
            Hediff = hediff;
            IsBleedSummary = bleedSummary;
            Bleeding = bleeding;
            StateIcon = stateIcon;
        }
    }

    public static class FrameHealthSummary
    {
        private const float CriticalPartFraction = 0.25f;

        public static List<FrameHealthIssue> For(Pawn pawn, bool frame)
        {
            var issues = new List<FrameHealthIssue>();
            if (pawn?.health?.hediffSet == null) return issues;
            HediffSet health = pawn.health.hediffSet;

            if (!frame && health.BleedRateTotal > 0f)
                issues.Add(new FrameHealthIssue(0, "Bleeding", health.BleedRateTotal.ToStringPercent() +
                    "/" + "LetterDay".Translate(), HealthUtility.RedColor, Color.white, pawn,
                    bleedSummary: true, bleeding: true));

            // Vanilla's common-ancestor list prevents a destroyed limb from
            // generating a row for every missing child part.
            foreach (Hediff_MissingPart missing in health.GetMissingPartsCommonAncestors())
                if (missing.Part != null)
                    issues.Add(FromHediff(pawn, missing, 1, 1));

            var describedParts = new HashSet<BodyPartRecord>();
            var visible = health.hediffs.Where(hediff => hediff.Visible && !(hediff is Hediff_MissingPart) &&
                (hediff.Part == null || !health.PartIsMissing(hediff.Part)) &&
                (hediff.IsCurrentlyLifeThreatening || hediff.Bleeding || hediff.TendPriority >= 0.65f ||
                    (frame && IsCriticalPart(pawn, health, hediff.Part))));
            foreach (var group in visible.GroupBy(hediff => new { hediff.Part, hediff.UIGroupKey }))
            {
                Hediff first = group.First();
                if (first.Part != null) describedParts.Add(first.Part);
                int priority = group.Any(hediff => hediff.IsCurrentlyLifeThreatening) ? 2 :
                    group.Any(hediff => hediff.Bleeding) ? 3 : 4;
                issues.Add(FromHediff(pawn, first, priority, group.Count(), group.Any(hediff => hediff.Bleeding)));
            }

            if (frame)
                foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
                {
                    if (health.PartIsMissing(part) || describedParts.Contains(part) ||
                        !IsCriticalPart(pawn, health, part)) continue;
                    var condition = HealthUtility.GetPartConditionLabel(pawn, part);
                    issues.Add(new FrameHealthIssue(5, part.LabelCap, condition.First,
                        condition.Second, condition.Second, pawn));
                }

            return issues.OrderBy(issue => issue.Priority).ThenBy(issue => issue.PartLabel).ToList();
        }

        private static bool IsCriticalPart(Pawn pawn, HediffSet health, BodyPartRecord part)
        {
            if (part == null) return false;
            float max = part.def.GetMaxHealth(pawn);
            if (max <= 0f) return false;
            float fraction = health.GetPartHealth(part) / max;
            return fraction > 0f && fraction <= CriticalPartFraction;
        }

        private static FrameHealthIssue FromHediff(Pawn pawn, Hediff hediff, int priority, int count,
            bool bleeding = false)
        {
            string partLabel = hediff.Part?.LabelCap ?? "WholeBody".Translate();
            Color partColor = hediff.Part == null ? HealthUtility.RedColor :
                HealthUtility.GetPartConditionLabel(pawn, hediff.Part).Second;
            string condition = hediff.LabelCap + (count > 1 ? " x" + count : "");
            return new FrameHealthIssue(priority, partLabel, condition, partColor, hediff.LabelColor,
                pawn, hediff, bleeding: bleeding || hediff.Bleeding, stateIcon: hediff.StateIcon);
        }
    }
}
