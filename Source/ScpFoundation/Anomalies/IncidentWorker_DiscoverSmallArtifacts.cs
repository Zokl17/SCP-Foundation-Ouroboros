using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
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
            if (def.defName == "SCP_Discover914")
            {
                QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_914Custody");
                if (script == null || Find.QuestManager.QuestsListForReading.Any(quest => quest.root == script
                    && (quest.State == QuestState.NotYetAccepted || quest.State == QuestState.Ongoing))) return false;
            }
            CompProperties_Scp properties = thingDef.GetCompProperties<CompProperties_Scp>();
            return properties == null || properties.anomaly == null
                || !Current.Game.GetComponent<ScpKnowledgeComponent>().HasEncountered(properties.anomaly);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            if (def.defName == "SCP_Discover914")
            {
                QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_914Custody");
                var slate = new Slate();
                slate.Set("map", map);
                slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(map));
                return script.CanRun(slate, map) && QuestUtility.GenerateQuestAndMakeAvailable(script, slate) != null;
            }
            ThingDef thingDef = ObjectDef;
            IntVec3 cell;
            if (!CellFinderLoose.TryFindRandomNotEdgeCellWith(12,
                candidate => ScpSpawnCells.FootprintClear(candidate, map, thingDef)
                    && map.reachability.CanReachColony(candidate), map, out cell)) return false;
            Thing thing = GenSpawn.Spawn(ThingMaker.MakeThing(thingDef), cell, map);
            CompScp scp = thing.TryGetComp<CompScp>();
            if (scp != null) scp.NotifyDiscovered(false);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival(def.defName.Substring(("SCP_" + "Discover").Length));
            SendStandardLetter(parms, thing);
            return true;
        }

    }
}
