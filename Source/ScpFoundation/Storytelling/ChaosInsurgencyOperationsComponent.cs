using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Runtime;
using Verse;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    public sealed class ChaosInsurgencyOperationsComponent : GameComponent
    {
        public const int OperationCooldownTicks = 1200000;
        private const string FactionDefName = "SCP_" + "ChaosInsurgency";
        private const string AssaultStrategyDefName = "ImmediateAttack";
        private int nextOperationTick = -1;

        public ChaosInsurgencyOperationsComponent(Game game) { }

        private Faction InsurgencyFaction
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
            Faction faction = InsurgencyFaction;
            if (faction != null) return faction;
            FactionDef definition = DefDatabase<FactionDef>.GetNamedSilentFail(FactionDefName);
            if (definition == null || Find.FactionManager == null) return null;
            FactionGenerator.CreateFactionAndAddToManager(definition);
            return InsurgencyFaction;
        }

        public bool CanStartOperation(Map map, bool ignoreCooldown)
        {
            if (map == null || !map.IsPlayerHome || !map.mapPawns.AnyFreeColonistSpawned)
                return false;
            if (!ignoreCooldown && nextOperationTick >= 0 && Find.TickManager.TicksGame < nextOperationTick)
                return false;
            return InsurgencyFaction != null
                && DefDatabase<RaidStrategyDef>.GetNamedSilentFail(AssaultStrategyDefName) != null;
        }

        public bool TryStartOperation(IncidentParms parms, bool ignoreCooldown)
        {
            Map map = parms == null ? null : parms.target as Map;
            if (!CanStartOperation(map, ignoreCooldown)) return false;

            ScpFactionRelationsComponent.EnsureHostileRelations();
            Faction faction = InsurgencyFaction;
            if (faction == null || faction.RelationKindWith(Faction.OfPlayer) != FactionRelationKind.Hostile)
                return false;
            HashSet<Pawn> previousPawns = new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
            parms.faction = faction;
            parms.raidStrategy = DefDatabase<RaidStrategyDef>.GetNamedSilentFail(AssaultStrategyDefName);
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            parms.points = Math.Max(500f, parms.points);
            if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(parms)) return false;

            // CE normally equips generated raiders; reconcile our custom gun ammo sets as well.
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                if (pawn.Faction == faction && !previousPawns.Contains(pawn))
                    CombatExtendedLoadoutBridge.NormalizePrimaryAmmo(pawn);
            nextOperationTick = Find.TickManager.TicksGame + OperationCooldownTicks;
            return true;
        }

        public bool DebugStartOperation(Map map)
        {
            if (EnsureFactionForDebug() == null || map == null) return false;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.forced = true;
            return TryStartOperation(parms, true);
        }

        public bool DebugSpawnFifteen(Map map, IntVec3 center)
        {
            if (map == null || !map.IsPlayerHome || !center.InBounds(map)) return false;
            Faction faction = EnsureFactionForDebug();
            PawnKindDef rifleman = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_ChaosInsurgent");
            PawnKindDef gunner = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_ChaosHeavy");
            ThingDef rifle = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_ChaosAKGrenadier");
            ThingDef machineGun = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_ChaosLightMachineGun");
            if (faction == null || rifleman == null || gunner == null || rifle == null || machineGun == null)
                return false;

            List<IntVec3> cells = GenRadial.RadialCellsAround(center, 9f, true)
                .Where(cell => cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map)
                    && !cell.GetThingList(map).Any(thing => thing is Pawn || thing.def.category == ThingCategory.Building))
                .Take(15).ToList();
            if (cells.Count != 15) return false;

            ScpFactionRelationsComponent.EnsureHostileRelations();
            ScpFactionRelationsComponent.EnsureHostile(faction, Faction.OfPlayer);

            var pawns = new List<Pawn>(15);
            for (int i = 0; i < 15; i++)
            {
                bool heavy = i >= 12;
                Pawn pawn = FoundationSupportComponent.GenerateEquippedPawn(
                    heavy ? gunner : rifleman, faction, heavy ? machineGun : rifle);
                GenSpawn.Spawn(pawn, cells[i], map);
                pawns.Add(pawn);
            }
            LordMaker.MakeNewLord(faction,
                new LordJob_AssaultColony(faction, false, true, false, false, false, false, false), map, pawns);
            return true;
        }

        // A saved legacy theft job may still reach its final toil after the update.
        // Drop the carried SCP before that job removes its pawn from the map.
        public void NotifyObjectStolen(Thing stolen, Pawn carrier)
        {
            if (carrier == null || !carrier.Spawned || carrier.carryTracker == null
                || carrier.carryTracker.CarriedThing != stolen) return;
            Thing dropped;
            carrier.carryTracker.TryDropCarriedThing(carrier.Position, ThingPlaceMode.Near, out dropped, null);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref nextOperationTick, "scpChaosNextOperationTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && nextOperationTick < -1)
                nextOperationTick = -1;
        }
    }
}
