using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ScpFoundation.Storytelling
{
    public sealed class FoundationPersonnelComponent : GameComponent
    {
        private List<Pawn> pawns = new List<Pawn>();
        private List<int> clearanceLevels = new List<int>();
        private List<int> roles = new List<int>();

        public FoundationPersonnelComponent(Game game) { }

        public int LevelFor(Pawn pawn)
        {
            if (pawn == null) return 0;
            int index = pawns.IndexOf(pawn);
            return index < 0 || index >= clearanceLevels.Count ? 0 : clearanceLevels[index];
        }

        public FoundationRole RoleFor(Pawn pawn)
        {
            int index = pawns.IndexOf(pawn);
            return index < 0 || index >= roles.Count ? FoundationRole.None : (FoundationRole)roles[index];
        }

        public int HighestLevelOn(Map map)
        {
            return map == null || map.mapPawns == null ? 0 : map.mapPawns.FreeColonistsSpawned
                .Select(LevelFor).DefaultIfEmpty(0).Max();
        }

        public FoundationPromotionResult PromotionStatus(Pawn pawn, FoundationSupportProgress support)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Faction != Faction.OfPlayer || pawn.skills == null)
                return FoundationPromotionResult.InvalidPawn;
            return FoundationAuthorityPolicy.CanPromote(LevelFor(pawn), support == null ? 0 : support.trust,
                pawn.ageTracker.AgeBiologicalYears,
                pawn.skills.GetSkill(SkillDefOf.Intellectual).Level,
                pawn.skills.GetSkill(SkillDefOf.Shooting).Level,
                pawn.WorkTypeIsDisabled(WorkTypeDefOf.Research));
        }

        public FoundationPromotionResult TryPromote(Pawn pawn, FoundationSupportProgress support,
            FoundationRole role = FoundationRole.None)
        {
            FoundationPromotionResult result = PromotionStatus(pawn, support);
            if (result != FoundationPromotionResult.Allowed) return result;
            int current = LevelFor(pawn);
            if (current == 3 && !FoundationAuthorityPolicy.QualifiesForLevelFour(role,
                pawn.skills.GetSkill(SkillDefOf.Intellectual).Level,
                pawn.skills.GetSkill(SkillDefOf.Shooting).Level))
                return FoundationPromotionResult.InsufficientDirectorSkills;
            if (support == null || !support.TrySpendOnCertification(FoundationAuthorityPolicy.PromotionCost(current)))
                return FoundationPromotionResult.InsufficientTrust;
            int index = pawns.IndexOf(pawn);
            if (index < 0)
            {
                pawns.Add(pawn);
                clearanceLevels.Add(current + 1);
                roles.Add((int)role);
            }
            else
            {
                clearanceLevels[index] = current + 1;
                if (current == 3) roles[index] = (int)role;
            }
            return FoundationPromotionResult.Allowed;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref pawns, "scpFoundationPersonnelPawns", LookMode.Reference);
            Scribe_Collections.Look(ref clearanceLevels, "scpFoundationPersonnelClearances", LookMode.Value);
            Scribe_Collections.Look(ref roles, "scpFoundationPersonnelRoles", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawns = pawns ?? new List<Pawn>();
                clearanceLevels = clearanceLevels ?? new List<int>();
                roles = roles ?? new List<int>();
                int count = System.Math.Min(pawns.Count, clearanceLevels.Count);
                var loadedPawns = new List<Pawn>(count);
                var loadedLevels = new List<int>(count);
                var loadedRoles = new List<int>(count);
                for (int i = 0; i < count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (pawn == null || pawn.Destroyed || loadedPawns.Contains(pawn)) continue;
                    loadedPawns.Add(pawn);
                    int level = System.Math.Max(0, System.Math.Min(FoundationAuthorityPolicy.StoryLevel, clearanceLevels[i]));
                    loadedLevels.Add(level);
                    FoundationRole role = i < roles.Count ? (FoundationRole)roles[i] : FoundationRole.None;
                    if (level >= 4 && role == FoundationRole.None && pawn.skills != null)
                        role = pawn.skills.GetSkill(SkillDefOf.Shooting).Level
                            > pawn.skills.GetSkill(SkillDefOf.Intellectual).Level
                            ? FoundationRole.MtfCaptain : FoundationRole.Scientist;
                    loadedRoles.Add((int)role);
                }
                pawns = loadedPawns;
                clearanceLevels = loadedLevels;
                roles = loadedRoles;
            }
        }
    }
}
