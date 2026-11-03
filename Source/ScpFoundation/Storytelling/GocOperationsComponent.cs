using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    public sealed class GocOperationsComponent : GameComponent
    {
        private enum OperationStage { None, Warning, Active }

        public const int WarningTicks = 120000;
        public const int OperationCooldownTicks = 1800000;
        private const string FactionDefName = "SCP_" + "GlobalOccultCoalition";
        private const string OperativeDefName = "SCP_" + "GocOperative";
        private const string HkOperativeDefName = "SCP_" + "GocHkOperative";
        private const string HeavyDefName = "SCP_" + "GocHeavyOperative";
        private OperationStage stage;
        private Thing target;
        private int arrivalTick = -1;
        private int nextOperationTick = -1;
        private List<Pawn> operationPawns = new List<Pawn>();

        public GocOperationsComponent(Game game) { }

        public Faction CoalitionFaction
        {
            get
            {
                FactionDef definition = DefDatabase<FactionDef>.GetNamedSilentFail(FactionDefName);
                return definition == null || Find.FactionManager == null
                    ? null
                    : Find.FactionManager.FirstFactionOfDef(definition);
            }
        }

        private Faction EnsureFactionForDebug()
        {
            Faction faction = CoalitionFaction;
            if (faction != null) return faction;
            FactionDef definition = DefDatabase<FactionDef>.GetNamedSilentFail(FactionDefName);
            if (definition == null || Find.FactionManager == null) return null;
            FactionGenerator.CreateFactionAndAddToManager(definition);
            return CoalitionFaction;
        }

        public bool HasPendingOperation { get { return stage != OperationStage.None; } }

        public bool CanScheduleOperation(Map map, bool ignoreCooldown)
        {
            if (map == null || !map.IsPlayerHome || !map.mapPawns.AnyFreeColonistSpawned || HasPendingOperation)
                return false;
            if (!ignoreCooldown && nextOperationTick >= 0 && Find.TickManager.TicksGame < nextOperationTick)
                return false;
            Faction faction = CoalitionFaction;
            if (faction == null || (!ignoreCooldown && faction.RelationKindWith(Faction.OfPlayer) == FactionRelationKind.Ally))
                return false;
            return ScpOperationTargeting.ContainedTargets(map).Any();
        }

        public bool TryScheduleOperation(Map map, bool ignoreCooldown)
        {
            if (!CanScheduleOperation(map, ignoreCooldown)) return false;
            List<CompScp> candidates = ScpOperationTargeting.ContainedTargets(map).ToList();
            if (candidates.Count == 0) return false;
            target = candidates.RandomElement().parent;
            stage = OperationStage.Warning;
            arrivalTick = Find.TickManager.TicksGame + WarningTicks;
            operationPawns.Clear();
            CompScp anomaly = target.TryGetComp<CompScp>();
            string label = anomaly == null ? target.LabelCap.ToString() : anomaly.PublicLabel;
            Find.LetterStack.ReceiveLetter("SCP_GocUltimatumTitle".Translate(),
                "SCP_GocUltimatumBody".Translate(label, GenDate.ToStringTicksToPeriod(WarningTicks, false)),
                LetterDefOf.ThreatSmall, target);
            return true;
        }

        public override void GameComponentTick()
        {
            if (stage == OperationStage.None || Find.TickManager.TicksGame % 250 != 0) return;
            if (target != null && target.Destroyed)
            {
                if (stage == OperationStage.Warning) CompleteByCompliance();
                else CompleteByElimination();
                return;
            }
            if (target == null || !target.Spawned || target.Map == null || !target.Map.IsPlayerHome)
            {
                AbortOperation("SCP_GocOperationTargetLostBody");
                return;
            }
            if (stage == OperationStage.Warning && Find.TickManager.TicksGame >= arrivalTick)
            {
                if (!SpawnEliminationTeam(target.Map))
                    AbortOperation("SCP_GocOperationDeploymentFailedBody");
                return;
            }
            if (stage == OperationStage.Active)
            {
                operationPawns.RemoveAll(pawn => pawn == null || pawn.Destroyed || pawn.Dead);
                if (operationPawns.Count == 0) AbortOperation("SCP_GocOperationDefeatedBody");
            }
        }

        private bool SpawnEliminationTeam(Map map)
        {
            Faction faction = CoalitionFaction;
            PawnKindDef operative = DefDatabase<PawnKindDef>.GetNamedSilentFail(OperativeDefName);
            PawnKindDef hkOperative = DefDatabase<PawnKindDef>.GetNamedSilentFail(HkOperativeDefName);
            PawnKindDef heavy = DefDatabase<PawnKindDef>.GetNamedSilentFail(HeavyDefName);
            ThingDef assaultRifle = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle");
            ThingDef hk416 = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_" + "Nu7HKCarbine");
            ThingDef m249 = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_" + "Nu7LightMachineGun");
            IntVec3 entry;
            if (faction == null || operative == null || hkOperative == null || heavy == null || assaultRifle == null
                || hk416 == null || m249 == null
                || !RCellFinder.TryFindRandomPawnEntryCell(out entry, map, 0f, false, cell => cell.Standable(map)))
                return false;

            operationPawns.Clear();
            for (int i = 0; i < 3; i++)
                operationPawns.Add(FoundationSupportComponent.GenerateEquippedPawn(operative, faction, assaultRifle));
            operationPawns.Add(FoundationSupportComponent.GenerateEquippedPawn(hkOperative, faction, hk416));
            operationPawns.Add(FoundationSupportComponent.GenerateEquippedPawn(heavy, faction, m249));
            operationPawns.Add(FoundationSupportComponent.GenerateEquippedPawn(heavy, faction, m249));
            foreach (Pawn pawn in operationPawns)
                GenSpawn.Spawn(pawn, CellFinder.RandomSpawnCellForPawnNear(entry, map, 6), map, WipeMode.Vanish);
            LordMaker.MakeNewLord(faction, new LordJob_AssaultThings(faction, new List<Thing> { target }, 1f, true), map, operationPawns);
            stage = OperationStage.Active;
            CompScp anomaly = target.TryGetComp<CompScp>();
            Find.LetterStack.ReceiveLetter("SCP_GocOperationArrivedTitle".Translate(),
                "SCP_GocOperationArrivedBody".Translate(anomaly != null ? anomaly.PublicLabel : target.def.LabelCap.ToString()), LetterDefOf.ThreatSmall, target);
            return true;
        }

        private void CompleteByCompliance()
        {
            Find.LetterStack.ReceiveLetter("SCP_GocOperationCompliedTitle".Translate(),
                "SCP_GocOperationCompliedBody".Translate(), LetterDefOf.NeutralEvent);
            ResetOperation();
        }

        private void CompleteByElimination()
        {
            Find.LetterStack.ReceiveLetter("SCP_GocOperationCompletedTitle".Translate(),
                "SCP_GocOperationCompletedBody".Translate(), LetterDefOf.NegativeEvent);
            ResetOperation();
        }

        private void AbortOperation(string bodyKey)
        {
            Find.LetterStack.ReceiveLetter("SCP_GocOperationAbortedTitle".Translate(), bodyKey.Translate(), LetterDefOf.NeutralEvent);
            ResetOperation();
        }

        private void ResetOperation()
        {
            stage = OperationStage.None;
            target = null;
            arrivalTick = -1;
            nextOperationTick = Find.TickManager.TicksGame + OperationCooldownTicks;
            operationPawns.Clear();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref stage, "scpGocOperationStage", OperationStage.None);
            Scribe_References.Look(ref target, "scpGocOperationTarget");
            Scribe_Values.Look(ref arrivalTick, "scpGocOperationArrivalTick", -1);
            Scribe_Values.Look(ref nextOperationTick, "scpGocNextOperationTick", -1);
            Scribe_Collections.Look(ref operationPawns, "scpGocOperationPawns", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (!System.Enum.IsDefined(typeof(OperationStage), stage)) stage = OperationStage.None;
                if (arrivalTick < -1) arrivalTick = -1;
                if (nextOperationTick < -1) nextOperationTick = -1;
                operationPawns = (operationPawns ?? new List<Pawn>()).Where(pawn => pawn != null && !pawn.Destroyed).ToList();
                if (stage == OperationStage.None) { target = null; operationPawns.Clear(); }
            }
        }

        public bool DebugSpawnStrikeTeam(Map map, IntVec3 entry)
        {
            Faction faction = EnsureFactionForDebug();
            PawnKindDef operative = DefDatabase<PawnKindDef>.GetNamedSilentFail(OperativeDefName);
            PawnKindDef hkOperative = DefDatabase<PawnKindDef>.GetNamedSilentFail(HkOperativeDefName);
            PawnKindDef heavy = DefDatabase<PawnKindDef>.GetNamedSilentFail(HeavyDefName);
            ThingDef assaultRifle = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle");
            ThingDef hk416 = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_" + "Nu7HKCarbine");
            ThingDef m249 = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_" + "Nu7LightMachineGun");
            if (map == null || faction == null || operative == null || hkOperative == null || heavy == null || assaultRifle == null
                || hk416 == null || m249 == null || !entry.InBounds(map))
                return false;

            var pawns = new List<Pawn>();
            for (int i = 0; i < 3; i++)
                pawns.Add(FoundationSupportComponent.GenerateEquippedPawn(operative, faction, assaultRifle));
            pawns.Add(FoundationSupportComponent.GenerateEquippedPawn(hkOperative, faction, hk416));
            pawns.Add(FoundationSupportComponent.GenerateEquippedPawn(heavy, faction, m249));
            pawns.Add(FoundationSupportComponent.GenerateEquippedPawn(heavy, faction, m249));
            FoundationSupportComponent.SpawnSupportTeam(map, entry, faction, pawns, 60000);
            return true;
        }

        public bool DebugScheduleOperation(Map map)
        {
            return EnsureFactionForDebug() != null && TryScheduleOperation(map, true);
        }
    }
}
