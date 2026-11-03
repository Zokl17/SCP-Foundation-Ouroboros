using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using ScpFoundation.Interactions;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_ScpOuroborosOpening : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            return map != null && map.IsPlayerHome
                && Current.Game.GetComponent<FoundationContactComponent>().progress.stage == FoundationContactStage.Established
                && CompFoundationAccessTerminal.Online(map) != null;
        }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_ScpOuroborosOpening {
                map = QuestGen.slate.Get<Map>("map"),
                inSignalEnable = QuestGen.quest.InitiateSignal,
                decodedSignal = QuestGenUtility.HardcodedSignalWithQuestID("OuroborosOpeningDecoded")
            });
        }
    }

    public sealed class QuestPart_ScpOuroborosOpening : QuestPartActivable
    {
        public Map map;
        public string decodedSignal;
        private int poweredTicks;
        private bool started;
        private bool completed;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "scpOuroborosMap");
            Scribe_Values.Look(ref decodedSignal, "scpOuroborosDecodedSignal");
            Scribe_Values.Look(ref poweredTicks, "scpOuroborosPoweredTicks", 0);
            Scribe_Values.Look(ref started, "scpOuroborosStarted", false);
            Scribe_Values.Look(ref completed, "scpOuroborosCompleted", false);
        }

        public override void QuestPartTick()
        {
            if (State != QuestPartState.Enabled || completed || map == null
                || Find.TickManager.TicksGame % 250 != 0) return;
            if (!started)
            {
                started = true;
                Find.LetterStack.ReceiveLetter("SCP_OuroborosStartedTitle".Translate(),
                    "SCP_OuroborosStartedBody".Translate(), LetterDefOf.NeutralEvent,
                    new LookTargets(map.Center, map));
            }
            if (CompFoundationAccessTerminal.Online(map) == null) return;
            poweredTicks += 250;
            if (poweredTicks < 60000) return;
            completed = true;
            Current.Game.GetComponent<ScpOuroborosArchiveComponent>().UnlockDecoded(Find.TickManager.TicksGame);
            foreach (QuestPart_Letter part in quest.PartsListForReading.OfType<QuestPart_Letter>())
                if (part.inSignal == decodedSignal && part.letter != null)
                {
                    part.letter.Label = "SCP_OuroborosDecodedTitle".Translate();
                    ChoiceLetter choice = part.letter as ChoiceLetter;
                    if (choice != null) choice.Text = "SCP_OuroborosArchiveNotice".Translate();
                }
            Find.SignalManager.SendSignal(new Signal(decodedSignal));
        }
    }
}
