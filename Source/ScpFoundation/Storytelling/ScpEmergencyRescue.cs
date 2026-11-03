using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Interactions;
using Verse;
using Verse.AI;

namespace ScpFoundation.Storytelling
{
    public sealed class ScpEmergencyRescue : MapComponent
    {
        private const int RescueDelayTicks = 2500;
        private int allDownSince = -1;
        private bool rescueSent;
        private Pawn medic;
        private List<Pawn> patients = new List<Pawn>();

        public ScpEmergencyRescue(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (!map.IsPlayerHome || Find.TickManager.TicksGame % 250 != 0) return;
            if (rescueSent) { AssignTreatment(); return; }
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Dead).ToList();
            if (colonists.Count == 0 || colonists.Any(p => !p.Downed) || !HostileScpPresent())
            {
                allDownSince = -1;
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (allDownSince < 0) { allDownSince = now; return; }
            if (now - allDownSince < RescueDelayTicks) return;
            Pawn responder = Current.Game.GetComponent<FoundationSupportComponent>().EmergencyEpsilon11(map);
            if (responder == null) return;
            medic = responder;
            patients = colonists.Take(3).ToList();
            rescueSent = true;
            AssignTreatment();
        }

        private bool HostileScpPresent()
        {
            if (map.mapPawns.AllPawnsSpawned.Any(pawn => pawn.Faction != null
                && pawn.Faction.HostileTo(Faction.OfPlayer)
                && (pawn is Pawn_Scp049 || pawn is Pawn_Scp0492 || pawn is Pawn_Scp106
                    || pawn is Pawn_Scp354Creature))) return true;
            return map.listerThings.AllThings.Any(thing => thing.Spawned
                && (thing is Thing_Scp173 || thing is Thing_Scp682
                    || thing.def.defName == "SCP_939Object"
                    || thing.def.defName == "SCP_939ObjectThree"));
        }

        private void AssignTreatment()
        {
            if (medic == null || medic.Dead || !medic.Spawned || medic.Map != map
                || medic.jobs == null) return;
            patients.RemoveAll(patient => patient == null || patient.Dead || !patient.Spawned
                || patient.Map != map || !patient.Downed);
            if (patients.Count == 0) return;
            JobDef treatment = DefDatabase<JobDef>.GetNamedSilentFail("SCP_500EmergencyTreatment");
            if (treatment == null || medic.inventory == null
                || !medic.inventory.innerContainer.Any(t => t.def.defName == "SCP_500Pill")) return;
            Pawn patientTarget = patients[0];
            if (medic.CurJob != null && medic.CurJob.def == treatment
                && medic.CurJob.targetA.Pawn == patientTarget) return;
            medic.jobs.StartJob(JobMaker.MakeJob(treatment, patientTarget), JobCondition.InterruptForced);
        }

        public void PatientTreated(Pawn patient)
        {
            patients.Remove(patient);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref allDownSince, "scpRescueAllDownSince", -1);
            Scribe_Values.Look(ref rescueSent, "scpRescueSent", false);
            Scribe_References.Look(ref medic, "scpRescueMedic");
            Scribe_Collections.Look(ref patients, "scpRescuePatients", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && patients == null)
                patients = new List<Pawn>();
        }
    }

    public sealed class JobDriver_Scp500EmergencyTreatment : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(180, TargetIndex.A);
            yield return Toils_General.Do(() =>
            {
                Pawn patient = job.targetA.Pawn;
                if (patient == null || patient.Dead || !patient.Downed || pawn.inventory == null) return;
                Thing pills = pawn.inventory.innerContainer.FirstOrDefault(thing =>
                    thing.def.defName == "SCP_500Pill");
                if (pills == null) return;
                pills.SplitOff(1).Destroy(DestroyMode.Vanish);
                IngestionOutcomeDoer_Scp500.Cure(patient, true);
                pawn.Map.GetComponent<ScpEmergencyRescue>().PatientTreated(patient);
            });
        }
    }
}
