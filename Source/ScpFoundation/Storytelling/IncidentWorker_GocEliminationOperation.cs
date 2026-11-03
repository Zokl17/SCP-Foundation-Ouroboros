using RimWorld;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class IncidentWorker_GocEliminationOperation : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            return map != null && Current.Game != null
                && Current.Game.GetComponent<GocOperationsComponent>().CanScheduleOperation(map, false);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map;
            return map != null && Current.Game.GetComponent<GocOperationsComponent>().TryScheduleOperation(map, false);
        }
    }
}
