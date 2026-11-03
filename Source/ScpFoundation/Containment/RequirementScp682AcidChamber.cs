using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using Verse;

namespace ScpFoundation.Containment
{
    public sealed class RequirementScp682AcidChamber : ContainmentRequirement
    {
        public override bool IsSatisfied(ContainmentContext context)
        {
            CompScpPost post = CompScpPost.ForObject(context.thing);
            if (post == null || post.parent.def.defName != "SCP_682AcidChamber"
                || !CompScpPost.IsObjectAtPost(context.thing)) return false;
            CompPowerTrader power = post.parent.TryGetComp<CompPowerTrader>();
            return power != null && power.PowerOn;
        }

        public override string Description() { return "SCP_682AcidChamberRequirement".Translate(); }
    }

    public sealed class RequirementSingleSpecimenRoom : ContainmentRequirement
    {
        public override bool IsSatisfied(ContainmentContext context)
        {
            if (context.room == null || context.thing.def.defName != "SCP_939Object"
                && context.thing.def.defName != "SCP_939ObjectThree") return false;
            ThingDef first = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_939Object");
            ThingDef third = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_939ObjectThree");
            if (first == null || third == null) return false;
            return context.thing.Map.listerThings.ThingsOfDef(first)
                .Concat(context.thing.Map.listerThings.ThingsOfDef(third))
                .Count(thing => thing.Spawned && thing.Position.GetRoom(context.thing.Map) == context.room) == 1;
        }

        public override string Description() { return "SCP_939SeparateRoomRequirement".Translate(); }
    }
}
