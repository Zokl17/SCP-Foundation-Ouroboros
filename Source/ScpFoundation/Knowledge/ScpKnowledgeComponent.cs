using System.Collections.Generic;
using ScpFoundation.Definitions;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Knowledge
{
    public enum KnowledgeLevel { Unknown, Observed, PartiallyDocumented, Documented }

    public sealed class ScpKnowledgeRecord : IExposable
    {
        public ScpDef anomaly;
        public KnowledgeLevel level;
        public float researchWork;
        public float experimentWork;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref anomaly, "anomaly");
            Scribe_Values.Look(ref level, "level", KnowledgeLevel.Unknown);
            Scribe_Values.Look(ref researchWork, "researchWork", 0f);
            Scribe_Values.Look(ref experimentWork, "experimentWork", 0f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && anomaly != null && anomaly.research != null)
            {
                researchWork = ResearchModel.Normalize(researchWork, anomaly.research.fullWork);
                // Older builds granted Observed immediately on capture. Re-evaluate only that
                // legacy level against actual work; higher earned dossiers remain preserved.
                if (level == KnowledgeLevel.Observed && researchWork < anomaly.research.observedWork)
                    level = KnowledgeLevel.Unknown;
                researchWork = System.Math.Max(ResearchModel.MinimumWorkForLevel((int)level,
                    anomaly.research.observedWork, anomaly.research.partialWork, anomaly.research.fullWork), researchWork);
                experimentWork = ResearchModel.Normalize(experimentWork, anomaly.research.observedWork * 3f);
            }
        }
    }

    // Definition-level knowledge survives loss/destruction of physical instances.
    public sealed class ScpKnowledgeComponent : GameComponent
    {
        private List<ScpKnowledgeRecord> records = new List<ScpKnowledgeRecord>();
        private readonly Dictionary<ScpDef, ScpKnowledgeRecord> index = new Dictionary<ScpDef, ScpKnowledgeRecord>();
        private HashSet<ScpDef> encountered = new HashSet<ScpDef>();
        private int scp093MapLootMask;
        private int scp093ArchiveRewardMask;
        public ScpKnowledgeComponent(Game game) { }
        public bool ClaimScp093FirstMapLoot(int variant)
        {
            if (variant < 0 || variant > 4 || (scp093MapLootMask & (1 << variant)) != 0) return false;
            scp093MapLootMask |= 1 << variant;
            return true;
        }
        public bool ClaimScp093ArchiveReward(int variant)
        {
            if (variant < 0 || variant > 4 || (scp093ArchiveRewardMask & (1 << variant)) != 0) return false;
            scp093ArchiveRewardMask |= 1 << variant;
            return true;
        }
        public bool HasEncountered(ScpDef anomaly) { return encountered.Contains(anomaly); }
        public void RecordEncounter(ScpDef anomaly) { encountered.Add(anomaly); }

        public float ResearchWork(ScpDef anomaly)
        {
            ScpKnowledgeRecord record;
            return index.TryGetValue(anomaly, out record) ? record.researchWork : 0;
        }

        public float ExperimentWork(ScpDef anomaly)
        {
            ScpKnowledgeRecord record;
            return anomaly != null && index.TryGetValue(anomaly, out record) ? record.experimentWork : 0f;
        }

        public int ExperimentStageFor(ScpDef anomaly)
        {
            return anomaly != null && anomaly.research != null
                ? ResearchModel.ExperimentStage(ExperimentWork(anomaly), anomaly.research.observedWork) : 0;
        }

        public bool ExperimentsComplete(ScpDef anomaly)
        {
            return ExperimentStageFor(anomaly) >= 3;
        }

        public bool NeedsResearch(ScpDef anomaly)
        {
            return anomaly != null && (!ExperimentsComplete(anomaly)
                || anomaly.scpId == "SCP-055" && LevelFor(anomaly) < KnowledgeLevel.Documented);
        }

        public bool DevAdvanceKnowledge(ScpDef anomaly)
        {
            if (anomaly == null || anomaly.research == null) return false;
            KnowledgeLevel level = LevelFor(anomaly);
            if (level < KnowledgeLevel.Documented)
            {
                SetLevel(anomaly, (KnowledgeLevel)((int)level + 1));
                return true;
            }
            int stage = ExperimentStageFor(anomaly);
            return stage < 3 && AddResearch(anomaly,
                (stage + 1) * anomaly.research.observedWork - ExperimentWork(anomaly));
        }

        public bool AddResearch(ScpDef anomaly, float amount)
        {
            if (anomaly == null || anomaly.research == null) return false;
            if (!index.ContainsKey(anomaly)) SetLevel(anomaly, KnowledgeLevel.Unknown);
            ScpKnowledgeRecord record = index[anomaly];
            if (record.level >= KnowledgeLevel.Documented)
            {
                float stageWork = anomaly.research.observedWork;
                int previousStage = ResearchModel.ExperimentStage(record.experimentWork, stageWork);
                record.experimentWork = ResearchModel.Advance(record.experimentWork, amount, stageWork * 3f);
                int completedStage = ResearchModel.ExperimentStage(record.experimentWork, stageWork);
                if (completedStage <= previousStage) return false;
                FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
                if (support != null)
                    for (int stage = previousStage + 1; stage <= completedStage; stage++)
                        support.progress.trust = System.Math.Min(FoundationSupportProgress.MaximumTrust,
                            support.progress.trust + ResearchModel.ExperimentTrust(anomaly.objectClass, stage));
                return true;
            }
            record.researchWork = ResearchModel.Advance(record.researchWork, amount, anomaly.research.fullWork);
            KnowledgeLevel next = (KnowledgeLevel)ResearchModel.Level(record.researchWork,
                anomaly.research.observedWork, anomaly.research.partialWork, anomaly.research.fullWork);
            if (next <= record.level) return false;
            record.level = next;
            return true;
        }

        public bool DegradeScp055ToObserved(ScpDef anomaly, ScpDef scp055)
        {
            if (anomaly == null || anomaly != scp055 || anomaly.research == null) return false;
            ScpKnowledgeRecord record;
            if (!index.TryGetValue(anomaly, out record) || record.level <= KnowledgeLevel.Observed) return false;
            record.level = KnowledgeLevel.Observed;
            record.researchWork = anomaly.research.observedWork;
            return true;
        }
        public int DiscoveredCount
        {
            get
            {
                int count = 0;
                foreach (ScpKnowledgeRecord record in records)
                    if (record.level > KnowledgeLevel.Unknown && !record.anomaly.archiveOnly) count++;
                return count;
            }
        }

        public KnowledgeLevel LevelFor(ScpDef anomaly)
        {
            ScpKnowledgeRecord record;
            return anomaly != null && index.TryGetValue(anomaly, out record) ? record.level : KnowledgeLevel.Unknown;
        }

        public void SetLevel(ScpDef anomaly, KnowledgeLevel level)
        {
            if (anomaly == null) return;
            ScpKnowledgeRecord record;
            if (!index.TryGetValue(anomaly, out record))
            {
                record = new ScpKnowledgeRecord { anomaly = anomaly };
                records.Add(record);
                index.Add(anomaly, record);
            }
            record.level = level;
            if (anomaly.research != null)
                record.researchWork = System.Math.Max(record.researchWork,
                    ResearchModel.MinimumWorkForLevel((int)level, anomaly.research.observedWork,
                        anomaly.research.partialWork, anomaly.research.fullWork));
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref records, "scpKnowledge", LookMode.Deep);
            Scribe_Collections.Look(ref encountered, "scpEncountered", LookMode.Def);
            Scribe_Values.Look(ref scp093MapLootMask, "scp093MapLootMask", 0);
            Scribe_Values.Look(ref scp093ArchiveRewardMask, "scp093ArchiveRewardMask", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (records == null) records = new List<ScpKnowledgeRecord>();
                if (encountered == null) encountered = new HashSet<ScpDef>();
                encountered.Remove(null);
                index.Clear();
                records.RemoveAll(record => record == null || record.anomaly == null);
                foreach (ScpKnowledgeRecord record in records) index[record.anomaly] = record;
            }
        }
    }
}
