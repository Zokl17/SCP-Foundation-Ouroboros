using LudeonTK;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Debugging
{
    public static class ChimeraDebugActions
    {
        public static void SpawnChimera()
        {
            Map map = Find.CurrentMap;
            if (!Prefs.DevMode || map == null || Current.Game == null) return;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_FoundationChimera");
            FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
            Faction faction = support == null ? null : support.FoundationFaction;
            if (faction == null)
            {
                FactionDef foundation = DefDatabase<FactionDef>.GetNamedSilentFail("SCP_Foundation");
                if (foundation != null && Find.FactionManager != null)
                {
                    FactionGenerator.CreateFactionAndAddToManager(foundation);
                    faction = Find.FactionManager.FirstFactionOfDef(foundation);
                }
            }
            if (kind == null || faction == null)
            {
                Messages.Message("SCP_ChimeraUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            IntVec3 origin = UI.MouseCell();
            if (!origin.InBounds(map)) return;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(origin, 8f, true))
            {
                if (!ScpSpawnCells.FootprintClear(cell, map, kind.race)) continue;
                Pawn_FoundationChimera pawn = PawnGenerator.GeneratePawn(kind, faction) as Pawn_FoundationChimera;
                if (pawn == null) return;
                GenSpawn.Spawn(pawn, cell, map);
                Find.Selector.Select(pawn);
                Messages.Message("SCP_ChimeraSpawned".Translate(), pawn, MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            Messages.Message("SCP_DebugAnomalyNoCell".Translate(), MessageTypeDefOf.RejectInput, false);
        }
    }
}
