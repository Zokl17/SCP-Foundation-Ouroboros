namespace ScpFoundation.Storytelling
{
    public enum FoundationContactStage { None, AwaitingReply, ReplyReceived, Established }

    // One conversation per game; unrelated to SCP knowledge or ending readiness.
    public sealed class FoundationContactProgress
    {
        public const int ReplyDelay = 15000;
        public const int StatueDelay = 15000;
        public FoundationContactStage stage;
        public bool initiatedByFoundation;
        public bool terminalHintShown;
        public int firstOnlineTick = -1;
        public bool CanIntroduceStatue(int now, bool online)
        {
            if (online && firstOnlineTick < 0) firstOnlineTick = now;
            return online && firstOnlineTick >= 0 && (long)now - firstOnlineTick >= StatueDelay;
        }
        public bool Introduce(bool online)
        {
            if (!online || terminalHintShown || stage != FoundationContactStage.None) return false;
            terminalHintShown = true;
            return true;
        }
        public int sentTick = -1, receivedTick = -1, confirmedTick = -1;

        public bool Request(int now, bool online, bool statueDiscovered)
        {
            if (!online || !statueDiscovered || now < 0 || stage != FoundationContactStage.None) return false;
            sentTick = now;
            stage = FoundationContactStage.AwaitingReply;
            return true;
        }
        public bool Deliver(int now, bool online)
        {
            if (!online || stage != FoundationContactStage.AwaitingReply || sentTick < 0
                || (long)now - sentTick < ReplyDelay) return false;
            receivedTick = now;
            stage = FoundationContactStage.ReplyReceived;
            return true;
        }
        public bool Confirm(int now, bool online)
        {
            if (!online || stage != FoundationContactStage.ReplyReceived || now < receivedTick) return false;
            confirmedTick = now;
            stage = FoundationContactStage.Established;
            return true;
        }
    }
}
