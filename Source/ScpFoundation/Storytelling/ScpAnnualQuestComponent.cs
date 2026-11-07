using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace ScpFoundation.Storytelling
{
    internal sealed class ScpAnnualQuestRoute
    {
        public readonly string questDefName;
        public readonly string[] numbers;
        public ScpAnnualQuestRoute(string questDefName, params string[] numbers)
        { this.questDefName = questDefName; this.numbers = numbers; }
    }

    public sealed class ScpAnnualQuestComponent : GameComponent
    {
        public readonly ScpAnnualQuestProgress progress = new ScpAnnualQuestProgress();
        private static readonly ScpAnnualQuestRoute[] routes = {
            new ScpAnnualQuestRoute("SCP_008Recovery", "008"),
            new ScpAnnualQuestRoute("SCP_035Recovery", "035"),
            new ScpAnnualQuestRoute("SCP_079Recovery", "079"),
            new ScpAnnualQuestRoute("SCP_096Recovery", "096"),
            new ScpAnnualQuestRoute("SCP_106Recovery", "106"),
            new ScpAnnualQuestRoute("SCP_294Custody", "294"),
            new ScpAnnualQuestRoute("SCP_914Custody", "914"),
            new ScpAnnualQuestRoute("SCP_330VillageRecovery", "330"),
            new ScpAnnualQuestRoute("SCP_354PerimeterDefense", "354"),
            new ScpAnnualQuestRoute("SCP_682Transfer", "682"),
            new ScpAnnualQuestRoute("SCP_939Transfer", "939"),
            new ScpAnnualQuestRoute("SCP_882WorkshopRaid", "882"),
            new ScpAnnualQuestRoute("SCP_ChaosSalvageRaid", "127"),
            new ScpAnnualQuestRoute("SCP_CollectorEstateRecovery", "067", "316", "012", "093")
        };

        public ScpAnnualQuestComponent(Game game) { }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 2500 != 0) return;
            FoundationContactProgress contact = Current.Game.GetComponent<FoundationContactComponent>().progress;
            if (contact.stage != FoundationContactStage.Established) return;
            // This confirmation accepts the first SCP-650 containment assignment.
            progress.Initialize(contact.confirmedTick);
            if (!progress.BeginAttempt(now)) return;
            if (TryOffer()) progress.Offered(now);
        }

        private static bool TryOffer()
        {
            Faction faction = Current.Game.GetComponent<FoundationSupportComponent>().FoundationFaction;
            if (faction == null || faction.HostileTo(Faction.OfPlayer)) return false;
            ScpArrivalRegistry registry = Current.Game.GetComponent<ScpArrivalRegistry>();
            var candidates = new List<ScpAnnualQuestRoute>(routes);
            var homes = Find.Maps.Where(map => map.IsPlayerHome && map.mapPawns.FreeColonistsSpawnedCount > 0).ToList();
            candidates.Shuffle();
            homes.Shuffle();
            foreach (ScpAnnualQuestRoute route in candidates)
            {
                if (route.numbers.Any(registry.HasArrived)) continue;
                QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail(route.questDefName);
                if (script == null || script.autoAccept || script.defaultHidden
                    || Find.QuestManager.QuestsListForReading.Any(quest => quest.root == script
                        && (quest.State == QuestState.NotYetAccepted || quest.State == QuestState.Ongoing))) continue;
                foreach (Map home in homes)
                {
                    float points = StorytellerUtility.DefaultThreatPointsNow(home);
                    if (points < script.rootMinPoints) continue;
                    var slate = new Slate();
                    slate.Set("map", home);
                    slate.Set("points", points);
                    if (!script.CanRun(slate, home)) continue;
                    if (QuestUtility.GenerateQuestAndMakeAvailable(script, slate) != null) return true;
                }
            }
            return false;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref progress.accepted650Tick, "scpAnnualQuestAccepted650Tick", -1);
            Scribe_Values.Look(ref progress.nextQuestTick, "scpAnnualQuestNextTick", -1L);
            Scribe_Values.Look(ref progress.nextAttemptTick, "scpAnnualQuestRetryTick", 0L);
            Scribe_Values.Look(ref progress.issuedQuests, "scpAnnualQuestsIssued", 0);
        }
    }
}
