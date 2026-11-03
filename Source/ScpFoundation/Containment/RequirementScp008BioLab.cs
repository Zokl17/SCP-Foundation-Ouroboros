using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using Verse;

namespace ScpFoundation.Containment
{
    public sealed class RequirementScp008BioLab : ContainmentRequirement
    {
        public override bool IsSatisfied(ContainmentContext context)
        {
            if (context.room == null || context.thing.Map == null) return false;
            ThingDef gate = DefDatabase<ThingDef>.GetNamedSilentFail("SCPV1_DeconGate");
            return gate != null && context.thing.Map.listerThings.ThingsOfDef(gate).Any(thing =>
                thing.Spawned && thing.Position.GetRoom(thing.Map) == context.room
                && thing.TryGetComp<CompRoomDecontaminator>() != null
                && thing.TryGetComp<CompPowerTrader>() != null
                && thing.TryGetComp<CompPowerTrader>().PowerOn);
        }

        public override string Description() { return "SCP_008BioLabRequirement".Translate(); }
    }
}
