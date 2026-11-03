using RimWorld;
using ScpFoundation.Anomalies;
using Verse;

namespace ScpFoundation.Debugging
{
    public static class ScpAnomalyDebugSpawn
    {
        public static void Spawn(string thingDefName)
        {
            Map map = Find.CurrentMap;
            if (!Prefs.DevMode || map == null) return;
            bool scp106 = thingDefName == "SCP_106Entity";
            bool scp049 = thingDefName == "SCP_049Entity";
            PawnKindDef kind = scp106 ? DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_106Kind")
                : scp049 ? DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_049Kind") : null;
            ThingDef definition = scp106 || scp049 ? (kind == null ? null : kind.race)
                : DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            if (definition == null)
            {
                Messages.Message("SCP_DebugAnomalyMissingDef".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            IntVec3 origin = UI.MouseCell();
            if (!origin.InBounds(map)) return;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(origin, 8f, true))
            {
                if (!ScpSpawnCells.FootprintClear(cell, map, definition)) continue;
                Thing thing = scp106 ? (Thing)Pawn_Scp106.Generate()
                    : scp049 ? Pawn_Scp049.Generate() : ThingMaker.MakeThing(definition);
                if (thing == null) return;
                Thing spawned = GenSpawn.Spawn(thing, cell, map);
                Find.Selector.Select(spawned);
                Messages.Message("SCP_DebugAnomalySpawned".Translate(spawned.LabelCap), spawned,
                    MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            Messages.Message("SCP_DebugAnomalyNoCell".Translate(), MessageTypeDefOf.RejectInput, false);
        }
    }
}
