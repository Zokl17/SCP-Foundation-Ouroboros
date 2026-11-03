using System.Linq;
using RimWorld;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class IncidentWorker_DiscoverSmallArtifacts : IncidentWorker
    {
        private ThingDef ObjectDef
        {
            get
            {
                string name = def.defName == "SCP_Discover012" ? "SCP_012Score"
                    : def.defName == "SCP_Discover914" ? "SCP_914Machine"
                    : def.defName == "SCP_Discover513" ? "SCP_513Bell"
                    : def.defName == "SCP_Discover207" ? "SCP_207Bottle" : null;
                return name == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(name);
            }
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            // Keep the old incident def for save compatibility; SCP-012 now belongs to the collector quest.
            if (def.defName == "SCP_Discover012") return false;
            Map map = parms.target as Map;
            ThingDef thingDef = ObjectDef;
            if (map == null || !map.IsPlayerHome || thingDef == null
                || !Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                || Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived(def.defName.Substring(("SCP_" + "Discover").Length))
                || map.mapPawns.FreeColonistsSpawnedCount < 2
                || map.listerThings.ThingsOfDef(thingDef).Any()) return false;
            CompProperties_Scp properties = thingDef.GetCompProperties<CompProperties_Scp>();
            return properties == null || properties.anomaly == null
                || !Current.Game.GetComponent<ScpKnowledgeComponent>().HasEncountered(properties.anomaly);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            ThingDef thingDef = ObjectDef;
            IntVec3 cell;
            if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(12,
                candidate => ScpSpawnCells.FootprintClear(candidate, map, thingDef)
                    && (thingDef.defName != "SCP_914Machine" || Scp914PadsClear(candidate, map, thingDef))
                    && map.reachability.CanReachColony(candidate), map, out cell)) return false;
            Thing thing = GenSpawn.Spawn(ThingMaker.MakeThing(thingDef), cell, map);
            if (thingDef.defName == "SCP_914Machine") thing.SetFaction(Faction.OfPlayer);
            CompScp scp = thing.TryGetComp<CompScp>();
            if (scp != null) scp.NotifyDiscovered(false);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival(def.defName.Substring(("SCP_" + "Discover").Length));
            SendStandardLetter(parms, thing);
            return true;
        }

        private static bool Scp914PadsClear(IntVec3 origin, Map map, ThingDef machine)
        {
            CellRect rect = GenAdj.OccupiedRect(origin, Rot4.North, machine.size);
            int z = (rect.minZ + rect.maxZ) / 2;
            return ScpSpawnCells.FootprintClear(new IntVec3(rect.minX - 1, 0, z), map, ThingDefOf.Steel)
                && ScpSpawnCells.FootprintClear(new IntVec3(rect.maxX + 1, 0, z), map, ThingDefOf.Steel);
        }
    }
}
