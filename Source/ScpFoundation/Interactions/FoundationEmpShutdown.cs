using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Interactions
{
    public sealed class Projectile_FoundationEmp : Bullet
    {
        private static readonly Color PulseColor = new Color32(45, 175, 255, 255);

        public override Color DrawColor { get { return PulseColor; } }
        public override Color DrawColorTwo { get { return PulseColor; } }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map impactMap = Map;
            IntVec3 impactCell = Position;
            float radius = def.projectile.explosionRadius;
            base.Impact(hitThing, blockedByShield);
            if (impactMap == null || blockedByShield) return;

            FoundationEmpPulse.Apply(impactMap, impactCell, radius, launcher, hitThing);
        }
    }

    public static class FoundationEmpPulse
    {
        private const int ShutdownTicks = 1200;
        private const int EmpStunPower = 20;
        private const int ImplantThreshold = 3;

        public static void Apply(Map map, IntVec3 cell, float radius, Thing attacker, Thing hitThing)
        {
            GenExplosion.DoExplosion(cell, map, radius, DamageDefOf.EMP, attacker, damAmount: EmpStunPower);
            FoundationEmpShutdownComponent.DisablePoweredDevices(map, cell, radius, ShutdownTicks);

            Pawn pawn = hitThing as Pawn;
            if (pawn == null || pawn.Dead || pawn.health == null || pawn.RaceProps == null
                || !pawn.RaceProps.Humanlike || pawn.health.hediffSet == null
                || pawn.health.hediffSet.hediffs.Count(h => h != null && h.def != null
                    && h.def.countsAsAddedPartOrImplant) < ImplantThreshold)
                return;

            HediffDef shock = DefDatabase<HediffDef>.GetNamedSilentFail("BrainShock");
            if (shock != null && !pawn.health.hediffSet.HasHediff(shock))
                pawn.health.AddHediff(shock);
        }
    }

    public sealed class FoundationEmpShutdownComponent : MapComponent
    {
        private List<EmpShutdownRecord> disabled = new List<EmpShutdownRecord>();

        public FoundationEmpShutdownComponent(Map map) : base(map) { }

        public static void DisablePoweredDevices(Map map, IntVec3 center, float radius, int durationTicks)
        {
            if (map == null || durationTicks <= 0) return;

            FoundationEmpShutdownComponent shutdown = map.GetComponent<FoundationEmpShutdownComponent>();
            foreach (Thing thing in GenRadial.RadialCellsAround(center, radius, true)
                .Where(cell => cell.InBounds(map))
                .SelectMany(cell => cell.GetThingList(map))
                .Distinct()
                .ToList())
            {
                if (thing.TryGetComp<CompPowerTrader>() != null)
                    shutdown.Disable(thing, durationTicks);
            }
        }

        public bool IsDisabled(Thing thing)
        {
            return thing != null && disabled.Any(record => record.device == thing && record.untilTick > Find.TickManager.TicksGame);
        }

        public void Disable(Thing thing, int durationTicks)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != map) return;
            CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
            if (power == null) return;

            int untilTick = Find.TickManager.TicksGame + durationTicks;
            EmpShutdownRecord record = disabled.FirstOrDefault(entry => entry.device == thing);
            if (record == null)
            {
                record = new EmpShutdownRecord
                {
                    device = thing,
                    untilTick = untilTick,
                    restorePowerOn = power.PowerOn
                };
                disabled.Add(record);
            }
            else if (untilTick > record.untilTick)
            {
                record.untilTick = untilTick;
            }

            power.PowerOn = false;
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int index = disabled.Count - 1; index >= 0; index--)
            {
                EmpShutdownRecord record = disabled[index];
                Thing thing = record.device;
                if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != map)
                {
                    disabled.RemoveAt(index);
                    continue;
                }

                CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
                if (power == null)
                {
                    disabled.RemoveAt(index);
                    continue;
                }

                if (now < record.untilTick)
                {
                    power.PowerOn = false;
                    continue;
                }

                power.PowerOn = record.restorePowerOn;
                disabled.RemoveAt(index);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref disabled, "scpEmpDisabledDevices", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                disabled = (disabled ?? new List<EmpShutdownRecord>())
                    .Where(record => record != null && record.device != null && !record.device.Destroyed)
                    .ToList();
        }
    }

    public sealed class EmpShutdownRecord : IExposable
    {
        public Thing device;
        public int untilTick;
        public bool restorePowerOn;

        public void ExposeData()
        {
            Scribe_References.Look(ref device, "device");
            Scribe_Values.Look(ref untilTick, "untilTick", 0);
            Scribe_Values.Look(ref restorePowerOn, "restorePowerOn", false);
        }
    }
}
