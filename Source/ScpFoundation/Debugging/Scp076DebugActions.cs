using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Debugging
{
    public static class Scp076DebugActions
    {
        [DebugAction("SCP Foundation", "Spawn SCP-076-2 at cursor", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SpawnAble()
        {
            Map map = Find.CurrentMap;
            if (!Prefs.DevMode || map == null || Current.Game == null) return;
            FactionDef foundationDef = DefDatabase<FactionDef>.GetNamedSilentFail("SCP_Foundation");
            if (foundationDef == null || Find.FactionManager == null) return;
            Faction faction = Find.FactionManager.FirstFactionOfDef(foundationDef);
            if (faction == null)
            {
                FactionGenerator.CreateFactionAndAddToManager(foundationDef);
                faction = Find.FactionManager.FirstFactionOfDef(foundationDef);
            }
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_076Kind");
            if (kind == null || faction == null)
            {
                Messages.Message("SCP_DebugAnomalyMissingDef".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            IntVec3 origin = UI.MouseCell();
            if (!origin.InBounds(map)) return;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(origin, 8f, true))
            {
                if (!ScpSpawnCells.FootprintClear(cell, map, kind.race)) continue;
                Pawn_Scp076 able = Pawn_Scp076.Generate(faction);
                if (able == null) return;
                FoundationSupportComponent.SpawnSupportTeam(map, cell, faction, new List<Pawn> { able }, 60000);
                Find.Selector.Select(able);
                Messages.Message("SCP_DebugAnomalySpawned".Translate(able.LabelCap), able,
                    MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            Messages.Message("SCP_DebugAnomalyNoCell".Translate(), MessageTypeDefOf.RejectInput, false);
        }
    }
}
