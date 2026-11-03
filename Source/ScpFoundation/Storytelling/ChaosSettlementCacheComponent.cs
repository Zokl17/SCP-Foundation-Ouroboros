using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ScpFoundation.Storytelling
{
    // Guarded anomalies in each generated Chaos settlement.
    public sealed class ChaosSettlementCacheComponent : MapComponent
    {
        private bool cachePlaced;
        private bool gunPlaced;
        private int pillStacksPlaced;

        public ChaosSettlementCacheComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if ((cachePlaced && gunPlaced) || Find.TickManager.TicksGame % 250 != 0) return;
            Settlement settlement = map.Parent as Settlement;
            Faction faction = settlement == null ? null : settlement.Faction;
            if (faction == null || faction.def == null || faction.def.defName != "SCP_ChaosInsurgency") return;
            ScpArrivalRegistry registry = Current.Game.GetComponent<ScpArrivalRegistry>();
            if (!registry.FirstEncounterComplete) return;
            if (registry.HasArrived("127")) return;

            ThingDef pillDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_500Pill");
            ThingDef gunDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_127Gun");
            if (pillDef == null || gunDef == null) return;
            Pawn keeper = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn.Faction == faction && pawn.inventory != null
                    && pawn.inventory.innerContainer != null && pawn.RaceProps.Humanlike)
                .OrderByDescending(pawn => pawn.kindDef != null && pawn.kindDef.defName == "SCP_ChaosCellCommander")
                .ThenByDescending(pawn => pawn.kindDef == null ? 0f : pawn.kindDef.combatPower)
                .FirstOrDefault();
            if (keeper == null) return;

            if (pillStacksPlaced < 2)
            {
                Thing pill = ThingMaker.MakeThing(pillDef);
                pill.stackCount = 10;
                if (!keeper.inventory.innerContainer.TryAdd(pill))
                {
                    pill.Destroy(DestroyMode.Vanish);
                    return;
                }
                pillStacksPlaced++;
                if (pillStacksPlaced < 2) return;
            }
            if (!gunPlaced)
            {
                Thing gun = ThingMaker.MakeThing(gunDef);
                if (keeper.inventory.innerContainer.TryAdd(gun)) gunPlaced = true;
                else { gun.Destroy(DestroyMode.Vanish); return; }
            }
            cachePlaced = true;
            registry.RecordArrival("127");
            if (map.mapPawns.FreeColonistsSpawned.Count > 0)
                Find.LetterStack.ReceiveLetter("SCP_ChaosCacheTitle".Translate(),
                    "SCP_ChaosCacheBody".Translate(), LetterDefOf.NeutralEvent, keeper);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref cachePlaced, "scpChaosCachePlaced", false);
            Scribe_Values.Look(ref gunPlaced, "scpChaosGunPlaced", false);
            Scribe_Values.Look(ref pillStacksPlaced, "scpChaosPillStacksPlaced", 0);
        }
    }
}
