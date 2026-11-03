using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using UnityEngine;
using Verse;

namespace ScpFoundation.Interactions
{
    public sealed class ScpDutyShift : IExposable
    {
        public int hours;
        public Pawn observerA, observerB, researcher;
        public bool Has(Pawn pawn, bool study) { return pawn != null && (study ? researcher == pawn : observerA == pawn || observerB == pawn); }
        public void ExposeData()
        {
            Scribe_Values.Look(ref hours, "hours", 0);
            Scribe_References.Look(ref observerA, "observerA");
            Scribe_References.Look(ref observerB, "observerB");
            Scribe_References.Look(ref researcher, "researcher");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) hours &= ShiftPolicy.AllHours;
        }
    }

    public sealed class CompProperties_ScpPost : CompProperties
    {
        public CompProperties_ScpPost() { compClass = typeof(CompScpPost); }
    }

    [StaticConstructorOnStartup]
    public sealed partial class CompScpPost : ThingComp
    {
        private static Material researchBarFilled;
        private static Material researchBarUnfilled;
        public bool useShifts;
        public List<ScpDutyShift> shifts = new List<ScpDutyShift>();
        private Thing assignedObject;
        private bool bindingInitialized;
        // RimWorld anchors multi-cell buildings at their center-side cell.
        public IntVec3 ObjectCell { get { return parent.Position; } }
        public bool Accepts(Thing thing) { return parent.def.defName == "SCP_682AcidChamber"
            ? thing != null && thing.def.defName == "SCP_682Object"
            : parent.def.defName == "SCP_262Hanger"
                ? thing != null && thing.def.defName == "SCP_262Coat"
                : thing != null && thing.def.defName != "SCP_682Object" && thing.def.defName != "SCP_262Coat"; }
        public override void Initialize(CompProperties properties)
        {
            base.Initialize(properties);
            SetDefaultShifts();
        }
        private void SetDefaultShifts()
        {
            shifts = new List<ScpDutyShift>();
            shifts.Add(new ScpDutyShift { hours = ShiftPolicy.Range(0, 8) });
            shifts.Add(new ScpDutyShift { hours = ShiftPolicy.Range(8, 16) });
            shifts.Add(new ScpDutyShift { hours = ShiftPolicy.Range(16, 24) });
        }
        public Thing BoundObject
        {
            get
            {
                if (assignedObject != null && assignedObject.Destroyed) assignedObject = null;
                // Adopt an old save's object only on this exact post, never guess by room.
                if (!bindingInitialized && assignedObject == null && parent.Spawned)
                {
                    Thing candidate = ObjectCell.GetThingList(parent.Map).FirstOrDefault(thing => Accepts(thing) && thing.TryGetComp<CompScpInteraction>() != null);
                    if (candidate != null)
                    {
                        if (!All(parent.Map).Any(post => post != this && post.assignedObject == candidate)) assignedObject = candidate;
                        bindingInitialized = true;
                    }
                }
                return assignedObject;
            }
        }
        public void Bind(Thing thing)
        {
            foreach (CompScpPost post in All(parent.Map)) if (thing != null && post != this && post.BoundObject == thing)
            { post.assignedObject = null; post.bindingInitialized = true; }
            assignedObject = thing;
            bindingInitialized = true;
            if (thing != null && thing.Spawned && thing.Position == ObjectCell)
            {
                CompScp anomaly = thing.TryGetComp<CompScp>();
                if (anomaly != null && anomaly.IsInCustody) anomaly.TryRecover();
            }
        }
        public static IEnumerable<CompScpPost> All(Map map)
        {
            if (map == null || !map.IsPlayerHome) return Enumerable.Empty<CompScpPost>();
            ThingDef tank = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_682AcidChamber");
            ThingDef hanger = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_262Hanger");
            IEnumerable<Thing> posts = map.listerThings.ThingsOfDef(ScpInteractionDefOf.SCP_ContainmentAnchor);
            if (tank != null) posts = posts.Concat(map.listerThings.ThingsOfDef(tank));
            if (hanger != null) posts = posts.Concat(map.listerThings.ThingsOfDef(hanger));
            return posts.Where(thing => thing.Faction == Faction.OfPlayer)
                .Select(thing => thing.TryGetComp<CompScpPost>()).Where(comp => comp != null);
        }
        public static CompScpPost ForObject(Thing thing)
        {
            if (thing == null || !thing.Spawned) return null;
            return All(thing.Map).FirstOrDefault(post => post.BoundObject == thing);
        }
        public bool OnDuty(Pawn pawn, bool study, bool early)
        {
            float hour = GenLocalDate.HourFloat(parent.Map);
            return shifts.Any(shift => shift.Has(pawn, study) && ShiftPolicy.Due(shift.hours, hour, early && !study));
        }
        public static bool IsObjectAtPost(Thing thing)
        {
            CompScpPost post = ForObject(thing);
            return SessionWorkPolicy.AtPost(thing != null && thing.Spawned,
                post != null && post.parent.Spawned, post != null,
                post != null && post.parent.Map == thing.Map,
                post != null && post.ObjectCell == thing.Position && post.Accepts(thing));
        }
        public override void PostDraw()
        {
            base.PostDraw();
            Thing thing = BoundObject;
            if (!parent.Spawned || thing == null || !thing.Spawned || thing.Map != parent.Map
                || thing.Position != ObjectCell || !Accepts(thing)) return;
            CompScp anomaly = thing.TryGetComp<CompScp>();
            if (anomaly == null || anomaly.Definition == null || anomaly.Definition.research == null) return;
            bool studying = ScpObservation.AssignedTo(thing, ScpInteractionDefOf.SCP_Study) > 0;
            if (!studying && !Find.Selector.IsSelected(parent) && !Find.Selector.IsSelected(thing)) return;
            ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
            KnowledgeLevel level = knowledge.LevelFor(anomaly.Definition);
            if (level >= KnowledgeLevel.Documented && knowledge.ExperimentsComplete(anomaly.Definition)) return;
            if (researchBarFilled == null)
                researchBarFilled = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.2f, 0.75f, 0.95f), false);
            if (researchBarUnfilled == null)
                researchBarUnfilled = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.08f, 0.1f, 0.12f), false);
            var spec = anomaly.Definition.research;
            GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest
            {
                center = parent.DrawPos + new Vector3(0f, 0.1f, -0.55f),
                size = new Vector2(0.85f, 0.11f),
                fillPercent = level >= KnowledgeLevel.Documented
                    ? ResearchModel.ExperimentProgress(knowledge.ExperimentWork(anomaly.Definition), spec.observedWork)
                    : ResearchModel.StageProgress(knowledge.ResearchWork(anomaly.Definition), (int)level,
                        spec.observedWork, spec.partialWork, spec.fullWork),
                filledMat = researchBarFilled,
                unfilledMat = researchBarUnfilled,
                margin = 0.03f,
                rotation = Rot4.North
            });
        }
        public bool CanStart(Pawn pawn, bool study)
        {
            if (pawn == null || pawn.workSettings == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight)
                || study && !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return false;
            WorkTypeDef work = study ? WorkTypeDefOf.Research : DefDatabase<WorkTypeDef>.GetNamed("BasicWorker");
            if (pawn.WorkTypeIsDisabled(work) || pawn.workSettings.GetPriority(work) == 0 || NeedsBreak(pawn)) return false;
            if (!useShifts) return true;
            if (!OnDuty(pawn, study, true)) return false;
            // A contradictory roster must not send one pawn to two posts. Stable priority.
            foreach (CompScpPost other in All(parent.Map))
                if (other != this && other.useShifts && other.BoundObject != null && other.BoundObject.Spawned
                    && other.parent.thingIDNumber < parent.thingIDNumber
                    && (other.OnDuty(pawn, false, false) || other.OnDuty(pawn, true, false))) return false;
            if (study && OnDuty(pawn, false, false)) return false;
            return true;
        }
        public static bool NeedsBreak(Pawn pawn)
        {
            return !pawn.Awake() || (pawn.timetable != null && pawn.timetable.CurrentAssignment == TimeAssignmentDefOf.Sleep)
                || (pawn.needs.food != null && pawn.needs.food.CurLevelPercentage < 0.2f)
                || (pawn.needs.rest != null && pawn.needs.rest.CurLevelPercentage < 0.2f);
        }
        public int ReliefReady(Thing thing)
        {
            List<Pawn> ready = ScpObservation.Observers(thing);
            return ready.Count(pawn => OnDuty(pawn, false, false)
                && ready.Any(other => other != pawn && ScpSurveillance.Sight(pawn.Position, other.Position, thing.Map)));
        }
        public string Summary()
        {
            if (parent.Spawned && parent.Position.GetThingList(parent.Map).OfType<Pawn_Scp049>()
                .Any(doctor => doctor.IsContainedAt(parent)))
                return "SCP_049InspectContained".Translate();
            if (!useShifts) return "SCP_ShiftsOff".Translate();
            var text = new StringBuilder();
            text.AppendLine("SCP_ShiftLocalHour".Translate(GenLocalDate.HourInteger(parent.Map)));
            foreach (ScpDutyShift shift in shifts.Where(shift => ShiftPolicy.Active(shift.hours, GenLocalDate.HourInteger(parent.Map))))
                text.AppendLine("SCP_ShiftCurrent".Translate(Name(shift.observerA), Name(shift.observerB), Name(shift.researcher)));
            Thing thing = BoundObject;
            text.AppendLine(thing == null ? "SCP_ShiftNoObject".Translate() : "SCP_ShiftBound".Translate(thing.TryGetComp<CompScp>().PublicLabel));
            if (thing != null && thing.Spawned && thing.Map == parent.Map)
            {
                text.AppendLine(thing.TryGetComp<CompScp>().OperationalLabel);
                int required = ScpObservation.StaffTarget(thing.TryGetComp<CompScp>().Definition);
                if (required > 0)
                {
                    int present = ScpObservation.Observers(thing).Count;
                    text.AppendLine((ScpSurveillance.Observed(thing) ? "SCP_ObserversRemoteCount" : "SCP_ObserversCount").Translate(present, required));
                }
            }
            return text.ToString();
        }
        public static string Name(Pawn pawn) { return pawn == null ? "SCP_ShiftNobody".Translate().ToString() : pawn.LabelShortCap.ToString(); }
        public string ValidateRoster()
        {
            var gaps = new List<string>();
            var conflicts = new HashSet<string>();
            Thing thing = BoundObject;
            int required = thing == null ? 2 : ScpObservation.StaffTarget(thing.TryGetComp<CompScp>().Definition);
            foreach (int hour in Enumerable.Range(0, 24))
            {
                var active = shifts.Where(shift => ShiftPolicy.Active(shift.hours, hour)).ToList();
                var observers = active.SelectMany(shift => new[] { shift.observerA, shift.observerB }).Where(pawn => pawn != null && !pawn.Dead).Distinct().ToList();
                if (observers.Count < required) gaps.Add(hour.ToString("00"));
                var assigned = observers.Concat(active.Select(shift => shift.researcher).Where(pawn => pawn != null)).Distinct();
                foreach (Pawn pawn in assigned)
                {
                    if (observers.Contains(pawn) && active.Any(shift => shift.researcher == pawn)) conflicts.Add(Name(pawn));
                    foreach (CompScpPost other in All(parent.Map))
                        if (other != this && other.useShifts && other.shifts.Any(shift => ShiftPolicy.Active(shift.hours, hour) && (shift.Has(pawn, false) || shift.Has(pawn, true)))) conflicts.Add(Name(pawn));
                }
            }
            var text = new StringBuilder();
            if (gaps.Count > 0) text.AppendLine("SCP_ShiftGaps".Translate(string.Join(", ", gaps.ToArray())));
            if (conflicts.Count > 0) text.AppendLine("SCP_ShiftConflicts".Translate(string.Join(", ", conflicts.ToArray())));
            if (gaps.Count + conflicts.Count == 0) text.AppendLine("SCP_ShiftRosterValid".Translate());
            foreach (ScpDutyShift shift in shifts.Where(shift => ShiftPolicy.Active(shift.hours, GenLocalDate.HourInteger(parent.Map))))
            {
                text.AppendLine(Name(shift.observerA) + ": " + DutyStatus(shift.observerA, false, true));
                text.AppendLine(Name(shift.observerB) + ": " + DutyStatus(shift.observerB, false, true));
                text.AppendLine(Name(shift.researcher) + ": " + DutyStatus(shift.researcher, true, true));
            }
            text.AppendLine("SCP_ShiftsWorkHint".Translate());
            return text.ToString();
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref useShifts, "scpUseShifts", false);
            Scribe_References.Look(ref assignedObject, "scpAssignedObject");
            Scribe_Values.Look(ref bindingInitialized, "scpPostBindingInitialized", false);
            Scribe_Collections.Look(ref shifts, "scpDutyShifts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (shifts == null) SetDefaultShifts();
                shifts.RemoveAll(shift => shift == null);
            }
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer || !parent.Spawned || !parent.Map.IsPlayerHome) yield break;
            yield return new Command_Action { defaultLabel = "SCP_ShiftsButton".Translate(), defaultDesc = "SCP_ShiftsButtonDesc".Translate(),
                icon = ScpIcons.Get(0), action = () => Find.WindowStack.Add(new Dialog_ScpShifts(this)) };
        }
        public override string CompInspectStringExtra() { return parent.Spawned ? Summary().TrimEnd() : null; }
    }
}
