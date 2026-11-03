using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using Verse;
using ScpFoundation.Anomalies;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_Scp106Recovery : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            PawnKindDef entity = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_106Kind");
            return map != null && map.IsPlayerHome && entity != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && map.mapPawns.FreeColonistsSpawnedCount >= 1
                && Current.Game.GetComponent<ScpArrivalRegistry>().CanArrive106(true)
                && !QuestPart_Scp106Recovery.ActiveFor(map);
        }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_Scp106Recovery {
                map = QuestGen.slate.Get<Map>("map"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                capturedSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp106Captured"),
                escapedSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp106Escaped")
            });
        }
    }

    public sealed class QuestPart_Scp106Recovery : QuestPartActivable
    {
        public Map map;
        public string capturedSignal;
        public string escapedSignal;
        private bool entityReleased;
        private bool completed;
        private int missingSinceTick = -1;

        public static bool ActiveFor(Map target)
        {
            return target != null && Find.QuestManager != null
                && Find.QuestManager.ActiveQuestsListForReading.Any(quest =>
                    quest.PartsListForReading.OfType<QuestPart_Scp106Recovery>()
                        .Any(part => part.map == target && part.State == QuestPartState.Enabled));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "scp106RecoveryMap");
            Scribe_Values.Look(ref capturedSignal, "scp106CapturedSignal");
            Scribe_Values.Look(ref escapedSignal, "scp106EscapedSignal");
            Scribe_Values.Look(ref entityReleased, "scp106EntityReleased", false);
            Scribe_Values.Look(ref completed, "scp106QuestCompleted", false);
            Scribe_Values.Look(ref missingSinceTick, "scp106MissingSinceTick", -1);
        }

        public override void QuestPartTick()
        {
            if (completed || map == null || Find.TickManager.TicksGame % 250 != 0) return;
            ThingDef chamberDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_106BaitStation");
            PawnKindDef entityKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_106Kind");
            ThingDef legacyDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_106Entity");
            if (chamberDef == null || entityKind == null) return;

            var chambers = map.listerThings.ThingsOfDef(chamberDef)
                .Select(thing => thing.TryGetComp<CompLarryProtocol>())
                .Where(comp => comp != null).ToList();
            if (chambers.Any(comp => comp.QuestCaptureTick > 0 && comp.QuestCaptureTick >= EnableTick))
            {
                completed = true;
                Find.SignalManager.SendSignal(new Signal(capturedSignal));
                return;
            }

            if (entityReleased)
            {
                if (map.mapPawns.AllPawnsSpawned.OfType<Pawn_Scp106>().Any(pawn => !pawn.Dead)
                    || legacyDef != null && map.listerThings.ThingsOfDef(legacyDef).Count > 0
                    || chambers.Any(comp => comp.Contains106))
                    missingSinceTick = -1;
                else if (missingSinceTick < 0) missingSinceTick = Find.TickManager.TicksGame;
                else if (Find.TickManager.TicksGame - missingSinceTick >= 2500)
                {
                    completed = true;
                    if (!escapedSignal.NullOrEmpty()) Find.SignalManager.SendSignal(new Signal(escapedSignal));
                }
                return;
            }
            if (!chambers.Any(comp => comp.QuestLureReady)) return;
            if (map.mapPawns.AllPawnsSpawned.OfType<Pawn_Scp106>().Any(pawn => !pawn.Dead)
                || legacyDef != null && map.listerThings.ThingsOfDef(legacyDef).Count > 0)
            {
                entityReleased = true;
                return;
            }

            IntVec3 cell;
            if (!CellFinder.TryFindRandomEdgeCellWith(
                candidate => ScpSpawnCells.FootprintClear(candidate, map, entityKind.race), map, 0f, out cell)) return;
            Pawn_Scp106 entity = Pawn_Scp106.Generate();
            if (entity == null) return;
            GenSpawn.Spawn(entity, cell, map);
            Current.Game.GetComponent<ScpArrivalRegistry>().Record106Route(true);
            entityReleased = true;
            Find.LetterStack.ReceiveLetter("SCP_106RecoveryArrivalTitle".Translate(),
                "SCP_106RecoveryArrivalBody".Translate(), LetterDefOf.ThreatBig, new LookTargets(cell, map));
        }
    }
}
