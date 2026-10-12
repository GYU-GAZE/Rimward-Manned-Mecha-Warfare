using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    public sealed class FrameHealthIssue
    {
        public readonly int Priority;
        public readonly string Label;
        public FrameHealthIssue(int priority, string label) { Priority = priority; Label = label; }
    }

    // Entirely derived from the real health trackers. The tab shows three rows
    // and an overflow link, while the normal Health card keeps every detail.
    public static class FrameHealthSummary
    {
        public const float CriticalPartFraction = 0.25f;

        public static List<FrameHealthIssue> For(Pawn pawn, bool frame)
        {
            var issues = new List<FrameHealthIssue>();
            if (pawn?.health?.hediffSet == null) return issues;
            HediffSet health = pawn.health.hediffSet;
            foreach (Hediff_MissingPart missing in health.GetMissingPartsCommonAncestors())
                if (missing.Part != null)
                    issues.Add(new FrameHealthIssue(0, missing.Part.LabelCap + "  DESTROYED"));

            foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
            {
                if (health.PartIsMissing(part)) continue;
                float max = part.def.GetMaxHealth(pawn);
                if (max <= 0f) continue;
                float fraction = health.GetPartHealth(part) / max;
                if (fraction > 0f && fraction <= CriticalPartFraction)
                    issues.Add(new FrameHealthIssue(1, part.LabelCap + "  " + fraction.ToStringPercent()));
            }

            if (!frame && health.BleedRateTotal > 0.01f)
            {
                string severity = health.BleedRateTotal >= 0.5f ? "severe" :
                    health.BleedRateTotal >= 0.15f ? "moderate" : "light";
                issues.Add(new FrameHealthIssue(1,
                    "Bleeding  " + health.BleedRateTotal.ToStringPercent() + "/day (" + severity + ")"));
            }

            foreach (Hediff hediff in health.hediffs)
            {
                if (!hediff.Visible || hediff is Hediff_MissingPart) continue;
                if (hediff.Part != null && health.PartIsMissing(hediff.Part)) continue;
                if (hediff.IsCurrentlyLifeThreatening)
                    issues.Add(new FrameHealthIssue(2, hediff.LabelCap + "  life threatening"));
                else if (!frame && !hediff.Bleeding && hediff.TendPriority >= 0.65f)
                    issues.Add(new FrameHealthIssue(3, hediff.LabelCap + "  severe"));
            }

            if (frame && pawn.Downed && issues.Count == 0)
                issues.Add(new FrameHealthIssue(2, "Structural capacity impaired"));
            return issues.OrderBy(issue => issue.Priority).ThenBy(issue => issue.Label).ToList();
        }
    }
}
