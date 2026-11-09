using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    [StaticConstructorOnStartup]
    public sealed class Pawn_Scp106 : Pawn
    {
        private int nextHuntTick;
        private int phaseReadyTick;
        private int retaliationReadyTick;
        private int recentShots;
        private int shotWindowEndTick;
        private IntVec3 phaseDestination = IntVec3.Invalid;
        private IntVec3 phasePortalCell = IntVec3.Invalid;
        private bool phaseIntoGround;
        private Mote activePortal;
        private IntVec3 detourDestination = IntVec3.Invalid;
        private int nextWanderTick;
        private static Graphic portrait;
        private const int WalkStepTicks = 63;
        private const int ShotsToSubmerge = 5;
        public const int MaximumIntegrity = 1400;
        private int integrity = MaximumIntegrity;
        private int vanishTick;

        public int Integrity { get { return integrity; } }
        public bool CanBeContained { get { return (integrity <= 0 || Downed) && !Dead; } }

        public override string Label { get { return "SCP-106"; } }
        public override string LabelNoCount { get { return "SCP-106"; } }
        public override string LabelShort { get { return "SCP-106"; } }

        public override void DrawGUIOverlay() { }

        public override string GetInspectString()
        {
            return (Downed || integrity <= 0 ? "SCP_106InspectDowned" : "SCP_106InspectActive").Translate();
        }

        public override IEnumerable<InspectTabBase> GetInspectTabs() { yield break; }

        public override IEnumerable<Gizmo> GetGizmos() { yield break; }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            if (!CanBeContained)
                foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn)) yield return option;
            if (!CanBeContained || !Spawned || selPawn == null || selPawn.Downed
                || selPawn.Faction != Faction.OfPlayer || selPawn.Map != Map) yield break;
            ThingDef chamberDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_106BaitStation");
            if (chamberDef == null) yield break;
            CompLarryProtocol chamber = Map.listerThings.ThingsOfDef(chamberDef)
                .Select(thing => thing.TryGetComp<CompLarryProtocol>())
                .Where(comp => comp != null && comp.CanReceiveDownedSCP)
                .OrderBy(comp => comp.parent.Position.DistanceToSquared(Position)).FirstOrDefault();
            if (chamber == null)
            {
                yield return new FloatMenuOption("SCP_106CarryNoChamber".Translate(), null);
                yield break;
            }
            if (!selPawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption("SCP_106CarryUnreachable".Translate(), null);
                yield break;
            }
            yield return new FloatMenuOption("SCP_106CarryToChamber".Translate(), () =>
                chamber.DispatchDownedSCP(selPawn, this));
        }

        public static Pawn_Scp106 Generate()
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_106Kind");
            Faction faction = ScpAnomalyFactionUtility.GetHostileFaction();
            if (kind == null || faction == null) return null;
            Pawn_Scp106 pawn = PawnGenerator.GeneratePawn(kind, faction) as Pawn_Scp106;
            if (pawn != null) pawn.Name = new NameSingle("SCP-106", false);
            return pawn;
        }

        public void DelayHunt(int ticks)
        {
            nextHuntTick = Find.TickManager.TicksGame + ticks;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextHuntTick, "scp106NextHunt", 0);
            Scribe_Values.Look(ref phaseReadyTick, "scp106PawnPhaseReadyTick", 0);
            Scribe_Values.Look(ref retaliationReadyTick, "scp106PawnRetaliationReadyTick", 0);
            Scribe_Values.Look(ref recentShots, "scp106RecentShots", 0);
            Scribe_Values.Look(ref shotWindowEndTick, "scp106ShotWindowEnd", 0);
            Scribe_Values.Look(ref phaseDestination, "scp106PawnPhaseDestination", IntVec3.Invalid);
            Scribe_Values.Look(ref phasePortalCell, "scp106PhasePortalCell", IntVec3.Invalid);
            Scribe_Values.Look(ref phaseIntoGround, "scp106PhaseIntoGround", false);
            Scribe_Values.Look(ref detourDestination, "scp106DetourDestination", IntVec3.Invalid);
            Scribe_Values.Look(ref nextWanderTick, "scp106NextWander", 0);
            Scribe_Values.Look(ref integrity, "scp106Integrity", MaximumIntegrity);
            Scribe_Values.Look(ref vanishTick, "scp106VanishTick", 0);
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (integrity > 0)
            {
                integrity = System.Math.Max(0, integrity - ScpIntegrityDamage.From(dinfo));
                if (integrity == 0) Collapse();
            }
            if (integrity > 0 && !Downed && dinfo.Instigator != null && dinfo.Instigator != this
                && dinfo.Def != null && dinfo.Def.isRanged && dinfo.Amount > 0f)
            {
                int now = Find.TickManager.TicksGame;
                if (now > shotWindowEndTick) recentShots = 0;
                recentShots++;
                shotWindowEndTick = now + 600;
                if (recentShots >= ShotsToSubmerge)
                {
                    recentShots = 0;
                    RetaliateAgainst(dinfo.Instigator);
                }
            }
            absorbed = true;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (phaseDestination.IsValid) return;
            if (portrait == null)
                portrait = GraphicDatabase.Get<Graphic_Single>("Things/Item/Scp106", ShaderDatabase.Cutout,
                    new Vector2(1.2f, 2.1f), Color.white);
            if (CanBeContained) drawLoc.y = Altitudes.AltitudeFor(AltitudeLayer.MoteOverhead);
            portrait.Draw(drawLoc, Downed || integrity <= 0 ? Rot4.East : Rot4.North, this);
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            // Pawn.DrawAt is bypassed by RimWorld's pawn render phases. Do not
            // render the human body, clothes or equipment behind SCP-106.
            if (phase == DrawPhase.Draw) DrawAt(drawLoc, flip);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned || Dead || Map == null) return;
            int now = Find.TickManager.TicksGame;
            if (now % 250 == 0 && needs != null)
            {
                if (needs.food != null) needs.food.CurLevel = needs.food.MaxLevel;
                if (needs.rest != null) needs.rest.CurLevel = needs.rest.MaxLevel;
            }
            if (integrity <= 0)
            {
                if (!Downed) Collapse();
                if (vanishTick != 0 && now >= vanishTick) Destroy(DestroyMode.Vanish);
                return;
            }
            if (CanBeContained || Map.GetComponent<Scp106AbductionComponent>().IsBusy(this)) return;
            if (phaseDestination.IsValid)
            {
                MaintainPortal(Map);
                pather.StopDead();
                if (now < phaseReadyTick) return;
                IntVec3 destination = phaseDestination;
                bool groundExit = phaseIntoGround;
                phaseDestination = IntVec3.Invalid;
                phasePortalCell = IntVec3.Invalid;
                phaseIntoGround = false;
                activePortal = null;
                if (FreeFor106(destination, Map)) PhaseTo(destination);
                if (groundExit) SpawnGroundExit(Map, Position);
                nextHuntTick = now + WalkStepTicks;
                return;
            }
            if (detourDestination.IsValid)
            {
                if (jobs != null && jobs.curJob != null && jobs.curJob.def == JobDefOf.Goto
                    && pather.Moving && Position != detourDestination) return;
                detourDestination = IntVec3.Invalid;
            }
            if (now < nextHuntTick) return;
            nextHuntTick = now + WalkStepTicks;
            if (jobs != null && jobs.curJob != null && jobs.curJob.targetA.Thing is Building_Turret)
                jobs.StopAll();
            Map map = Map;
            IntVec3 baitCell = CompLarryProtocol.ActiveBaitCell(map);
            Scp106AbductionComponent abductions = map.GetComponent<Scp106AbductionComponent>();
            if (!baitCell.IsValid)
            {
                Pawn fallen = map.mapPawns.AllPawnsSpawned.FirstOrDefault(pawn =>
                    pawn != this && pawn.Downed && !pawn.Dead && pawn.RaceProps.Humanlike
                    && (pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony)
                    && pawn.Position.DistanceToSquared(Position) <= 4);
                if (fallen != null && abductions.TryBegin(this, fallen)) return;
            }
            Pawn prey = baitCell.IsValid ? null : map.mapPawns.FreeColonistsSpawned
                .Where(pawn => !pawn.Dead && pawn.RaceProps.IsFlesh)
                .OrderBy(pawn => pawn.Position.DistanceToSquared(Position)).FirstOrDefault();
            if (!baitCell.IsValid && prey == null)
            {
                Wander(map, now);
                return;
            }

            IntVec3 target = baitCell.IsValid ? baitCell : prey.Position;
            int dx = target.x - Position.x;
            int dz = target.z - Position.z;
            if (dx != 0 || dz != 0)
            {
                int distance = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz));
                IntVec3 next = new IntVec3(Position.x + (int)System.Math.Round(dx / (double)distance), 0,
                    Position.z + (int)System.Math.Round(dz / (double)distance));
                if (FreeFor106(next, map)) WalkTo(next);
                else if (next.InBounds(map) && next.GetThingList(map).Any(thing => thing is Building_Turret))
                    StartDetour(target, map);
                else if (next.InBounds(map) && (!next.Standable(map)
                    || next.GetThingList(map).Any(thing => thing.def.category == ThingCategory.Building)))
                {
                    if (!PhaseThroughWall(next, dx, dz, distance, now)) StartDetour(target, map);
                }
            }

            DamageDef acid = DefDatabase<DamageDef>.GetNamedSilentFail("AcidBurn");
            if (acid == null) return;
            if (prey != null && prey.Spawned && Position.DistanceToSquared(prey.Position) <= 4)
            {
                if (!prey.Downed) prey.TakeDamage(new DamageInfo(acid, 8f, 0f, -1f, this));
                if (prey.Downed && !prey.Dead && !baitCell.IsValid)
                    abductions.TryBegin(this, prey);
            }
        }

        private void Collapse()
        {
            phaseDestination = IntVec3.Invalid;
            phasePortalCell = IntVec3.Invalid;
            activePortal = null;
            if (vanishTick == 0 && Find.TickManager != null)
                vanishTick = Find.TickManager.TicksGame + 60000;
            if (jobs != null) jobs.StopAll();
            if (pather != null) pather.StopDead();
            health.forceDowned = true;
            HediffDef collapse = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_106Incapacitated");
            if (collapse != null && !health.hediffSet.HasHediff(collapse))
                health.AddHediff(collapse);
            else
                health.CheckForStateChange(null, null);
        }

        private void WalkTo(IntVec3 destination)
        {
            if (jobs == null || jobs.curJob != null && jobs.curJob.def == JobDefOf.Goto
                && jobs.curJob.targetA.Cell == destination) return;
            jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, destination), JobCondition.InterruptForced);
        }

        private void Wander(Map map, int now)
        {
            if (now < nextWanderTick || pather.Moving) return;
            nextWanderTick = now + 600;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(Position, 9f, false).InRandomOrder())
            {
                if (!FreeFor106(cell, map) || cell.DistanceToSquared(Position) < 16
                    || !map.reachability.CanReach(Position, cell, PathEndMode.OnCell,
                        TraverseParms.For(this))) continue;
                detourDestination = cell;
                WalkTo(cell);
                return;
            }
        }

        private void StartDetour(IntVec3 target, Map map)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(target, 5f, true))
            {
                if (!FreeFor106(cell, map) || cell == Position
                    || !map.reachability.CanReach(Position, cell, PathEndMode.OnCell,
                        TraverseParms.For(this))) continue;
                detourDestination = cell;
                WalkTo(cell);
                return;
            }
            Wander(map, Find.TickManager.TicksGame);
        }

        private bool PhaseThroughWall(IntVec3 entryCell, int dx, int dz, int distance, int now)
        {
            Map map = Map;
            IntVec3 origin = Position;
            int blockedCells = 1;
            for (int step = 2; step <= 8; step++)
            {
                IntVec3 cell = new IntVec3(origin.x + (int)System.Math.Round(dx * step / (double)distance), 0,
                    origin.z + (int)System.Math.Round(dz * step / (double)distance));
                if (!cell.InBounds(map)) break;
                if (!FreeFor106(cell, map)) { blockedCells++; continue; }
                phaseDestination = cell;
                phasePortalCell = entryCell;
                phaseIntoGround = false;
                activePortal = null;
                phaseReadyTick = now + WalkStepTicks * blockedCells;
                if (jobs != null) jobs.StopAll();
                pather.StopDead();
                MaintainPortal(map);
                return true;
            }
            return false;
        }

        private void RetaliateAgainst(Thing attacker)
        {
            if (!Spawned || CanBeContained || attacker == null || !attacker.Spawned || attacker.Map != Map) return;
            int now = Find.TickManager.TicksGame;
            if (now < retaliationReadyTick || phaseDestination.IsValid) return;
            // A blocked ring around the shooter means no counter-phase, not an escape
            // to an unrelated cell around SCP-106.
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(attacker.Position, 3f, true))
            {
                if (!FreeFor106(cell, Map) || cell == Position) continue;
                phaseDestination = cell;
                phasePortalCell = Position;
                phaseIntoGround = true;
                activePortal = null;
                phaseReadyTick = now + 250;
                retaliationReadyTick = now + 1500;
                if (jobs != null) jobs.StopAll();
                pather.StopDead();
                MaintainPortal(Map);
                return;
            }
        }

        private void MaintainPortal(Map map)
        {
            if (!phasePortalCell.IsValid || !phasePortalCell.InBounds(map)) return;
            if (activePortal == null || activePortal.Destroyed)
            {
                ThingDef portal = DefDatabase<ThingDef>.GetNamedSilentFail(
                    phaseIntoGround ? "SCP_106GroundPortal" : "SCP_106WallPortal");
                if (portal != null) activePortal = MoteMaker.MakeStaticMote(phasePortalCell, map, portal, 1f);
            }
            if (activePortal != null) activePortal.Maintain();
        }

        private static void SpawnGroundExit(Map map, IntVec3 cell)
        {
            ThingDef portal = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_106GroundExit");
            if (portal != null && cell.InBounds(map)) MoteMaker.MakeStaticMote(cell, map, portal, 1f);
        }

        private bool FreeFor106(IntVec3 cell, Map map)
        {
            return cell.InBounds(map) && cell.Standable(map)
                && !cell.GetThingList(map).Any(thing => thing is Pawn
                    || thing.def.category == ThingCategory.Building);
        }

        private void PhaseTo(IntVec3 destination)
        {
            Map map = Map;
            IntVec3 origin = Position;
            DeSpawn();
            GenSpawn.Spawn(this, destination, map);
            ThingDef filth = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_SpentAcid");
            if (filth != null) FilthMaker.TryMakeFilth(origin, map, filth);
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), "GetOptions")]
    internal static class Scp106DownedMenuPatch
    {
        private static void Postfix(ref List<FloatMenuOption> __result, ref FloatMenuContext context)
        {
            if (__result == null || context.ClickedThings == null) return;
            Pawn_Scp106 entity = context.ClickedThings.OfType<Pawn_Scp106>()
                .FirstOrDefault(pawn => pawn.CanBeContained);
            if (entity == null) return;
            string carry = "SCP_106CarryToChamber".Translate();
            string noChamber = "SCP_106CarryNoChamber".Translate();
            string unreachable = "SCP_106CarryUnreachable".Translate();
            Texture2D icon = ContentFinder<Texture2D>.Get("Things/Item/Scp106");
            for (int index = __result.Count - 1; index >= 0; index--)
            {
                FloatMenuOption option = __result[index];
                if (option == null) continue;
                string label = option.Label ?? string.Empty;
                if (label == carry || label == noChamber || label == unreachable)
                {
                    option.iconThing = null;
                    Traverse.Create(option).Field("iconTex").SetValue(icon);
                    option.revalidateClickTarget = null;
                    continue;
                }
                if (option.iconThing == entity || option.revalidateClickTarget == entity
                    || label.IndexOf("scp-106", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    __result.RemoveAt(index);
            }
        }
    }
}
