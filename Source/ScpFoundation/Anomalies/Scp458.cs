using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class Thing_Scp458Box : ThingWithComps
    {
        private const int UseInterval = 15000;
        private int nextUseTick;

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!Spawned || Faction != null && Faction != Faction.OfPlayer) yield break;
            var command = new Command_Action {
                defaultLabel = "SCP_458OpenAction".Translate(),
                defaultDesc = "SCP_458OpenDesc".Translate(),
                icon = def.uiIcon,
                action = ChooseOpener
            };
            if (Find.TickManager.TicksGame < nextUseTick)
                command.Disable("SCP_458CoolingDown".Translate(
                    ((nextUseTick - Find.TickManager.TicksGame) / 2500f).ToString("0.0")));
            yield return command;
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(pawn)) yield return option;
            if (CanOpen(pawn))
                yield return new FloatMenuOption("SCP_458OpenAction".Translate(), () => Assign(pawn));
        }

        private bool CanOpen(Pawn pawn)
        {
            return Spawned && Find.TickManager.TicksGame >= nextUseTick
                && pawn != null && pawn.IsColonistPlayerControlled && !pawn.Drafted
                && !pawn.Dead && !pawn.Downed && pawn.jobs != null && pawn.Map == Map
                && pawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some);
        }

        private void ChooseOpener()
        {
            var options = new List<FloatMenuOption>();
            foreach (Pawn candidate in Map.mapPawns.FreeColonistsSpawned)
            {
                if (!CanOpen(candidate)) continue;
                Pawn opener = candidate;
                options.Add(new FloatMenuOption(opener.LabelShortCap, () => Assign(opener)));
            }
            if (options.Count == 0)
                Messages.Message("SCP_458NoOpener".Translate(), this,
                    MessageTypeDefOf.RejectInput, false);
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        private void Assign(Pawn pawn)
        {
            if (!CanOpen(pawn)) return;
            JobDef job = DefDatabase<JobDef>.GetNamedSilentFail("SCP_458Open");
            if (job != null) pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(job, this), JobTag.Misc);
        }

        public bool TryOpen(Pawn pawn)
        {
            if (!Spawned || pawn == null || pawn.Map != Map
                || Find.TickManager.TicksGame < nextUseTick) return false;
            ThingDef pizzaDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_458Pizza");
            if (pizzaDef == null) return false;
            Thing pizza = ThingMaker.MakeThing(pizzaDef);
            Thing placed;
            if (!GenPlace.TryPlaceThing(pizza, pawn.Position, Map, ThingPlaceMode.Near, out placed))
            { pizza.Destroy(DestroyMode.Vanish); return false; }
            nextUseTick = Find.TickManager.TicksGame + UseInterval;
            Messages.Message("SCP_458PizzaReady".Translate(pawn.LabelShortCap), placed,
                MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextUseTick, "scp458NextUseTick", 0);
        }
    }

    public sealed class JobDriver_Scp458Open : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(120, TargetIndex.A);
            yield return Toils_General.Do(() => {
                Thing_Scp458Box box = job.targetA.Thing as Thing_Scp458Box;
                if (box != null) box.TryOpen(pawn);
            });
        }
    }
}
