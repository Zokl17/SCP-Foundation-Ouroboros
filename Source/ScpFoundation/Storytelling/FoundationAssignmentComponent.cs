using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Containment;
using ScpFoundation.Interactions;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class FoundationAssignmentComponent : GameComponent
    {
        public readonly FoundationAssignmentProgress progress = new FoundationAssignmentProgress();
        private Thing subject;
        public FoundationAssignmentComponent(Game game) { }
        public bool ContactEstablished
        {
            get { return Current.Game.GetComponent<FoundationContactComponent>().progress.stage == FoundationContactStage.Established; }
        }
        public string SubjectLabel
        {
            get { return subject == null || subject.Destroyed ? "SCP_AssignmentMissing".Translate().ToString()
                : subject.TryGetComp<CompScp>().PublicLabel; }
        }
        private static bool Eligible(Thing thing)
        {
            if (thing == null || !thing.Spawned || !Find.Maps.Contains(thing.Map) || !thing.Map.IsPlayerHome) return false;
            CompScp scp = thing.TryGetComp<CompScp>();
            return scp != null && scp.Definition != null && scp.Definition.source != null
                && !scp.Definition.source.originalFixture && scp.Definition.scpId == "SCP-650" && scp.IsInCustody
                && ScpOperationalPolicy.IsAtPost(scp.OperationalFlags);
        }
        public static IEnumerable<Thing> Candidates(Map map)
        {
            if (map == null || !Find.Maps.Contains(map)) return Enumerable.Empty<Thing>();
            return map.GetComponent<ScpStoryStateComponent>().Anomalies.Select(comp => comp.parent)
                .Where(thing => thing.Map == map && Eligible(thing)).OrderBy(thing => thing.thingIDNumber).Cast<Thing>();
        }
        public bool Accept(Map map)
        {
            return progress.Accept(ContactEstablished, CompFoundationAccessTerminal.Online(map) != null);
        }
        private void FindSubject()
        {
            if (Eligible(subject)) return;
            Thing candidate = Find.Maps.SelectMany(map => Candidates(map)).OrderBy(thing => thing.thingIDNumber).FirstOrDefault();
            if (candidate == null || candidate == subject) return;
            subject = candidate;
            progress.lastTick = -1;
            progress.lastQualified = false;
        }
        public bool Qualified
        {
            get
            {
                if (!Eligible(subject)) return false;
                CompScp scp = subject.TryGetComp<CompScp>();
                return ScpOperationalPolicy.AllowsResearch(scp.OperationalFlags) && scp.CanResearchNow;
            }
        }
        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 250 != 0 || progress.stage != FoundationAssignmentStage.Active) return;
            FindSubject();
            if (progress.Advance(now, Qualified))
                Find.LetterStack.ReceiveLetter("SCP_AssignmentReadyTitle".Translate(), "SCP_AssignmentReadyBody".Translate(),
                    LetterDefOf.PositiveEvent, subject);
        }
        public bool Submit(Map map)
        {
            Thing terminal = CompFoundationAccessTerminal.Online(map);
            if (!ContactEstablished || terminal == null || progress.stage != FoundationAssignmentStage.Ready) return false;
            List<IntVec3> cells = GenRadial.RadialCellsAround(terminal.Position, 6f, true)
                .Where(cell => cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map)
                    && cell.GetThingList(map).Count == 0).Take(3).ToList();
            if (cells.Count < 3)
            {
                Messages.Message("SCP_AssignmentNoSpace".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            string[] defs = { "MedicineIndustrial", "ComponentIndustrial", "ComponentSpacer" };
            int[] counts = { 10, 10, 5 };
            for (int i = 0; i < defs.Length; i++)
            {
                Thing reward = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(defs[i]));
                reward.stackCount = counts[i];
                GenSpawn.Spawn(reward, cells[i], map);
            }
            progress.Complete(true, true);
            Current.Game.GetComponent<FoundationSupportComponent>().progress.Unlock();
            Messages.Message("SCP_AssignmentDelivered".Translate(), MessageTypeDefOf.PositiveEvent, false);
            return true;
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref progress.stage, "scpFoundationAssignmentStage", FoundationAssignmentStage.Available);
            Scribe_Values.Look(ref progress.stableTicks, "scpFoundationAssignmentTicks", 0);
            Scribe_Values.Look(ref progress.lastTick, "scpFoundationAssignmentLastTick", -1);
            Scribe_Values.Look(ref progress.lastQualified, "scpFoundationAssignmentQualified", false);
            Scribe_References.Look(ref subject, "scpFoundationAssignmentSubject");
        }
    }
}
