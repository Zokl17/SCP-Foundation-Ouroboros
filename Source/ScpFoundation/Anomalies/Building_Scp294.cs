using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Definitions;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class Building_Scp294 : Building
    {
        private const int Price = 50;
        private const int RefillTicks = 60000;
        private int readyTick;
        internal ScpDef Anomaly { get { return DefDatabase<ScpDef>.GetNamedSilentFail("SCP_Anomaly294"); } }

        private ScpKnowledgeComponent Knowledge
        {
            get { return Current.Game == null ? null : Current.Game.GetComponent<ScpKnowledgeComponent>(); }
        }

        private KnowledgeLevel Level
        {
            get { return Knowledge == null ? KnowledgeLevel.Unknown : Knowledge.LevelFor(Anomaly); }
        }

        public override string GetInspectString()
        {
            string baseText = base.GetInspectString();
            string status;
            if (!Spawned || !CompScpPost.IsObjectAtPost(this)) status = "SCP_294NeedsPost".Translate();
            else if (Level < KnowledgeLevel.Observed) status = "SCP_294NeedsStudy".Translate();
            else if (Level < KnowledgeLevel.Documented) status = "SCP_294NeedsExperiments".Translate();
            else status = Ready ? "SCP_294Ready".Translate() : "SCP_294Refilling".Translate();
            return string.IsNullOrEmpty(baseText) ? status : baseText + "\n" + status;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            CompScp anomaly = GetComp<CompScp>();
            if (anomaly != null)
                foreach (Gizmo gizmo in anomaly.CompGetGizmosExtra()) yield return gizmo;
            if (!Spawned || Faction != Faction.OfPlayer) yield break;
            CompScp scp = GetComp<CompScp>();
            if (scp == null) yield break;
            yield return new Command_Action
            {
                defaultLabel = "SCP_294DossierButton".Translate(),
                defaultDesc = "SCP_294DossierHint".Translate(),
                action = () => MainTabWindow_ScpTerminal.Open(scp)
            };
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(pawn)) yield return option;
            if (!Spawned || pawn == null || pawn.Faction != Faction.OfPlayer || pawn.Dead
                || pawn.Downed || pawn.jobs == null || pawn.Map != Map) yield break;
            if (!pawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some)) yield break;
            if (!CompScpPost.IsObjectAtPost(this))
            {
                bool found = false;
                foreach (CompScpPost post in CompScpPost.All(Map))
                {
                    if (!CanMoveTo(pawn, post)) continue;
                    Thing destination = post.parent;
                    found = true;
                    yield return new FloatMenuOption("SCP_294MoveToPost".Translate(destination.Position),
                        () => StartMoveJob(pawn, destination));
                }
                if (!found) yield return new FloatMenuOption("SCP_294NoPost".Translate(), null);
                yield break;
            }
            if (!HasClearance(pawn))
            {
                yield return new FloatMenuOption("SCP_294NoClearance".Translate(), null);
                yield break;
            }
            if (Level < KnowledgeLevel.Observed)
            {
                yield return new FloatMenuOption("SCP_294StudyOrder".Translate(), () => StartJob(pawn, "SCP_294Study"));
                yield break;
            }
            if (Level < KnowledgeLevel.Documented)
            {
                yield return new FloatMenuOption("SCP_294ExperimentOrder".Translate(), () => StartJob(pawn, "SCP_294Experiment"));
                yield break;
            }
            if (Knowledge != null && !Knowledge.ExperimentsComplete(Anomaly))
                yield return new FloatMenuOption("SCP_RunExperiments".Translate(), () => StartJob(pawn, "SCP_294Experiment"));
            if (!Ready)
            {
                yield return new FloatMenuOption("SCP_294Refilling".Translate(), null);
                yield break;
            }
            if (!HasSilver(pawn))
            {
                yield return new FloatMenuOption("SCP_294NoSilver".Translate(Price), null);
                yield break;
            }
            for (int i = 1; i <= 5; i++)
            {
                int choice = i;
                yield return new FloatMenuOption(("SCP_294Choice" + choice).Translate(Price), () =>
                {
                    StartJob(pawn, "SCP_294Dispense", choice);
                });
            }
        }

        private void StartJob(Pawn pawn, string defName, int choice = 0)
        {
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail(defName);
            if (def == null) return;
            Job job = JobMaker.MakeJob(def, this);
            job.count = choice;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private void StartMoveJob(Pawn pawn, Thing destination)
        {
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("SCP_294MoveToPost");
            if (def == null || !CanMoveTo(pawn, destination.TryGetComp<CompScpPost>())) return;
            Job job = JobMaker.MakeJob(def, this, destination);
            job.count = 1;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        internal bool CanMoveTo(Pawn pawn, CompScpPost post)
        {
            return pawn != null && post != null && post.parent.def == ScpInteractionDefOf.SCP_ContainmentAnchor
                && post.parent.Spawned && post.parent.Map == Map
                && post.BoundObject == null && post.parent.Faction == Faction.OfPlayer
                && !post.parent.Position.GetThingList(Map).Any(thing => thing != pawn
                    && (thing is Pawn || thing.def.category == ThingCategory.Item))
                && !post.parent.Position.GetThingList(Map).Any(thing => thing != post.parent
                    && thing.def.category == ThingCategory.Building)
                && pawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some)
                && pawn.CanReserveAndReach(post.parent, PathEndMode.OnCell, Danger.Some);
        }

        internal string Dossier()
        {
            KnowledgeLevel level = Level;
            string text = level < KnowledgeLevel.Observed
                ? "SCP_294UnknownReport".Translate() : "SCP_294ObservedReport".Translate();
            if (level >= KnowledgeLevel.PartiallyDocumented)
                text += "\n\n" + "SCP_294PartialReport".Translate();
            if (level >= KnowledgeLevel.Documented)
                text += "\n\n" + "SCP_294DocumentedReport".Translate();
            if (level < KnowledgeLevel.Documented && Knowledge != null && Anomaly != null)
            {
                int target = level < KnowledgeLevel.Observed ? (int)Anomaly.research.observedWork
                    : (int)Anomaly.research.fullWork;
                text += "\n\n" + "SCP_294ResearchProgress".Translate((int)Knowledge.ResearchWork(Anomaly), target);
            }
            else if (level >= KnowledgeLevel.Documented && Knowledge != null && Anomaly != null)
                text += ScpArticleCatalog.ExperimentDossierText(Anomaly, Knowledge);
            return text;
        }

        internal bool CanResearch(Pawn pawn, JobDef jobDef)
        {
            if (!Spawned || pawn == null || pawn.Map != Map || !HasClearance(pawn)
                || Knowledge == null || Anomaly == null || !CompScpPost.IsObjectAtPost(this)) return false;
            KnowledgeLevel level = Level;
            return jobDef.defName == "SCP_294Study" && level < KnowledgeLevel.Observed
                || jobDef.defName == "SCP_294Experiment" && level >= KnowledgeLevel.Observed
                    && !Knowledge.ExperimentsComplete(Anomaly);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref readyTick, "scp294ReadyTick", 0);
        }

        internal void Dispense(Pawn pawn, int choice)
        {
            if (!Spawned || pawn == null || pawn.Map != Map || !HasClearance(pawn)
                || !CompScpPost.IsObjectAtPost(this) || Level < KnowledgeLevel.Documented
                || !Ready || choice < 1 || choice > 5) return;
            if (!HasSilver(pawn))
            {
                Messages.Message("SCP_294NoSilver".Translate(Price), this, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (choice == 2 && (pawn.needs == null || pawn.needs.mood == null)) return;
            if (choice >= 3 && pawn.health == null) return;

            ThoughtDef thought = choice == 2
                ? DefDatabase<ThoughtDef>.GetNamedSilentFail("SCP_294BestDrink") : null;
            string effectName = choice == 3 ? "SCP_294MedicalKnowledge"
                : choice == 4 ? "SCP_294EngineeringKnowledge"
                : choice == 5 ? "SCP_294WeaponKnowledge" : null;
            HediffDef effect = effectName == null ? null
                : DefDatabase<HediffDef>.GetNamedSilentFail(effectName);
            if (choice == 2 && thought == null || choice >= 3 && effect == null) return;

            Thing gold = null;
            if (choice == 1)
            {
                gold = ThingMaker.MakeThing(ThingDefOf.Gold);
                gold.stackCount = 5;
                if (!GenPlace.TryPlaceThing(gold, pawn.Position, Map, ThingPlaceMode.Near))
                {
                    gold.Destroy(DestroyMode.Vanish);
                    return;
                }
            }
            SpendSilver(pawn);
            if (choice == 2)
                pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
            else if (choice >= 3)
            {
                Hediff previous = pawn.health.hediffSet.GetFirstHediffOfDef(effect);
                if (previous != null) pawn.health.RemoveHediff(previous);
                pawn.health.AddHediff(effect);
            }
            readyTick = Find.TickManager.TicksGame + RefillTicks;
            ScpEventRecord dispensed = ScpEventJournal.Add("SCP-294", this,
                "SCP_Journal294Dispensed".Translate(pawn.LabelShortCap, ("SCP_294Choice" + choice).Translate(Price)));
            Messages.Message(ScpEventJournal.Notice(dispensed, "SCP_294Dispensed".Translate(pawn.LabelShortCap)), pawn,
                MessageTypeDefOf.PositiveEvent, false);
        }

        private bool Ready { get { return Find.TickManager.TicksGame >= readyTick; } }

        private static bool HasClearance(Pawn pawn)
        {
            return Current.Game != null && Current.Game.GetComponent<FoundationPersonnelComponent>()
                .LevelFor(pawn) >= 1;
        }

        private bool HasSilver(Pawn pawn)
        {
            int total = 0;
            foreach (Thing stack in Map.listerThings.ThingsOfDef(ThingDefOf.Silver))
            {
                if (!AvailableSilver(pawn, stack)) continue;
                total += stack.stackCount;
                if (total >= Price) return true;
            }
            return false;
        }

        private void SpendSilver(Pawn pawn)
        {
            int remaining = Price;
            foreach (Thing stack in new List<Thing>(Map.listerThings.ThingsOfDef(ThingDefOf.Silver)))
            {
                if (!AvailableSilver(pawn, stack)) continue;
                int count = Math.Min(remaining, stack.stackCount);
                stack.SplitOff(count).Destroy(DestroyMode.Vanish);
                remaining -= count;
                if (remaining == 0) break;
            }
        }

        private static bool AvailableSilver(Pawn pawn, Thing stack)
        {
            return stack.Spawned && !stack.IsForbidden(pawn)
                && pawn.CanReach(stack, PathEndMode.Touch, Danger.Some);
        }
    }
}
