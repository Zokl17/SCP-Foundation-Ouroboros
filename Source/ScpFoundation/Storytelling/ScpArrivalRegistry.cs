using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Definitions;
using ScpFoundation.Knowledge;
using Verse;

namespace ScpFoundation.Storytelling
{
    // Records arrivals across all maps and survives loss of the physical object.
    public sealed class ScpArrivalRegistry : GameComponent
    {
        private HashSet<string> arrived = new HashSet<string>();
        private int scp106Routes;
        private bool scp106Contained;
        private bool scp106ChamberResearchUnlocked;
        private HashSet<string> containmentResearchUnlocked = new HashSet<string>();
        private int scp999DistressTicks;

        public ScpArrivalRegistry(Game game) { }

        public bool HasArrived(string number)
        {
            if (arrived.Contains(number)) return true;
            if (Find.QuestManager != null && Find.QuestManager.QuestsListForReading.Any(quest =>
                (quest.State == QuestState.NotYetAccepted || quest.State == QuestState.Ongoing)
                && quest.PartsListForReading.OfType<QuestPart_RecordScpArrival>()
                    .Any(part => part.number == number))) return true;
            ScpDef anomaly = DefDatabase<ScpDef>.GetNamedSilentFail("SCP_Anomaly" + number);
            return anomaly != null && Current.Game != null
                && Current.Game.GetComponent<ScpKnowledgeComponent>().HasEncountered(anomaly);
        }

        // The Foundation terminal introduces the player to SCP-650 first.
        // Other arrival routes must wait until that encounter has happened.
        public bool FirstEncounterComplete { get { return HasArrived("650"); } }

        public void RecordArrival(string number) { arrived.Add(number); }

        public bool CanArrive106(bool questRoute)
        {
            int route = questRoute ? 1 : 2;
            if ((scp106Routes & route) != 0 || scp106Contained || Current.Game == null) return false;
            bool pendingQuest = Find.QuestManager != null
                && Find.QuestManager.QuestsListForReading.Any(quest =>
                    (quest.State == QuestState.NotYetAccepted || quest.State == QuestState.Ongoing)
                    && quest.PartsListForReading.OfType<QuestPart_Scp106Recovery>().Any());
            if (pendingQuest && (questRoute || !HasUsed106Route(true))) return false;
            ThingDef chamber = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_106BaitStation");
            return !Find.Maps.Any(map =>
                map.mapPawns.AllPawnsSpawned.OfType<Pawn_Scp106>().Any(pawn => !pawn.Dead)
                || chamber != null && map.listerThings.ThingsOfDef(chamber)
                    .Any(thing => {
                        CompLarryProtocol protocol = thing.TryGetComp<CompLarryProtocol>();
                        return protocol != null && protocol.Contains106;
                    }));
        }

        public void Record106Route(bool questRoute)
        {
            scp106Routes |= questRoute ? 1 : 2;
        }

        public bool HasUsed106Route(bool questRoute)
        {
            return (scp106Routes & (questRoute ? 1 : 2)) != 0;
        }

        public bool IsContainmentResearchUnlocked(string projectName)
        {
            if (containmentResearchUnlocked == null)
                containmentResearchUnlocked = new HashSet<string>();
            if (containmentResearchUnlocked.Contains(projectName)) return true;
            if (projectName == "SCP_106Chamber" && scp106ChamberResearchUnlocked)
            {
                containmentResearchUnlocked.Add(projectName);
                return true;
            }
            string questName;
            string number;
            switch (projectName)
            {
                case "SCP_106Chamber": questName = "SCP_106Recovery"; number = "106"; break;
                case "SCP_882Reservoir": questName = "SCP_882WorkshopRaid"; number = "882"; break;
                case "SCP_682AcidContainment": questName = "SCP_682Transfer"; number = "682"; break;
                case "SCP_127LockerResearch": questName = "SCP_ChaosSalvageRaid"; number = "127"; break;
                case "SCP_093MirrorResearch": questName = "SCP_CollectorEstateRecovery"; number = "093"; break;
                case "SCP_262HangerResearch":
                    if (Find.QuestManager != null && Find.QuestManager.QuestsListForReading.Any(quest =>
                        quest.root != null && quest.root.defName == "SCP_354PerimeterDefense"
                        && quest.State == QuestState.EndedSuccess))
                        containmentResearchUnlocked.Add(projectName);
                    return containmentResearchUnlocked.Contains(projectName);
                default: return true;
            }
            bool offered = Find.QuestManager != null && Find.QuestManager.QuestsListForReading.Any(quest =>
                quest.root != null && quest.root.defName == questName);
            if (offered || HasArrived(number)) containmentResearchUnlocked.Add(projectName);
            return containmentResearchUnlocked.Contains(projectName);
        }

        public void RecordContainmentResearchUnlock(string projectName)
        {
            if (containmentResearchUnlocked == null)
                containmentResearchUnlocked = new HashSet<string>();
            containmentResearchUnlocked.Add(projectName);
        }

        public void Set106Contained(bool contained) { scp106Contained = contained; }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 250 != 0 || now < 3 * 60000) return;
            if (!FirstEncounterComplete || HasArrived("999")) { scp999DistressTicks = 0; return; }
            Map distressedMap = Find.Maps.FirstOrDefault(map => map.IsPlayerHome
                && map.mapPawns.FreeColonistsSpawned.Any(pawn => pawn.needs != null
                    && pawn.needs.mood != null && pawn.needs.mood.CurLevelPercentage < 0.3f));
            if (distressedMap == null) { scp999DistressTicks = 0; return; }
            scp999DistressTicks += 250;
            if (scp999DistressTicks < 6000) return;
            IncidentDef arrival = DefDatabase<IncidentDef>.GetNamedSilentFail("SCP_Visit999");
            scp999DistressTicks = arrival != null && arrival.Worker.TryExecute(
                StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, distressedMap)) ? 0 : 5000;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref arrived, "scpArrived", LookMode.Value);
            Scribe_Values.Look(ref scp106Routes, "scp106ArrivalRoutes", 0);
            Scribe_Values.Look(ref scp106Contained, "scp106Contained", false);
            Scribe_Values.Look(ref scp106ChamberResearchUnlocked, "scp106ChamberResearchUnlocked", false);
            Scribe_Collections.Look(ref containmentResearchUnlocked, "scpContainmentResearchUnlocked", LookMode.Value);
            Scribe_Values.Look(ref scp999DistressTicks, "scp999DistressTicks", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && arrived == null)
                arrived = new HashSet<string>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && containmentResearchUnlocked == null)
                containmentResearchUnlocked = new HashSet<string>();
        }
    }
}
