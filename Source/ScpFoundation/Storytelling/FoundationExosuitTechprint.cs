using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ScpFoundation.Storytelling
{
    internal static class FoundationExosuitTechprint
    {
        internal static void Deliver(Map map)
        {
            if (map == null) return;
            ResearchProjectDef project = DefDatabase<ResearchProjectDef>
                .GetNamedSilentFail("SCP_FoundationPoweredArmor");
            if (project == null) return;
            ThingDef blueprint = DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(def =>
            {
                CompProperties_Techprint comp = def.GetCompProperties<CompProperties_Techprint>();
                return comp != null && comp.project == project;
            });
            if (blueprint == null) return;
            IntVec3 cell = DropCellFinder.TradeDropSpot(map);
            if (!cell.IsValid) return;
            DropPodUtility.DropThingsNear(cell, map, new List<Thing> { ThingMaker.MakeThing(blueprint) },
                110, false, false, false, false, true, Faction.OfPlayer);
        }
    }
}
