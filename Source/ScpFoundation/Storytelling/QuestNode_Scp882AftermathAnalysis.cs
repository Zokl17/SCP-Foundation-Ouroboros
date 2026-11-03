using RimWorld;
using RimWorld.QuestGen;
using ScpFoundation.Interactions;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_Scp882AftermathAnalysis : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            return map != null && map.IsPlayerHome
                && Current.Game.GetComponent<ScpOuroborosArchiveComponent>().AftermathSecured;
        }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_Scp882AftermathAnalysis
            {
                map = QuestGen.slate.Get<Map>("map"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                completedSignal = QuestGenUtility.HardcodedSignalWithQuestID("Scp882AnalysisCompleted")
            });
        }
    }

    public sealed class QuestPart_Scp882AftermathAnalysis : QuestPartActivable
    {
        public Map map;
        public string completedSignal;
        private int analysisTicks;
        private bool completed;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "scp882AnalysisMap");
            Scribe_Values.Look(ref completedSignal, "scp882AnalysisCompletedSignal");
            Scribe_Values.Look(ref analysisTicks, "scp882AnalysisTicks", 0);
            Scribe_Values.Look(ref completed, "scp882AnalysisCompleted", false);
        }

        public override void QuestPartTick()
        {
            if (State != QuestPartState.Enabled || completed || map == null
                || Find.TickManager.TicksGame % 250 != 0) return;
            if (!ScpOuroborosArchiveComponent.HasContained882(map)
                || CompFoundationAccessTerminal.Online(map) == null) return;
            analysisTicks += 250;
            if (analysisTicks < 15000) return;
            completed = true;
            Current.Game.GetComponent<ScpOuroborosArchiveComponent>().UnlockAnalysis(Find.TickManager.TicksGame);
            Find.SignalManager.SendSignal(new Signal(completedSignal));
        }
    }
}
