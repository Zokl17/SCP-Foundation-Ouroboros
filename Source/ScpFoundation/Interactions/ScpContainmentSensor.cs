using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Containment;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using UnityEngine;
using Verse;

namespace ScpFoundation.Interactions
{
    public sealed class CompProperties_ContainmentSensor : CompProperties
    {
        public CompProperties_ContainmentSensor() { compClass = typeof(CompContainmentSensor); }
    }

    public sealed class CompContainmentSensor : ThingComp
    {
        private Thing post, legacyConsole, subject;
        private bool armed;
        private readonly SensorAlarmPolicy alarm = new SensorAlarmPolicy();
        public static IEnumerable<CompContainmentSensor> All(Map map)
        {
            return map.listerThings.ThingsOfDef(ScpInteractionDefOf.SCPV1_Sensor)
                .Where(t => t.Faction == Faction.OfPlayer).Select(t => t.TryGetComp<CompContainmentSensor>()).Where(c => c != null);
        }
        public static string ConsoleReport(Thing terminal)
        {
            var sensors = terminal.Spawned ? All(terminal.Map).OrderBy(c => c.parent.thingIDNumber).ToList()
                : new List<CompContainmentSensor>();
            return sensors.Count == 0 ? "SCP_SensorNoLinks".Translate().ToString()
                : string.Join("\n\n", sensors.Select(c => c.Report()).ToArray());
        }
        private bool Available(Thing thing)
        {
            return parent.Spawned && thing != null && thing.Spawned && thing.Map == parent.Map && thing.Faction == Faction.OfPlayer;
        }
        private Thing RoomPost(out string error)
        {
            error = null;
            if (!parent.Spawned) { error = "SCP_SensorNoRoomPost"; return null; }
            Room room = parent.Position.GetRoom(parent.Map);
            if (room == null || room.TouchesMapEdge || room.IsDoorway || room.OpenRoofCount != 0)
            { error = "SCP_SensorWrongRoom"; return null; }
            bool ambiguous;
            CompScpPost candidate = SensorRoomPolicy.Unique(CompScpPost.All(parent.Map), c => c.parent.Position.GetRoom(parent.Map) == room, out ambiguous);
            if (candidate == null)
            { error = ambiguous ? "SCP_SensorMultiplePosts" : "SCP_SensorNoRoomPost"; return null; }
            return candidate.parent;
        }
        private bool IsReporter()
        {
            Room room = parent.Position.GetRoom(parent.Map);
            return All(parent.Map).Where(c => c.parent.Position.GetRoom(parent.Map) == room)
                .OrderByDescending(c => ScpSurveillance.Powered(c.parent)).ThenBy(c => c.parent.thingIDNumber).FirstOrDefault() == this;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref post, "scpSensorPost");
            Scribe_References.Look(ref legacyConsole, "scpSensorConsole");
            Scribe_References.Look(ref subject, "scpSensorSubject");
            Scribe_Values.Look(ref armed, "scpSensorArmed", false);
            Scribe_Values.Look(ref alarm.confirmed, "scpSensorConfirmed");
            Scribe_Values.Look(ref alarm.pending, "scpSensorPending");
            Scribe_Values.Look(ref alarm.pendingSince, "scpSensorPendingSince", 0);
        }
        private string Sample(out string details)
        {
            details = "";
            string bindingError;
            Thing roomPost = RoomPost(out bindingError);
            if (roomPost == null) return bindingError;
            if (!ScpSurveillance.Powered(parent)) return "SCP_SensorNoPower";
            Thing target = roomPost.TryGetComp<CompScpPost>().BoundObject;
            CompScp scp = target == null ? null : target.TryGetComp<CompScp>();
            if (scp == null || !target.Spawned || target.Map != parent.Map
                || !ScpOperationalPolicy.IsAtPost(scp.OperationalFlags)) return "SCP_SensorNoObject";
            Room room = parent.Position.GetRoom(parent.Map);
            if (room == null || room.TouchesMapEdge || room.IsDoorway || room.OpenRoofCount != 0
                || target.Position.GetRoom(parent.Map) != room) return "SCP_SensorWrongRoom";
            KnowledgeLevel level = Current.Game.GetComponent<ScpKnowledgeComponent>().LevelFor(scp.Definition);
            var context = new ContainmentContext(scp.parent);
            var lines = new List<string>();
            var failures = new List<string>();
            foreach (ContainmentRequirement requirement in scp.Definition.requirements)
            {
                // Clearance zero already exposes the containment instructions in CompScp.
                if (scp.Clearance != 0 && level < requirement.visibleFrom) continue;
                string value;
                if (requirement is RequirementTemperature) value = target.AmbientTemperature.ToString("F1") + " °C";
                else if (requirement is RequirementLight) value = parent.Map.glowGrid.GroundGlowAt(target.Position).ToString("P0");
                else if (requirement is RequirementPower)
                {
                    var power = target.TryGetComp<CompPowerTrader>();
                    value = (power != null && power.PowerOn ? "SCP_Yes" : "SCP_No").Translate().ToString();
                }
                else continue;
                bool met = requirement.IsSatisfied(context);
                if (!met) failures.Add(requirement.id);
                lines.Add("SCP_SensorReading".Translate((met ? "SCP_CheckMet" : "SCP_CheckUnmet").Translate(), requirement.Description(), value));
            }
            details = scp.PublicLabel + "\n"
                + (lines.Count == 0 ? "SCP_SensorNoParameters".Translate().ToString() : string.Join("\n", lines.ToArray()));
            return failures.Count == 0 ? "ok" : "bad:" + string.Join(",", failures.OrderBy(s => s).ToArray());
        }
        public string Report()
        {
            string readings;
            string state = Sample(out readings);
            return "SCP_SensorAutoHeading".Translate() + "\n"
                + (state == "ok" || state.StartsWith("bad:") ? readings : state.Translate().ToString());
        }
        public override void CompTickRare()
        {
            if (!parent.Spawned || parent.Faction != Faction.OfPlayer) return;
            string bindingError;
            Thing nextPost = RoomPost(out bindingError);
            if (nextPost != null)
            {
                if (post != nextPost) { post = nextPost; subject = null; alarm.Reset(); }
                armed = true;
            }
            if (!armed || !IsReporter()) return;
            Thing next = Available(post) ? post.TryGetComp<CompScpPost>().BoundObject : null;
            if (next != subject) { subject = next; alarm.Reset(); }
            string readings;
            string state = Sample(out readings);
            if (alarm.Advance(state, Find.TickManager.TicksGame))
                Find.LetterStack.ReceiveLetter((state == "ok" ? "SCP_SensorRestored" : "SCP_SensorWarning").Translate(),
                    Report(), state == "ok" ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent, parent);
        }
        public override string CompInspectStringExtra() { return parent.Spawned ? Report() : null; }
    }

    public sealed class Dialog_SensorTelemetry : Window
    {
        private readonly Thing console;
        private Vector2 scroll;
        private float nextRefresh;
        private string report = "";
        public override Vector2 InitialSize { get { return new Vector2(760, 620); } }
        public Dialog_SensorTelemetry(Thing console)
        {
            this.console = console;
            doCloseX = true; doCloseButton = true;
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!console.Spawned) { Close(); return; }
            if (Time.realtimeSinceStartup >= nextRefresh) { report = CompContainmentSensor.ConsoleReport(console); nextRefresh = Time.realtimeSinceStartup + 1f; }
            Widgets.Label(new Rect(0, 0, rect.width - 40, 55), "SCP_SensorPanelHint".Translate());
            Widgets.LabelScrollable(new Rect(0, 60, rect.width, rect.height - 110), report, ref scroll);
        }
    }
}
