using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Knowledge;
using Verse;

namespace ScpFoundation.Containment
{
    // A fresh context is shared by requirements for one evaluation only.
    // Never retain a Room between checks: its topology can change.
    public sealed class ContainmentContext
    {
        public readonly ThingWithComps thing;
        public readonly Room room;
        public ContainmentContext(ThingWithComps thing)
        {
            this.thing = thing;
            room = thing.Position.GetRoom(thing.Map);
        }
    }

    public abstract class ContainmentRequirement
    {
        public string id;
        public KnowledgeLevel visibleFrom = KnowledgeLevel.Unknown;
        public abstract bool IsSatisfied(ContainmentContext context);
        public abstract string Description();
        public virtual IEnumerable<string> ConfigErrors()
        {
            if (string.IsNullOrEmpty(id)) yield return "Containment requirement must have a stable id.";
        }
    }

    public sealed class RequirementRoom : ContainmentRequirement
    {
        public int minCells = 9;
        public bool roofed = true;
        public override bool IsSatisfied(ContainmentContext context)
        {
            Room room = context.room;
            return room != null && !room.TouchesMapEdge && !room.IsDoorway
                && room.CellCount >= minCells && (!roofed || room.OpenRoofCount == 0);
        }
        public override string Description() { return "SCP_RequirementRoom".Translate(minCells, (roofed ? "SCP_Yes" : "SCP_No").Translate()); }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (minCells < 1) yield return "minCells must be positive.";
        }
    }

    public sealed class RequirementTemperature : ContainmentRequirement
    {
        public float min = 10f;
        public float max = 30f;
        public override bool IsSatisfied(ContainmentContext context)
        {
            float temperature = context.thing.AmbientTemperature;
            return temperature >= min && temperature <= max;
        }
        public override string Description() { return "SCP_RequirementTemperature".Translate(min, max); }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (float.IsNaN(min) || float.IsNaN(max) || float.IsInfinity(min) || float.IsInfinity(max) || min > max)
                yield return "Temperature range must be finite and ordered.";
        }
    }

    public sealed class RequirementLight : ContainmentRequirement
    {
        public float min = 0.3f;
        public float max = 1f;
        public override bool IsSatisfied(ContainmentContext context)
        {
            float light = context.thing.Map.glowGrid.GroundGlowAt(context.thing.Position);
            return light >= min && light <= max;
        }
        public override string Description() { return "SCP_RequirementLight".Translate(min.ToString("P0"), max.ToString("P0")); }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (float.IsNaN(min) || float.IsNaN(max) || min < 0 || max > 1 || min > max)
                yield return "Light range must be ordered within 0..1.";
        }
    }

    public sealed class RequirementPower : ContainmentRequirement
    {
        public override bool IsSatisfied(ContainmentContext context)
        {
            CompPowerTrader power = context.thing.TryGetComp<CompPowerTrader>();
            return power != null && power.PowerOn;
        }
        public override string Description() { return "SCP_RequirementPower".Translate(); }
    }

    public sealed class RequirementWallMaterial : ContainmentRequirement
    {
        public List<ThingDef> allowedMaterials = new List<ThingDef>();
        public List<ThingDef> allowedWallDefs = new List<ThingDef>();
        public bool includeDoors;

        public override bool IsSatisfied(ContainmentContext context)
        {
            Room room = context.room;
            if (room == null || room.TouchesMapEdge || room.IsDoorway) return false;
            return WallMaterialPolicy.IsSatisfied(Boundary(context), includeDoors);
        }

        private IEnumerable<BoundaryElement> Boundary(ContainmentContext context)
        {
            Room room = context.room;
            foreach (IntVec3 cell in room.BorderCellsCardinal)
            {
                if (!cell.InBounds(context.thing.Map)) continue;
                Building wall = cell.GetEdifice(context.thing.Map);
                if (wall == null) continue;
                // Room borders also include internal obstacles. Equipment is not a wall.
                bool door = wall is Building_Door;
                bool structural = !door && wall.def.holdsRoof && wall.def.fillPercent >= 1f && wall.def.passability == Traversability.Impassable;
                yield return new BoundaryElement { wall = structural, door = door,
                    allowed = allowedWallDefs.Contains(wall.def) || (wall.Stuff != null && allowedMaterials.Contains(wall.Stuff)) };
            }
        }

        public override string Description()
        {
            string materials = string.Join(", ", allowedMaterials.Concat(allowedWallDefs).Where(def => def != null).Select(def => def.LabelCap.ToString()).ToArray());
            return (includeDoors ? "SCP_RequirementWallsDoors" : "SCP_RequirementWalls").Translate(materials);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (allowedMaterials.Count + allowedWallDefs.Count == 0) yield return "Wall requirement needs an allowed material or wall definition.";
            if (allowedMaterials.Any(def => def == null) || allowedWallDefs.Any(def => def == null)) yield return "Unresolved wall requirement reference.";
        }
    }
}
