using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using ScpFoundation.Anomalies;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_Scp914Custody : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            if (map == null || !map.IsPlayerHome || map.mapPawns.FreeColonistsSpawnedCount == 0
                || Current.Game == null || Find.QuestManager == null
                || !Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                || Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("914")
                || DefDatabase<ThingDef>.GetNamedSilentFail("SCP_914Transit") == null
                || DefDatabase<ThingDef>.GetNamedSilentFail("SCP_914Machine") == null
                || Find.Maps.Any(other => other.listerThings.ThingsOfDef(
                    DefDatabase<ThingDef>.GetNamed("SCP_914Machine")).Count > 0)) return false;
            FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
            return support.progress.unlocked && support.FoundationFaction != null
                && !support.FoundationFaction.HostileTo(Faction.OfPlayer);
        }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_Scp914Custody {
                map = QuestGen.slate.Get<Map>("map"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                installedSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp914Installed"),
                lostSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp914Lost")
            });
        }
    }

    public sealed class QuestPart_Scp914Custody : QuestPartActivable
    {
        public Map map;
        public string installedSignal;
        public string lostSignal;
        private Thing cargo;
        private bool delivered;
        private bool finished;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "scp914QuestMap");
            Scribe_References.Look(ref cargo, "scp914QuestCargo");
            Scribe_Values.Look(ref installedSignal, "scp914InstalledSignal");
            Scribe_Values.Look(ref lostSignal, "scp914LostSignal");
            Scribe_Values.Look(ref delivered, "scp914Delivered", false);
            Scribe_Values.Look(ref finished, "scp914QuestFinished", false);
        }

        public override void QuestPartTick()
        {
            if (State != QuestPartState.Enabled || finished || Find.TickManager.TicksGame % 250 != 0) return;
            if (map == null) { Finish(false); return; }
            ThingDef machine = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_914Machine");
            if (machine != null && map.listerThings.ThingsOfDef(machine).Any(thing => thing.Spawned
                && thing.Faction == Faction.OfPlayer)) { Finish(true); return; }
            if (delivered) { if (cargo == null || cargo.Destroyed) Finish(false); return; }
            ThingDef transit = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_914Transit");
            IntVec3 dropCell = DropCellFinder.TradeDropSpot(map);
            if (transit == null || !dropCell.IsValid || !dropCell.InBounds(map)) return;
            cargo = ThingMaker.MakeThing(transit);
            DropPodUtility.DropThingsNear(dropCell, map, new List<Thing> { cargo }, 110,
                false, false, false, false, true, Faction.OfPlayer);
            delivered = true;
            Find.LetterStack.ReceiveLetter("SCP_914CargoArrivedTitle".Translate(),
                "SCP_914CargoArrivedBody".Translate(), LetterDefOf.NeutralEvent,
                new TargetInfo(dropCell, map));
        }

        private void Finish(bool success)
        {
            finished = true;
            if (success)
            {
                Thing machine = map.listerThings.ThingsOfDef(
                    DefDatabase<ThingDef>.GetNamed("SCP_914Machine"))
                    .FirstOrDefault(thing => thing.Spawned && thing.Faction == Faction.OfPlayer);
                Find.LetterStack.ReceiveLetter("SCP_914InstalledTitle".Translate(),
                    "SCP_914InstalledBody".Translate(), LetterDefOf.PositiveEvent,
                    machine == null ? new TargetInfo(map.Center, map) : new TargetInfo(machine));
            }
            Find.SignalManager.SendSignal(new Signal(success ? installedSignal : lostSignal));
        }
    }
}
