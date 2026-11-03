using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ScpFoundation.Interactions
{
    public sealed class IngestionOutcomeDoer_Scp500 : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            Cure(pawn, false);
        }

        public static void Cure(Pawn pawn, bool emergency)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null) return;

            List<Hediff> diseases = pawn.health.hediffSet.hediffs
                .Where(hediff => hediff != null && hediff.def != null
                    && (hediff.def.isInfection || hediff.def.makesSickThought || hediff.def.chronic
                        || hediff.def.defName == "SCP_207Overdrive" || hediff.def == HediffDefOf.BloodLoss)
                    && !(hediff is Hediff_Injury) && !(hediff is Hediff_MissingPart))
                .ToList();
            foreach (Hediff disease in diseases)
                pawn.health.RemoveHediff(disease);

            foreach (Hediff_MissingPart stump in pawn.health.hediffSet.hediffs
                .OfType<Hediff_MissingPart>().Where(part => part.TendableNow(false)).ToList())
                stump.Tended(1f, 1f, 0);

            if (emergency)
                foreach (Hediff_Injury wound in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
                    wound.Heal(wound.Severity);

            HediffDef regenerationDef = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_500Regeneration");
            if (regenerationDef != null)
            {
                HediffWithComps regeneration = pawn.health.hediffSet.GetFirstHediffOfDef(regenerationDef)
                    as HediffWithComps;
                if (regeneration == null)
                    pawn.health.AddHediff(regenerationDef);
                else
                {
                    HediffComp_Disappears duration = regeneration.GetComp<HediffComp_Disappears>();
                    if (duration != null) duration.ResetElapsedTicks();
                }
            }

            if (pawn.IsColonistPlayerControlled)
                Messages.Message((diseases.Count > 0 ? "SCP_500Cured" : "SCP_500NoDisease")
                    .Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.NeutralEvent, false);
        }
    }

    public sealed class HediffCompProperties_Scp500Regeneration : HediffCompProperties
    {
        public float healPerPulse = 1f;
        public int pulseIntervalTicks = 250;

        public HediffCompProperties_Scp500Regeneration()
        {
            compClass = typeof(HediffComp_Scp500Regeneration);
        }
    }

    public sealed class HediffComp_Scp500Regeneration : HediffComp
    {
        private HediffCompProperties_Scp500Regeneration Props
        {
            get { return (HediffCompProperties_Scp500Regeneration)props; }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || pawn.health == null || pawn.health.hediffSet == null
                || Props.pulseIntervalTicks <= 0 || Find.TickManager.TicksGame % Props.pulseIntervalTicks != 0)
                return;

            List<Hediff_Injury> wounds = pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>()
                .Where(injury => injury.Severity > 0f &&
                    (injury.GetComp<HediffComp_GetsPermanent>() == null
                     || !injury.GetComp<HediffComp_GetsPermanent>().IsPermanent))
                .ToList();
            foreach (Hediff_Injury wound in wounds)
                wound.Heal(Props.healPerPulse);
        }
    }
}
