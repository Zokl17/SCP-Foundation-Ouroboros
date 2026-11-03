using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ScpFoundation.Anomalies
{
    public sealed class Building_Scp106Entity : Building
    {
        public override Vector3 DrawPos
        {
            get
            {
                CompScp106Entity behavior = GetComp<CompScp106Entity>();
                return base.DrawPos + (behavior == null ? Vector3.zero : behavior.DrawOffset);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            // The entity uses Building for its map behavior, but claiming or
            // deconstructing it is not a containment action.
            yield break;
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (dinfo.Instigator is Pawn && dinfo.Instigator != this)
            {
                CompScp106Entity behavior = GetComp<CompScp106Entity>();
                if (behavior != null) behavior.RetaliateAgainst(dinfo.Instigator);
            }
            absorbed = true;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            CompScp106Entity behavior = GetComp<CompScp106Entity>();
            if (behavior != null && behavior.IsSubmerged) return;
            base.DrawAt(drawLoc, flip);
        }
    }

    public sealed class CompProperties_Scp106Entity : CompProperties
    {
        public CompProperties_Scp106Entity() { compClass = typeof(CompScp106Entity); }
    }

    public sealed class CompScp106Entity : ThingComp
    {
        private int nextHuntTick;
        private int phaseReadyTick;
        private int retaliationReadyTick;
        private IntVec3 phaseDestination = IntVec3.Invalid;
        private IntVec3 lastStepFrom = IntVec3.Invalid;
        private int lastStepTick;
        private const int WalkStepTicks = 90;

        public bool IsSubmerged { get { return phaseDestination.IsValid; } }
        public Vector3 DrawOffset
        {
            get
            {
                if (!lastStepFrom.IsValid) return Vector3.zero;
                float remaining = 1f - Mathf.Clamp01((Find.TickManager.TicksGame - lastStepTick) / (float)WalkStepTicks);
                // Keep the visible body inside its selectable destination cell.
                return new Vector3(lastStepFrom.x - parent.Position.x, 0f,
                    lastStepFrom.z - parent.Position.z) * (remaining * 0.45f);
            }
        }

        public void RetaliateAgainst(Thing attacker)
        {
            if (!parent.Spawned || attacker == null || !attacker.Spawned || attacker.Map != parent.Map) return;
            int now = Find.TickManager.TicksGame;
            if (now < retaliationReadyTick) return;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(attacker.Position, 2f, true))
            {
                if (!FreeFor106(cell, parent.Map) || cell == parent.Position) continue;
                phaseDestination = cell;
                phaseReadyTick = now + 250;
                retaliationReadyTick = now + 1500;
                return;
            }
        }

        public void DelayHunt(int ticks)
        {
            nextHuntTick = Find.TickManager.TicksGame + ticks;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref nextHuntTick, "scp106EntityNextHunt", 0);
            Scribe_Values.Look(ref phaseReadyTick, "scp106PhaseReadyTick", 0);
            Scribe_Values.Look(ref retaliationReadyTick, "scp106RetaliationReadyTick", 0);
            Scribe_Values.Look(ref phaseDestination, "scp106PhaseDestination", IntVec3.Invalid);
            Scribe_Values.Look(ref lastStepFrom, "scp106LastStepFrom", IntVec3.Invalid);
            Scribe_Values.Look(ref lastStepTick, "scp106LastStepTick", 0);
        }

        public override void CompTick()
        {
            if (!parent.Spawned) return;
            // Existing saves contain the former building representation. Replace it
            // after loading so the same SCP uses the pawn path follower too.
            Pawn_Scp106 upgraded = Pawn_Scp106.Generate();
            if (upgraded != null)
            {
                upgraded.DelayHunt(System.Math.Max(0, nextHuntTick - Find.TickManager.TicksGame));
                GenSpawn.Spawn(upgraded, parent.Position, parent.Map);
                parent.Destroy(DestroyMode.Vanish);
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (phaseDestination.IsValid)
            {
                if (now < phaseReadyTick) return;
                IntVec3 destination = phaseDestination;
                phaseDestination = IntVec3.Invalid;
                if (FreeFor106(destination, parent.Map)) MoveTo(destination, false);
                nextHuntTick = now + WalkStepTicks;
                return;
            }
            if (now < nextHuntTick) return;
            nextHuntTick = now + WalkStepTicks;
            Map map = parent.Map;
            IntVec3 baitCell = CompLarryProtocol.ActiveBaitCell(map);
            Pawn prey = baitCell.IsValid ? null : map.mapPawns.FreeColonistsSpawned
                .Where(p => !p.Dead && p.RaceProps.IsFlesh)
                .OrderBy(p => p.Position.DistanceToSquared(parent.Position)).FirstOrDefault();
            if (!baitCell.IsValid && prey == null) return;

            IntVec3 origin = parent.Position;
            IntVec3 target = baitCell.IsValid ? baitCell : prey.Position;
            int dx = target.x - origin.x;
            int dz = target.z - origin.z;
            if (dx != 0 || dz != 0)
            {
                int distance = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz));
                IntVec3 next = new IntVec3(origin.x + (int)System.Math.Round(dx / (double)distance), 0,
                    origin.z + (int)System.Math.Round(dz / (double)distance));
                if (FreeFor106(next, map)) MoveTo(next, true);
                else if (next.InBounds(map) && (!next.Standable(map)
                    || next.GetThingList(map).Any(t => t.def.category == ThingCategory.Building)))
                {
                    int blockedCells = 1;
                    for (int step = 2; step <= 8; step++)
                    {
                        IntVec3 cell = new IntVec3(origin.x + (int)System.Math.Round(dx * step
                            / (double)distance), 0,
                            origin.z + (int)System.Math.Round(dz * step / (double)distance));
                        if (!cell.InBounds(map)) break;
                        if (!FreeFor106(cell, map)) { blockedCells++; continue; }
                        phaseDestination = cell;
                        phaseReadyTick = now + 90 * blockedCells;
                        return;
                    }
                }
            }
            DamageDef acid = DefDatabase<DamageDef>.GetNamedSilentFail("AcidBurn");
            if (acid == null) return;
            if (prey != null && prey.Spawned && parent.Position.DistanceToSquared(prey.Position) <= 4)
                prey.TakeDamage(new DamageInfo(acid, 8f, 0f, -1f, parent));
        }

        private bool FreeFor106(IntVec3 cell, Map map)
        {
            return cell.InBounds(map) && cell.Standable(map)
                && !cell.GetThingList(map).Any(t => t is Pawn
                    || (t.def.category == ThingCategory.Building && t != parent));
        }

        private void MoveTo(IntVec3 destination, bool animate)
        {
            Map map = parent.Map;
            IntVec3 origin = parent.Position;
            parent.DeSpawn();
            GenSpawn.Spawn(parent, destination, map);
            lastStepFrom = animate ? origin : IntVec3.Invalid;
            lastStepTick = Find.TickManager.TicksGame;
            ThingDef filth = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_SpentAcid");
            if (filth != null) FilthMaker.TryMakeFilth(origin, map, filth);
        }
    }

    public sealed class CompProperties_Scp049Entity : CompProperties
    {
        public CompProperties_Scp049Entity() { compClass = typeof(CompScp049Entity); }
    }

    public sealed class CompScp049Entity : ThingComp
    {
        private bool accessOpen;
        private int nextTreatmentTick;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref accessOpen, "scp049AccessOpen", false);
            Scribe_Values.Look(ref nextTreatmentTick, "scp049EntityNextTreatment", 0);
        }

        public override string CompInspectStringExtra()
        {
            return accessOpen ? "SCP_049AccessOpen".Translate() : "SCP_049AccessClosed".Translate();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            yield return new Command_Action { defaultLabel = (accessOpen ? "SCP_049Seal" : "SCP_049AllowAccess").Translate(),
                action = () => { accessOpen = !accessOpen; } };
        }

        public override void CompTickRare()
        {
            // Saved maps may still contain the immobile pre-pawn SCP-049.
            if (parent.Spawned)
            {
                Pawn_Scp049 doctor = Pawn_Scp049.Generate();
                if (doctor != null)
                {
                    Map oldMap = parent.Map;
                    IntVec3 oldCell = parent.Position;
                    parent.Destroy(DestroyMode.Vanish);
                    GenSpawn.Spawn(doctor, oldCell, oldMap);
                    return;
                }
            }
        }
    }
}
