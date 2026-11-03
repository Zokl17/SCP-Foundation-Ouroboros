using System.Linq;
using RimWorld;
using Verse;

namespace ScpFoundation.Interactions
{
    internal static class ScpStudyApparel
    {
        public static float ResearchAmount(Pawn pawn, float amount)
        {
            return pawn != null && pawn.apparel != null
                && pawn.apparel.WornApparel.Any(apparel => apparel.def.defName == "SCP_ResearchCoat")
                ? amount * 2f : amount;
        }
    }

    public sealed class ThoughtWorker_ScpDClassUniform : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn pawn)
        {
            return pawn != null && pawn.apparel != null
                && pawn.apparel.WornApparel.Any(apparel => apparel.def.defName == "SCP_DClassUniform")
                ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
        }
    }
}
