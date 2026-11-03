using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Interactions;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp035 : CompProperties
    {
        public CompProperties_Scp035() { compClass = typeof(CompScp035); }
    }

    public sealed class CompScp035 : ThingComp
    {
        private const int PulseTicks = 250;
        private const int ExposureTicks = 12500;
        private const int DecayTicks = 5000;
        private Pawn currentWearer;
        private Faction originalFaction;
        private bool changedFaction;
        private int wornSince;

        private Apparel Mask { get { return (Apparel)parent; } }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref currentWearer, "scp035Wearer");
            Scribe_References.Look(ref originalFaction, "scp035OriginalFaction");
            Scribe_Values.Look(ref changedFaction, "scp035ChangedFaction", false);
            Scribe_Values.Look(ref wornSince, "scp035WornSince", 0);
        }

        public override void CompTick()
        {
            if (Find.TickManager.TicksGame % PulseTicks != 0) return;
            Pawn wearer = Mask.Wearer;
            if (wearer != currentWearer)
            {
                ReleasePreviousWearer();
                currentWearer = wearer;
                wornSince = Find.TickManager.TicksGame;
                if (wearer != null && !wearer.Dead)
                {
                    if (wearer.Faction == null || !wearer.Faction.HostileTo(Faction.OfPlayer))
                    {
                        originalFaction = wearer.Faction;
                        changedFaction = true;
                        Lord previous = LordUtility.GetLord(wearer);
                        if (previous != null) previous.RemovePawn(wearer);
                        wearer.SetFaction(Faction.OfAncientsHostile);
                        if (wearer.Spawned)
                            LordMaker.MakeNewLord(Faction.OfAncientsHostile,
                                new LordJob_AssaultColony(Faction.OfAncientsHostile,
                                    false, true, false, false, false, false, false),
                                wearer.Map, new List<Pawn> { wearer });
                    }
                    Find.LetterStack.ReceiveLetter("SCP_035PossessionTitle".Translate(),
                        "SCP_035PossessionBody".Translate(wearer.Named("PAWN")), LetterDefOf.ThreatBig, wearer);
                }
            }
            if (wearer != null && !wearer.Dead)
            {
                if (wearer.mindState.mentalStateHandler.CurStateDef == MentalStateDefOf.Berserk)
                    wearer.mindState.mentalStateHandler.Reset();
                Decay(wearer);
                if (wearer.Dead) return;
            }
            Map map = parent.MapHeld;
            if (map == null || wearer != null && wearer.Dead) return;
            IntVec3 position = wearer == null ? parent.Position : wearer.Position;
            HediffDef exposureDef = DefDatabase<HediffDef>.GetNamed("SCP_035Exposure");
            ThoughtDef thoughtDef = DefDatabase<ThoughtDef>.GetNamed("SCP_035MaskCalls");
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == wearer || pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike
                    || pawn.health == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight)) continue;
                bool sees = pawn.Position.DistanceToSquared(position) <= 4f
                    || ScpSurveillance.Sight(pawn.Position, position, map);
                if (!sees) continue;
                if (pawn.needs != null && pawn.needs.mood != null)
                    pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
                Hediff exposure = pawn.health.hediffSet.GetFirstHediffOfDef(exposureDef);
                if (exposure == null)
                {
                    exposure = pawn.health.AddHediff(exposureDef);
                    exposure.Severity = 0f;
                }
                exposure.Severity = Mathf.Min(1f, exposure.Severity + (float)PulseTicks / ExposureTicks);
                if (exposure.Severity > 0.9999f) exposure.Severity = 1f;
                if (exposure.Severity >= 1f && wearer == null && parent.Spawned
                    && pawn.jobs != null && pawn.Awake())
                {
                    if (pawn.CurJob != null && pawn.CurJob.def == JobDefOf.Wear
                        && pawn.CurJob.targetA.Thing == parent) break;
                    if (pawn.CanReserve(parent) && pawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly))
                    {
                        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Wear, parent));
                        break;
                    }
                }
            }
        }

        private void ReleasePreviousWearer()
        {
            if (currentWearer != null && !currentWearer.Dead)
            {
                HediffDef decayDef = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_035HostDecay");
                Hediff decay = decayDef == null ? null : currentWearer.health.hediffSet.GetFirstHediffOfDef(decayDef);
                if (decay != null) currentWearer.health.RemoveHediff(decay);
                if (changedFaction)
                {
                    Lord lord = LordUtility.GetLord(currentWearer);
                    if (lord != null) lord.RemovePawn(currentWearer);
                    currentWearer.SetFaction(originalFaction);
                }
            }
            changedFaction = false;
            originalFaction = null;
        }

        private void Decay(Pawn wearer)
        {
            HediffDef decayDef = DefDatabase<HediffDef>.GetNamed("SCP_035HostDecay");
            Hediff decay = wearer.health.hediffSet.GetFirstHediffOfDef(decayDef);
            if (decay == null) decay = wearer.health.AddHediff(decayDef);
            int elapsed = Find.TickManager.TicksGame - wornSince;
            decay.Severity = Mathf.Min(1f, (float)elapsed / DecayTicks);
            if (elapsed < DecayTicks) return;
            BodyPartRecord brain = wearer.health.hediffSet.GetBrain();
            Mask.Wearer.apparel.TryDrop(Mask);
            if (brain != null && !wearer.Dead)
                wearer.TakeDamage(new DamageInfo(DamageDefOf.Crush, 999f, 999f, -1f, Mask, brain));
            if (!wearer.Dead) wearer.Kill(null);
        }
    }
}
