using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    // Each module is a normal vanilla turret. Replacing the building changes only
    // its gun/top definition; the shared emplacement and its position stay the same.
    public sealed class Building_ModularGroundTurret : Building_TurretGun
    {
        protected override void Tick()
        {
            base.Tick();
            if (CurrentTarget.IsValid) Top.ForceFaceTarget(CurrentTarget);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (Faction != Faction.OfPlayer || !Spawned) yield break;

            foreach (string name in new[] { "SCP_GroundQuadTurret", "SCP_GroundMinigunTurret", "SCP_GroundNavalTripleTurret", "SCP_GroundSingleCannonTurret" })
            {
                if (name == def.defName) continue;
                ThingDef replacement = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (replacement == null) continue;
                string label = name == "SCP_GroundQuadTurret" ? "SCP_SwitchToQuad"
                    : name == "SCP_GroundMinigunTurret" ? "SCP_SwitchToMinigun"
                    : name == "SCP_GroundNavalTripleTurret" ? "SCP_SwitchToNavalTriple" : "SCP_SwitchToSingleCannon";
                yield return new Command_Action
                {
                    defaultLabel = label.Translate(),
                    defaultDesc = "SCP_SwitchTurretModuleDesc".Translate(),
                    icon = replacement.building.turretGunDef.uiIcon,
                    action = () => ModularTurretSwap.Replace(this, replacement)
                };
            }
            ThingDef emptyMount = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_GroundTurretMount");
            if (emptyMount != null)
                yield return new Command_Action
                {
                    defaultLabel = "SCP_RemoveTurretModule".Translate(),
                    icon = emptyMount.uiIcon,
                    action = () => ModularTurretSwap.Replace(this, emptyMount)
                };
        }
    }

    public sealed class Building_ModularTurretMount : Building
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (Faction != Faction.OfPlayer || !Spawned) yield break;
            foreach (string name in new[] { "SCP_GroundQuadTurret", "SCP_GroundMinigunTurret", "SCP_GroundNavalTripleTurret", "SCP_GroundSingleCannonTurret" })
            {
                ThingDef module = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (module == null) continue;
                string label = name == "SCP_GroundQuadTurret" ? "SCP_InstallQuad"
                    : name == "SCP_GroundMinigunTurret" ? "SCP_InstallMinigun"
                    : name == "SCP_GroundNavalTripleTurret" ? "SCP_InstallNavalTriple" : "SCP_InstallSingleCannon";
                yield return new Command_Action
                {
                    defaultLabel = label.Translate(),
                    defaultDesc = "SCP_InstallTurretModuleDesc".Translate(),
                    icon = module.building.turretGunDef.uiIcon,
                    action = () => ModularTurretSwap.Replace(this, module)
                };
            }
        }
    }

    internal static class ModularTurretSwap
    {
        internal static void Replace(Thing origin, ThingDef replacement)
        {
            if (!origin.Spawned || origin.Map == null || replacement == null) return;
            Map map = origin.Map;
            IntVec3 position = origin.Position;
            Rot4 rotation = origin.Rotation;
            Faction owner = origin.Faction;
            float healthFraction = (float)origin.HitPoints / origin.MaxHitPoints;
            Thing next = ThingMaker.MakeThing(replacement);
            origin.Destroy(DestroyMode.Vanish);
            next.SetFaction(owner);
            next.HitPoints = System.Math.Max(1, (int)(next.MaxHitPoints * healthFraction));
            GenSpawn.Spawn(next, position, map, rotation);
        }
    }

    [StaticConstructorOnStartup]
    internal static class ModularTurretTopCentering
    {
        static ModularTurretTopCentering()
        {
            new Harmony("local.scp5000.foundation.modular-turret-centering").Patch(
                AccessTools.Method(typeof(TurretTop), "DrawTurret"),
                prefix: new HarmonyMethod(typeof(ModularTurretTopCentering), "CenterTop"));
        }

        private static void CenterTop(TurretTop __instance, Building_Turret ___parentTurret,
            ref Vector3 __1)
        {
            Building_ModularGroundTurret turret = ___parentTurret as Building_ModularGroundTurret;
            if (turret == null) return;
            float forward = turret.def.defName == "SCP_GroundNavalTripleTurret" ? 0.6f
                : turret.def.defName == "SCP_GroundSingleCannonTurret"
                || turret.def.defName == "SCP_GroundQuadTurret"
                || turret.def.defName == "SCP_GroundMinigunTurret" ? 0.5f : 0f;
            if (forward > 0f)
                __1 += new Vector3(0f, 0f, forward).RotatedBy(__instance.CurRotation);
        }
    }
}
