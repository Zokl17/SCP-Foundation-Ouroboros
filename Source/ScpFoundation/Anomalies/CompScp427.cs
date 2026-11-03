using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp427 : CompProperties
    {
        public CompProperties_Scp427() { compClass = typeof(CompScp427); }
    }

    public sealed class CompScp427 : ThingComp
    {
        private const float ExposurePerPulse = 0.1f;
        private bool open;

        private Pawn Wearer { get { Apparel apparel = parent as Apparel; return apparel == null ? null : apparel.Wearer; } }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref open, "scp427Open", false);
        }

        public override string CompInspectStringExtra()
        {
            string status = (open ? "SCP_427Open" : "SCP_427Closed").Translate().ToString();
            Pawn wearer = Wearer;
            HediffDef exposureDef = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_427Exposure");
            Hediff exposure = wearer == null || wearer.health == null
                || wearer.health.hediffSet == null || exposureDef == null
                ? null : wearer.health.hediffSet.GetFirstHediffOfDef(exposureDef);
            return exposure == null ? status : status + "\n"
                + "SCP_427ExposureStatus".Translate(Mathf.RoundToInt(exposure.Severity * 100f)).ToString();
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            Pawn wearer = Wearer;
            if (wearer == null || !wearer.IsColonistPlayerControlled) yield break;
            yield return new Command_Action
            {
                defaultLabel = (open ? "SCP_427Close" : "SCP_427OpenCommand").Translate(),
                defaultDesc = "SCP_427ToggleDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("Things/Item/ScpArtifacts/Scp427Amulet"),
                action = () => open = !open
            };
        }

        public override void CompTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;
            Pawn wearer = Wearer;
            if (!open || wearer == null || wearer.Dead || wearer.health == null
                || wearer.health.hediffSet == null) return;

            HediffDef mutationDef = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_427Mutation");
            if (mutationDef != null && wearer.health.hediffSet.HasHediff(mutationDef)) return;

            foreach (Hediff_Injury wound in wearer.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
            {
                HediffComp_GetsPermanent permanent = wound.GetComp<HediffComp_GetsPermanent>();
                if (permanent == null || !permanent.IsPermanent) wound.Heal(1f);
            }

            foreach (Hediff illness in wearer.health.hediffSet.hediffs.Where(hediff => hediff.def != null
                && (hediff.def.isInfection || hediff.def.makesSickThought || hediff.def.chronic
                    || hediff.def == HediffDefOf.BloodLoss || hediff.def.defName == "SCP_207Overdrive")
                && !(hediff is Hediff_Injury) && !(hediff is Hediff_MissingPart)).ToList())
                illness.Severity = System.Math.Max(0f, illness.Severity - 0.05f);

            HediffDef exposureDef = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_427Exposure");
            if (exposureDef == null || mutationDef == null) return;
            Hediff exposure = wearer.health.hediffSet.GetFirstHediffOfDef(exposureDef);
            if (exposure == null)
            {
                wearer.health.AddHediff(exposureDef);
                exposure = wearer.health.hediffSet.GetFirstHediffOfDef(exposureDef);
            }
            if (exposure == null) return;
            exposure.Severity += ExposurePerPulse;
            if (exposure.Severity < 1f) return;

            wearer.health.RemoveHediff(exposure);
            wearer.health.AddHediff(mutationDef);
            open = false;
            Find.LetterStack.ReceiveLetter("SCP_427MutationTitle".Translate(),
                "SCP_427MutationBody".Translate(wearer.Named("PAWN")), LetterDefOf.ThreatBig, wearer);
        }
    }
}
