using RimWorld;
using ScpFoundation.Knowledge;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public sealed partial class CompScpPost
    {
        private static WorkTypeDef DutyWork(bool study)
        {
            return study ? WorkTypeDefOf.Research : DefDatabase<WorkTypeDef>.GetNamed("BasicWorker");
        }

        private void PreparePawn(Pawn pawn, bool study)
        {
            if (pawn == null || pawn.Dead || pawn.workSettings == null || pawn.WorkTypeIsDisabled(DutyWork(study))) return;
            // Enable a disabled work type; never overwrite the player's existing priority.
            if (pawn.workSettings.GetPriority(DutyWork(study)) == 0) pawn.workSettings.SetPriority(DutyWork(study), 3);
            Thing thing = BoundObject;
            CompScpInteraction interaction = thing == null ? null : thing.TryGetComp<CompScpInteraction>();
            if (interaction == null) return;
            if (study) interaction.researchEnabled = true;
            else interaction.observationEnabled = true;
        }

        public void AssignDuty(ScpDutyShift shift, int role, Pawn pawn)
        {
            if (role == 0) shift.observerA = pawn;
            else if (role == 1) shift.observerB = pawn;
            else shift.researcher = pawn;
            if (pawn == null) return;
            useShifts = true;
            PreparePawn(pawn, role == 2);
        }

        public void EnableRoster()
        {
            useShifts = true;
            foreach (ScpDutyShift shift in shifts)
            {
                PreparePawn(shift.observerA, false);
                PreparePawn(shift.observerB, false);
                PreparePawn(shift.researcher, true);
            }
        }

        public string DutyStatus(Pawn pawn, bool study, bool detailed = false)
        {
            if (pawn == null) return "SCP_ShiftNobody".Translate();
            if (!useShifts) return "SCP_DutyDisabled".Translate();
            if (!pawn.Spawned || pawn.Map != parent.Map || pawn.Dead || pawn.Downed || pawn.InMentalState)
                return "SCP_DutyUnavailable".Translate();
            if (pawn.Drafted) return "SCP_DutyDrafted".Translate();
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight)
                || study && !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return "SCP_DutyIncapable".Translate();
            if (pawn.workSettings == null || pawn.WorkTypeIsDisabled(DutyWork(study))) return "SCP_DutyIncapable".Translate();
            int priority = pawn.workSettings.GetPriority(DutyWork(study));
            if (priority == 0) return "SCP_DutyWorkDisabled".Translate();
            if (NeedsBreak(pawn)) return "SCP_DutyRest".Translate();
            if (!OnDuty(pawn, study, true)) return "SCP_DutyNotTime".Translate();
            if (!CanStart(pawn, study)) return "SCP_DutyConflict".Translate();
            Thing thing = BoundObject;
            CompScpInteraction interaction = thing == null ? null : thing.TryGetComp<CompScpInteraction>();
            if (interaction == null || !interaction.CanWorkAtPost) return "SCP_ShiftNoObject".Translate();
            bool researcherWatching = study && !interaction.Anomaly.CanResearchNow;
            if (study && !Current.Game.GetComponent<ScpKnowledgeComponent>().NeedsResearch(interaction.Anomaly.Definition))
                return "SCP_DutyStudyDone".Translate();
            if (!(study ? interaction.researchEnabled : interaction.observationEnabled)) return "SCP_DutyObjectDisabled".Translate();
            if (pawn.CurJob != null && pawn.CurJob.targetA.Thing == thing
                && pawn.CurJob.def == (study ? ScpInteractionDefOf.SCP_Study : ScpInteractionDefOf.SCP_Observe))
                return (pawn.pather.Moving ? "SCP_DutyComing" : researcherWatching ? "SCP_DutyWatchingForRecovery" : "SCP_DutyWorking").Translate();
            if (!study && ScpSurveillance.Observed(thing)) return "SCP_DutyRemote".Translate();
            if (detailed)
            {
                if (!pawn.CanReserve(thing, ShiftPolicy.ReservationSlots, 0)) return "SCP_DutyReserved".Translate();
                IntVec3 spot;
                if (!ScpObservation.TrySpot(pawn, thing, out spot, sameRoom: study)) return "SCP_NoObservationSpot".Translate();
            }
            return researcherWatching ? "SCP_DutyQueuedForRecovery".Translate(priority) : "SCP_DutyQueued".Translate(priority);
        }
    }
}
