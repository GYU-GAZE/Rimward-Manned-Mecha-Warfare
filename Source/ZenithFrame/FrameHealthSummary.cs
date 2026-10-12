using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ZenithFrame
{
    public sealed class FrameHealthIssue
    {
        public readonly int Priority;
        public readonly string Label;
        public FrameHealthIssue(int priority, string label) { Priority = priority; Label = label; }
    }

    // Derived from the live Pawn health tracker; the tab decides visible rows.
    public static class FrameHealthSummary
    {
        private const float CriticalPartFraction = 0.25f;

        public static List<FrameHealthIssue> For(Pawn pawn, bool frame)
        {
            var issues = new List<FrameHealthIssue>();
            if (pawn?.health?.hediffSet == null) return issues;
            HediffSet health = pawn.health.hediffSet;

            if (!frame && health.BleedRateTotal > 0f)
                issues.Add(new FrameHealthIssue(0, "Bleeding  " + health.BleedRateTotal.ToStringPercent() + "/day"));

            // Vanilla folds descendants under their first missing ancestor.
            var missingParts = health.GetMissingPartsCommonAncestors().ToList();
            var describedParts = new HashSet<BodyPartRecord>();
            foreach (Hediff_MissingPart missing in missingParts)
                if (missing.Part != null)
                    issues.Add(new FrameHealthIssue(1, missing.Part.LabelCap + "  Destroyed"));

            foreach (Hediff hediff in health.hediffs)
            {
                if (!hediff.Visible || hediff is Hediff_MissingPart) continue;
                if (hediff.Part != null && health.PartIsMissing(hediff.Part)) continue;
                bool threatening = hediff.IsCurrentlyLifeThreatening;
                if (!threatening && !hediff.Bleeding && hediff.TendPriority < 0.65f) continue;
                if (hediff.Part != null) describedParts.Add(hediff.Part);
                string label = hediff.LabelCap;
                if (hediff.Part != null && !label.ToLowerInvariant().Contains(hediff.Part.Label.ToLowerInvariant()))
                    label += " - " + hediff.Part.Label;
                if (threatening) label += "  Life threatening";
                else if (hediff.Bleeding) label += "  Bleeding";
                issues.Add(new FrameHealthIssue(threatening ? 2 : 4, label));
            }

            if (frame)
                foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
                {
                    if (health.PartIsMissing(part) || describedParts.Contains(part)) continue;
                    float max = part.def.GetMaxHealth(pawn);
                    if (max <= 0f) continue;
                    float fraction = health.GetPartHealth(part) / max;
                    if (fraction > 0f && fraction <= CriticalPartFraction)
                        issues.Add(new FrameHealthIssue(3, part.LabelCap + "  " + fraction.ToStringPercent()));
                }
            return issues.OrderBy(issue => issue.Priority).ThenBy(issue => issue.Label).ToList();
        }
    }
}
