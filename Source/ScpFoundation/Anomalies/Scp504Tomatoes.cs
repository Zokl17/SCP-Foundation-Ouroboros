using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Runtime;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp504 : CompProperties
    {
        public CompProperties_Scp504() { compClass = typeof(CompScp504); }
    }

    public sealed class CompScp504 : ThingComp
    {
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            return SpeechTestOptions(pawn, parent);
        }

        internal static IEnumerable<FloatMenuOption> SpeechTestOptions(Pawn pawn, Thing source)
        {
            if (pawn == null || !pawn.IsColonistPlayerControlled || !source.Spawned) yield break;
            CompScp504Bush bush = source.TryGetComp<CompScp504Bush>();
            if (bush != null && !CompScpPost.IsObjectAtPost(source))
            {
                yield return new FloatMenuOption("SCP_504NeedsPost".Translate(), null);
                yield break;
            }
            if (!HasRipeFruit(source))
            {
                yield return new FloatMenuOption("SCP_504NoRipeFruit".Translate(), null);
                yield break;
            }
            var listeners = source.Map.mapPawns.FreeColonistsSpawned.Where(other =>
                other != pawn && !other.Downed && other.Awake() && CanHear(source, other));
            bool found = false;
            foreach (Pawn listener in listeners)
            {
                found = true;
                Pawn chosen = listener;
                bool reachable = pawn.CanReserveAndReach(chosen, PathEndMode.Touch, Danger.Some);
                yield return new FloatMenuOption("SCP_504TestWith".Translate(chosen.LabelShortCap), reachable ? (System.Action)(() =>
                {
                    JobDef definition = DefDatabase<JobDef>.GetNamedSilentFail("SCP_504SpeechTest");
                    if (definition != null)
                        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(definition, source, chosen), JobTag.Misc);
                }) : null);
            }
            if (!found) yield return new FloatMenuOption("SCP_504NeedListener".Translate(), null);
        }

        internal static bool HasRipeFruit(Thing source)
        {
            if (source == null || !source.Spawned) return false;
            CompScp504Bush bush = source.TryGetComp<CompScp504Bush>();
            return bush != null ? bush.RipeCount > 0 : source.TryGetComp<CompScp504>() != null && source.stackCount > 0;
        }

        internal static bool CanHear(Thing tomatoes, Pawn speaker)
        {
            return tomatoes != null && tomatoes.Spawned && speaker != null && speaker.Spawned
                && !speaker.Dead && tomatoes.Map == speaker.Map
                && tomatoes.Position.DistanceToSquared(speaker.Position) <= Scp504Policy.HearingRadius * Scp504Policy.HearingRadius
                && tomatoes.Position.GetRoom(tomatoes.Map) == speaker.GetRoom();
        }

        internal bool LaunchAt(Pawn speaker)
        {
            if (!CanHear(parent, speaker) || parent.stackCount < 1 || !LaunchProjectile(parent, speaker)) return false;
            parent.SplitOff(1).Destroy(DestroyMode.Vanish);
            return true;
        }

        internal static bool LaunchProjectile(Thing source, Pawn speaker)
        {
            ThingDef projectileDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_504TomatoProjectile");
            if (projectileDef == null) return false;
            Map map = source.Map;
            Vector3 origin = source.Position.ToVector3Shifted();
            Projectile projectile = GenSpawn.Spawn(ThingMaker.MakeThing(projectileDef), source.Position, map) as Projectile;
            if (projectile == null) return false;
            projectile.Launch(source, origin, speaker, speaker,
                ProjectileHitFlags.IntendedTarget | ProjectileHitFlags.NonTargetWorld);
            return true;
        }
    }

    public sealed class CompProperties_Scp504Bush : CompProperties
    {
        public CompProperties_Scp504Bush() { compClass = typeof(CompScp504Bush); }
    }

    [StaticConstructorOnStartup]
    public sealed class CompScp504Bush : ThingComp
    {
        private static Graphic ripeGraphic;
        private static readonly Vector3[] FruitOffsets =
        {
            new Vector3(0.13f, 0.03f, 0.12f), new Vector3(0.20f, 0.03f, 0.01f),
            new Vector3(0.33f, 0.03f, 0.04f), new Vector3(0.27f, 0.03f, -0.06f),
            new Vector3(0.37f, 0.03f, -0.10f)
        };
        private int ripeCount;
        private int growthProgress;
        private int lastGrowthTick;

        public int RipeCount { get { return ripeCount; } }

        internal bool DebugRipenOneFruit()
        {
            CompScp scp = parent.TryGetComp<CompScp>();
            if (!parent.Spawned || scp == null || !scp.IsInCustody
                || !CompScpPost.IsObjectAtPost(parent) || ripeCount >= Scp504Policy.FruitCapacity) return false;
            ripeCount = Scp504Policy.Grow(ripeCount, ref growthProgress,
                Scp504Policy.GrowthTicksPerFruit, true);
            return true;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            lastGrowthTick = Find.TickManager.TicksGame;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref ripeCount, "scp504RipeCount", 0);
            Scribe_Values.Look(ref growthProgress, "scp504GrowthProgress", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ripeCount = Mathf.Clamp(ripeCount, 0, Scp504Policy.FruitCapacity);
                growthProgress = Mathf.Clamp(growthProgress, 0, Scp504Policy.GrowthTicksPerFruit - 1);
            }
        }

        public override void CompTickRare()
        {
            if (!parent.Spawned) return;
            int now = Find.TickManager.TicksGame;
            int elapsed = Mathf.Clamp(now - lastGrowthTick, 0, GenTicks.TickRareInterval);
            lastGrowthTick = now;
            CompScp scp = parent.TryGetComp<CompScp>();
            bool atPost = scp != null && scp.IsInCustody && CompScpPost.IsObjectAtPost(parent);
            ripeCount = Scp504Policy.Grow(ripeCount, ref growthProgress, elapsed, atPost);
        }

        public override string CompInspectStringExtra()
        {
            string count = "SCP_504RipeCount".Translate(ripeCount, Scp504Policy.FruitCapacity);
            if (!parent.Spawned || !CompScpPost.IsObjectAtPost(parent)) return count + "\n" + "SCP_504NeedsPost".Translate();
            if (ripeCount >= Scp504Policy.FruitCapacity) return count;
            return count + "\n" + "SCP_504NextFruit".Translate(
                GenDate.ToStringTicksToPeriod(Scp504Policy.GrowthTicksPerFruit - growthProgress, allowSeconds: false));
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            return CompScp504.SpeechTestOptions(pawn, parent);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (ripeCount == 0) return;
            if (ripeGraphic == null)
                ripeGraphic = GraphicDatabase.Get<Graphic_Single>("Things/Item/Scp504RipeFruit",
                    ShaderDatabase.Cutout, new Vector2(0.18f, 0.18f), Color.white);
            for (int i = 0; i < ripeCount; i++)
                ripeGraphic.Draw(parent.DrawPos + FruitOffsets[i], Rot4.North, parent);
        }

        internal bool LaunchAt(Pawn speaker)
        {
            if (ripeCount < 1 || !CompScp504.CanHear(parent, speaker)
                || !CompScp504.LaunchProjectile(parent, speaker)) return false;
            ripeCount--;
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractWith")]
    internal static class Scp504SocialPatch
    {
        private static void Postfix(bool __result, Pawn ___pawn, InteractionDef intDef)
        {
            if (!__result || ___pawn == null || !___pawn.Spawned || intDef == null
                || !Scp504Policy.Provokes(intDef.defName)) return;
            ThingDef tomatoes = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_504Tomatoes");
            ThingDef bush = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_504Bush");
            if (tomatoes == null || bush == null) return;
            Thing nearest = ___pawn.Map.listerThings.ThingsOfDef(tomatoes)
                .Concat(___pawn.Map.listerThings.ThingsOfDef(bush))
                .Where(thing => CompScp504.CanHear(thing, ___pawn) && CompScp504.HasRipeFruit(thing))
                .OrderBy(thing => thing.Position.DistanceToSquared(___pawn.Position))
                .FirstOrDefault();
            if (nearest == null) return;
            CompScp504Bush plant = nearest.TryGetComp<CompScp504Bush>();
            if (plant != null) plant.LaunchAt(___pawn);
            else nearest.TryGetComp<CompScp504>().LaunchAt(___pawn);
        }
    }

    public sealed class Projectile_Scp504Tomato : Projectile
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Pawn victim = blockedByShield ? null : hitThing as Pawn;
            if (victim != null && victim.health != null && victim.RaceProps != null && victim.RaceProps.body != null)
            {
                BodyPartRecord limb = victim.RaceProps.body.AllParts.FirstOrDefault(part =>
                    (part.def == BodyPartDefOf.Arm || part.def == BodyPartDefOf.Leg)
                    && victim.health.hediffSet.GetPartHealth(part) > 1f);
                if (limb != null)
                {
                    float amount = Scp504Policy.SafeBluntDamage(victim.health.hediffSet.GetPartHealth(limb));
                    if (amount > 0f)
                        victim.TakeDamage(new DamageInfo(DamageDefOf.Blunt, amount, 0f, -1f, Launcher, limb));
                }
            }
            base.Impact(hitThing, blockedByShield);
        }
    }
}
