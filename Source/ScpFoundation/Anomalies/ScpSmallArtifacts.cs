using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class Thing_ScpSmallArtifact : ThingWithComps
    {
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(pawn)) yield return option;
            if (!Spawned || pawn == null || !pawn.IsColonistPlayerControlled || !pawn.RaceProps.Humanlike || pawn.Drafted
                || pawn.Dead || pawn.Downed || pawn.jobs == null || pawn.Map != Map) yield break;
            string key = def.defName == "SCP_330Bowl" ? "SCP_330Take"
                : def.defName == "SCP_513Bell" ? "SCP_513Ring"
                : def.defName == "SCP_207Bottle" ? "SCP_207Drink" : null;
            if (key == null) yield break;
            if (!ScpOperationalClearance.Allows(this))
            {
                yield return new FloatMenuOption("SCP_RequiresResearchLevelTwo".Translate(), null);
                yield break;
            }
            bool reachable = pawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some);
            yield return new FloatMenuOption(key.Translate(), reachable ? (Action)(() =>
            {
                JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("SCP_UseSmallArtifact");
                if (jobDef != null) pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, this), JobTag.Misc);
            }) : null);
        }

        public void Use(Pawn pawn)
        {
            if (!Spawned || pawn == null || pawn.Dead || pawn.Map != Map
                || !ScpOperationalClearance.Allows(this)) return;
            if (def.defName == "SCP_330Bowl")
            {
                CompScp330 bowl = GetComp<CompScp330>();
                if (bowl != null) bowl.Take(pawn);
            }
            else if (def.defName == "SCP_513Bell")
            {
                CompScp513 bell = GetComp<CompScp513>();
                if (bell != null) bell.Ring();
            }
            else if (def.defName == "SCP_207Bottle")
            {
                HediffDef effect = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_207Overdrive");
                if (effect == null || pawn.health == null) return;
                if (pawn.health.hediffSet.GetFirstHediffOfDef(effect) == null)
                    pawn.health.AddHediff(effect);
                Destroy(DestroyMode.Vanish);
                Messages.Message("SCP_207Consumed".Translate(pawn.LabelShortCap), pawn,
                    MessageTypeDefOf.NegativeEvent, false);
            }
        }
    }

    public sealed class JobDriver_ScpUseSmallArtifact : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(120, TargetIndex.A);
            yield return Toils_General.Do(() =>
            {
                Thing_ScpSmallArtifact artifact = job.targetA.Thing as Thing_ScpSmallArtifact;
                if (artifact != null) artifact.Use(pawn);
            });
        }
    }

    public sealed class CompProperties_Scp012 : CompProperties
    {
        public CompProperties_Scp012() { compClass = typeof(CompScp012); }
    }

    public sealed class CompScp012 : ThingComp
    {
        public override void CompTickRare()
        {
            if (!parent.Spawned || parent.Map.glowGrid.GroundGlowAt(parent.Position) < 0.15f) return;
            Map map = parent.Map;
            Room room = parent.Position.GetRoom(map);
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike || !pawn.Awake()
                    || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight)
                    || pawn.Position.DistanceToSquared(parent.Position) > 100
                    || pawn.GetRoom() != room || !ScpSurveillance.Sight(pawn.Position, parent.Position, map)) continue;
                if (!pawn.Drafted && pawn.jobs != null && pawn.Position.DistanceToSquared(parent.Position) > 2
                    && (pawn.CurJob == null || pawn.CurJob.def != JobDefOf.Goto
                        || pawn.CurJob.targetA.Thing != parent))
                    pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, parent), JobCondition.InterruptForced);
                if (pawn.Position.DistanceToSquared(parent.Position) <= 2 && Rand.Chance(1f / 6f))
                {
                    BodyPartRecord part = pawn.RaceProps.body.AllParts
                        .Where(candidate => candidate.def.defName == "Hand" && !pawn.health.hediffSet.PartIsMissing(candidate))
                        .InRandomOrder().FirstOrDefault() ?? pawn.RaceProps.body.corePart;
                    pawn.TakeDamage(new DamageInfo(DamageDefOf.Cut, 2f, 0f, -1f, parent, part));
                }
            }
        }
    }

    public sealed class CompProperties_Scp330 : CompProperties
    {
        public CompProperties_Scp330() { compClass = typeof(CompScp330); }
    }

    public sealed class CompScp330 : ThingComp
    {
        private List<int> pawnIds = new List<int>();
        private List<int> counts = new List<int>();
        private List<int> firstTicks = new List<int>();

        public override void PostExposeData()
        {
            Scribe_Collections.Look(ref pawnIds, "scp330PawnIds", LookMode.Value);
            Scribe_Collections.Look(ref counts, "scp330Counts", LookMode.Value);
            Scribe_Collections.Look(ref firstTicks, "scp330FirstTicks", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawnIds = pawnIds ?? new List<int>();
                counts = counts ?? new List<int>();
                firstTicks = firstTicks ?? new List<int>();
                int length = Math.Min(pawnIds.Count, Math.Min(counts.Count, firstTicks.Count));
                pawnIds = pawnIds.Take(length).ToList();
                counts = counts.Take(length).ToList();
                firstTicks = firstTicks.Take(length).ToList();
            }
        }

        public override string CompInspectStringExtra() { return "SCP_330Rule".Translate(); }

        public void Take(Pawn pawn)
        {
            int now = Find.TickManager.TicksGame;
            int index = pawnIds.IndexOf(pawn.thingIDNumber);
            if (index < 0)
            {
                index = pawnIds.Count;
                pawnIds.Add(pawn.thingIDNumber);
                counts.Add(0);
                firstTicks.Add(now);
            }
            if (now - firstTicks[index] >= GenDate.TicksPerDay)
            {
                counts[index] = 0;
                firstTicks[index] = now;
            }
            counts[index]++;
            if (counts[index] > 2)
            {
                foreach (BodyPartRecord hand in pawn.RaceProps.body.AllParts.Where(part => part.def.defName == "Hand"))
                    if (!pawn.health.hediffSet.PartIsMissing(hand))
                        pawn.TakeDamage(new DamageInfo(DamageDefOf.Cut, 100f, 999f, -1f, parent, hand));
                Messages.Message("SCP_330Violation".Translate(pawn.LabelShortCap), pawn,
                    MessageTypeDefOf.ThreatBig, false);
                return;
            }
            string[] colors = { "Yellow", "Green", "Red", "Purple", "Black" };
            ThingDef candyDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_330Candy" + colors[Rand.Range(0, colors.Length)]);
            if (candyDef != null)
            {
                Thing candy = ThingMaker.MakeThing(candyDef);
                GenPlace.TryPlaceThing(candy, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }
            Messages.Message("SCP_330Taken".Translate(pawn.LabelShortCap, counts[index]), pawn,
                MessageTypeDefOf.NeutralEvent, false);
        }
    }

    public sealed class Thing_Scp330Candy : ThingWithComps
    {
        protected override void PostIngested(Pawn ingester)
        {
            base.PostIngested(ingester);
            if (ingester == null || ingester.health == null) return;
            HediffDef effect = DefDatabase<HediffDef>.GetNamedSilentFail(def.defName + "Effect");
            if (effect == null) return;
            Hediff old = ingester.health.hediffSet.GetFirstHediffOfDef(effect);
            if (old != null) ingester.health.RemoveHediff(old);
            ingester.health.AddHediff(effect);
        }
    }

    public sealed class Hediff_Scp330Red : HediffWithComps
    {
        public override void Tick()
        {
            base.Tick();
            if (Find.TickManager.TicksGame % 250 != 0) return;
            foreach (Hediff_Injury injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
                if (injury.Bleeding) injury.Heal(0.1f);
        }
    }

    public sealed class CompProperties_Scp513 : CompProperties
    {
        public CompProperties_Scp513() { compClass = typeof(CompScp513); }
    }

    public sealed class CompScp513 : ThingComp
    {
        public void Ring()
        {
            if (!parent.Spawned) return;
            RingAt(parent.Map, parent.Position);
        }

        public void RingAt(Map map, IntVec3 position)
        {
            if (map == null || !position.IsValid) return;
            HediffDef effect = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_513Haunted");
            if (effect == null) return;
            Room room = position.GetRoom(map);
            int exposed = 0;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Dead || !pawn.RaceProps.Humanlike || pawn.GetRoom() != room
                    || pawn.health == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Hearing)
                    || pawn.Position.DistanceToSquared(position) > 400) continue;
                if (pawn.health.hediffSet.GetFirstHediffOfDef(effect) == null)
                    pawn.health.AddHediff(effect);
                exposed++;
            }
            Messages.Message("SCP_513Rang".Translate(exposed), new LookTargets(position, map),
                MessageTypeDefOf.NegativeEvent, false);
        }
    }

    public sealed class Hediff_Scp513Haunted : HediffWithComps
    {
        public override void Tick()
        {
            base.Tick();
            if (Find.TickManager.TicksGame % 250 != 0) return;
            if (pawn.needs != null && pawn.needs.rest != null)
                pawn.needs.rest.CurLevel = Math.Max(0f, pawn.needs.rest.CurLevel - 0.02f);
            ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("SCP_513Paranoia");
            if (thought != null && pawn.needs != null && pawn.needs.mood != null)
                pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
        }
    }

    public sealed class Hediff_Scp207Overdrive : HediffWithComps
    {
        private bool visitorDose;

        public void MarkVisitorDose() { visitorDose = true; }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref visitorDose, "scp207VisitorDose", false);
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit)
        {
            base.Notify_PawnDied(dinfo, culprit);
            if (!visitorDose || pawn.inventory == null || pawn.inventory.innerContainer == null
                || pawn.MapHeld == null) return;
            Thing bottle = pawn.inventory.innerContainer
                .FirstOrDefault(thing => thing.def.defName == "SCP_207Bottle");
            Thing dropped;
            if (bottle != null)
                pawn.inventory.innerContainer.TryDrop(bottle, pawn.PositionHeld, pawn.MapHeld,
                    ThingPlaceMode.Near, out dropped);
        }

        public override void Tick()
        {
            base.Tick();
            if (Find.TickManager.TicksGame % 250 != 0) return;
            if (pawn.needs != null && pawn.needs.rest != null) pawn.needs.rest.CurLevel = 1f;
            Severity = Math.Min(1f, Severity + 250f / (2f * GenDate.TicksPerDay));
            if (Severity >= 1f)
                HealthUtility.AdjustSeverity(pawn, HediffDefOf.BloodLoss, 0.025f);
        }
    }
}
