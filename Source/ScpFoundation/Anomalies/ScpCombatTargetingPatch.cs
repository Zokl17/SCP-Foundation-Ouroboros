using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Anomalies
{
    // Weapon verbs normally reject Item targets. These SCPs behave as combatants,
    // while SCP-049 and SCP-106 are already targetable because they are pawns.
    [HarmonyPatch(typeof(TargetingParameters), "CanTarget")]
    internal static class ScpCombatTargetingPatch
    {
        private static void Postfix(TargetInfo __0, ITargetingSource __1, ref bool __result)
        {
            Verb verb = __1 as Verb;
            if (__result || verb == null || verb.CasterPawn == null) return;
            Thing target = __0.Thing;
            if (target == null || !target.Spawned || !target.def.selectable
                || !target.def.isAutoAttackableMapObject) return;
            if (target is Thing_Scp173 || target is Thing_Scp682
                || IsScp939(target) || target is Thing_Scp096)
                __result = true;
        }

        internal static bool IsScp939(Thing thing)
        {
            return thing is Thing_ScpDirectional && thing.def != null
                && (thing.def.defName == "SCP_939Object" || thing.def.defName == "SCP_939ObjectThree");
        }
    }

    // These sprites extend beyond their occupied cells and slide while moving.
    // A click on the visible body must target the creature, not the ground beside it.
    [HarmonyPatch(typeof(GenUI), "TargetsAt", new System.Type[] {
        typeof(Vector3), typeof(TargetingParameters), typeof(bool), typeof(ITargetingSource) })]
    internal static class Scp939MouseTargetPatch
    {
        private static void Postfix(Vector3 __0, TargetingParameters __1,
            ITargetingSource __3, ref IEnumerable<LocalTargetInfo> __result)
        {
            Map map = Find.CurrentMap;
            if (map == null || __1 == null || !(__3 is Verb)) return;
            IntVec3 mouseCell = __0.ToIntVec3();
            if (!mouseCell.InBounds(map)) return;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(mouseCell, 3.5f, true))
            {
                if (!cell.InBounds(map) || cell.Fogged(map)) continue;
                foreach (Thing thing in cell.GetThingList(map))
                {
                    if (!(thing is Thing_Scp682) && !ScpCombatTargetingPatch.IsScp939(thing)) continue;
                    Thing_ScpDirectional creature = (Thing_ScpDirectional)thing;
                    CompScp682 movement682 = creature.GetComp<CompScp682>();
                    CompScp939 movement939 = creature.GetComp<CompScp939>();
                    Vector3 offset = movement682 != null ? movement682.DrawOffset
                        : movement939 != null ? movement939.DrawOffset : Vector3.zero;
                    Vector3 center = creature.DrawPos + offset;
                    Vector2 size = movement682 == null ? new Vector2(2.5f, 2.5f)
                        : creature.Rotation.IsHorizontal ? new Vector2(4f, 2f) : new Vector2(2.67f, 4f);
                    if (Mathf.Abs(center.x - __0.x) > size.x / 2f
                        || Mathf.Abs(center.z - __0.z) > size.y / 2f
                        || !__1.CanTarget(new TargetInfo(creature), __3)) continue;
                    __result = WithVisibleCreature(creature, __result);
                    return;
                }
            }
        }

        private static IEnumerable<LocalTargetInfo> WithVisibleCreature(
            Thing_ScpDirectional creature, IEnumerable<LocalTargetInfo> targets)
        {
            yield return creature;
            foreach (LocalTargetInfo target in targets)
                if (target.Thing != creature) yield return target;
        }
    }
}
