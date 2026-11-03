using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using ScpFoundation.Interactions;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp093DiscEntry : CompProperties
    {
        public CompProperties_Scp093DiscEntry() { compClass = typeof(CompScp093DiscEntry); }
    }

    public sealed class CompScp093DiscEntry : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Spawned && parent.Map.IsPlayerHome
                && !CompScp093Mirror.IsDiscAtActiveMirror(parent))
                yield return new Command_Action {
                    defaultLabel = "SCP_093ApplyDisc".Translate(),
                    defaultDesc = "SCP_093ApplyDiscDesc".Translate(),
                    icon = parent.def.uiIcon,
                    action = ChooseVisitor
                };
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            MapPortal mirror = FindMirror();
            CompScp093Mirror comp = mirror == null ? null : mirror.TryGetComp<CompScp093Mirror>();
            if (comp != null && comp.CanApply(parent, pawn))
                yield return new FloatMenuOption("SCP_093ApplyDisc".Translate(), () => Assign(pawn, mirror));
        }

        private MapPortal FindMirror()
        {
            if (!parent.Spawned || !parent.Map.IsPlayerHome) return null;
            Room room = parent.Position.GetRoom(parent.Map);
            if (room == null || room.TouchesMapEdge || room.IsDoorway) return null;
            ThingDef definition = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_093Mirror");
            if (definition == null) return null;
            foreach (Thing thing in parent.Map.listerThings.ThingsOfDef(definition))
                if (thing.Spawned && thing.Position.GetRoom(parent.Map) == room)
                    return thing as MapPortal;
            return null;
        }

        private void ChooseVisitor()
        {
            MapPortal mirror = FindMirror();
            CompScp093Mirror comp = mirror == null ? null : mirror.TryGetComp<CompScp093Mirror>();
            if (comp == null)
            {
                Messages.Message("SCP_093NoMirror".Translate(), parent, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (comp.DiscAttached)
            {
                Messages.Message("SCP_093AlreadyAttached".Translate(), mirror, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (mirror.PocketMapExists)
            {
                Messages.Message("SCP_093ExistingMap".Translate(), mirror, MessageTypeDefOf.RejectInput, false);
                return;
            }
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (Pawn pawn in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                if (!comp.CanApply(parent, pawn)) continue;
                Pawn visitor = pawn;
                options.Add(new FloatMenuOption(visitor.LabelShortCap, () => Assign(visitor, mirror)));
            }
            if (options.Count == 0)
                Messages.Message("SCP_093NoVisitor".Translate(), parent, MessageTypeDefOf.RejectInput, false);
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        private void Assign(Pawn pawn, MapPortal mirror)
        {
            CompScp093Mirror comp = mirror == null ? null : mirror.TryGetComp<CompScp093Mirror>();
            if (comp == null || !comp.CanApply(parent, pawn)) return;
            JobDef definition = DefDatabase<JobDef>.GetNamed("SCP_093ApplyDisc");
            Job job = JobMaker.MakeJob(definition, parent, mirror);
            job.count = 1;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    internal static class Scp093VisitorGizmoPatch
    {
        private static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance == null || !__instance.IsColonistPlayerControlled || Find.Maps == null) return;
            ThingDef mirrorDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_093Mirror");
            if (mirrorDef == null) return;
            foreach (Map map in Find.Maps)
            {
                if (!map.IsPlayerHome) continue;
                foreach (Thing thing in map.listerThings.ThingsOfDef(mirrorDef))
                {
                    CompScp093Mirror mirror = thing.TryGetComp<CompScp093Mirror>();
                    if (mirror == null || mirror.Visitor != __instance || !mirror.DiscAttached) continue;
                    __result = WithDetach(__result, mirror, thing);
                    return;
                }
            }
        }

        private static IEnumerable<Gizmo> WithDetach(IEnumerable<Gizmo> original,
            CompScp093Mirror mirror, Thing thing)
        {
            foreach (Gizmo gizmo in original) yield return gizmo;
            yield return new Command_Action {
                defaultLabel = "SCP_093DetachDisc".Translate(),
                defaultDesc = "SCP_093DetachDiscDesc".Translate(),
                icon = thing.def.uiIcon,
                action = mirror.RequestClose
            };
        }
    }

    public sealed class JobDriver_Scp093ApplyDisc : JobDriver
    {
        private Thing originalPost;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref originalPost, "scp093OriginalPost");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (originalPost == null)
            {
                CompScpPost post = CompScpPost.ForObject(job.targetA.Thing);
                originalPost = post == null ? null : post.parent;
            }
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed)
                && pawn.Reserve(job.targetB, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);
            AddFinishAction(condition =>
            {
                if (pawn.carryTracker != null && pawn.carryTracker.CarriedThing == job.targetA.Thing)
                {
                    Thing dropped;
                    pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out dropped);
                }
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_General.Wait(180, TargetIndex.B);
            yield return Toils_General.Do(Enter);
        }

        private void Enter()
        {
            Thing disc = job.targetA.Thing;
            MapPortal mirror = job.targetB.Thing as MapPortal;
            CompScp093Mirror comp = mirror == null ? null : mirror.TryGetComp<CompScp093Mirror>();
            Map home = pawn.Map;
            if (disc == null || mirror == null || comp == null || comp.DiscAttached
                || home == null || mirror.Map != home || !home.IsPlayerHome
                || pawn.carryTracker.CarriedThing != disc || pawn.Dead || pawn.Downed)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            Thing placed;
            if (!pawn.carryTracker.TryDropCarriedThing(mirror.Position, ThingPlaceMode.Direct, out placed)
                || placed != disc || !disc.Spawned
                || disc.Position.GetRoom(home) != mirror.Position.GetRoom(home))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            CompScpPost post = originalPost == null ? null : originalPost.TryGetComp<CompScpPost>();
            if (post != null && post.BoundObject == disc) post.Bind(null);

            comp.BeginVisit(disc, pawn);
            Map destination;
            try { destination = mirror.GetOtherMap(); }
            catch (Exception error)
            {
                comp.AbortVisit();
                Log.Error("SCP-093 pocket map generation failed: " + error);
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            IntVec3 landing = mirror.GetDestinationLocation();
            if (destination == null || !landing.IsValid)
            {
                comp.AbortVisit();
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            if (!landing.Standable(destination))
                landing = CellFinder.StandableCellNear(landing, destination, 5f);
            if (!landing.IsValid)
            {
                comp.AbortVisit();
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            pawn.DeSpawnOrDeselect();
            GenSpawn.Spawn(pawn, landing, destination);
            mirror.OnEntered(pawn);
            comp.NotifyEntered();
            if (pawn.IsPlayerControlled && pawn.drafter != null)
                pawn.drafter.Drafted = true;
        }
    }
}
