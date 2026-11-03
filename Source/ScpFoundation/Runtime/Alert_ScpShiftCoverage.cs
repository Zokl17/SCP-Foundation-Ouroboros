using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class Alert_ScpShiftCoverage : Alert
    {
        public Alert_ScpShiftCoverage() { defaultPriority = AlertPriority.Medium; }
        public override string GetLabel() { return "SCP_ShiftCoverageAlert".Translate(); }
        private List<CompScpPost> Uncovered()
        {
            var posts = new List<CompScpPost>();
            if (Current.Game == null) return posts;
            foreach (Map map in Find.Maps)
            foreach (CompScpPost post in CompScpPost.All(map))
            {
                Thing thing = post.BoundObject;
                if (!post.useShifts || thing == null || !thing.Spawned || thing.Map != map) continue;
                CompScp scp = thing.TryGetComp<CompScp>();
                if (scp == null || !scp.IsInCustody) continue;
                int required = ScpObservation.StaffTarget(scp.Definition);
                if (required > 0 && post.ReliefReady(thing) < required) posts.Add(post);
            }
            return posts;
        }
        public override AlertReport GetReport() { return AlertReport.CulpritsAre(Uncovered().Select(post => (Thing)post.parent).ToList()); }
        public override TaggedString GetExplanation()
        {
            return "SCP_ShiftCoverageHelp".Translate() + "\n\n" + string.Join("\n\n", Uncovered().Select(post => post.parent.LabelCap + " • " + post.parent.Position + "\n" + post.Summary()).ToArray());
        }
    }
}
