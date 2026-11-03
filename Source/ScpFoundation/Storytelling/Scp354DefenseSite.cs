using RimWorld.Planet;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class Scp354DefenseSite : Site
    {
        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            GenStep_Scp354Defense.RemoveGeneratedDebris(Map);
        }

        public override bool ShouldRemoveMapNow(out bool removeWorldObject)
        {
            if (HasMap && Find.CurrentMap == Map)
            {
                removeWorldObject = false;
                return false;
            }
            return base.ShouldRemoveMapNow(out removeWorldObject);
        }
    }
}
