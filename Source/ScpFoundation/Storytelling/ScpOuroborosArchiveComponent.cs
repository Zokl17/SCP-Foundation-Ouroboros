using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Definitions;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class ScpOuroborosArchiveEntry
    {
        public readonly string id;
        public readonly int tick;
        public readonly string title;
        public readonly string body;
        public readonly bool read;

        public ScpOuroborosArchiveEntry(string id, int tick, string title, string body, bool read)
        {
            this.id = id;
            this.tick = tick;
            this.title = title;
            this.body = body;
            this.read = read;
        }
    }

    // A durable terminal archive; letters only announce that a new file is available.
    public sealed class ScpOuroborosArchiveComponent : GameComponent
    {
        private int decodedTick = -1;
        private int historyTick = -1;
        private int aftermathTick = -1;
        private int analysisTick = -1;
        private int greenVisitTick = -1;
        private int finalReportTick = -1;
        private int signalAnalysisTick = -1;
        private int signalRecoveryTick = -1;
        private int churchCacheTick = -1;
        private int churchReportTick = -1;
        private int churchResolutionTick = -1;
        private int sample217AnalysisTick = -1;
        private int parts629AnalysisTick = -1;
        private int routingLogTick = -1;
        private int ouroborosFinalTick = -1;
        private bool sample217AnalysisRead;
        private bool parts629AnalysisRead;
        private bool sample217Transferred;
        private bool parts629Transferred;
        private bool routingLogRead;
        private bool ouroborosFinalRead;
        private bool routingQuestOffered;
        private bool decodedRead;
        private bool decodedLetterLocalized;
        private bool historyRead;
        private bool aftermathRead;
        private bool analysisRead;
        private bool greenVisitRead;
        private bool finalReportRead;
        private bool signalAnalysisRead;
        private bool signalRecoveryRead;
        private bool churchCacheRead;
        private bool churchRaidCompleted;
        private bool churchResolutionRead;
        private bool finalReportPrompted;
        private bool aftermathSecured;
        private bool analysisQuestOffered;
        private bool defenseTrustGranted;
        private bool greenVisitQuestOffered;
        private bool greenVisitRetryOffered;
        private bool signalResearchQuestOffered;
        private bool signalSiteQuestOffered;
        private bool churchCacheQuestOffered;
        private bool churchTrustGranted;
        private bool churchResearchGranted;
        private bool churchPizzaGranted;
        private string meetingScientist;

        public bool AftermathSecured { get { return aftermathTick >= 0 && aftermathSecured; } }
        public int AftermathTick { get { return aftermathTick; } }
        public int AnalysisTick { get { return analysisTick; } }
        public int GreenVisitTick { get { return greenVisitTick; } }
        public int FinalReportTick { get { return finalReportTick; } }
        public int SignalAnalysisTick { get { return signalAnalysisTick; } }
        public int SignalRecoveryTick { get { return signalRecoveryTick; } }
        public int ChurchCacheTick { get { return churchCacheTick; } }
        public int ChurchReportTick { get { return churchReportTick; } }
        public int ChurchResolutionTick { get { return churchResolutionTick; } }
        public int RoutingLogTick { get { return routingLogTick; } }
        public int OuroborosFinalTick { get { return ouroborosFinalTick; } }
        public bool FinalReportAvailable
        {
            get
            {
                if (greenVisitTick < 0 || finalReportTick >= 0 || Find.QuestManager == null) return false;
                QuestScriptDef visit = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_O5GreenVisit");
                return visit != null && Find.QuestManager.QuestsListForReading.Any(quest =>
                    quest.root == visit && quest.State == QuestState.EndedSuccess);
            }
        }

        public ScpOuroborosArchiveComponent(Game game) { }

        public void UnlockDecoded(int tick) { if (decodedTick < 0) decodedTick = tick; }
        public void UnlockHistory(int tick) { if (historyTick < 0) historyTick = tick; }
        public void UnlockAftermath(int tick, bool secured)
        {
            if (aftermathTick >= 0) return;
            aftermathTick = tick;
            aftermathSecured = secured;
        }
        public void UnlockAnalysis(int tick) { if (analysisTick < 0) analysisTick = tick; }
        public void UnlockSignalAnalysis(int tick) { if (signalAnalysisTick < 0) signalAnalysisTick = tick; }
        public void UnlockSignalRecovery(int tick) { if (signalRecoveryTick < 0) signalRecoveryTick = tick; }
        public void RecordChurchEvidence(string defName)
        {
            int now = Find.TickManager.TicksGame;
            if (defName == "SCP_OuroborosSealed217Sample" && sample217AnalysisTick < 0)
            {
                sample217AnalysisTick = now;
                Current.Game.GetComponent<ScpKnowledgeComponent>().RecordEncounter(
                    DefDatabase<ScpDef>.GetNamed("SCP_Anomaly217"));
            }
            else if (defName == "SCP_OuroborosParts629Cache" && parts629AnalysisTick < 0)
            {
                parts629AnalysisTick = now;
                Current.Game.GetComponent<ScpKnowledgeComponent>().RecordEncounter(
                    DefDatabase<ScpDef>.GetNamed("SCP_Anomaly629"));
            }
        }
        public void UnlockRoutingLog(int tick)
        {
            if (routingLogTick >= 0) return;
            routingLogTick = tick;
            Find.LetterStack.ReceiveLetter("SCP_RoutingLogRecoveredTitle".Translate(),
                "SCP_RoutingLogRecoveredBody".Translate(), LetterDefOf.PositiveEvent);
        }
        public bool CanCloseOuroboros(Map map)
        {
            return routingLogTick >= 0 && ouroborosFinalTick < 0 && map != null
                && map.IsPlayerHome && CompFoundationAccessTerminal.Online(map) != null;
        }
        public bool CloseOuroboros(Map map)
        {
            if (!CanCloseOuroboros(map)) return false;
            ouroborosFinalTick = Find.TickManager.TicksGame;
            FoundationSupportProgress support = Current.Game.GetComponent<FoundationSupportComponent>().progress;
            int gained = System.Math.Min(10, System.Math.Max(0,
                FoundationSupportProgress.MaximumTrust - support.trust));
            support.trust += gained;
            Find.LetterStack.ReceiveLetter("SCP_OuroborosFinalTitle".Translate(),
                "SCP_OuroborosFinalLetter".Translate(gained), LetterDefOf.PositiveEvent);
            return true;
        }
        public void Mark629RoutingInsight()
        {
            if (parts629AnalysisTick >= 0) parts629AnalysisRead = false;
        }
        public bool TransferChurchEvidence(string defName, Map map)
        {
            if (map == null || !map.IsPlayerHome
                || CompFoundationAccessTerminal.Online(map) == null) return false;
            bool is217 = defName == "SCP_OuroborosSealed217Sample";
            if (is217 ? sample217AnalysisTick < 0 || sample217Transferred
                : defName != "SCP_OuroborosParts629Cache" || parts629AnalysisTick < 0
                    || parts629Transferred) return false;
            if (is217) sample217Transferred = true;
            else parts629Transferred = true;
            FoundationSupportProgress support = Current.Game.GetComponent<FoundationSupportComponent>().progress;
            int gained = System.Math.Min(2, System.Math.Max(0,
                FoundationSupportProgress.MaximumTrust - support.trust));
            support.trust += gained;
            Messages.Message("SCP_ChurchEvidenceTransferred".Translate(gained),
                MessageTypeDefOf.PositiveEvent, false);
            return true;
        }
        public void UnlockChurchCache(int tick, bool raidCompleted = false)
        {
            if (churchCacheTick >= 0) return;
            churchCacheTick = tick;
            churchRaidCompleted = raidCompleted;
            Find.LetterStack.ReceiveLetter((raidCompleted ? "SCP_ChurchRaidTitle" : "SCP_ChurchCacheTitle").Translate(),
                (raidCompleted ? "SCP_ChurchRaidLetter" : "SCP_ChurchCacheLetter").Translate(), LetterDefOf.PositiveEvent);
        }
        public void UnlockGreenVisit(int tick, Pawn scientist)
        {
            if (greenVisitTick >= 0) return;
            greenVisitTick = tick;
            meetingScientist = scientist == null ? "SCP_O5GreenUnknownScientist".Translate().ToString()
                : scientist.LabelShortCap;
        }

        public bool CanSubmitFinalReport(Map map)
        {
            return FinalReportAvailable && map != null && map.IsPlayerHome
                && CompFoundationAccessTerminal.Online(map) != null && HasContained882(map);
        }

        public bool SubmitFinalReport(Map map)
        {
            if (!CanSubmitFinalReport(map)) return false;
            finalReportTick = Find.TickManager.TicksGame;
            FoundationSupportProgress support = Current.Game.GetComponent<FoundationSupportComponent>().progress;
            int gained = System.Math.Min(10, System.Math.Max(0, FoundationSupportProgress.MaximumTrust - support.trust));
            support.trust += gained;
            Find.LetterStack.ReceiveLetter("SCP_882FinalReportTitle".Translate(),
                "SCP_882FinalReportLetter".Translate(gained), LetterDefOf.PositiveEvent);
            return true;
        }

        public bool CanSubmitChurchReport(Map map)
        {
            return churchCacheTick >= 0 && churchReportTick < 0 && map != null && map.IsPlayerHome
                && CompFoundationAccessTerminal.Online(map) != null;
        }

        public bool SubmitChurchReport(Map map)
        {
            if (!CanSubmitChurchReport(map)) return false;
            churchReportTick = Find.TickManager.TicksGame;
            Messages.Message("SCP_ChurchReportSent".Translate(), MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        public int UnreadCount
        {
            get
            {
                return (decodedTick >= 0 && !decodedRead ? 1 : 0)
                    + (historyTick >= 0 && !historyRead ? 1 : 0)
                    + (aftermathTick >= 0 && !aftermathRead ? 1 : 0)
                    + (analysisTick >= 0 && !analysisRead ? 1 : 0)
                    + (greenVisitTick >= 0 && !greenVisitRead ? 1 : 0)
                    + (finalReportTick >= 0 && !finalReportRead ? 1 : 0)
                    + (signalAnalysisTick >= 0 && !signalAnalysisRead ? 1 : 0)
                    + (signalRecoveryTick >= 0 && !signalRecoveryRead ? 1 : 0)
                    + (churchCacheTick >= 0 && !churchCacheRead ? 1 : 0)
                    + (sample217AnalysisTick >= 0 && !sample217AnalysisRead ? 1 : 0)
                    + (parts629AnalysisTick >= 0 && !parts629AnalysisRead ? 1 : 0)
                    + (routingLogTick >= 0 && !routingLogRead ? 1 : 0)
                    + (ouroborosFinalTick >= 0 && !ouroborosFinalRead ? 1 : 0)
                    + (churchResolutionTick >= 0 && !churchResolutionRead ? 1 : 0);
            }
        }

        public List<ScpOuroborosArchiveEntry> Entries()
        {
            var entries = new List<ScpOuroborosArchiveEntry>();
            if (decodedTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("decoded", decodedTick,
                "SCP_OuroborosDecodedTitle".Translate(), "SCP_OuroborosDecodedBody".Translate(), decodedRead));
            if (historyTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("history", historyTick,
                "SCP_882HistoryTitle".Translate(), "SCP_882HistoryBody".Translate(), historyRead));
            if (aftermathTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("aftermath", aftermathTick,
                "SCP_882AftermathArchiveTitle".Translate(), "SCP_882AftermathArchiveBody".Translate(), aftermathRead));
            if (analysisTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("analysis", analysisTick,
                "SCP_882AnalysisArchiveTitle".Translate(), "SCP_882AnalysisArchiveBody".Translate(), analysisRead));
            if (greenVisitTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("greenVisit", greenVisitTick,
                "SCP_O5GreenTranscriptTitle".Translate(),
                "SCP_O5GreenTranscriptBody".Translate(meetingScientist), greenVisitRead));
            if (finalReportTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("finalReport", finalReportTick,
                "SCP_882FinalReportTitle".Translate(), "SCP_882FinalReportBody".Translate(), finalReportRead));
            if (signalAnalysisTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("signalAnalysis", signalAnalysisTick,
                "SCP_SignalAnalysisTitle".Translate(), "SCP_SignalAnalysisBody".Translate(), signalAnalysisRead));
            if (signalRecoveryTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("signalRecovery", signalRecoveryTick,
                "SCP_SignalRecoveryTitle".Translate(), "SCP_SignalRecoveryBody".Translate(), signalRecoveryRead));
            if (churchCacheTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("churchCache", churchCacheTick,
                (churchRaidCompleted ? "SCP_ChurchRaidTitle" : "SCP_ChurchCacheTitle").Translate(),
                (churchRaidCompleted ? "SCP_ChurchRaidBody" : "SCP_ChurchCacheBody").Translate(), churchCacheRead));
            if (sample217AnalysisTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("sample217",
                sample217AnalysisTick, "SCP_217AnalysisTitle".Translate(),
                "SCP_217AnalysisBody".Translate().ToString() + (sample217Transferred
                    ? "\n\n" + "SCP_217TransferredNote".Translate().ToString() : ""), sample217AnalysisRead));
            if (parts629AnalysisTick >= 0)
            {
                ScpDef parts = DefDatabase<ScpDef>.GetNamedSilentFail("SCP_Anomaly629");
                bool insight = parts != null && Current.Game.GetComponent<ScpKnowledgeComponent>()
                    .LevelFor(parts) >= KnowledgeLevel.PartiallyDocumented;
                entries.Add(new ScpOuroborosArchiveEntry("parts629",
                    parts629AnalysisTick, "SCP_629AnalysisTitle".Translate(),
                    "SCP_629AnalysisBody".Translate().ToString() + (insight
                        ? "\n\n" + "SCP_629RoutingIndexArchive".Translate().ToString() : "")
                        + (parts629Transferred ? "\n\n"
                            + "SCP_629TransferredNote".Translate().ToString() : ""), parts629AnalysisRead));
            }
            if (routingLogTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("routingLog",
                routingLogTick, "SCP_RoutingLogArchiveTitle".Translate(),
                "SCP_RoutingLogArchiveBody".Translate(), routingLogRead));
            if (ouroborosFinalTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("ouroborosFinal",
                ouroborosFinalTick, "SCP_OuroborosFinalTitle".Translate(),
                "SCP_OuroborosFinalBody".Translate(), ouroborosFinalRead));
            if (churchResolutionTick >= 0) entries.Add(new ScpOuroborosArchiveEntry("churchResolution", churchResolutionTick,
                "SCP_ChurchResolutionTitle".Translate(), "SCP_ChurchResolutionBody".Translate(), churchResolutionRead));
            return entries.OrderByDescending(entry => entry.tick).ToList();
        }

        public void MarkRead(string id)
        {
            if (id == "decoded" && decodedTick >= 0) decodedRead = true;
            else if (id == "history" && historyTick >= 0) historyRead = true;
            else if (id == "aftermath" && aftermathTick >= 0) aftermathRead = true;
            else if (id == "analysis" && analysisTick >= 0) analysisRead = true;
            else if (id == "greenVisit" && greenVisitTick >= 0) greenVisitRead = true;
            else if (id == "finalReport" && finalReportTick >= 0) finalReportRead = true;
            else if (id == "signalAnalysis" && signalAnalysisTick >= 0) signalAnalysisRead = true;
            else if (id == "signalRecovery" && signalRecoveryTick >= 0) signalRecoveryRead = true;
            else if (id == "churchCache" && churchCacheTick >= 0) churchCacheRead = true;
            else if (id == "sample217" && sample217AnalysisTick >= 0) sample217AnalysisRead = true;
            else if (id == "parts629" && parts629AnalysisTick >= 0) parts629AnalysisRead = true;
            else if (id == "routingLog" && routingLogTick >= 0) routingLogRead = true;
            else if (id == "ouroborosFinal" && ouroborosFinalTick >= 0) ouroborosFinalRead = true;
            else if (id == "churchResolution" && churchResolutionTick >= 0) churchResolutionRead = true;
        }

        public static bool HasContained882(Map map)
        {
            if (map == null) return false;
            foreach (string name in new[] { "SCP_882Core", "SCP_882CoreFull" })
            {
                ThingDef definition = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (definition != null && map.listerThings.ThingsOfDef(definition)
                    .Any(thing => thing.Spawned && thing.TryGetComp<CompScp882>() != null
                        && thing.TryGetComp<CompScp882>().ContainsObject)) return true;
            }
            return false;
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 250 != 0) return;
            if (parts629AnalysisTick >= 0)
            {
                ScpDef parts = DefDatabase<ScpDef>.GetNamedSilentFail("SCP_Anomaly629");
                ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
                if (parts != null && knowledge != null && knowledge.LevelFor(parts) == KnowledgeLevel.Unknown)
                    knowledge.SetLevel(parts, parts629Transferred ? KnowledgeLevel.PartiallyDocumented
                        : KnowledgeLevel.Observed);
            }
            UnloadRecoveredTransponder();
            if (decodedTick < 0 && Find.QuestManager != null)
            {
                QuestScriptDef opening = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_OuroborosOpening");
                if (opening != null && Find.QuestManager.QuestsListForReading.Any(quest =>
                    quest.root == opening && quest.State == QuestState.EndedSuccess)) UnlockDecoded(now);
            }
            if (decodedTick >= 0 && !decodedLetterLocalized)
            {
                foreach (ChoiceLetter letter in Find.LetterStack.LettersListForReading
                    .OfType<ChoiceLetter>().Concat(Find.Archive.ArchivablesListForReading
                        .OfType<ChoiceLetter>()))
                    if (letter.Label.ToString() == "Project 001 fragment decoded")
                    {
                        letter.Label = "SCP_OuroborosDecodedTitle".Translate();
                        letter.Text = "SCP_OuroborosArchiveNotice".Translate();
                    }
                decodedLetterLocalized = true;
            }
            Scp882OuroborosSiegeComponent siege = Current.Game.GetComponent<Scp882OuroborosSiegeComponent>();
            if (siege != null && siege.ArchiveStarted && historyTick < 0) UnlockHistory(now);
            if (siege != null && siege.DefenseFinished && aftermathTick < 0)
            {
                Map home = Find.Maps.FirstOrDefault(map => map.IsPlayerHome);
                if (home != null) UnlockAftermath(now, siege.OutcomeSecured || siege.ObjectStillAtHome(home));
            }
            if (AftermathSecured && !defenseTrustGranted)
            {
                FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
                int gained = System.Math.Min(15, FoundationSupportProgress.MaximumTrust - support.progress.trust);
                support.progress.trust += gained;
                defenseTrustGranted = true;
                Messages.Message("SCP_882DefenseTrustGranted".Translate(gained),
                    MessageTypeDefOf.PositiveEvent, false);
            }
            if (analysisTick >= 0 && !greenVisitQuestOffered && Find.QuestManager != null)
            {
                QuestScriptDef visit = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_O5GreenVisit");
                if (visit != null && Find.QuestManager.QuestsListForReading.Any(quest => quest.root == visit))
                    greenVisitQuestOffered = true;
                else if (visit != null && Find.Maps.Any(map => map.IsPlayerHome)
                    && QuestUtility.GenerateQuestAndMakeAvailable(visit, 1000f) != null)
                    greenVisitQuestOffered = true;
            }
            if (Find.QuestManager != null)
                foreach (Quest visit in Find.QuestManager.QuestsListForReading
                    .Where(candidate => candidate.State == QuestState.EndedSuccess
                        && candidate.root != null && candidate.root.defName == "SCP_O5GreenVisit"))
                    foreach (QuestPart_ScpO5GreenVisit part in visit.PartsListForReading
                        .OfType<QuestPart_ScpO5GreenVisit>())
                        part.ReleaseStrandedDelegation();
            if (analysisTick >= 0 && greenVisitTick < 0 && greenVisitQuestOffered
                && !greenVisitRetryOffered && Find.QuestManager != null)
            {
                QuestScriptDef visit = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_O5GreenVisit");
                if (visit != null)
                {
                    List<Quest> visits = Find.QuestManager.QuestsListForReading
                        .Where(quest => quest.root == visit).ToList();
                    bool failedWithoutBetrayal = visits.Any(quest => quest.State == QuestState.EndedFailed
                        && quest.PartsListForReading.OfType<QuestPart_ScpO5GreenVisit>()
                            .Any(part => !part.RetaliationTriggered));
                    bool pending = visits.Any(quest => quest.State == QuestState.NotYetAccepted
                        || quest.State == QuestState.Ongoing);
                    if (failedWithoutBetrayal && !pending && Find.Maps.Any(map => map.IsPlayerHome)
                        && QuestUtility.GenerateQuestAndMakeAvailable(visit, 1000f) != null)
                        greenVisitRetryOffered = true;
                }
            }
            if (analysisTick >= 0 && Find.QuestManager != null)
            {
                QuestScriptDef visit = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_O5GreenVisit");
                foreach (Quest quest in Find.QuestManager.QuestsListForReading.Where(quest => quest.root == visit
                    && quest.State == QuestState.NotYetAccepted
                    && !quest.PartsListForReading.OfType<QuestPart_RequirementsToAcceptScpO5Scientist>().Any()))
                {
                    QuestPart_ScpO5GreenVisit visitPart = quest.PartsListForReading
                        .OfType<QuestPart_ScpO5GreenVisit>().FirstOrDefault();
                    if (visitPart != null && visitPart.map != null)
                        quest.AddPart(new QuestPart_RequirementsToAcceptScpO5Scientist { map = visitPart.map });
                }
            }
            if (!finalReportPrompted && FinalReportAvailable)
            {
                finalReportPrompted = true;
                Find.LetterStack.ReceiveLetter("SCP_882FinalReportReadyTitle".Translate(),
                    "SCP_882FinalReportReadyBody".Translate(), LetterDefOf.NeutralEvent);
            }
            if (finalReportTick >= 0 && !signalResearchQuestOffered && Find.QuestManager != null)
            {
                QuestScriptDef signalQuest = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_OuroborosSignalResearch");
                if (signalQuest != null && Find.QuestManager.QuestsListForReading.Any(quest => quest.root == signalQuest))
                    signalResearchQuestOffered = true;
            }
            OfferFollowup("SCP_OuroborosSignalSite", finalReportTick >= 0
                && (!signalResearchQuestOffered || signalRecoveryTick >= 0 || signalAnalysisTick >= 0),
                ref signalSiteQuestOffered);
            OfferFollowup("SCP_OuroborosChurchCache", signalRecoveryTick >= 0 && signalAnalysisTick >= 0,
                ref churchCacheQuestOffered);
            ScpDef parts629 = DefDatabase<ScpDef>.GetNamedSilentFail("SCP_Anomaly629");
            OfferFollowup("SCP_OuroborosRoutingLog", churchResolutionTick >= 0
                && parts629 != null && Current.Game.GetComponent<ScpKnowledgeComponent>()
                    .LevelFor(parts629) >= KnowledgeLevel.PartiallyDocumented,
                ref routingQuestOffered);
            if (churchReportTick >= 0 && churchResolutionTick < 0 && now - churchReportTick >= 15000
                && Find.Maps.Any(map => map.IsPlayerHome && CompFoundationAccessTerminal.Online(map) != null))
            {
                churchResolutionTick = now;
                Find.LetterStack.ReceiveLetter("SCP_ChurchResolutionTitle".Translate(),
                    "SCP_ChurchResolutionLetter".Translate(), LetterDefOf.NeutralEvent);
            }
            GrantChurchCacheRewards();
            if (!AftermathSecured || analysisQuestOffered || Find.QuestManager == null
                || Current.Game.GetComponent<FoundationContactComponent>().progress.stage != FoundationContactStage.Established) return;
            Map missionMap = Find.Maps.FirstOrDefault(map => map.IsPlayerHome
                && CompFoundationAccessTerminal.Online(map) != null);
            if (missionMap == null) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail("SCP_882AftermathAnalysis");
            if (script == null) return;
            if (Find.QuestManager.QuestsListForReading.Any(quest => quest.root == script))
            { analysisQuestOffered = true; return; }
            if (QuestUtility.GenerateQuestAndMakeAvailable(script, 1000f) != null)
                analysisQuestOffered = true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref decodedTick, "scpOuroborosDecodedArchiveTick", -1);
            Scribe_Values.Look(ref historyTick, "scpOuroborosHistoryArchiveTick", -1);
            Scribe_Values.Look(ref aftermathTick, "scpOuroborosAftermathArchiveTick", -1);
            Scribe_Values.Look(ref analysisTick, "scpOuroborosAnalysisArchiveTick", -1);
            Scribe_Values.Look(ref greenVisitTick, "scpOuroborosGreenVisitArchiveTick", -1);
            Scribe_Values.Look(ref finalReportTick, "scpOuroborosFinalReportTick", -1);
            Scribe_Values.Look(ref signalAnalysisTick, "scpOuroborosSignalAnalysisTick", -1);
            Scribe_Values.Look(ref signalRecoveryTick, "scpOuroborosSignalRecoveryTick", -1);
            Scribe_Values.Look(ref churchCacheTick, "scpOuroborosChurchCacheTick", -1);
            Scribe_Values.Look(ref churchReportTick, "scpOuroborosChurchReportTick", -1);
            Scribe_Values.Look(ref churchResolutionTick, "scpOuroborosChurchResolutionTick", -1);
            Scribe_Values.Look(ref sample217AnalysisTick, "scp217AnalysisTick", -1);
            Scribe_Values.Look(ref parts629AnalysisTick, "scp629AnalysisTick", -1);
            Scribe_Values.Look(ref routingLogTick, "scpRoutingLogTick", -1);
            Scribe_Values.Look(ref ouroborosFinalTick, "scpOuroborosFinalTick", -1);
            Scribe_Values.Look(ref sample217AnalysisRead, "scp217AnalysisRead", false);
            Scribe_Values.Look(ref parts629AnalysisRead, "scp629AnalysisRead", false);
            Scribe_Values.Look(ref sample217Transferred, "scp217Transferred", false);
            Scribe_Values.Look(ref parts629Transferred, "scp629Transferred", false);
            Scribe_Values.Look(ref routingLogRead, "scpRoutingLogRead", false);
            Scribe_Values.Look(ref ouroborosFinalRead, "scpOuroborosFinalRead", false);
            Scribe_Values.Look(ref routingQuestOffered, "scpRoutingQuestOffered", false);
            Scribe_Values.Look(ref decodedRead, "scpOuroborosDecodedArchiveRead", false);
            Scribe_Values.Look(ref decodedLetterLocalized, "scpOuroborosDecodedLetterLocalized", false);
            Scribe_Values.Look(ref historyRead, "scpOuroborosHistoryArchiveRead", false);
            Scribe_Values.Look(ref aftermathRead, "scpOuroborosAftermathArchiveRead", false);
            Scribe_Values.Look(ref analysisRead, "scpOuroborosAnalysisArchiveRead", false);
            Scribe_Values.Look(ref greenVisitRead, "scpOuroborosGreenVisitArchiveRead", false);
            Scribe_Values.Look(ref finalReportRead, "scpOuroborosFinalReportRead", false);
            Scribe_Values.Look(ref signalAnalysisRead, "scpOuroborosSignalAnalysisRead", false);
            Scribe_Values.Look(ref signalRecoveryRead, "scpOuroborosSignalRecoveryRead", false);
            Scribe_Values.Look(ref churchCacheRead, "scpOuroborosChurchCacheRead", false);
            Scribe_Values.Look(ref churchRaidCompleted, "scpOuroborosChurchRaidCompleted", false);
            Scribe_Values.Look(ref churchResolutionRead, "scpOuroborosChurchResolutionRead", false);
            Scribe_Values.Look(ref finalReportPrompted, "scpOuroborosFinalReportPrompted", false);
            Scribe_Values.Look(ref aftermathSecured, "scpOuroborosAftermathSecured", false);
            Scribe_Values.Look(ref analysisQuestOffered, "scpOuroborosAnalysisQuestOffered", false);
            Scribe_Values.Look(ref defenseTrustGranted, "scpOuroborosDefenseTrustGranted", false);
            Scribe_Values.Look(ref greenVisitQuestOffered, "scpOuroborosGreenVisitQuestOffered", false);
            Scribe_Values.Look(ref greenVisitRetryOffered, "scpOuroborosGreenVisitRetryOffered", false);
            Scribe_Values.Look(ref signalResearchQuestOffered, "scpOuroborosSignalResearchQuestOffered", false);
            Scribe_Values.Look(ref signalSiteQuestOffered, "scpOuroborosSignalSiteQuestOffered", false);
            Scribe_Values.Look(ref churchCacheQuestOffered, "scpOuroborosChurchCacheQuestOffered", false);
            Scribe_Values.Look(ref churchTrustGranted, "scpOuroborosChurchTrustGranted", false);
            Scribe_Values.Look(ref churchResearchGranted, "scpOuroborosChurchResearchGranted", false);
            Scribe_Values.Look(ref churchPizzaGranted, "scpOuroborosChurchPizzaGranted", false);
            Scribe_Values.Look(ref meetingScientist, "scpOuroborosMeetingScientist");
        }

        private static void OfferFollowup(string defName, bool ready, ref bool offered)
        {
            if (!ready || offered || Find.QuestManager == null) return;
            QuestScriptDef script = DefDatabase<QuestScriptDef>.GetNamedSilentFail(defName);
            if (script == null) return;
            if (Find.QuestManager.QuestsListForReading.Any(quest => quest.root == script))
            { offered = true; return; }
            if (Find.Maps.Any(map => map.IsPlayerHome)
                && QuestUtility.GenerateQuestAndMakeAvailable(script, 1000f) != null) offered = true;
        }

        private void UnloadRecoveredTransponder()
        {
            if (signalRecoveryTick < 0 || signalAnalysisTick >= 0) return;
            foreach (Map home in Find.Maps.Where(map => map.IsPlayerHome))
                foreach (Pawn pawn in home.mapPawns.FreeColonistsSpawned)
                {
                    if (pawn.inventory == null) continue;
                    Thing module = pawn.inventory.innerContainer.FirstOrDefault(thing =>
                        thing.def.defName == "SCP_OuroborosTransponder");
                    if (module == null) continue;
                    Thing dropped;
                    if (pawn.inventory.innerContainer.TryDrop(module, pawn.Position, home,
                        ThingPlaceMode.Near, out dropped) && dropped != null)
                        Messages.Message("SCP_SignalTransponderUnloaded".Translate(), dropped,
                            MessageTypeDefOf.PositiveEvent, false);
                }
        }

        private void GrantChurchCacheRewards()
        {
            if (churchCacheTick < 0) return;
            if (!churchTrustGranted)
            {
                FoundationSupportProgress support = Current.Game.GetComponent<FoundationSupportComponent>().progress;
                support.trust = System.Math.Min(FoundationSupportProgress.MaximumTrust, support.trust + 8);
                churchTrustGranted = true;
            }
            if (!churchResearchGranted)
            {
                ResearchProjectDef fusor = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("SCP_FoundationFusor");
                if (fusor != null && (fusor.IsFinished || fusor.PrerequisitesCompleted))
                {
                    if (!fusor.IsFinished)
                    {
                        float amount = System.Math.Min(600f,
                            System.Math.Max(0f, fusor.CostApparent - Find.ResearchManager.GetProgress(fusor)));
                        if (amount > 0f) Find.ResearchManager.AddProgress(fusor, amount, null);
                    }
                    churchResearchGranted = true;
                }
            }
            if (churchPizzaGranted) return;
            Map home = Find.Maps.FirstOrDefault(map => map.IsPlayerHome && map.mapPawns.FreeColonistsSpawned.Count > 0);
            ThingDef boxDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_458Box");
            if (home == null || boxDef == null) return;
            Thing box = ThingMaker.MakeThing(boxDef);
            Thing placed;
            if (!GenPlace.TryPlaceThing(box, home.mapPawns.FreeColonistsSpawned[0].Position,
                home, ThingPlaceMode.Near, out placed)) { box.Destroy(DestroyMode.Vanish); return; }
            churchPizzaGranted = true;
            ScpFoundation.Definitions.ScpDef article = DefDatabase<ScpFoundation.Definitions.ScpDef>
                .GetNamedSilentFail("SCP_Anomaly458");
            if (article != null)
                Current.Game.GetComponent<ScpFoundation.Knowledge.ScpKnowledgeComponent>()
                    .RecordEncounter(article);
            Messages.Message("SCP_458DeliveryMessage".Translate(), placed,
                MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
