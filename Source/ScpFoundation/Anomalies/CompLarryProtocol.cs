using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Verse.AI;
using ScpFoundation.Storytelling;

namespace ScpFoundation.Anomalies
{
    public sealed class CompScp106BaitContainer : CompThingContainer
    {
        public Pawn Bait { get { return ContainedThing as Pawn; } }

        public override string CompInspectStringExtra()
        {
            CompLarryProtocol chamber = parent.GetComp<CompLarryProtocol>();
            return chamber != null && chamber.Contains106
                ? "SCP_106ChamberOccupant".Translate().ToString() : base.CompInspectStringExtra();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield break;
        }
    }

    public sealed class CompProperties_LarryProtocol : CompProperties
    {
        public int safeDistanceCells = 30;
        public CompProperties_LarryProtocol() { compClass = typeof(CompLarryProtocol); }
    }

    public sealed class CompLarryProtocol : ThingComp
    {
        private bool contained;
        private bool breached;
        private bool legacyOccupied;
        private int nextDayTick, neglectedDays;
        private IntVec3 lureCell = IntVec3.Invalid;
        private int questCaptureTick;
        private Pawn protocolBait;
        private Mote protocolGlow;
        private Effecter protocolHeadGlow;

        public int QuestCaptureTick { get { return questCaptureTick; } }
        public bool Contains106 { get { return contained; } }
        public bool CanReceiveDownedSCP { get { return parent.Spawned && !contained && PowerOn
            && protocolBait == null && BaitContainer != null && BaitContainer.Empty; } }
        private CompScp106BaitContainer BaitContainer { get { return parent.GetComp<CompScp106BaitContainer>(); } }
        private bool BaitReady { get { return parent.Spawned && !contained && PowerOn
            && protocolBait != null && BaitContainer != null && BaitContainer.Bait == protocolBait; } }
        public bool QuestLureReady
        {
            get { return BaitReady && QuestPart_Scp106Recovery.ActiveFor(parent.Map); }
        }

        public static IntVec3 ActiveBaitCell(Map map)
        {
            if (map == null) return IntVec3.Invalid;
            ThingDef chamber = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_106BaitStation");
            if (chamber == null) return IntVec3.Invalid;
            CompLarryProtocol active = map.listerThings.ThingsOfDef(chamber)
                .Select(t => t.TryGetComp<CompLarryProtocol>()).FirstOrDefault(comp => comp != null && comp.BaitReady);
            return active == null ? IntVec3.Invalid
                : active.lureCell.IsValid ? active.lureCell : active.parent.InteractionCell;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref contained, "scp106Contained", true);
            Scribe_Values.Look(ref breached, "scp106Breached", !contained);
            Scribe_Values.Look(ref legacyOccupied, "scp106LegacyOccupied", true);
            Scribe_Values.Look(ref nextDayTick, "scp106NextDay", 0);
            Scribe_Values.Look(ref neglectedDays, "scp106NeglectedDays", 0);
            Scribe_Values.Look(ref lureCell, "scp106LureCell", IntVec3.Invalid);
            Scribe_Values.Look(ref questCaptureTick, "scp106QuestCaptureTick", 0);
            Scribe_References.Look(ref protocolBait, "scp106ProtocolBait");
        }

        public override string CompInspectStringExtra()
        {
            string status = "SCP_106ChamberStatus".Translate(contained ? "SCP_106Contained".Translate()
                : breached ? "SCP_106Breached".Translate() : "SCP_106Empty".Translate(), neglectedDays);
            if (protocolBait != null)
                status += "\n" + "SCP_106ProtocolWaiting".Translate(protocolBait.LabelShortCap);
            if (!contained || !parent.Spawned) return status;
            int radius = ((CompProperties_LarryProtocol)props).safeDistanceCells;
            bool personnelNearby = parent.Map.mapPawns.FreeColonistsSpawned.Any(pawn =>
                !pawn.Dead && pawn.Position.DistanceToSquared(parent.Position) < radius * radius);
            return personnelNearby ? status + "\n" + "SCP_106SafetyDistance".Translate(radius).ToString() : status;
        }

        private bool PowerOn
        {
            get { CompPowerTrader power = parent.GetComp<CompPowerTrader>(); return power != null && power.PowerOn; }
        }

        public override void CompTick()
        {
            if (!parent.Spawned) return;
            if (protocolBait == null && BaitContainer != null && BaitContainer.Bait != null)
                protocolBait = BaitContainer.Bait;
            if (protocolBait != null) TickProtocol();
            if (Find.TickManager.TicksGame % 250 != 0) return;
            if (!contained) return;
            KeepPersonnelOutside();
            int now = Find.TickManager.TicksGame;
            if (nextDayTick == 0)
                nextDayTick = now + 60000;
            while (now >= nextDayTick && contained)
            {
                bool powered = PowerOn;
                neglectedDays = powered ? 0 : neglectedDays + 1;
                nextDayTick += 60000;
                float breachChance = 1f - (float)Math.Pow(0.8, neglectedDays);
                if (neglectedDays > 0 && Rand.Chance(breachChance)) Breach();
            }
        }

        private void KeepPersonnelOutside()
        {
            Map map = parent.Map;
            IntVec3 center = parent.Position;
            int radius = ((CompProperties_LarryProtocol)props).safeDistanceCells;
            int radiusSquared = radius * radius;
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.Dead || pawn.Downed || pawn.Drafted || pawn.InMentalState || pawn.jobs == null
                    || pawn.Position.DistanceToSquared(center) >= radiusSquared) continue;
                if (pawn.CurJob != null && pawn.CurJob.def == JobDefOf.Goto && pawn.CurJob.targetA.IsValid
                    && pawn.CurJob.targetA.Cell.DistanceToSquared(center) >= radiusSquared) continue;
                int dx = pawn.Position.x - center.x;
                int dz = pawn.Position.z - center.z;
                double distance = Math.Sqrt(dx * dx + dz * dz);
                if (distance < 1d) { dx = 1; dz = 0; distance = 1d; }
                IntVec3 desired = new IntVec3(center.x + (int)Math.Round(dx * (radius + 2) / distance), 0,
                    center.z + (int)Math.Round(dz * (radius + 2) / distance));
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(desired, 6f, true))
                {
                    if (!cell.InBounds(map) || !cell.Standable(map)
                        || cell.DistanceToSquared(center) < radiusSquared
                        || !map.reachability.CanReach(pawn.Position, cell, PathEndMode.OnCell,
                            TraverseParms.For(pawn))) continue;
                    pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobCondition.InterruptForced);
                    break;
                }
            }
        }

        public bool Breach()
        {
            if (!parent.Spawned || breached) return false;
            ThingDef definition = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_106Pawn");
            if (definition == null) return false;
            Map map = parent.Map;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, 12f, true))
            {
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map)
                    || cell.GetThingList(map).Any(t => t is Pawn || t.def.category == ThingCategory.Building)) continue;
                Pawn_Scp106 entity = Pawn_Scp106.Generate();
                if (entity == null) return false;
                GenSpawn.Spawn(entity, cell, map);
                if (PowerOn) entity.DelayHunt(1250);
                bool wasContained = contained;
                contained = false;
                if (wasContained) Current.Game.GetComponent<ScpArrivalRegistry>().Set106Contained(false);
                breached = true;
                Find.LetterStack.ReceiveLetter((wasContained ? "SCP_106BreachTitle" : "SCP_106ArrivalTitle").Translate(),
                    (wasContained ? (PowerOn ? "SCP_106BreachBodyLit" : "SCP_106BreachBody")
                        : "SCP_106ArrivalBody").Translate(),
                    LetterDefOf.ThreatBig, parent);
                return true;
            }
            return false;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            if (!parent.Spawned) yield break;
            if (contained && legacyOccupied)
                yield return new Command_Action { defaultLabel = "SCP_106MarkEmpty".Translate(),
                    defaultDesc = "SCP_106MarkEmptyDesc".Translate(), action = () => {
                        contained = false; breached = false; legacyOccupied = false;
                        neglectedDays = 0; nextDayTick = 0;
                    } };
            bool recoveryQuest = QuestPart_Scp106Recovery.ActiveFor(parent.Map);
            Command_Action recall = new Command_Action { defaultLabel = "SCP_106ProtocolCommand".Translate(),
                defaultDesc = "SCP_106ProtocolDescription".Translate(), action = BeginBaitSelection };
            if (contained) recall.Disable("SCP_106AlreadyContained".Translate());
            else if (protocolBait != null || BaitContainer != null && !BaitContainer.Empty)
                recall.Disable("SCP_106ProtocolRunning".Translate());
            else if (!PowerOn) recall.Disable("SCP_106NoPower".Translate());
            else if (!recoveryQuest && FindActiveEntity() == null
                && !parent.Map.mapPawns.AllPawnsSpawned.OfType<Pawn_Scp106>().Any(p => !p.Dead))
                recall.Disable("SCP_106NoActiveBreach".Translate());
            yield return recall;
        }

        private Thing FindActiveEntity()
        {
            if (!parent.Spawned) return null;
            ThingDef definition = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_106Entity");
            return definition == null ? null : parent.Map.listerThings.ThingsOfDef(definition).FirstOrDefault();
        }

        private void BeginBaitSelection()
        {
            if (!parent.Spawned || contained || !PowerOn || protocolBait != null) return;
            TargetingParameters parameters = TargetingParameters.ForPawns();
            parameters.validator = target => IsValidBait(target.Thing as Pawn);
            Find.Targeter.BeginTargeting(parameters, target =>
            {
                Pawn victim = target.Pawn;
                if (!IsValidBait(victim)) return;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "SCP_106ProtocolConfirm".Translate(victim.Named("PAWN")), () => DispatchBait(victim)));
            });
        }

        private bool IsValidBait(Pawn victim)
        {
            return victim != null && victim.Spawned && !victim.Dead && victim.Map == parent.Map
                && (victim.Faction == Faction.OfPlayer || victim.IsPrisonerOfColony);
        }

        private void DispatchBait(Pawn victim)
        {
            if (!IsValidBait(victim) || contained || !PowerOn || protocolBait != null
                || !QuestPart_Scp106Recovery.ActiveFor(parent.Map) && FindActiveEntity() == null
                    && !parent.Map.mapPawns.AllPawnsSpawned.OfType<Pawn_Scp106>().Any(p => !p.Dead)) return;
            Pawn worker = victim.Faction == Faction.OfPlayer && !victim.Downed && !victim.InMentalState
                ? victim : parent.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p =>
                    !p.Downed && !p.Drafted && !p.InMentalState && p.jobs != null
                    && p.CanReserveAndReach(victim, PathEndMode.Touch, Danger.Some));
            if (worker == null)
            {
                Messages.Message("SCP_106NoCarrier".Translate(), parent, MessageTypeDefOf.RejectInput);
                return;
            }
            IntVec3 cell = GenRadial.RadialCellsAround(parent.Position, 4f, true)
                .FirstOrDefault(c => c.InBounds(parent.Map) && c.Standable(parent.Map)
                    && !parent.OccupiedRect().Contains(c) && !c.GetThingList(parent.Map).Any(t => t is Pawn)
                    && parent.Map.reachability.CanReach(worker.Position, c, PathEndMode.OnCell,
                        TraverseParms.For(worker)));
            if (!cell.IsValid || cell == IntVec3.Zero)
            {
                Messages.Message("SCP_106QuestNoBaitCell".Translate(), parent, MessageTypeDefOf.RejectInput);
                return;
            }
            JobDef def = DefDatabase<JobDef>.GetNamed("SCP_106ProtocolBait");
            Job job = JobMaker.MakeJob(def, victim, parent, cell);
            if (!worker.jobs.TryTakeOrderedJob(job))
                Messages.Message("SCP_106NoCarrier".Translate(), parent, MessageTypeDefOf.RejectInput);
        }

        public void DispatchDownedSCP(Pawn worker, Pawn_Scp106 entity)
        {
            if (!CanReceiveDownedSCP || worker == null || worker.Downed || worker.Map != parent.Map
                || entity == null || !entity.CanBeContained || !entity.Spawned
                || !worker.CanReserveAndReach(entity, PathEndMode.Touch, Danger.Deadly)) return;
            IntVec3 cell = GenRadial.RadialCellsAround(parent.Position, 4f, true)
                .FirstOrDefault(c => c.InBounds(parent.Map) && c.Standable(parent.Map)
                    && !parent.OccupiedRect().Contains(c)
                    && parent.Map.reachability.CanReach(worker.Position, c, PathEndMode.OnCell,
                        TraverseParms.For(worker)));
            if (!cell.IsValid || cell == IntVec3.Zero) return;
            JobDef def = DefDatabase<JobDef>.GetNamed("SCP_106CarryToChamber");
            Job job = JobMaker.MakeJob(def, entity, parent, cell);
            job.count = 1;
            worker.jobs.TryTakeOrderedJob(job);
        }

        public bool ContainDownedSCP(Pawn_Scp106 entity)
        {
            if (!CanReceiveDownedSCP || entity == null || !entity.CanBeContained) return false;
            if (entity.Map != parent.Map && entity.holdingOwner == null) return false;
            if (QuestPart_Scp106Recovery.ActiveFor(parent.Map))
                questCaptureTick = Find.TickManager.TicksGame;
            entity.Destroy(DestroyMode.Vanish);
            contained = true;
            Current.Game.GetComponent<ScpArrivalRegistry>().Set106Contained(true);
            breached = false;
            legacyOccupied = false;
            neglectedDays = 0;
            nextDayTick = Find.TickManager.TicksGame + 60000;
            Find.LetterStack.ReceiveLetter("SCP_106CarryCompleteTitle".Translate(),
                "SCP_106CarryCompleteBody".Translate(), LetterDefOf.PositiveEvent, parent);
            return true;
        }

        public bool BeginProtocol(Pawn victim)
        {
            if (!IsValidBait(victim) || contained || !PowerOn || protocolBait != null
                || BaitContainer == null || !BaitContainer.Empty
                || victim.Position.DistanceToSquared(parent.Position) > 25) return false;
            Map map = parent.Map;
            IntVec3 cell = victim.Position;
            victim.DeSpawnOrDeselect();
            if (!BaitContainer.GetDirectlyHeldThings().TryAdd(victim, true))
            {
                if (!victim.Spawned && victim.holdingOwner == null)
                    GenSpawn.Spawn(victim, cell, map);
                return false;
            }
            protocolBait = victim;
            lureCell = cell;
            SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail("SCP_106FemurBreaker");
            if (sound != null) sound.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
            return true;
        }

        private void TickProtocol()
        {
            if (protocolBait.Dead || BaitContainer == null || BaitContainer.Bait != protocolBait || !PowerOn)
            {
                CancelProtocol();
                return;
            }
            Thing entity = FindActiveEntity();
            Pawn_Scp106 legacy = parent.Map.mapPawns.AllPawnsSpawned.OfType<Pawn_Scp106>()
                .FirstOrDefault(pawn => !pawn.Dead);
            if (entity == null && legacy == null && !QuestPart_Scp106Recovery.ActiveFor(parent.Map))
            {
                CancelProtocol();
                return;
            }
            CellRect captureArea = parent.OccupiedRect().ExpandedBy(1);
            if (entity != null && captureArea.Contains(entity.Position)
                || legacy != null && captureArea.Contains(legacy.Position))
            {
                Execute(protocolBait, entity, legacy);
                return;
            }
            ThingDef glowDef = DefDatabase<ThingDef>.GetNamedSilentFail("RipScannerGlow_South");
            if (glowDef != null)
            {
                if (protocolGlow == null || protocolGlow.Destroyed)
                    protocolGlow = MoteMaker.MakeAttachedOverlay(parent, glowDef, Vector3.zero, 0.7f);
                if (protocolGlow != null) protocolGlow.Maintain();
            }
            EffecterDef headGlowDef = DefDatabase<EffecterDef>.GetNamedSilentFail("RipScannerHeadGlow");
            if (headGlowDef != null)
            {
                if (protocolHeadGlow == null) protocolHeadGlow = headGlowDef.Spawn();
                TargetInfo target = new TargetInfo(parent);
                protocolHeadGlow.EffectTick(target, target);
            }
        }

        private void CancelProtocol()
        {
            if (BaitContainer != null && parent.Spawned && !BaitContainer.Empty)
                BaitContainer.GetDirectlyHeldThings().TryDropAll(parent.InteractionCell, parent.Map,
                    ThingPlaceMode.Near);
            protocolBait = null;
            lureCell = IntVec3.Invalid;
            CleanupProtocolEffects();
        }

        private void CleanupProtocolEffects()
        {
            protocolGlow = null;
            if (protocolHeadGlow == null) return;
            protocolHeadGlow.Cleanup();
            protocolHeadGlow = null;
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (map != null && BaitContainer != null && !BaitContainer.Empty)
                BaitContainer.GetDirectlyHeldThings().TryDropAll(parent.Position, map, ThingPlaceMode.Near);
            protocolBait = null;
            lureCell = IntVec3.Invalid;
            if (contained && map != null)
            {
                Pawn_Scp106 entity = Pawn_Scp106.Generate();
                if (entity != null)
                {
                    IntVec3 cell = GenRadial.RadialCellsAround(parent.Position, 12f, true)
                        .FirstOrDefault(c => c.InBounds(map) && c.Standable(map)
                            && !c.GetThingList(map).Any(t => t is Pawn));
                    if (!cell.IsValid || cell == IntVec3.Zero) cell = parent.Position;
                    GenSpawn.Spawn(entity, cell, map);
                    contained = false;
                    breached = true;
                    if (Current.Game != null)
                        Current.Game.GetComponent<ScpArrivalRegistry>().Set106Contained(false);
                    Find.LetterStack.ReceiveLetter("SCP_106BreachTitle".Translate(),
                        "SCP_106BreachBody".Translate(), LetterDefOf.ThreatBig, entity);
                }
            }
            CleanupProtocolEffects();
            base.PostDeSpawn(map, mode);
        }

        private void Execute(Pawn victim, Thing entity, Pawn_Scp106 legacy)
        {
            if (contained || !parent.Spawned || !PowerOn || victim == null
                || BaitContainer == null || BaitContainer.Bait != victim
                || entity == null && legacy == null) return;
            DamageDef acid = DefDatabase<DamageDef>.GetNamedSilentFail("AcidBurn");
            if (acid != null) victim.TakeDamage(new DamageInfo(acid, 6f, 0f, -1f, entity ?? (Thing)legacy));
            if (!victim.Dead) victim.Kill(null);
            if (!victim.Dead) return;
            BaitContainer.GetDirectlyHeldThings().TryDropAll(parent.InteractionCell, parent.Map,
                ThingPlaceMode.Near);
            if (entity != null) entity.Destroy(DestroyMode.Vanish);
            if (legacy != null) legacy.Destroy(DestroyMode.Vanish);
            protocolBait = null;
            lureCell = IntVec3.Invalid;
            CleanupProtocolEffects();
            contained = true;
            Current.Game.GetComponent<ScpArrivalRegistry>().Set106Contained(true);
            breached = false;
            legacyOccupied = false;
            neglectedDays = 0;
            nextDayTick = Find.TickManager.TicksGame + 60000;
            if (QuestPart_Scp106Recovery.ActiveFor(parent.Map))
                questCaptureTick = Find.TickManager.TicksGame;
            Find.LetterStack.ReceiveLetter("SCP_106ProtocolCompleteTitle".Translate(),
                "SCP_106ProtocolCompleteBody".Translate(), LetterDefOf.PositiveEvent, parent);
        }
    }
}
