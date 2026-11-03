using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ScpFoundation.Interactions
{
    public struct ScpExternalImpactState
    {
        internal Thing PreviousWeapon;
        internal bool PreviousImpact;
    }

    // CE projectiles are ThingWithComps rather than vanilla Projectile. The
    // optional adapter enters this scope only while CE applies a bullet hit.
    public static class ScpExternalProjectileContext
    {
        public static ScpExternalImpactState Begin(Thing weapon)
        {
            var state = new ScpExternalImpactState
            {
                PreviousWeapon = ScpWeaponEffects.CurrentShotWeapon,
                PreviousImpact = ScpWeaponEffects.ExternalProjectileImpact
            };
            ScpWeaponEffects.CurrentShotWeapon = weapon;
            ScpWeaponEffects.ExternalProjectileImpact = true;
            return state;
        }

        public static void End(ScpExternalImpactState state)
        {
            ScpWeaponEffects.CurrentShotWeapon = state.PreviousWeapon;
            ScpWeaponEffects.ExternalProjectileImpact = state.PreviousImpact;
        }
    }

    public sealed class ScpWeaponTraitExtension : DefModExtension
    {
        public string effect;
        public float magnitude = 1f;
    }

    internal static class ScpWeaponEffects
    {
        private sealed class FireModeState { public bool SingleShot; }
        private static readonly ConditionalWeakTable<CompUniqueWeapon, FireModeState> FireModes =
            new ConditionalWeakTable<CompUniqueWeapon, FireModeState>();
        private static readonly FieldInfo ProjectileEquipment = AccessTools.Field(typeof(Projectile), "equipment");
        private static readonly FieldInfo LastMovedTick = AccessTools.Field(typeof(Pawn_PathFollower), "lastMovedTick");
        private static readonly FieldInfo BurstShotsLeft = AccessTools.Field(typeof(Verb), "burstShotsLeft");
        private static readonly FieldInfo ShotEquipmentFactor = AccessTools.Field(typeof(ShotReport), "factorFromEquipment");
        private static readonly FieldInfo ShotDarknessPenalty = AccessTools.Field(typeof(ShotReport), "offsetFromDarkness");
        [ThreadStatic] private static Projectile currentProjectile;
        [ThreadStatic] private static Thing currentShotWeapon;
        [ThreadStatic] private static bool externalProjectileImpact;
        [ThreadStatic] private static bool applyingSecondaryDamage;

        internal static ScpWeaponTraitExtension Get(Thing weapon, string effect)
        {
            CompUniqueWeapon comp = weapon == null ? null : weapon.TryGetComp<CompUniqueWeapon>();
            if (comp == null || comp.TraitsListForReading == null) return null;
            foreach (WeaponTraitDef trait in comp.TraitsListForReading)
            {
                ScpWeaponTraitExtension extension = trait.GetModExtension<ScpWeaponTraitExtension>();
                if (extension != null && extension.effect == effect) return extension;
            }
            return null;
        }

        internal static bool SingleShot(Thing weapon)
        {
            CompUniqueWeapon comp = weapon == null ? null : weapon.TryGetComp<CompUniqueWeapon>();
            return comp != null && Get(weapon, "FireSelector") != null && FireModes.GetOrCreateValue(comp).SingleShot;
        }

        internal static void ToggleSingleShot(Thing weapon)
        {
            CompUniqueWeapon comp = weapon == null ? null : weapon.TryGetComp<CompUniqueWeapon>();
            if (comp != null && Get(weapon, "FireSelector") != null)
                FireModes.GetOrCreateValue(comp).SingleShot = !FireModes.GetOrCreateValue(comp).SingleShot;
        }

        internal static void SaveFireMode(CompUniqueWeapon comp)
        {
            if (comp == null || comp.TraitsListForReading == null ||
                !comp.TraitsListForReading.Any(t => t.defName == "SCP_FireSelector")) return;
            FireModeState state = FireModes.GetOrCreateValue(comp);
            bool singleShot = state.SingleShot;
            Scribe_Values.Look(ref singleShot, "scpWeaponSingleShot", false);
            state.SingleShot = singleShot;
        }

        internal static void StartProjectile(Projectile projectile) { currentProjectile = projectile; }
        internal static Projectile CurrentProjectile { get { return currentProjectile; } set { currentProjectile = value; } }
        internal static Thing CurrentShotWeapon { get { return currentShotWeapon; } set { currentShotWeapon = value; } }
        internal static bool ExternalProjectileImpact { get { return externalProjectileImpact; } set { externalProjectileImpact = value; } }
        internal static bool ApplyingSecondaryDamage { get { return applyingSecondaryDamage; } set { applyingSecondaryDamage = value; } }

        internal static Thing WeaponFor(DamageInfo info)
        {
            Thing weapon = currentProjectile != null ? ProjectileEquipment.GetValue(currentProjectile) as Thing
                : externalProjectileImpact ? currentShotWeapon : null;
            if (weapon == null)
            {
                Pawn shooter = info.Instigator as Pawn;
                weapon = shooter == null || shooter.equipment == null ? null : shooter.equipment.Primary;
            }
            return weapon != null && (info.Weapon == weapon.def ||
                externalProjectileImpact && info.Weapon == null) ? weapon : null;
        }

        internal static void AddTimedHediff(Pawn pawn, string defName)
        {
            if (pawn == null || pawn.Dead) return;
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (def == null) return;
            Hediff old = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (old != null) pawn.health.RemoveHediff(old);
            pawn.health.AddHediff(def);
        }

        internal static void ApplyShotReport(Thing caster, Verb verb, LocalTargetInfo target, ref ShotReport result)
        {
            Thing weapon = verb == null ? null : verb.EquipmentSource;
            if (weapon == null || weapon.TryGetComp<CompUniqueWeapon>() == null) return;
            Pawn shooter = caster as Pawn;
            Pawn targetPawn = target.Thing as Pawn;
            float factor = 1f;
            bool moving = targetPawn != null && targetPawn.pather != null && targetPawn.pather.MovingNow;
            ScpWeaponTraitExtension extension = Get(weapon, "MovingTargetAccuracy");
            if (moving && extension != null) factor *= extension.magnitude;
            extension = Get(weapon, "StationaryAccuracy");
            if (shooter != null && shooter.pather != null && extension != null &&
                !shooter.pather.MovingNow && Find.TickManager.TicksGame -
                (int)LastMovedTick.GetValue(shooter.pather) >= 120) factor *= extension.magnitude;
            extension = Get(weapon, "FirstShotAccuracy");
            if (extension != null && BurstShotsLeft != null &&
                (int)BurstShotsLeft.GetValue(verb) == verb.BurstShotCount) factor *= extension.magnitude;
            if (targetPawn != null)
            {
                HediffDef tracerDef = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_WeaponTracerMark");
                Hediff_WeaponTracer tracer = tracerDef == null ? null :
                    targetPawn.health.hediffSet.GetFirstHediffOfDef(tracerDef) as Hediff_WeaponTracer;
                if (tracer != null && shooter != null && tracer.source != null &&
                    tracer.source.Faction == shooter.Faction) factor *= 1.10f;
                HediffDef flashDef = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_WeaponFlashConcealment");
                if (flashDef != null && targetPawn.health.hediffSet.HasHediff(flashDef) &&
                    targetPawn.Map != null && targetPawn.Map.glowGrid.GroundGlowAt(targetPawn.Position) < 0.30f)
                    factor *= 0.90f;
            }
            if (factor == 1f && Get(weapon, "ThermalSight") == null) return;
            object boxed = result;
            if (factor != 1f)
                ShotEquipmentFactor.SetValue(boxed, (float)ShotEquipmentFactor.GetValue(boxed) * factor);
            if (Get(weapon, "ThermalSight") != null) ShotDarknessPenalty.SetValue(boxed, 0f);
            result = (ShotReport)boxed;
        }

        internal static void BeforeDamage(Thing victim, ref DamageInfo info)
        {
            if (applyingSecondaryDamage || victim == null) return;
            Thing weapon = WeaponFor(info);
            if (weapon == null) return;
            bool projectile = currentProjectile != null || externalProjectileImpact;
            ScpWeaponTraitExtension extension = projectile ? Get(weapon, "NonlethalRounds") : Get(weapon, "Bayonet");
            if (extension != null) info.SetAmount(Mathf.Max(1f, info.Amount * extension.magnitude));
            if (!projectile) return;
            extension = Get(weapon, "BioferriteResonator");
            Pawn shooter = info.Instigator as Pawn;
            if (extension != null && victim.def.defName.StartsWith("SCP_", StringComparison.Ordinal) &&
                (shooter == null || victim.Faction != shooter.Faction))
                info.SetAmount(info.Amount * extension.magnitude);
            HediffDef analysisDef = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_WeaponEnemyAnalysis");
            Hediff_WeaponAnalysis analysis = victim is Pawn && analysisDef != null
                ? ((Pawn)victim).health.hediffSet.GetFirstHediffOfDef(
                    analysisDef) as Hediff_WeaponAnalysis : null;
            if (analysis != null && analysis.weapon == weapon && Get(weapon, "EnemyAnalysis") != null)
                info.SetAmount(info.Amount * (1f + analysis.Severity));
        }

        internal static void AfterDamage(Thing victim, DamageInfo info, DamageWorker.DamageResult result)
        {
            if (applyingSecondaryDamage || currentProjectile == null && !externalProjectileImpact || !(victim is Pawn) ||
                result == null || result.totalDamageDealt <= 0f) return;
            Thing weapon = WeaponFor(info);
            if (weapon == null) return;
            Pawn pawn = (Pawn)victim;
            if (pawn.Dead) return;
            if (Get(weapon, "EnemyAnalysis") != null)
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_WeaponEnemyAnalysis");
                Hediff_WeaponAnalysis analysis = pawn.health.hediffSet.GetFirstHediffOfDef(def) as Hediff_WeaponAnalysis;
                if (analysis == null || analysis.weapon != weapon)
                {
                    if (analysis != null) pawn.health.RemoveHediff(analysis);
                    analysis = (Hediff_WeaponAnalysis)pawn.health.AddHediff(def);
                    analysis.weapon = weapon;
                    analysis.Severity = 0f;
                }
                analysis.Severity = Mathf.Min(0.15f, analysis.Severity + 0.05f);
                analysis.expiresAt = Find.TickManager.TicksGame + 2500;
            }
            if (Get(weapon, "TracerRounds") != null)
            {
                AddTimedHediff(pawn, "SCP_WeaponTracerMark");
                Hediff_WeaponTracer tracer = pawn.health.hediffSet.GetFirstHediffOfDef(
                    DefDatabase<HediffDef>.GetNamedSilentFail("SCP_WeaponTracerMark")) as Hediff_WeaponTracer;
                if (tracer != null) tracer.source = info.Instigator as Pawn;
            }
            HediffDef sedationDef = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_TranqSedation");
            if (Get(weapon, "NonlethalRounds") != null && sedationDef != null)
                HealthUtility.AdjustSeverity(pawn, sedationDef, 0.25f);
            if (Get(weapon, "CryoRounds") != null) AddTimedHediff(pawn, "SCP_WeaponCryoSlow");
            if (Get(weapon, "CorrosiveRounds") != null && pawn.apparel != null && pawn.apparel.WornApparel.Count > 0)
            {
                Apparel armor = pawn.apparel.WornApparel.Find(a => a.GetStatValue(StatDefOf.ArmorRating_Sharp) > 0f);
                if (armor != null)
                {
                    applyingSecondaryDamage = true;
                    try { armor.TakeDamage(new DamageInfo(DamageDefOf.Burn, 5f, 0f, -1f, info.Instigator)); }
                    finally { applyingSecondaryDamage = false; }
                }
            }
            ScpWeaponTraitExtension ricochet = Get(weapon, "RicochetRounds");
            if (ricochet != null && pawn.Map != null && Rand.Chance(0.30f))
            {
                Pawn shooter = info.Instigator as Pawn;
                Pawn other = null;
                foreach (Pawn candidate in pawn.Map.mapPawns.AllPawnsSpawned)
                {
                    if (candidate != pawn && !candidate.Dead &&
                        candidate.Position.InHorDistOf(pawn.Position, 4f) &&
                        (shooter == null || candidate.Faction != shooter.Faction) &&
                        GenSight.LineOfSight(pawn.Position, candidate.Position, pawn.Map))
                    { other = candidate; break; }
                }
                if (other != null)
                {
                    applyingSecondaryDamage = true;
                    try { other.TakeDamage(new DamageInfo(DamageDefOf.Bullet,
                        Mathf.Max(1f, info.Amount * ricochet.magnitude), 0.2f, -1f, info.Instigator)); }
                    finally { applyingSecondaryDamage = false; }
                }
            }
        }
    }

    public sealed class Hediff_WeaponAnalysis : HediffWithComps
    {
        public Thing weapon;
        public int expiresAt;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref weapon, "scpAnalyzedByWeapon");
            Scribe_Values.Look(ref expiresAt, "scpAnalysisExpiresAt", 0);
        }
        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (expiresAt > 0 && Find.TickManager.TicksGame >= expiresAt)
                pawn.health.RemoveHediff(this);
        }
    }

    public sealed class Hediff_WeaponTracer : HediffWithComps
    {
        public Pawn source;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref source, "scpTracerSource");
        }
    }

    [HarmonyPatch(typeof(ShotReport), "HitReportFor")]
    internal static class ScpWeaponShotReportPatch
    {
        private static void Postfix(Thing caster, Verb verb, LocalTargetInfo target, ref ShotReport __result)
        {
            ScpWeaponEffects.ApplyShotReport(caster, verb, target, ref __result);
        }
    }

    [HarmonyPatch(typeof(Thing), "TakeDamage")]
    internal static class ScpWeaponDamagePatch
    {
        private static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            ScpWeaponEffects.BeforeDamage(__instance, ref dinfo);
        }
        private static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
        {
            ScpWeaponEffects.AfterDamage(__instance, dinfo, __result);
        }
    }

    [HarmonyPatch(typeof(Projectile), "Impact")]
    internal static class ScpWeaponProjectileContextPatch
    {
        private static void Prefix(Projectile __instance, out Projectile __state)
        {
            __state = ScpWeaponEffects.CurrentProjectile;
            ScpWeaponEffects.StartProjectile(__instance);
        }
        private static void Postfix(Projectile __state) { ScpWeaponEffects.CurrentProjectile = __state; }
    }

    // Bullet.Impact calls the base method before applying damage. Keep the projectile
    // available for the whole bullet impact, including its later TakeDamage call.
    [HarmonyPatch(typeof(Bullet), "Impact")]
    internal static class ScpWeaponBulletContextPatch
    {
        private static void Prefix(Bullet __instance, out Projectile __state)
        {
            __state = ScpWeaponEffects.CurrentProjectile;
            ScpWeaponEffects.CurrentProjectile = __instance;
        }
        private static void Postfix(Projectile __state) { ScpWeaponEffects.CurrentProjectile = __state; }
    }

    [HarmonyPatch(typeof(Projectile), "CanHit")]
    internal static class ScpWeaponFriendlyFirePatch
    {
        private static readonly FieldInfo Equipment = AccessTools.Field(typeof(Projectile), "equipment");
        private static void Postfix(Projectile __instance, Thing thing, ref bool __result)
        {
            if (!__result || !(thing is Pawn) || !(__instance.Launcher is Pawn)) return;
            Thing weapon = Equipment.GetValue(__instance) as Thing;
            if (ScpWeaponEffects.Get(weapon, "FriendlyFireLock") != null &&
                thing.Faction != null && __instance.Launcher.Faction != null &&
                (thing.Faction == __instance.Launcher.Faction ||
                 thing.Faction.RelationKindWith(__instance.Launcher.Faction) == FactionRelationKind.Ally))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(Verb_Shoot), "get_ShotsPerBurst")]
    internal static class ScpWeaponFireSelectorBurstPatch
    {
        private static void Postfix(Verb_Shoot __instance, ref int __result)
        {
            if (ScpWeaponEffects.SingleShot(__instance.EquipmentSource)) __result = 1;
        }
    }

    [HarmonyPatch(typeof(CompUniqueWeapon), "PostExposeData")]
    internal static class ScpWeaponFireModeSavePatch
    {
        private static void Postfix(CompUniqueWeapon __instance) { ScpWeaponEffects.SaveFireMode(__instance); }
    }

    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    internal static class ScpWeaponFireModeGizmoPatch
    {
        private static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance.Faction != Faction.OfPlayer || __instance.equipment == null ||
                ScpWeaponEffects.Get(__instance.equipment.Primary, "FireSelector") == null) return;
            __result = WithFireSelector(__result, __instance.equipment.Primary);
        }
        private static IEnumerable<Gizmo> WithFireSelector(IEnumerable<Gizmo> original, Thing weapon)
        {
            foreach (Gizmo gizmo in original) yield return gizmo;
            yield return new Command_Action {
                defaultLabel = (ScpWeaponEffects.SingleShot(weapon)
                    ? "SCP_WeaponSingleFire" : "SCP_WeaponBurstFire").Translate(),
                defaultDesc = "SCP_WeaponFireModeDesc".Translate(),
                action = () => ScpWeaponEffects.ToggleSingleShot(weapon)
            };
        }
    }

    [HarmonyPatch(typeof(Verb), "TryCastNextBurstShot")]
    internal static class ScpWeaponShotVisualContextPatch
    {
        private static void Prefix(Verb __instance, out Thing __state)
        {
            __state = ScpWeaponEffects.CurrentShotWeapon;
            ScpWeaponEffects.CurrentShotWeapon = __instance.EquipmentSource;
        }
        private static void Postfix(Verb __instance, Thing __state)
        {
            if (ScpWeaponEffects.Get(__instance.EquipmentSource, "FlashHider") != null)
                ScpWeaponEffects.AddTimedHediff(__instance.CasterPawn, "SCP_WeaponFlashConcealment");
            ScpWeaponEffects.CurrentShotWeapon = __state;
        }
    }

    [HarmonyPatch(typeof(SoundStarter), "PlayOneShot", new Type[] { typeof(SoundDef), typeof(SoundInfo) })]
    internal static class ScpWeaponSuppressSoundPatch
    {
        private static bool Prefix() { return ScpWeaponEffects.Get(ScpWeaponEffects.CurrentShotWeapon, "Suppressor") == null; }
    }

    [HarmonyPatch(typeof(SoundStarter), "PlayOneShotOnCamera", new Type[] { typeof(SoundDef), typeof(Map) })]
    internal static class ScpWeaponSuppressTailPatch
    {
        private static bool Prefix() { return ScpWeaponEffects.Get(ScpWeaponEffects.CurrentShotWeapon, "Suppressor") == null; }
    }

    [HarmonyPatch(typeof(FleckMaker), "Static", new Type[] { typeof(IntVec3), typeof(Map), typeof(FleckDef), typeof(float) })]
    internal static class ScpWeaponFlashHiderPatch
    {
        private static bool Prefix(FleckDef fleckDef)
        {
            return fleckDef != FleckDefOf.ShotFlash ||
                ScpWeaponEffects.Get(ScpWeaponEffects.CurrentShotWeapon, "FlashHider") == null;
        }
    }

    [HarmonyPatch(typeof(CompShield), "PostPreApplyDamage")]
    internal static class ScpWeaponShieldBreakerPatch
    {
        private static readonly MethodInfo EnergyMax = AccessTools.PropertyGetter(typeof(CompShield), "EnergyMax");
        private static readonly MethodInfo BreakShield = AccessTools.Method(typeof(CompShield), "Break");
        private static void Postfix(CompShield __instance, DamageInfo dinfo, bool absorbed, ref float ___energy)
        {
            if (!absorbed || ___energy <= 0f ||
                ScpWeaponEffects.Get(ScpWeaponEffects.WeaponFor(dinfo), "ShieldBreakerRounds") == null) return;
            float max = (float)EnergyMax.Invoke(__instance, null);
            ___energy = Mathf.Max(0f, ___energy - max * 0.25f);
            if (___energy <= 0f) BreakShield.Invoke(__instance, null);
        }
    }
}
