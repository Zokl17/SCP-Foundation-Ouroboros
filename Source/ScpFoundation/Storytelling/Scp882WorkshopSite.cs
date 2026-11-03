using RimWorld.Planet;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class Scp882WorkshopSite : Site
    {
        public override bool ShouldRemoveMapNow(out bool removeWorldObject)
        {
            // The developer map button opens a site without a caravan. Keep its
            // visible map until the player switches away; otherwise Site removes it.
            if (HasMap && Find.CurrentMap == Map)
            {
                removeWorldObject = false;
                return false;
            }

            return base.ShouldRemoveMapNow(out removeWorldObject);
        }
    }
}
