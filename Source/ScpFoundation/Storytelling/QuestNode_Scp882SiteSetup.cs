using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using RimWorld.Planet;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_Scp882SiteSetup : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail("SCP_ChaosInsurgency");
            return factionDef != null && Find.FactionManager.FirstFactionOfDef(factionDef) != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("882")
                && DefDatabase<SitePartDef>.GetNamedSilentFail("SCP_882Workshop") != null;
        }

        protected override void RunInt()
        {
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamed("SCP_ChaosInsurgency");
            Faction faction = Find.FactionManager.FirstFactionOfDef(factionDef);
            ScpFactionRelationsComponent.EnsureHostile(faction, Faction.OfPlayer);
            QuestGen.slate.Set("siteFaction", faction);
            QuestGen.slate.Set("sitePartDefs", new List<SitePartDef> {
                DefDatabase<SitePartDef>.GetNamed("SCP_882Workshop")
            });
        }
    }

    // Ensure ownership before spawning, never while a live map is generating.
    public sealed class QuestNode_Scp882ClaimSite : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            Site site = QuestGen.slate.Get<Site>("site");
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamed("SCP_ChaosInsurgency");
            Faction faction = Find.FactionManager.FirstFactionOfDef(factionDef);
            if (site != null && faction != null && site.Faction != faction)
                site.SetFaction(faction);
        }
    }
}
