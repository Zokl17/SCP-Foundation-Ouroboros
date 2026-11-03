using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ScpFoundation.Interactions
{
    public static class MonitoringDisplayPolicy
    {
        public const int BaseCapacity = 3;
        public const int ChannelsPerDisplay = 2;
        public const int LinkRange = 8;

        public static int Capacity(int poweredDisplays)
        {
            return BaseCapacity + System.Math.Max(0, poweredDisplays) * ChannelsPerDisplay;
        }

        public static Thing LinkedConsole(Thing display)
        {
            if (display == null || !display.Spawned || display.Map == null) return null;
            CompFacility comp = display.TryGetComp<CompFacility>();
            return comp == null ? null : comp.LinkedBuildings
                .Where(console => console != null && console.Spawned && console.Faction == Faction.OfPlayer
                    && console.def == ScpInteractionDefOf.SCPV1_MonitoringConsole)
                .OrderBy(console => console.Position.DistanceToSquared(display.Position))
                .ThenBy(console => console.thingIDNumber).FirstOrDefault();
        }

        public static IEnumerable<Thing> DisplaysFor(Thing console)
        {
            if (console == null || !console.Spawned || console.Map == null) yield break;
            CompAffectedByFacilities affected = console.TryGetComp<CompAffectedByFacilities>();
            if (affected == null) yield break;
            foreach (Thing display in affected.LinkedFacilitiesListForReading)
                if (display != null && display.Spawned && display.def == ScpInteractionDefOf.SCPV1_MonitoringDisplay)
                    yield return display;
        }

        public static int PoweredDisplaysFor(Thing console)
        {
            return DisplaysFor(console).Count(ScpSurveillance.Powered);
        }

        public static int CapacityFor(Thing console)
        {
            return Capacity(PoweredDisplaysFor(console));
        }
    }

    public sealed class CompProperties_MonitoringDisplay : CompProperties
    {
        public CompProperties_MonitoringDisplay() { compClass = typeof(CompMonitoringDisplay); }
    }

    public sealed class CompMonitoringDisplay : ThingComp
    {
        private Effecter watchingEffect;
        private Thing linkedConsole;
        private bool active;
        private int nextRefresh;

        private void CleanupEffect()
        {
            if (watchingEffect == null) return;
            watchingEffect.Cleanup();
            watchingEffect = null;
        }

        public override void CompTick()
        {
            if (!parent.Spawned) { CleanupEffect(); return; }
            int now = Find.TickManager.TicksGame;
            if (now >= nextRefresh)
            {
                nextRefresh = now + 30;
                linkedConsole = MonitoringDisplayPolicy.LinkedConsole(parent);
                CompMonitoringConsole console = linkedConsole == null ? null : linkedConsole.TryGetComp<CompMonitoringConsole>();
                active = ScpSurveillance.Powered(parent) && console != null && console.Operator != null;
            }
            if (!active) { CleanupEffect(); return; }
            if (watchingEffect == null) watchingEffect = ScpInteractionDefOf.WatchingTelevision.Spawn();
            var target = new TargetInfo(parent);
            watchingEffect.EffectTick(target, target);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            CleanupEffect();
            base.PostDeSpawn(map, mode);
        }

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned) return null;
            Thing console = MonitoringDisplayPolicy.LinkedConsole(parent);
            if (console == null) return "SCP_DisplayUnlinked".Translate(MonitoringDisplayPolicy.LinkRange);
            return (ScpSurveillance.Powered(parent) ? "SCP_DisplayLinked" : "SCP_DisplayNoPower")
                .Translate(MonitoringDisplayPolicy.ChannelsPerDisplay);
        }
    }
}
