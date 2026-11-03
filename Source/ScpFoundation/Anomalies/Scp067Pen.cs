using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp067 : CompProperties
    {
        public CompProperties_Scp067() { compClass = typeof(CompScp067); }
    }

    public sealed class CompScp067 : ThingComp
    {
        internal static bool TryFindDrawingStation(Thing pen, Pawn pawn, out Building table, out Building chair)
        {
            table = null;
            chair = null;
            if (pen == null || !pen.Spawned) return false;
            Room room = pen.Position.GetRoom(pen.Map);
            if (room == null) return false;
            List<Building> furniture = pen.Map.listerThings.AllThings.OfType<Building>()
                .Where(b => b.Spawned && b.Position.GetRoom(pen.Map) == room).ToList();
            foreach (Building candidate in furniture)
            {
                if (candidate.def.replaceTags == null || !candidate.def.replaceTags.Contains("Table")) continue;
                foreach (Building seat in furniture)
                {
                    if (seat.def.replaceTags == null || !seat.def.replaceTags.Contains("Chair")
                        || !candidate.OccupiedRect().ExpandedBy(1).Contains(seat.Position)
                        || (pawn != null && !pawn.CanReserveAndReach(seat, PathEndMode.OnCell, Danger.Some))) continue;
                    table = candidate;
                    chair = seat;
                    return true;
                }
            }
            return false;
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            if (pawn == null || !pawn.IsColonistPlayerControlled || pawn.Drafted || !parent.Spawned)
                yield break;
            CompScp anomaly = parent.TryGetComp<CompScp>();
            if (anomaly == null || !anomaly.IsInCustody || !CompScpPost.IsObjectAtPost(parent))
            {
                yield return new FloatMenuOption("SCP_067NeedsPost".Translate(), null);
                yield break;
            }
            if (!pawn.CanReserveAndReach(parent, PathEndMode.Touch, Danger.Some))
            {
                yield return new FloatMenuOption("SCP_067CannotReach".Translate(), null);
                yield break;
            }
            Building table, chair;
            if (!TryFindDrawingStation(parent, pawn, out table, out chair))
            {
                yield return new FloatMenuOption("SCP_067NeedsDrawingStation".Translate(), null);
                yield break;
            }
            yield return new FloatMenuOption("SCP_067DrawOrder".Translate(), () =>
            {
                JobDef definition = DefDatabase<JobDef>.GetNamedSilentFail("SCP_067Draw");
                if (definition != null)
                {
                    Job job = JobMaker.MakeJob(definition, parent, chair, table);
                    job.count = 1;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            });
        }
    }

    public sealed class JobDriver_Scp067Draw : JobDriver
    {
        private Thing originalPost;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref originalPost, "scp067DrawingPost");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (originalPost == null)
            {
                CompScpPost post = CompScpPost.ForObject(job.targetA.Thing);
                originalPost = post == null ? null : post.parent;
            }
            if (originalPost == null || !originalPost.Spawned) return false;
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed)
                && pawn.Reserve(job.targetB, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);
            this.FailOn(() => originalPost == null || !originalPost.Spawned);
            AddFinishAction(condition =>
            {
                if (pawn.carryTracker.CarriedThing == job.targetA.Thing)
                {
                    Thing dropped;
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out dropped);
                }
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.OnCell);
            yield return Toils_General.Wait(5000, TargetIndex.C);
            yield return Toils_General.Do(() =>
            {
                Thing pen = job.targetA.Thing;
                Building chair = job.targetB.Thing as Building;
                Building table = job.targetC.Thing as Building;
                CompScpPost post = originalPost.TryGetComp<CompScpPost>();
                if (pen == null || pawn.carryTracker.CarriedThing != pen
                    || post == null || post.BoundObject != pen
                    || chair == null || table == null || !chair.Spawned || !table.Spawned
                    || chair.Position.GetRoom(pawn.Map) != originalPost.Position.GetRoom(pawn.Map)
                    || table.Position.GetRoom(pawn.Map) != originalPost.Position.GetRoom(pawn.Map)
                    || !table.OccupiedRect().ExpandedBy(1).Contains(chair.Position))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                int roll = Rand.RangeInclusive(0, 9);
                string drawingName = roll < 6 ? "SCP_067DrawingGood"
                    : roll < 9 ? "SCP_067DrawingExcellent" : "SCP_067DrawingLegendary";
                QualityCategory qualityCategory = roll < 6 ? QualityCategory.Good
                    : roll < 9 ? QualityCategory.Excellent : QualityCategory.Legendary;
                ThingDef drawingDef = DefDatabase<ThingDef>.GetNamedSilentFail(drawingName);
                if (drawingDef == null) return;
                Thing drawing = ThingMaker.MakeThing(drawingDef);
                CompQuality quality = drawing.TryGetComp<CompQuality>();
                if (quality != null) quality.SetQuality(qualityCategory, ArtGenerationContext.Colony);
                if (!GenPlace.TryPlaceThing(drawing, table.Position, pawn.Map, ThingPlaceMode.Near))
                {
                    drawing.Destroy(DestroyMode.Vanish);
                    return;
                }
                if (pawn.skills != null) pawn.skills.Learn(SkillDefOf.Artistic, 400f);
                Messages.Message("SCP_067DrawingComplete".Translate(pawn.LabelShortCap), drawing,
                    MessageTypeDefOf.PositiveEvent, false);
            });
            yield return Toils_General.Do(() =>
            {
                IntVec3 spot = GenRadial.RadialCellsAround(originalPost.Position, 1.5f, false)
                    .Where(cell => cell != originalPost.Position && cell.InBounds(pawn.Map)
                        && cell.Standable(pawn.Map)
                        && cell.GetRoom(pawn.Map) == originalPost.Position.GetRoom(pawn.Map)
                        && (cell.GetFirstPawn(pawn.Map) == null || cell.GetFirstPawn(pawn.Map) == pawn)
                        && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some))
                    .OrderBy(cell => cell.DistanceToSquared(pawn.Position))
                    .DefaultIfEmpty(IntVec3.Invalid).First();
                if (!spot.IsValid) { EndJobWith(JobCondition.Incompletable); return; }
                job.targetC = spot;
            });
            yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
            yield return Toils_General.Do(() =>
            {
                Thing pen = job.targetA.Thing;
                CompScpPost post = originalPost.TryGetComp<CompScpPost>();
                Thing placed;
                if (post == null || post.BoundObject != pen || pawn.carryTracker.CarriedThing != pen
                    || !pawn.carryTracker.TryDropCarriedThing(originalPost.Position, ThingPlaceMode.Direct, out placed)
                    || placed != pen || !pen.Spawned || pen.Position != originalPost.Position)
                { EndJobWith(JobCondition.Incompletable); return; }
                post.Bind(pen);
            });
        }
    }
}
