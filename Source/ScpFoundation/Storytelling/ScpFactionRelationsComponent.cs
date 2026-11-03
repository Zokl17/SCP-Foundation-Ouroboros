using System.Linq;
using RimWorld;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class ScpFactionRelationsComponent : GameComponent
    {
        private const int RefreshInterval = 2500;
        private const string Prefix = "SCP_";

        private static readonly string[,] HostilePairs =
        {
            { Prefix + "Foundation", Prefix + "ChaosInsurgency" },
            { Prefix + "Foundation", Prefix + "ChurchBrokenGod" },
            { Prefix + "Foundation", Prefix + "SarkicCults" },
            { Prefix + "Foundation", Prefix + "SerpentsHand" },
            { Prefix + "GlobalOccultCoalition", Prefix + "ChaosInsurgency" },
            { Prefix + "GlobalOccultCoalition", Prefix + "ChurchBrokenGod" },
            { Prefix + "GlobalOccultCoalition", Prefix + "SarkicCults" },
            { Prefix + "GlobalOccultCoalition", Prefix + "SerpentsHand" },
            { Prefix + "ChaosInsurgency", Prefix + "ChurchBrokenGod" },
            { Prefix + "ChaosInsurgency", Prefix + "SarkicCults" },
            { Prefix + "ChaosInsurgency", Prefix + "SerpentsHand" },
            { Prefix + "ChurchBrokenGod", Prefix + "SarkicCults" },
            { Prefix + "ChurchBrokenGod", Prefix + "SerpentsHand" },
            { Prefix + "SarkicCults", Prefix + "SerpentsHand" }
        };

        public ScpFactionRelationsComponent(Game game) { }

        public override void FinalizeInit()
        {
            EnsureMissingWorldFactions();
            EnsurePlaceholderLeaders();
            EnsureHostileRelations();
        }

        private static void EnsurePlaceholderLeaders()
        {
            if (Find.FactionManager == null) return;
            foreach (string suffix in new[] { "ChurchBrokenGod", "SarkicCults" })
            {
                Faction faction = FindFaction(Prefix + suffix);
                if (faction != null && !faction.defeated && faction.leader == null)
                    faction.TryGenerateNewLeader();
            }
        }

        private static void EnsureMissingWorldFactions()
        {
            if (Find.FactionManager == null || Find.WorldGrid == null) return;
            foreach (string suffix in new[] { "Foundation", "ChaosInsurgency", "GlobalOccultCoalition", "ChurchBrokenGod", "SarkicCults" })
            {
                FactionDef definition = DefDatabase<FactionDef>.GetNamedSilentFail(Prefix + suffix);
                // Include defeated factions: loading must not resurrect them or duplicate existing factions.
                if (definition == null || Find.FactionManager.AllFactionsListForReading.Any(f => f.def == definition)) continue;
                // Vanilla also creates a settlement, ideology, leader and initial diplomatic relations.
                FactionGenerator.CreateFactionAndAddToManager(definition);
            }
        }

        public override void StartedNewGame()
        {
            EnsureHostileRelations();
        }

        public override void LoadedGame()
        {
            EnsureHostileRelations();
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % RefreshInterval == 0)
                EnsureHostileRelations();
        }

        public static void EnsureHostileRelations()
        {
            if (Find.FactionManager == null) return;
            for (int index = 0; index < HostilePairs.GetLength(0); index++)
            {
                if (HostilePairs[index, 0] == Prefix + "ChaosInsurgency"
                    && HostilePairs[index, 1] == Prefix + "ChurchBrokenGod"
                    && Current.Game != null
                    && Current.Game.GetComponent<Scp882OuroborosSiegeComponent>().ChurchCeasefireActive)
                    continue;
                Faction first = FindFaction(HostilePairs[index, 0]);
                Faction second = FindFaction(HostilePairs[index, 1]);
                if (first == null || second == null || first == second
                    || first.RelationKindWith(second) == FactionRelationKind.Hostile) continue;
                EnsureHostile(first, second);
            }
            // A player's Foundation-themed colony belongs to Faction.OfPlayer, not SCP_Foundation.
            Faction chaos = FindFaction(Prefix + "ChaosInsurgency");
            Faction player = Faction.OfPlayer;
            if (chaos != null && player != null && chaos != player
                && chaos.RelationKindWith(player) != FactionRelationKind.Hostile)
                EnsureHostile(chaos, player);
        }

        public static void EnsureHostile(Faction first, Faction second)
        {
            if (first == null || second == null || first == second
                || first.RelationKindWith(second) == FactionRelationKind.Hostile) return;

            if (first.HasGoodwill && second.HasGoodwill)
                first.TryAffectGoodwillWith(second, -100 - first.GoodwillWith(second), false, false);
            else
                first.SetRelationDirect(second, FactionRelationKind.Hostile, false,
                    "SCP_InterestGroupHostilityReason".Translate(), null);
        }

        private static Faction FindFaction(string defName)
        {
            FactionDef definition = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
            return definition == null ? null : Find.FactionManager.FirstFactionOfDef(definition);
        }
    }
}
