using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    public sealed class Scp330VillageSite : Site
    {
        public override bool ShouldRemoveMapNow(out bool removeWorldObject)
        {
            if (HasMap && Find.CurrentMap == Map)
            {
                removeWorldObject = false;
                return false;
            }
            return base.ShouldRemoveMapNow(out removeWorldObject);
        }
    }

    public sealed class QuestNode_Scp330VillageSetup : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            return map != null && map.IsPlayerHome && map.mapPawns.FreeColonistsSpawnedCount >= 2
                && Current.Game != null && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("330")
                && Faction.OfAncients != null && Faction.OfAncientsHostile != null
                && !Faction.OfAncients.HostileTo(Faction.OfPlayer)
                && DefDatabase<ThingDef>.GetNamedSilentFail("Reliquary") != null
                && DefDatabase<SitePartDef>.GetNamedSilentFail("SCP_330Village") != null;
        }

        protected override void RunInt()
        {
            QuestGen.slate.Set("siteFaction", Faction.OfAncients);
            QuestGen.slate.Set("sitePartDefs", new List<SitePartDef> {
                DefDatabase<SitePartDef>.GetNamed("SCP_330Village")
            });
        }
    }

    public sealed class QuestNode_Scp330VillageClaimSite : QuestNode
    {
        protected override bool TestRunInt(Slate slate) { return true; }

        protected override void RunInt()
        {
            Site site = QuestGen.slate.Get<Site>("site");
            if (site != null && site.Faction != Faction.OfAncients)
                site.SetFaction(Faction.OfAncients);
        }
    }

    public sealed class QuestNode_Scp330VillageTrack : QuestNode
    {
        protected override bool TestRunInt(Slate slate) { return true; }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_Scp330Village {
                site = QuestGen.slate.Get<Site>("site"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                recoveredSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp330Recovered"),
                lostSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp330Lost")
            });
        }
    }

    public sealed class QuestPart_Scp330Village : QuestPartActivable
    {
        public Site site;
        public string recoveredSignal;
        public string lostSignal;
        private Thing relic;
        private Thing bowl;
        private int arrivedTick = -1;
        private int initialRelicHealth;
        private bool provoked;
        private bool finished;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref site, "scp330VillageSite");
            Scribe_References.Look(ref relic, "scp330VillageReliquary");
            Scribe_References.Look(ref bowl, "scp330VillageBowl");
            Scribe_Values.Look(ref recoveredSignal, "scp330RecoveredSignal");
            Scribe_Values.Look(ref lostSignal, "scp330LostSignal");
            Scribe_Values.Look(ref arrivedTick, "scp330VillageArrivedTick", -1);
            Scribe_Values.Look(ref initialRelicHealth, "scp330VillageRelicHealth", 0);
            Scribe_Values.Look(ref provoked, "scp330VillageProvoked", false);
            Scribe_Values.Look(ref finished, "scp330VillageFinished", false);
        }

        public override void QuestPartTick()
        {
            if (State != QuestPartState.Enabled || finished || Find.TickManager.TicksGame % 60 != 0) return;
            Map map = site == null ? null : site.Map;
            if (map != null && arrivedTick < 0)
            {
                arrivedTick = Find.TickManager.TicksGame;
                ThingDef relicDef = DefDatabase<ThingDef>.GetNamed("Reliquary");
                relic = map.listerThings.ThingsOfDef(relicDef).FirstOrDefault();
                CompThingContainer container = relic == null ? null : relic.TryGetComp<CompThingContainer>();
                bowl = container == null ? null : container.GetDirectlyHeldThings()
                    .FirstOrDefault(thing => thing.def.defName == "SCP_330Bowl");
                initialRelicHealth = relic == null ? 0 : relic.HitPoints;
                if (bowl == null) { Finish(false); return; }
            }
            if (bowl == null || bowl.Destroyed) { if (arrivedTick >= 0) Finish(false); return; }
            if (bowl.MapHeld != null && bowl.MapHeld.IsPlayerHome) { Finish(true); return; }
            if (map == null || provoked || arrivedTick < 0) return;
            CompThingContainer holder = relic == null || relic.Destroyed ? null : relic.TryGetComp<CompThingContainer>();
            bool disturbed = holder == null || !holder.GetDirectlyHeldThings().Contains(bowl)
                || relic.HitPoints < initialRelicHealth;
            if (disturbed || Find.TickManager.TicksGame - arrivedTick >= 60000)
                Provoke(map);
        }

        private void Provoke(Map map)
        {
            provoked = true;
            List<Pawn> villagers = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn.Faction == Faction.OfAncients && !pawn.Dead).ToList();
            foreach (Pawn pawn in villagers)
            {
                Lord previous = LordUtility.GetLord(pawn);
                if (previous != null) previous.RemovePawn(pawn);
                pawn.SetFaction(Faction.OfAncientsHostile);
            }
            if (villagers.Count > 0)
                LordMaker.MakeNewLord(Faction.OfAncientsHostile,
                    new LordJob_DefendBase(Faction.OfAncientsHostile,
                        relic == null ? map.Center : relic.Position, 45, false), map, villagers);
            Find.LetterStack.ReceiveLetter("SCP_330VillagersHostileTitle".Translate(),
                "SCP_330VillagersHostileBody".Translate(), LetterDefOf.ThreatBig, site);
        }

        private void Finish(bool success)
        {
            finished = true;
            Find.SignalManager.SendSignal(new Signal(success ? recoveredSignal : lostSignal));
        }
    }

    public sealed class CompProperties_Scp330Reliquary : CompProperties
    {
        public CompProperties_Scp330Reliquary() { compClass = typeof(CompScp330Reliquary); }
    }

    public sealed class CompScp330Reliquary : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned || parent.Map.Parent == null
                || parent.Map.Parent.def.defName != "SCP_330VillageSite") yield break;
            CompThingContainer container = parent.TryGetComp<CompThingContainer>();
            if (container == null || !container.GetDirectlyHeldThings()
                .Any(thing => thing.def.defName == "SCP_330Bowl")) yield break;
            yield return new Command_Action {
                defaultLabel = "SCP_330RetrieveButton".Translate(),
                defaultDesc = "SCP_330RetrieveDesc".Translate(),
                action = ChooseRetriever
            };
        }

        private void ChooseRetriever()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (Pawn pawn in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                Pawn worker = pawn;
                if (worker.Drafted || !worker.CanReserveAndReach(parent, PathEndMode.InteractionCell, Danger.Some))
                    continue;
                options.Add(new FloatMenuOption(worker.LabelShortCap, () => {
                    Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("SCP_330Retrieve"), parent);
                    job.playerForced = true;
                    worker.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }));
            }
            if (options.Count == 0)
                options.Add(new FloatMenuOption("SCP_330NoRetriever".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    public sealed class JobDriver_Scp330Retrieve : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_General.Wait(300, TargetIndex.A);
            yield return Toils_General.Do(() => {
                Thing reliquary = job.targetA.Thing;
                CompThingContainer container = reliquary == null ? null : reliquary.TryGetComp<CompThingContainer>();
                if (container == null || !reliquary.Spawned) return;
                container.GetDirectlyHeldThings().TryDropAll(reliquary.InteractionCell,
                    reliquary.Map, ThingPlaceMode.Near);
            });
        }
    }
}
