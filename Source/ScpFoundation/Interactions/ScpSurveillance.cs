using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Containment;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public static class ScpSurveillance
    {
        public static bool Sight(IntVec3 from, IntVec3 to, Map map)
        {
            if (!from.InBounds(map) || !to.InBounds(map)) return false;
            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(from, to))
            {
                if (cell == from || cell == to) continue;
                Building wall = cell.GetEdifice(map);
                if (wall != null && wall.def == ScpInteractionDefOf.SCPV1_GlassWall) continue;
                if (!cell.CanBeSeenOver(map)) return false;
            }
            return true;
        }
        public static bool Powered(Thing thing)
        {
            var power = thing.TryGetComp<CompPowerTrader>();
            return thing.Spawned && thing.Faction == Faction.OfPlayer && power != null
                && !thing.Map.GetComponent<FoundationEmpShutdownComponent>().IsDisabled(thing)
                && (power.PowerOn || CompEmergencyAccumulator.BackupFor(thing));
        }
        public static bool Permitted(Thing thing)
        {
            var scp = thing.TryGetComp<CompScp>();
            return scp != null && scp.Definition.requirements.OfType<RequirementObservers>().Any(req => req.allowRemote);
        }
        public static bool EligibleAtPost(Thing thing)
        {
            CompScp scp = thing == null ? null : thing.TryGetComp<CompScp>();
            return scp != null && Permitted(thing)
                && ScpOperationalPolicy.AllowsRemoteObservation(scp.OperationalFlags);
        }
        public static bool CameraSees(Thing camera, Thing target, bool requirePower = true)
        {
            if ((requirePower && !Powered(camera)) || !ScpWallCamera.Mounted(camera) || !target.Spawned || target.Map != camera.Map) return false;
            IntVec3 delta = target.Position - camera.Position;
            bool sameRoom = camera.Position.GetRoom(camera.Map) == target.Position.GetRoom(target.Map);
            return SurveillancePolicy.CameraCovers(delta.x, delta.z, sameRoom) && Sight(camera.Position, target.Position, camera.Map);
        }
        public static bool HasCamera(Thing target)
        {
            CompScpPost post = CompScpPost.ForObject(target);
            return post != null && HasCamera(post);
        }
        public static bool CameraSeesPost(Thing camera, CompScpPost post, bool requirePower = true)
        {
            return post != null && post.parent.Spawned && CameraSees(camera, post.parent, requirePower);
        }
        public static bool HasCamera(CompScpPost post, bool requirePower = true)
        {
            return post != null && post.parent.Spawned && post.parent.Map.listerThings.ThingsOfDef(ScpInteractionDefOf.SCPV1_Camera)
                .Any(camera => CameraSeesPost(camera, post, requirePower));
        }
        public static bool Observed(Thing target)
        {
            if (!target.Spawned || !EligibleAtPost(target)) return false;
            return target.Map.listerThings.ThingsOfDef(ScpInteractionDefOf.SCPV1_MonitoringConsole)
                .Any(console => console.TryGetComp<CompMonitoringConsole>().Covers(target));
        }
        public static bool FitOperator(Pawn pawn)
        {
            return pawn.IsColonistPlayerControlled && !pawn.Drafted && !pawn.Dead && !pawn.Downed
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight) && !CompScpPost.NeedsBreak(pawn);
        }
    }

    public sealed class CompProperties_MonitoringConsole : CompProperties
    {
        public CompProperties_MonitoringConsole() { compClass = typeof(CompMonitoringConsole); }
    }
    public sealed partial class CompMonitoringConsole : ThingComp
    {
        private bool hadOperator;
        private bool guideShown;
        private readonly SensorAlarmPolicy staffingAlarm = new SensorAlarmPolicy();
        public int Capacity { get { return MonitoringDisplayPolicy.CapacityFor(parent); } }
        public int PoweredDisplays { get { return MonitoringDisplayPolicy.PoweredDisplaysFor(parent); } }
        public List<Thing> PostTargets
        {
            get
            {
                if (!parent.Spawned) return new List<Thing>();
                return CompScpPost.All(parent.Map)
                    .Where(post => post.BoundObject != null && post.BoundObject.Spawned
                        && post.BoundObject.Map == parent.Map && ScpSurveillance.EligibleAtPost(post.BoundObject))
                    .OrderBy(post => post.parent.thingIDNumber)
                    .Select(post => post.BoundObject).Distinct().ToList();
            }
        }
        public List<Thing> AutomaticTargets { get { return PostTargets.Take(Capacity).ToList(); } }
        public bool HasAssignments { get { return AutomaticTargets.Count > 0; } }
        public Pawn Operator
        {
            get
            {
                if (!parent.Spawned) return null;
                return parent.Map.mapPawns.FreeColonistsSpawned.ToList().FirstOrDefault(pawn => !pawn.Dead && !pawn.Downed
                    && pawn.Awake() && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight)
                    && !pawn.pather.Moving && pawn.Position == parent.InteractionCell && pawn.CurJob != null
                    && pawn.CurJob.def == ScpInteractionDefOf.SCP_Monitor && pawn.CurJob.targetA.Thing == parent);
            }
        }
        public string StatusFor(Thing target)
        {
            if (target == null || !target.Spawned || !parent.Spawned || target.Map != parent.Map) return "SCP_MonitorOffPost";
            if (!ScpSurveillance.Permitted(target)) return "SCP_MonitorProtocol";
            CompScp scp = target.TryGetComp<CompScp>();
            if (scp == null || !ScpOperationalPolicy.AllowsRemoteObservation(scp.OperationalFlags)) return "SCP_MonitorOffPost";
            if (!AutomaticTargets.Contains(target)) return "SCP_MonitorNoChannel";
            CompScpPost post = CompScpPost.ForObject(target);
            if (post == null || !ScpSurveillance.HasCamera(post))
                return post != null && ScpSurveillance.HasCamera(post, false) ? "SCP_CameraNoPower" : "SCP_MonitorNoCamera";
            if (!ScpSurveillance.Powered(parent)) return "SCP_MonitorNoPower";
            if (Operator == null) return "SCP_MonitorNoOperator";
            return "SCP_MonitorLive";
        }
        public bool Covers(Thing target) { return StatusFor(target) == "SCP_MonitorLive"; }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref useShifts, "scpConsoleUseShifts", false);
            Scribe_Collections.Look(ref shifts, "scpConsoleShifts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) { shifts = shifts ?? Defaults(); shifts.RemoveAll(s => s == null); }
            Scribe_Values.Look(ref hadOperator, "scpMonitorHadOperator", false);
            Scribe_Values.Look(ref guideShown, "scpMonitorGuideShown", false);
            Scribe_Values.Look(ref staffingAlarm.confirmed, "scpMonitorStaffConfirmed");
            Scribe_Values.Look(ref staffingAlarm.pending, "scpMonitorStaffPending");
            Scribe_Values.Look(ref staffingAlarm.pendingSince, "scpMonitorStaffSince", 0);
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned || parent.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Action { defaultLabel = "SCP_ConsoleShifts".Translate(), defaultDesc = "SCP_ConsoleRosterHelp".Translate(), icon = ScpIcons.Get(3),
                action = () => Find.WindowStack.Add(new Dialog_ConsoleShifts(this)) };
            yield return new Command_Action { defaultLabel = "SCP_MonitorGuideButton".Translate(), defaultDesc = "SCP_MonitorGuideButtonDesc".Translate(), icon = ScpIcons.Get(2),
                action = () => Find.WindowStack.Add(new Dialog_MessageBox("SCP_MonitorGuideBody".Translate(), title:"SCP_MonitorGuideTitle".Translate())) };
            yield return new Command_Action { defaultLabel = "SCP_SensorPanel".Translate(), defaultDesc = "SCP_SensorPanelHint".Translate(), icon = ScpIcons.Get(3),
                action = () => Find.WindowStack.Add(new Dialog_SensorTelemetry(parent)) };
            yield return new Command_Action { defaultLabel = "SCP_MonitorWork".Translate(), defaultDesc = "SCP_MonitorDutyHint".Translate(), icon = ScpIcons.Get(0), action = () =>
            {
                var options = parent.Map.mapPawns.FreeColonistsSpawned.Where(p => WorkGiver_ScpMonitor.CanWork(p, parent, true))
                    .Select(p => new FloatMenuOption(p.LabelShortCap, () => { Job job = WorkGiver_ScpMonitor.CreateJob(p,parent,true); if (job != null) p.jobs.TryTakeOrderedJob(job, JobTag.Misc); })).ToList();
                if (options.Count == 0) options.Add(new FloatMenuOption("SCP_MonitorUnavailable".Translate(), null));
                Find.WindowStack.Add(new FloatMenu(options));
            }};
        }
        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned) return null;
            List<Thing> targets = PostTargets;
            string check = "SCP_CheckMet".Translate();
            string missing = "SCP_CheckUnmet".Translate();
            string text = "SCP_MonitorSummary".Translate(Operator == null ? "SCP_ShiftNobody".Translate().ToString() : Operator.LabelShortCap.ToString(), System.Math.Min(targets.Count, Capacity), Capacity);
            text += "\n" + "SCP_MonitorDisplays".Translate(PoweredDisplays, MonitoringDisplayPolicy.ChannelsPerDisplay);
            text += "\n" + "SCP_MonitorOperatorFlag".Translate(Operator == null ? missing : check);
            text += "\n" + RosterSummary();
            foreach (Thing t in targets)
            {
                CompScpPost post = CompScpPost.ForObject(t);
                bool camera = post != null && ScpSurveillance.HasCamera(post);
                text += "\n" + "SCP_MonitorCameraFlag".Translate(camera ? check : missing, t.TryGetComp<CompScp>().PublicLabel)
                    + " — " + StatusFor(t).Translate();
            }
            if (targets.Count == 0) text += "\n" + "SCP_MonitorAutomaticEmpty".Translate();
            return text;
        }
        public override void CompTick()
        {
            if (!parent.Spawned || parent.Faction != Faction.OfPlayer || (Find.TickManager.TicksGame + parent.thingIDNumber) % 250 != 0) return;
            if (!guideShown && ScpSurveillance.Powered(parent))
            {
                guideShown = true;
                Find.LetterStack.ReceiveLetter("SCP_MonitorGuideTitle".Translate(), "SCP_MonitorGuideBody".Translate(), LetterDefOf.NeutralEvent, parent);
            }
            if (!HasAssignments) { hadOperator = false; staffingAlarm.Reset(); return; }
            bool present = Operator != null;
            if (present) hadOperator = true;
            if (!hadOperator && !useShifts) return;
            string state = !ScpSurveillance.Powered(parent) ? "SCP_MonitorNoPower" : !present ? "SCP_MonitorNoOperator"
                : useShifts && !Due(Operator,true) && Relief == null ? "SCP_ConsoleReliefMissing" : "ok";
            if (staffingAlarm.Advance(state, Find.TickManager.TicksGame))
                Find.LetterStack.ReceiveLetter((state == "ok" ? "SCP_MonitorRestored" : "SCP_MonitorInterrupted").Translate(),
                    (state == "ok" ? "SCP_MonitorRestored".Translate().ToString() : state.Translate().ToString() + "\n" + "SCP_MonitorDutyHint".Translate().ToString()),
                    state == "ok" ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent, parent);
        }
    }

    public sealed class WorkGiver_ScpMonitor : WorkGiver_Scanner
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) { return pawn.Map.listerThings.ThingsOfDef(ScpInteractionDefOf.SCPV1_MonitoringConsole); }
        public static bool CanWork(Pawn pawn, Thing console, bool forced = false)
        {
            return ScpSurveillance.Powered(console) && !console.IsForbidden(pawn) && ScpSurveillance.FitOperator(pawn)
                && console.TryGetComp<CompMonitoringConsole>().HasAssignments
                && pawn.CanReserve(console, 2, 0) && console.TryGetComp<CompMonitoringConsole>().EntryCell(pawn,forced).IsValid;
        }
        public static Job CreateJob(Pawn pawn, Thing console, bool forced = false)
        {
            if (!CanWork(pawn,console,forced)) return null;
            Job job = JobMaker.MakeJob(ScpInteractionDefOf.SCP_Monitor, console, console.TryGetComp<CompMonitoringConsole>().EntryCell(pawn,forced));
            job.playerForced = forced;
            return job;
        }
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) { return CanWork(pawn, t, forced); }
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) { return CreateJob(pawn,t,forced); }
    }
}
