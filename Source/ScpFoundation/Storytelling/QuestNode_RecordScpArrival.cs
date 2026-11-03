using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_RecordScpArrival : QuestNode
    {
        public string number;

        protected override bool TestRunInt(Slate slate) { return !number.NullOrEmpty(); }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_RecordScpArrival {
                number = number,
                inSignalEnable = QuestGen.quest.InitiateSignal
            });
        }
    }

    public sealed class QuestPart_RecordScpArrival : QuestPartActivable
    {
        public string number;
        private bool recorded;

        public override void QuestPartTick()
        {
            if (recorded || State != QuestPartState.Enabled || Current.Game == null) return;
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival(number);
            recorded = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref number, "scpArrivalNumber");
            Scribe_Values.Look(ref recorded, "scpArrivalRecorded", false);
        }
    }
}
