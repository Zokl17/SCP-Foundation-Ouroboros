using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ScpFoundation.Interactions
{
    public sealed class Projectile_TranqDart : Bullet
    {
        private const float HumanSizedDose = 0.24f;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Pawn pawn = hitThing as Pawn;
            base.Impact(hitThing, blockedByShield);
            if (blockedByShield || pawn == null || pawn.Dead || pawn.RaceProps == null || !pawn.RaceProps.IsFlesh)
                return;

            float bodySize = pawn.BodySize < 0.1f ? 0.1f : pawn.BodySize;
            HealthUtility.AdjustSeverity(pawn, FoundationMedicalDefOf.SCP_TranqSedation, HumanSizedDose / bodySize);
        }
    }

    [DefOf]
    public static class FoundationMedicalDefOf
    {
        public static HediffDef SCP_TranqSedation;
        public static HediffDef SCP_ContainmentNetting;

        static FoundationMedicalDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FoundationMedicalDefOf));
        }
    }

    public sealed class IngestionOutcomeDoer_Amnestic : IngestionOutcomeDoer
    {
        public int memoriesToRemove = 3;
        public HediffDef sideEffect;

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            MemoryThoughtHandler memoryHandler = null;
            if (pawn.needs != null && pawn.needs.mood != null && pawn.needs.mood.thoughts != null)
                memoryHandler = pawn.needs.mood.thoughts.memories;
            List<Thought_Memory> removable = memoryHandler == null ? null : memoryHandler.Memories
                .Where(memory => memory != null && !memory.permanent && memory.MoodOffset() < 0f)
                .OrderBy(memory => memory.MoodOffset())
                .Take(memoriesToRemove)
                .ToList();

            int removed = 0;
            if (removable != null)
            {
                foreach (Thought_Memory memory in removable)
                {
                    memoryHandler.RemoveMemory(memory);
                    removed++;
                }
            }

            if (sideEffect != null && pawn.health != null)
                pawn.health.AddHediff(sideEffect);

            if (pawn.IsColonistPlayerControlled)
            {
                string message = removed > 0
                    ? "SCP_AmnesticMemoriesRemoved".Translate(pawn.Named("PAWN"), removed)
                    : "SCP_AmnesticNoMemories".Translate(pawn.Named("PAWN"));
                Messages.Message(message, pawn, MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }

    public sealed class IngestionOutcomeDoer_Hemostatic : IngestionOutcomeDoer
    {
        public int woundsToTend = 4;
        public float tendQuality = 0.7f;

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            List<Hediff_Injury> wounds = pawn.health == null || pawn.health.hediffSet == null ? null :
                pawn.health.hediffSet.hediffs
                    .OfType<Hediff_Injury>()
                    .Where(injury => injury.Bleeding && injury.TendableNow(false))
                    .OrderByDescending(injury => injury.BleedRate)
                    .Take(woundsToTend)
                    .ToList();

            int tended = 0;
            if (wounds != null)
            {
                foreach (Hediff_Injury wound in wounds)
                {
                    wound.Tended(tendQuality, tendQuality, 0);
                    tended++;
                }
            }

            if (pawn.IsColonistPlayerControlled)
            {
                string message = tended > 0
                    ? "SCP_HemostaticWoundsTended".Translate(pawn.Named("PAWN"), tended)
                    : "SCP_HemostaticNoWounds".Translate(pawn.Named("PAWN"));
                Messages.Message(message, pawn, MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
