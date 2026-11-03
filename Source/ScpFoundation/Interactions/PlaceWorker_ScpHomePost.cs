using Verse;

namespace ScpFoundation.Interactions
{
    public sealed class PlaceWorker_ScpHomePost : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot,
            Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            return map != null && map.IsPlayerHome
                ? (AcceptanceReport)true
                : "SCP_PostOnlyAtHome".Translate();
        }
    }
}
