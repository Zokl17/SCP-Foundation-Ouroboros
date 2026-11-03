using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Containment;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public sealed class CompProperties_ScpInteraction : CompProperties
    {
        public CompProperties_ScpInteraction() { compClass = typeof(CompScpInteraction); }
    }

    public sealed class CompScpInteraction : ThingComp
    {
        public bool observationEnabled = true;
        public bool researchEnabled;
        public CompScp Anomaly { get { return parent.GetComp<CompScp>(); } }
        public bool CanWorkAtPost { get { return Anomaly.IsInCustody && ScpOperationalPolicy.IsAtPost(Anomaly.OperationalFlags); } }
        private bool SafeToCarryBy(Pawn pawn)
        {
            CompScp682 reptile = parent.TryGetComp<CompScp682>();
            CompScp096 shy = parent.TryGetComp<CompScp096>();
            return (reptile == null || reptile.IsStunned)
                && (shy == null || shy.IsIncapacitated)
                && (!(parent is Thing_Scp173) || Scp173Transport.SelectEscorts(parent, pawn).Count == 2);
        }
        public override void PostExposeData()
        {
            Scribe_Values.Look(ref observationEnabled, "scpObservationEnabled", true);
            Scribe_Values.Look(ref researchEnabled, "scpResearchEnabled", false);
        }

        public string Dossier()
        {
            ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
            KnowledgeLevel level = knowledge.LevelFor(Anomaly.Definition);
            var spec = Anomaly.Definition.research;
            if (spec == null) return "SCP_DossierUnknown".Translate();
            if (Anomaly.Clearance == 0 && !Anomaly.CanResearchNow) return Anomaly.ClearanceHint;
            if (level == KnowledgeLevel.Unknown)
                return "SCP_DossierLevelZeroResearch".Translate() + "\n\n" + ResearchProgress(knowledge, level, spec);
            CompScp055 antimeme = parent.TryGetComp<CompScp055>();
            string dossier = antimeme == null ? spec.observedReportKey.Translate().ToString() : antimeme.DossierText(spec.observedReportKey);
            if (level >= KnowledgeLevel.PartiallyDocumented) dossier += "\n\n" + (antimeme == null ? spec.partialReportKey.Translate().ToString() : antimeme.DossierText(spec.partialReportKey));
            if (level >= KnowledgeLevel.Documented) dossier += "\n\n" + (antimeme == null ? spec.documentedReportKey.Translate().ToString() : antimeme.DossierText(spec.documentedReportKey));
            if (level >= KnowledgeLevel.Documented) return dossier + "\n\n" + "SCP_ResearchComplete".Translate()
                + ScpArticleCatalog.ExperimentDossierText(Anomaly.Definition, knowledge);
            return dossier + "\n\n" + ResearchProgress(knowledge, level, spec);
        }

        private string ResearchProgress(ScpKnowledgeComponent knowledge, KnowledgeLevel level, ScpFoundation.Definitions.ScpResearchSpec spec)
        {
            float work = ResearchModel.CurrentStageWork(knowledge.ResearchWork(Anomaly.Definition), (int)level,
                spec.observedWork, spec.partialWork, spec.fullWork);
            float required = ResearchModel.NextStageWork((int)level, spec.observedWork, spec.partialWork, spec.fullWork);
            float remaining = required - work;
            return "SCP_ResearchProgress".Translate((int)level + 1,
                (required <= 0 ? 1f : work / required).ToString("P0"),
                GenDate.ToStringTicksToPeriod((int)System.Math.Ceiling(remaining), allowSeconds: false));
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.def.defName == "SCP_330Bowl" && (parent.Map == null || !parent.Map.IsPlayerHome))
                yield break;
            if (parent.Map != null && parent.Map.IsPlayerHome
                && !CompScp093Mirror.IsDiscAtActiveMirror(parent))
                yield return new Command_Action { defaultLabel = "SCP_MoveToPostButton".Translate(),
                    defaultDesc = (parent is Thing_Scp173 ? "SCP_173TransportDesc" : "SCP_MoveToPostDesc").Translate(),
                    icon = ScpIcons.Get(5), action = ChooseCarrier };
            yield return new Command_Action { defaultLabel = "SCP_TerminalTitle".Translate(), defaultDesc = "SCP_TerminalOpenDesc".Translate(),
                icon = ScpIcons.Get(3), action = () => MainTabWindow_ScpTerminal.Open(Anomaly) };
            if (!CanWorkAtPost) yield break;
            yield return new Command_Toggle { defaultLabel = "SCP_ObserveToggle".Translate(), defaultDesc = "SCP_ObserveToggleDesc".Translate(),
                icon = ScpIcons.Get(0), isActive = () => observationEnabled, toggleAction = () => observationEnabled = !observationEnabled };
            yield return new Command_Toggle { defaultLabel = "SCP_ResearchToggle".Translate(), defaultDesc = "SCP_ResearchToggleDesc".Translate(),
                icon = ScpIcons.Get(1), isActive = () => researchEnabled, toggleAction = () => researchEnabled = !researchEnabled };
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            if (parent.def.defName == "SCP_330Bowl" && (parent.Map == null || !parent.Map.IsPlayerHome))
                yield break;
            if (parent.Map == null || !parent.Map.IsPlayerHome) yield break;
            if (CompScp093Mirror.IsDiscAtActiveMirror(parent)) yield break;
            if (!pawn.IsColonistPlayerControlled || pawn.Drafted || !pawn.CanReserve(parent, ShiftPolicy.ReservationSlots, 0)) yield break;
            bool foundAnchor = false;
            foreach (Thing anchor in CompScpPost.All(pawn.Map).Where(post => post.Accepts(parent))
                .Select(post => post.parent).OrderBy(thing => thing.Position.DistanceToSquared(parent.Position)))
            {
                if (!AnchorAvailable(anchor, parent) || !pawn.CanReserveAndReach(parent, PathEndMode.Touch, Danger.Some)
                    || !pawn.CanReserveAndReach(anchor.TryGetComp<CompScpPost>().ObjectCell, PathEndMode.OnCell, Danger.Some)) continue;
                Thing destination = anchor;
                foundAnchor = true;
                bool canMove = SafeToCarryBy(pawn);
                string orderLabel = canMove ? "SCP_CaptureOrder".Translate(destination.LabelCap).ToString()
                    : parent is Thing_Scp173 ? "SCP_173TransportNeedsTeam".Translate().ToString()
                    : parent is Thing_Scp682 ? "SCP_682NeedsIncapacitation".Translate().ToString()
                    : parent is Thing_Scp096 ? "SCP_096NeedsIncapacitation".Translate().ToString()
                    : "SCP_CaptureOrder".Translate(destination.LabelCap).ToString();
                yield return new FloatMenuOption(orderLabel, canMove ? (System.Action)(() =>
                {
                    AssignCapture(pawn, destination);
                }) : null);
                break;
            }
            if (!foundAnchor) yield return new FloatMenuOption((parent.def.defName == "SCP_093Disc"
                ? "SCP_093NoCapturePost" : "SCP_NoCapturePost").Translate(), null);
            if (!CanWorkAtPost)
            {
                yield return new FloatMenuOption("SCP_MoveToPostFirst".Translate(), null);
                yield break;
            }
            yield return new FloatMenuOption("SCP_ObserveOrder".Translate(), () => AssignSession(pawn, ScpInteractionDefOf.SCP_Observe));
            if (Anomaly.CanResearchNow && Current.Game.GetComponent<ScpKnowledgeComponent>().NeedsResearch(Anomaly.Definition))
                yield return new FloatMenuOption("SCP_StudyOrder".Translate(), () => AssignSession(pawn, ScpInteractionDefOf.SCP_Study));
            if (ScpOperationalPolicy.NeedsRecovery(Anomaly.OperationalFlags))
                yield return new FloatMenuOption("SCP_RestoreOrder".Translate(), () => AssignSession(pawn, ScpInteractionDefOf.SCP_Restore));
        }

        public static bool AnchorAvailable(Thing anchor)
        {
            CompScpPost post = anchor.TryGetComp<CompScpPost>();
            return anchor.Spawned && anchor.Faction == Faction.OfPlayer
                && post != null && post.BoundObject == null
                && !post.ObjectCell.GetThingList(anchor.Map).Any(thing => thing is Pawn
                    || thing.def.category == ThingCategory.Item);
        }

        public static bool AnchorAvailable(Thing anchor, Thing target)
        {
            CompScpPost post = anchor == null ? null : anchor.TryGetComp<CompScpPost>();
            if (target == null || post == null || !anchor.Spawned || anchor.Faction != Faction.OfPlayer
                || !post.Accepts(target) || (post.BoundObject != null && post.BoundObject != target)
                || (post.BoundObject == target && CompScpPost.IsObjectAtPost(target))) return false;
            foreach (IntVec3 cell in GenAdj.OccupiedRect(post.ObjectCell,
                Rot4.North, target.def.size))
                if (!cell.InBounds(anchor.Map) || !cell.Standable(anchor.Map)
                    || cell.GetThingList(anchor.Map).Any(thing => thing != anchor && thing != target
                        && (thing is Pawn || thing.def.category == ThingCategory.Item))) return false;
            return true;
        }

        private void ChooseCarrier()
        {
            if (!parent.Spawned) return;
            var options = new List<FloatMenuOption>();
            foreach (Pawn candidate in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                Pawn pawn = candidate;
                if (pawn.Drafted || !pawn.IsColonistPlayerControlled || !SafeToCarryBy(pawn)
                    || !pawn.CanReserveAndReach(parent, PathEndMode.Touch, Danger.Some)) continue;
                Thing anchor = CompScpPost.All(parent.Map).Where(post => post.Accepts(parent))
                    .Select(post => post.parent)
                    .Where(post => AnchorAvailable(post, parent) && pawn.CanReserveAndReach(post.TryGetComp<CompScpPost>().ObjectCell, PathEndMode.OnCell, Danger.Some))
                    .OrderBy(post => post.Position.DistanceToSquared(parent.Position)).FirstOrDefault();
                if (anchor == null) continue;
                Thing destination = anchor;
                List<Pawn> escorts = parent is Thing_Scp173 ? Scp173Transport.SelectEscorts(parent, pawn) : null;
                string label = escorts == null ? pawn.LabelShortCap + " — " + destination.LabelCap
                    : "SCP_173CrewOption".Translate(pawn.LabelShortCap, destination.LabelCap,
                        escorts[0].LabelShortCap, escorts[1].LabelShortCap).ToString();
                options.Add(new FloatMenuOption(label, () =>
                {
                    if (!parent.Spawned || !AnchorAvailable(destination, parent)) return;
                    AssignCapture(pawn, destination);
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption(parent is Thing_Scp173
                ? "SCP_173TransportNeedsTeam".Translate().ToString()
                : parent is Thing_Scp682 && !parent.GetComp<CompScp682>().IsStunned
                    ? "SCP_682NeedsIncapacitation".Translate().ToString()
                    : parent is Thing_Scp096 && !parent.GetComp<CompScp096>().IsIncapacitated
                        ? "SCP_096NeedsIncapacitation".Translate().ToString()
                    : (parent.def.defName == "SCP_093Disc"
                        ? "SCP_093NoCapturePost" : "SCP_NoCarrierOrPost").Translate().ToString(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void AssignCapture(Pawn pawn, Thing destination)
        {
            if (!SafeToCarryBy(pawn))
            {
                Messages.Message((parent is Thing_Scp682 ? "SCP_682NeedsIncapacitation"
                    : "SCP_173TransportNeedsTeam").Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Job job = JobMaker.MakeJob(ScpInteractionDefOf.SCP_Capture, parent,
                destination.TryGetComp<CompScpPost>().ObjectCell, destination);
            job.count = 1;
            job.haulMode = HaulMode.ToCellNonStorage;
            List<Pawn> assigned = new List<Pawn>();
            if (parent is Thing_Scp173)
            {
                List<Pawn> escorts = Scp173Transport.SelectEscorts(parent, pawn);
                job.targetQueueB = escorts.Select(escort => (LocalTargetInfo)escort).ToList();
                foreach (Pawn escort in escorts)
                {
                    Job watch = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("SCP_173Escort"), pawn, parent);
                    watch.playerForced = true;
                    if (!escort.jobs.TryTakeOrderedJob(watch, JobTag.Misc))
                    {
                        StopEscorts(assigned);
                        Messages.Message("SCP_173TransportNeedsTeam".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    assigned.Add(escort);
                }
            }
            if (!pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc)) { StopEscorts(assigned); return; }
            if (assigned.Count == 2)
                Messages.Message("SCP_173TransportAssigned".Translate(pawn.LabelShortCap,
                    assigned[0].LabelShortCap, assigned[1].LabelShortCap), MessageTypeDefOf.NeutralEvent, false);
        }

        private static void StopEscorts(List<Pawn> escorts)
        {
            foreach (Pawn escort in escorts)
                if (escort.CurJob != null && escort.CurJob.def.defName == "SCP_173Escort")
                    escort.jobs.EndCurrentJob(JobCondition.Incompletable);
        }

        private void AssignSession(Pawn pawn, JobDef def)
        {
            if (!CanWorkAtPost) { Messages.Message("SCP_MoveToPostFirst".Translate(), MessageTypeDefOf.RejectInput, false); return; }
            if (def == ScpInteractionDefOf.SCP_Restore && !Anomaly.TryRecover())
            {
                Anomaly.ShowRequirements();
                return;
            }
            if (def == ScpInteractionDefOf.SCP_Restore) return;
            IntVec3 cell;
            if (!ScpObservation.TrySpot(pawn, parent, out cell, sameRoom: def != ScpInteractionDefOf.SCP_Observe)) { Messages.Message("SCP_NoObservationSpot".Translate(), MessageTypeDefOf.RejectInput, false); return; }
            if (def == ScpInteractionDefOf.SCP_Observe) observationEnabled = true;
            if (def == ScpInteractionDefOf.SCP_Study) researchEnabled = true;
            Job ordered = JobMaker.MakeJob(def, parent, cell);
            ordered.playerForced = true;
            pawn.jobs.TryTakeOrderedJob(ordered, JobTag.Misc);
        }
    }
}
