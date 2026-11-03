using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    // Adapt the Empire ceremony's lord/duty flow to a meeting with no royal outcome.
    public sealed class LordJob_ScpO5Meeting : LordJob
    {
        private Pawn visitor;
        private Pawn scientist;
        private IntVec3 visitorSpot = IntVec3.Invalid;
        private IntVec3 scientistSpot = IntVec3.Invalid;

        public Pawn Scientist { get { return scientist; } }

        public LordJob_ScpO5Meeting() { }

        public LordJob_ScpO5Meeting(Pawn visitor, Pawn scientist,
            IntVec3 visitorSpot, IntVec3 scientistSpot)
        {
            this.visitor = visitor;
            this.scientist = scientist;
            this.visitorSpot = visitorSpot;
            this.scientistSpot = scientistSpot;
        }

        public void UpdateSpots(IntVec3 newVisitorSpot, IntVec3 newScientistSpot)
        {
            if (visitorSpot == newVisitorSpot && scientistSpot == newScientistSpot) return;
            visitorSpot = newVisitorSpot;
            scientistSpot = newScientistSpot;
            if (lord != null && lord.CurLordToil != null) lord.CurLordToil.UpdateAllDuties();
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            graph.AddToil(new LordToil_ScpO5Meeting());
            return graph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref visitor, "scpO5MeetingVisitor");
            Scribe_References.Look(ref scientist, "scpO5MeetingScientist");
            Scribe_Values.Look(ref visitorSpot, "scpO5MeetingVisitorSpot", IntVec3.Invalid);
            Scribe_Values.Look(ref scientistSpot, "scpO5MeetingScientistSpot", IntVec3.Invalid);
        }

        public IntVec3 SpotFor(Pawn pawn)
        {
            return pawn == visitor ? visitorSpot : pawn == scientist ? scientistSpot : IntVec3.Invalid;
        }
    }

    public sealed class LordToil_ScpO5Meeting : LordToil
    {
        public override void UpdateAllDuties()
        {
            LordJob_ScpO5Meeting meeting = lord.LordJob as LordJob_ScpO5Meeting;
            if (meeting == null) return;
            foreach (Pawn pawn in lord.ownedPawns)
            {
                IntVec3 spot = meeting.SpotFor(pawn);
                if (!spot.IsValid || pawn.mindState == null) continue;
                PawnDuty duty = new PawnDuty(DutyDefOf.BestowingCeremony_MoveInPlace, spot);
                duty.locomotion = LocomotionUrgency.Jog;
                pawn.mindState.duty = duty;
            }
        }
    }
}
