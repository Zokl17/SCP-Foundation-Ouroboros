using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class Pawn_Scp131 : Pawn
    {
        private Pawn bondedPawn;

        public Pawn BondedPawn
        {
            get { return bondedPawn; }
            set { bondedPawn = value; }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref bondedPawn, "scp131BondedPawn");
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            return (text.NullOrEmpty() ? "" : text + "\n") + (bondedPawn == null
                ? "SCP_131Unbonded".Translate() : "SCP_131Bonded".Translate(bondedPawn.LabelShortCap));
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(pawn)) yield return option;
            if (!Spawned || pawn == null || !pawn.IsColonistPlayerControlled || !pawn.RaceProps.Humanlike
                || pawn.Dead || pawn.Downed || pawn.Map != Map || pawn.Position.DistanceToSquared(Position) > 2)
                yield break;
            yield return new FloatMenuOption("SCP_131Befriend".Translate(), () => bondedPawn = pawn);
        }

        public Pawn ValidBondedPawn
        {
            get
            {
                if (bondedPawn != null && (!bondedPawn.Spawned || bondedPawn.Dead || bondedPawn.Map != Map))
                    bondedPawn = null;
                return bondedPawn;
            }
        }
    }

    public sealed class JobGiver_Scp131Follow : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Pawn_Scp131 pod = pawn as Pawn_Scp131;
            if (pod == null || pawn.Map == null || pawn.Downed || pawn.Dead) return null;
            ThingDef statueDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_173Object");
            Thing statue = statueDef == null ? null : pawn.Map.listerThings.ThingsOfDef(statueDef).FirstOrDefault();
            if (statue == null) return null;
            if (!CompScpPost.IsObjectAtPost(statue))
            {
                if (pawn.Position.DistanceToSquared(statue.Position) <= 9
                    && ScpSurveillance.Sight(pawn.Position, statue.Position, pawn.Map)) return null;
                if (!pawn.CanReach(statue, PathEndMode.Touch, Danger.Some)) return null;
                Job watch = JobMaker.MakeJob(JobDefOf.Goto, statue);
                watch.expiryInterval = 300;
                return watch;
            }
            Pawn followed = pod == null ? null : pod.ValidBondedPawn;
            CompScpPost post = CompScpPost.ForObject(statue);
            if (post == null || !post.useShifts || post.shifts == null
                || !post.shifts.Any(shift => shift.Has(followed, false) || shift.Has(followed, true)))
                return null;
            if (followed == null || pawn.Position.DistanceToSquared(followed.Position) <= 4
                || !pawn.CanReach(followed, PathEndMode.Touch, Danger.Some)) return null;
            Job job = JobMaker.MakeJob(JobDefOf.Goto, followed);
            job.expiryInterval = 300;
            return job;
        }
    }
}
