using System.Linq;
using Verse;

namespace ScpFoundation.Runtime
{
    public static class ScrambleProtection
    {
        public const string VisorDefName = "SCP_" + "ScrambleVisor";

        public static bool IsProtected(Pawn pawn)
        {
            return pawn != null && pawn.apparel != null
                && pawn.apparel.WornApparel.Any(apparel => apparel.def.defName == VisorDefName);
        }
    }
}
