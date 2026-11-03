using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Runtime;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp055 : CompProperties
    {
        public CompProperties_Scp055() { compClass = typeof(CompScp055); }
    }

    public sealed class CompScp055 : ThingComp
    {
        private readonly AntimemeticMemoryProgress memory = new AntimemeticMemoryProgress();
        private bool firstForgettingLetterSent;
        private Pawn lastResearcher;

        public override void CompTickRare()
        {
            if (!parent.Spawned) return;
            Pawn researcher = parent.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(pawn => pawn.CurJob != null
                && pawn.CurJob.def == ScpInteractionDefOf.SCP_Study && pawn.CurJob.targetA.Thing == parent);
            if (researcher != null) lastResearcher = researcher;
            CompScp anomaly = parent.TryGetComp<CompScp>();
            if (anomaly == null || anomaly.Definition != ScpInteractionDefOf.SCP_Anomaly055
                || !memory.Update(Find.TickManager.TicksGame, researcher != null)) return;
            ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
            if (!knowledge.DegradeScp055ToObserved(anomaly.Definition, ScpInteractionDefOf.SCP_Anomaly055)) return;
            if (firstForgettingLetterSent) return;
            firstForgettingLetterSent = true;
            Find.LetterStack.ReceiveLetter("SCP_055ForgottenTitle".Translate(), "SCP_055ForgottenBody".Translate(),
                LetterDefOf.NeutralEvent, parent);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref memory.lastStudyTick, "scp055LastStudyTick", -1);
            Scribe_Values.Look(ref firstForgettingLetterSent, "scp055ForgettingLetterSent", false);
            Scribe_References.Look(ref lastResearcher, "scp055LastResearcher");
        }

        public string DossierText(string reportKey)
        {
            string text = reportKey.Translate();
            if (reportKey != "SCP_055Documented") return text;
            Pawn subject = lastResearcher != null && !lastResearcher.Destroyed ? lastResearcher : parent.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            Pawn interviewer = parent.Map.mapPawns.FreeColonistsSpawned.Where(pawn => pawn != subject)
                .OrderByDescending(pawn => pawn.skills == null ? 0 : pawn.skills.GetSkill(SkillDefOf.Intellectual).Level).FirstOrDefault();
            string interviewerName = interviewer == null ? "SCP_055FallbackInterviewer".Translate().ToString() : interviewer.LabelShortCap.ToString();
            string subjectName = subject == null ? "SCP_055FallbackSubject".Translate().ToString() : subject.LabelShortCap.ToString();
            return text + "\n\n" + "SCP_055Interview".Translate(interviewerName, subjectName);
        }
    }
}
