using RimWorld;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp008Custody : CompProperties
    {
        public CompProperties_Scp008Custody() { compClass = typeof(CompScp008Custody); }
    }

    public sealed class CompScp008Custody : ThingComp
    {
        private const int TransferDelay = 5 * 60000;
        private int transferTick = -1;
        private bool resolved;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref transferTick, "scp008TransferTick", -1);
            Scribe_Values.Look(ref resolved, "scp008TransferResolved", false);
        }

        public override void CompTickRare()
        {
            if (resolved || !parent.Spawned || !parent.Map.IsPlayerHome) return;
            CompScp scp = parent.TryGetComp<CompScp>();
            if (scp != null && scp.IsContained && scp.CanResearchNow)
            {
                resolved = true;
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (transferTick < 0)
            {
                transferTick = now + TransferDelay;
                Find.LetterStack.ReceiveLetter("SCP_008TransferWarningTitle".Translate(),
                    "SCP_008TransferWarningBody".Translate(), LetterDefOf.NeutralEvent, parent);
                return;
            }
            if (now < transferTick) return;
            resolved = true;
            Find.LetterStack.ReceiveLetter("SCP_008TransferredTitle".Translate(),
                "SCP_008TransferredBody".Translate(), LetterDefOf.NeutralEvent, parent);
            parent.Destroy(DestroyMode.Vanish);
        }
    }
}
