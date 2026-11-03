using System;
using System.Reflection;
using Verse;

namespace ScpFoundation.Runtime
{
    internal static class CombatExtendedLoadoutBridge
    {
        private const string CombatExtendedPackageId = "CETeam.CombatExtended";
        private const string NormalizerTypeName = "ScpFoundation.CombatExtendedCompat.CeAmmoLoadoutNormalizer";
        private static MethodInfo normalizeMethod;
        private static bool resolved;
        private static bool failureLogged;

        internal static void NormalizePrimaryAmmo(Pawn pawn)
        {
            if (pawn == null || !ModsConfig.IsActive(CombatExtendedPackageId)) return;
            Resolve();
            if (normalizeMethod == null) return;

            try
            {
                normalizeMethod.Invoke(null, new object[] { pawn });
            }
            catch (Exception exception)
            {
                if (failureLogged) return;
                failureLogged = true;
                Log.Error("[SCP Foundation] Combat Extended ammunition normalization failed: " + exception);
            }
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(NormalizerTypeName, false);
                if (type == null) continue;
                normalizeMethod = type.GetMethod("Normalize", BindingFlags.Public | BindingFlags.Static);
                return;
            }
        }
    }
}
