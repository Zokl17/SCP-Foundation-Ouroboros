using System.Collections.Generic;

namespace ScpFoundation.Containment
{
    public struct BoundaryElement
    {
        public bool wall;
        public bool door;
        public bool allowed;
    }

    public static class WallMaterialPolicy
    {
        public static bool IsSatisfied(IEnumerable<BoundaryElement> boundary, bool includeDoors)
        {
            bool foundWall = false;
            foreach (BoundaryElement element in boundary)
            {
                if (element.door && !includeDoors) continue;
                if (!element.wall && !element.door) continue;
                if (element.wall) foundWall = true;
                if (!element.allowed) return false;
            }
            return foundWall;
        }
    }
}
