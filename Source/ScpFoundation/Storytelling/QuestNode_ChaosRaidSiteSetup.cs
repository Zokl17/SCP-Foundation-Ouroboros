using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_ChaosRaidSiteSetup : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail("SCP_ChaosInsurgency");
            return factionDef != null && Find.FactionManager.FirstFactionOfDef(factionDef) != null
                && Current.Game.GetComponent<ScpArrivalRegistry>().FirstEncounterComplete
                && !Current.Game.GetComponent<ScpArrivalRegistry>().HasArrived("127")
                && DefDatabase<SitePartDef>.GetNamedSilentFail("SCP_ChaosRaid") != null;
        }

        protected override void RunInt()
        {
            Faction faction = Find.FactionManager.FirstFactionOfDef(
                DefDatabase<FactionDef>.GetNamed("SCP_ChaosInsurgency"));
            ScpFactionRelationsComponent.EnsureHostile(faction, Faction.OfPlayer);
            QuestGen.slate.Set("siteFaction", faction);
            QuestGen.slate.Set("sitePartDefs", new List<SitePartDef> {
                DefDatabase<SitePartDef>.GetNamed("SCP_ChaosRaid")
            });
        }
    }
}
