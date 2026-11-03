using RimWorld;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Anomalies
{
    public static class ScpAnomalyFactionUtility
    {
        public static Faction GetDoctorFaction()
        {
            return GetHostileFaction();
        }

        public static Faction GetHostileFaction()
        {
            Faction faction = GetOrCreate("SCP_AnomalousHostiles");
            if (faction != null) ScpFactionRelationsComponent.EnsureHostile(faction, Faction.OfPlayer);
            return faction;
        }

        private static Faction GetOrCreate(string defName)
        {
            if (Find.FactionManager == null) return null;
            FactionDef definition = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
            if (definition == null) return null;
            Faction faction = Find.FactionManager.FirstFactionOfDef(definition);
            if (faction == null)
            {
                FactionGenerator.CreateFactionAndAddToManager(definition);
                faction = Find.FactionManager.FirstFactionOfDef(definition);
            }
            return faction;
        }
    }
}
