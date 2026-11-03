using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ScpFoundation.Containment
{
    public sealed class RequirementDoor : ContainmentRequirement
    {
        public List<ThingDef> allowedDoorDefs = new List<ThingDef>();
        public bool requireClosed = true;
        public int openGraceTicks = 750;
        private readonly Dictionary<Building_Door, int> openedAt = new Dictionary<Building_Door, int>();

        public override bool IsSatisfied(ContainmentContext context)
        {
            if (context.room == null || context.room.TouchesMapEdge || context.room.IsDoorway) return false;
            List<Building_Door> doors = context.room.BorderCellsCardinal
                .Where(cell => cell.InBounds(context.thing.Map)).Select(cell => cell.GetEdifice(context.thing.Map))
                .OfType<Building_Door>().Distinct().ToList();
            if (doors.Count == 0) return false;
            int now = Find.TickManager.TicksGame;
            foreach (Building_Door door in doors)
            {
                if (!allowedDoorDefs.Contains(door.def)) return false;
                if (!door.Open)
                {
                    openedAt.Remove(door);
                    continue;
                }
                int since;
                if (!openedAt.TryGetValue(door, out since))
                {
                    since = now;
                    openedAt.Add(door, since);
                }
                if (DoorClosurePolicy.Violates(requireClosed, true, door.HoldOpen,
                    now - since, openGraceTicks)) return false;
            }
            return true;
        }

        public override string Description()
        {
            string doors = string.Join(", ", allowedDoorDefs.Where(def => def != null).Select(def => def.LabelCap.ToString()).ToArray());
            return (requireClosed ? "SCP_RequirementClosedDoor" : "SCP_RequirementDoor").Translate(doors);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (allowedDoorDefs.Count == 0) yield return "Door requirement needs an allowed door definition.";
            if (allowedDoorDefs.Any(def => def == null)) yield return "Unresolved door requirement reference.";
            if (openGraceTicks < 0) yield return "Door open grace cannot be negative.";
        }
    }

}
