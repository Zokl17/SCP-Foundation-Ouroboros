using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Containment;
using ScpFoundation.Knowledge;
using ScpFoundation.Storytelling;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public class WorkGiver_ScpObserve : WorkGiver_Scanner
    {
        protected virtual bool Study { get { return false; } }
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (var anomaly in pawn.Map.GetComponent<ScpStoryStateComponent>().Anomalies)
                if (anomaly.IsInCustody && anomaly.parent.GetComp<CompScpInteraction>() != null) yield return anomaly.parent;
        }
        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            CompScpInteraction interaction = thing.TryGetComp<CompScpInteraction>();
            if (interaction == null || !interaction.CanWorkAtPost || thing.IsForbidden(pawn)) return false;
            bool studyJob = SessionWorkPolicy.UseStudyJob(Study);
            if (!Study && (!interaction.Anomaly.Definition.requirements.OfType<RequirementObservers>().Any()
                || ScpSurveillance.Observed(thing))) return false;
            CompScpPost post = CompScpPost.ForObject(thing);
            if (post != null && !post.CanStart(pawn, Study)) return false;
            if (Study)
            {
                if (!interaction.researchEnabled
                    || ScpObservation.AssignedTo(thing, ScpInteractionDefOf.SCP_Study, pawn) >= 1
                    || !Current.Game.GetComponent<ScpKnowledgeComponent>().NeedsResearch(interaction.Anomaly.Definition)) return false;
            }
            else if (!interaction.observationEnabled || ScpObservation.AssignedTo(thing, ScpInteractionDefOf.SCP_Observe, pawn)
                >= ScpObservation.StaffTarget(interaction.Anomaly.Definition) * (post != null && post.useShifts ? 2 : 1)) return false;
            IntVec3 spot;
            return pawn.CanReserve(thing, ShiftPolicy.ReservationSlots, 0) && ScpObservation.TrySpot(pawn, thing, out spot, sameRoom: studyJob);
        }
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (!HasJobOnThing(pawn, thing, forced)) return null;
            bool studyJob = SessionWorkPolicy.UseStudyJob(Study);
            IntVec3 spot;
            if (!ScpObservation.TrySpot(pawn, thing, out spot, sameRoom: studyJob)) return null;
            return JobMaker.MakeJob(studyJob ? ScpInteractionDefOf.SCP_Study : ScpInteractionDefOf.SCP_Observe, thing, spot);
        }
    }
    public sealed class WorkGiver_ScpStudy : WorkGiver_ScpObserve
    {
        protected override bool Study { get { return true; } }
    }
}
