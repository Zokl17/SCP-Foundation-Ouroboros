using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp262ExtraWeapon : CompProperties
    {
        public CompProperties_Scp262ExtraWeapon()
        {
            compClass = typeof(CompScp262ExtraWeapon);
        }
    }

    public sealed class CompScp262ExtraWeapon : ThingComp
    {
        private ThingWithComps extraWeapon;
        private int nextAttackTick;

        private Pawn Wearer { get { Apparel apparel = parent as Apparel; return apparel == null ? null : apparel.Wearer; } }

        public ThingWithComps ExtraWeapon
        {
            get
            {
                Pawn pawn = Wearer;
                if (extraWeapon != null && (extraWeapon.Destroyed || pawn == null
                    || pawn.inventory == null || !pawn.inventory.innerContainer.Contains(extraWeapon)))
                    ReleaseWeapon();
                return extraWeapon;
            }
        }

        private void ReleaseWeapon()
        {
            Verb verb = GunVerb(extraWeapon);
            Pawn pawn = Wearer;
            if (verb != null && pawn != null && verb.caster == pawn)
            {
                Stance_Warmup warmup = pawn.stances.curStance as Stance_Warmup;
                if (warmup != null && warmup.verb == verb)
                    pawn.stances.CancelBusyStanceSoft();
                verb.Reset();
                verb.caster = null;
            }
            extraWeapon = null;
            nextAttackTick = 0;
        }

        private static Verb GunVerb(ThingWithComps gun)
        {
            CompEquippable equippable = gun == null ? null : gun.GetComp<CompEquippable>();
            return equippable == null ? null : equippable.PrimaryVerb;
        }

        private static bool IsSuitableGun(ThingWithComps gun)
        {
            if (gun == null || gun.Destroyed || !gun.def.IsRangedWeapon
                || gun.def.weaponTags != null && gun.def.weaponTags.Contains("Grenade"))
                return false;
            Verb verb = GunVerb(gun);
            return verb != null && !verb.IsMeleeAttack && verb.verbProps.range > 1f;
        }

        private void EnsureLegendaryQuality()
        {
            CompQuality quality = parent.GetComp<CompQuality>();
            if (quality != null && quality.Quality != QualityCategory.Legendary)
                quality.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Colony);
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            EnsureLegendaryQuality();
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            Pawn pawn = Wearer;
            if (pawn == null || !pawn.IsColonistPlayerControlled) yield break;
            yield return new Command_Action
            {
                defaultLabel = "SCP_262WeaponCommand".Translate(),
                defaultDesc = "SCP_262WeaponCommandDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("Things/Item/ScpArtifacts/Scp262Coat"),
                action = () => OpenWeaponMenu(pawn)
            };
        }

        private void OpenWeaponMenu(Pawn pawn)
        {
            var options = new List<FloatMenuOption>();
            if (ExtraWeapon != null)
                options.Add(new FloatMenuOption("SCP_262ReleaseWeapon".Translate(), ReleaseWeapon));

            ThingWithComps held = pawn.equipment == null ? null : pawn.equipment.Primary;
            if (pawn.inventory != null && IsSuitableGun(held))
            {
                options.Add(new FloatMenuOption("SCP_262UseHeldWeapon".Translate(held.LabelCap), () =>
                {
                    if (Wearer != pawn || pawn.equipment == null || pawn.inventory == null
                        || pawn.equipment.Primary != held || !IsSuitableGun(held)) return;
                    if (!pawn.equipment.TryTransferEquipmentToContainer(held, pawn.inventory.innerContainer))
                    {
                        Messages.Message("SCP_262CannotStowWeapon".Translate(), MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    ReleaseWeapon();
                    extraWeapon = held;
                }));
            }

            if (pawn.inventory != null)
            {
                foreach (Thing thing in pawn.inventory.innerContainer)
                {
                    ThingWithComps gun = thing as ThingWithComps;
                    if (!IsSuitableGun(gun) || gun == ExtraWeapon) continue;
                    ThingWithComps choice = gun;
                    options.Add(new FloatMenuOption(choice.LabelCap, () =>
                    {
                        ReleaseWeapon();
                        extraWeapon = choice;
                    }));
                }
            }

            if (options.Count == 0)
                options.Add(new FloatMenuOption("SCP_262NoWeapon".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override void CompTick()
        {
            base.CompTick();
            EnsureLegendaryQuality();
            Pawn pawn = Wearer;
            ThingWithComps gun = ExtraWeapon;
            if (pawn == null || gun == null || !IsSuitableGun(gun)) return;

            Verb verb = GunVerb(gun);
            if (verb.caster != pawn) verb.caster = pawn;
            // Inventory weapons with TickerType.Never do not tick their verbs on their own.
            if (gun.def.tickerType == TickerType.Never)
                gun.GetComp<CompEquippable>().VerbTracker.VerbsTick();

            int now = Find.TickManager.TicksGame;
            if (!pawn.Drafted || !pawn.Spawned || pawn.DeadOrDowned || verb.state == VerbState.Bursting
                || now < nextAttackTick || pawn.stances.curStance is Stance_Warmup)
                return;

            Thing target = FindTarget(pawn, verb);
            if (target == null) { nextAttackTick = now + 30; return; }
            if (!verb.Available() || !verb.TryStartCastOn(new LocalTargetInfo(target), false, false, true))
            {
                nextAttackTick = now + 60;
                return;
            }

            float cooldown = gun.GetStatValue(StatDefOf.RangedWeapon_Cooldown);
            nextAttackTick = now + Math.Max(90,
                (int)Math.Ceiling((verb.WarmupTime + Math.Max(0f, cooldown)) * 60f));
        }

        private static Thing FindTarget(Pawn pawn, Verb verb)
        {
            Thing target = pawn.CurJob == null ? null : pawn.CurJob.targetA.Thing;
            if (CanAttack(pawn, verb, target)) return target;
            target = pawn.mindState == null ? null : pawn.mindState.enemyTarget;
            if (CanAttack(pawn, verb, target)) return target;

            Thing closest = null;
            float bestDistance = float.MaxValue;
            foreach (Pawn candidate in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (!CanAttack(pawn, verb, candidate)) continue;
                float distance = (candidate.Position - pawn.Position).LengthHorizontalSquared;
                if (distance >= bestDistance) continue;
                closest = candidate;
                bestDistance = distance;
            }
            return closest;
        }

        private static bool CanAttack(Pawn pawn, Verb verb, Thing target)
        {
            return target != null && target.Spawned && target.Map == pawn.Map
                && target.HostileTo(pawn) && verb.CanHitTarget(new LocalTargetInfo(target));
        }

        public override string CompInspectStringExtra()
        {
            ThingWithComps gun = ExtraWeapon;
            return gun == null ? null : "SCP_262WeaponLoaded".Translate(gun.LabelCap);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            ReleaseWeapon();
            base.Notify_Unequipped(pawn);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref extraWeapon, "scp262ExtraWeapon");
            Scribe_Values.Look(ref nextAttackTick, "scp262NextAttackTick", 0);
        }
    }

    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(PawnRenderUtility), "DrawEquipmentAndApparelExtras")]
    internal static class Scp262ExtraWeaponDrawPatch
    {
        private static Graphic activeHandsGraphic;
        private static Graphic idleHandsGraphic;

        private static void Postfix(Pawn pawn, Vector3 drawPos, Rot4 facing)
        {
            if (pawn == null || pawn.Dead || pawn.apparel == null) return;
            foreach (Apparel worn in pawn.apparel.WornApparel)
            {
                CompScp262ExtraWeapon coat = worn.GetComp<CompScp262ExtraWeapon>();
                if (coat == null) continue;
                bool active = coat.ExtraWeapon != null || pawn.CurJob != null && pawn.CurJob.workGiverDef != null;
                if (active && facing != Rot4.North)
                {
                    if (activeHandsGraphic == null)
                        activeHandsGraphic = GraphicDatabase.Get<Graphic_Multi>(
                        "Things/Pawn/Humanlike/Apparel/Scp262Arms/Scp262Arms",
                        ShaderDatabase.Cutout, new Vector2(1f, 1f), Color.white);
                    float armOffset = facing == Rot4.East ? 0.08f : facing == Rot4.West ? -0.08f : 0f;
                    activeHandsGraphic.Draw(drawPos + new Vector3(armOffset, 0.02f, 0f), facing, pawn);
                }
                else if (facing == Rot4.South)
                {
                    if (idleHandsGraphic == null)
                        idleHandsGraphic = GraphicDatabase.Get<Graphic_Single>(
                            "Things/Pawn/Humanlike/Apparel/Scp262Arms/Scp262ArmsIdle",
                            ShaderDatabase.Cutout, new Vector2(1f, 1f), Color.white);
                    idleHandsGraphic.Draw(drawPos + new Vector3(0f, 0.02f, -0.15f), facing, pawn);
                }

                ThingWithComps gun = coat.ExtraWeapon;
                if (gun == null) continue;
                float side = facing == Rot4.West || facing == Rot4.North ? -0.32f : 0.32f;
                Vector3 position = drawPos + new Vector3(side, 0.01f, -0.05f);
                float angle = facing == Rot4.East ? 90f : facing == Rot4.West ? 270f
                    : facing == Rot4.North ? 180f : 0f;
                PawnRenderUtility.DrawEquipmentAiming(gun, position, angle);
                break;
            }
        }
    }
}
