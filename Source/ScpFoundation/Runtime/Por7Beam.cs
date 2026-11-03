using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Utility;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ScpFoundation.Runtime
{
    public sealed class CompProperties_Por7Emitter : CompProperties
    {
        public CompProperties_Por7Emitter()
        {
            compClass = typeof(CompPor7Emitter);
        }
    }

    public sealed class CompPor7Emitter : ThingComp
    {
        private const float HeatPerBurst = 45f;
        private const float MaximumStartingHeat = 55f;
        private const float CoolingPerTick = 0.015f;

        private float heat;
        private int lastHeatTick;

        public bool CanStartBurst
        {
            get
            {
                CoolDown();
                return heat <= MaximumStartingHeat;
            }
        }

        public int HeatPercent
        {
            get { CoolDown(); return (int)System.Math.Round(heat); }
        }

        public void RegisterBurst()
        {
            CoolDown();
            heat = System.Math.Min(100f, heat + HeatPerBurst);
        }

        private void CoolDown()
        {
            int tick = Find.TickManager.TicksGame;
            if (lastHeatTick > 0 && tick > lastHeatTick)
                heat = System.Math.Max(0f, heat - (tick - lastHeatTick) * CoolingPerTick);
            lastHeatTick = tick;
        }

        public override string CompInspectStringExtra()
        {
            CoolDown();
            return "SCP_Por7Heat".Translate(System.Math.Round(heat));
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref heat, "por7Heat", 0f);
            Scribe_Values.Look(ref lastHeatTick, "por7LastHeatTick", 0);
        }
    }

    public sealed class CompProperties_Por7Pack : CompProperties
    {
        public CompProperties_Por7Pack()
        {
            compClass = typeof(CompPor7Pack);
        }
    }

    public sealed class CompPor7Pack : ThingComp
    {
        private Pawn operatorPawn;
        private ThingWithComps stowedWeapon;
        private ThingWithComps deployedEmitter;
        private int deploymentTick;
        private int prepareEndTick;
        private IntVec3 prepareCell = IntVec3.Invalid;
        private bool prepared;
        private const int PrepareTicks = 300;

        private Pawn Wearer { get { Apparel apparel = parent as Apparel; return apparel == null ? null : apparel.Wearer; } }
        private CompApparelReloadable Power { get { return parent.GetComp<CompApparelReloadable>(); } }
        private CompPor7Emitter Heat { get { return parent.GetComp<CompPor7Emitter>(); } }

        public bool IsPrepared { get { return prepared; } }

        public bool IsActiveEmitter(ThingWithComps weapon)
        {
            return deployedEmitter != null && deployedEmitter == weapon;
        }

        internal void ReturnEmitterIfActive(ThingWithComps weapon)
        {
            if (IsActiveEmitter(weapon)) RestoreWeapon(operatorPawn);
        }

        public bool CanStartBurst
        {
            get { return Power != null && Power.RemainingCharges > 0 && Heat != null && Heat.CanStartBurst; }
        }

        public void RegisterBurst()
        {
            Power.UsedOnce();
            Heat.RegisterBurst();
            prepared = false;
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            Pawn pawn = Wearer;
            if (pawn == null || !pawn.IsColonistPlayerControlled) yield break;
            yield return new Gizmo_Por7Controller(this, pawn);
        }

        private sealed class Gizmo_Por7Controller : Gizmo
        {
            private readonly CompPor7Pack pack;
            private readonly Pawn pawn;
            private readonly Texture2D packIcon;
            private readonly Texture2D fireIcon;
            private readonly Texture2D prepareIcon;
            private readonly Texture2D reloadIcon;

            public Gizmo_Por7Controller(CompPor7Pack pack, Pawn pawn)
            {
                this.pack = pack;
                this.pawn = pawn;
                Order = -90f;
                packIcon = ContentFinder<Texture2D>.Get("Things/Item/Apparel/ScpPor7PowerPack");
                fireIcon = ContentFinder<Texture2D>.Get("Things/Item/Equipment/WeaponRanged/ScpPor7Emitter");
                prepareIcon = ContentFinder<Texture2D>.Get("Things/Item/Apparel/ScpPor7PowerPack");
                reloadIcon = ContentFinder<Texture2D>.Get("Things/Item/Resource/ScpPor7PowerCell");
            }

            public override float GetWidth(float maxWidth)
            {
                return 405f;
            }

            public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
            {
                Rect panel = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
                GameFont oldFont = Text.Font;
                TextAnchor oldAnchor = Text.Anchor;
                Color oldColor = GUI.color;
                try
                {
                    GUI.DrawTexture(panel, Command.BGTex);
                    Widgets.DrawHighlightIfMouseover(panel);
                    GUI.DrawTexture(new Rect(panel.x + 10f, panel.y + 5f, 65f, 50f),
                        packIcon, ScaleMode.ScaleToFit);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperCenter;
                    Widgets.Label(new Rect(panel.x + 5f, panel.y + 55f, 75f, 20f),
                        "SCP_Por7PanelTitle".Translate());
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = new Color(0.4f, 0.4f, 0.4f, 1f);
                    Widgets.DrawLineVertical(panel.x + 85f, panel.y + 10f, 55f);
                    Widgets.DrawLineVertical(panel.x + 269f, panel.y + 10f, 55f);
                    GUI.color = Color.white;

                    string operatorReason = !pawn.Drafted || pawn.DeadOrDowned || !pawn.Spawned
                        ? "SCP_Por7MustDraft".Translate().ToString() : null;
                    string activeReason = pack.prepareEndTick > 0
                        ? "SCP_Por7Preparing".Translate().ToString()
                        : pack.deployedEmitter != null
                            ? "SCP_Por7AlreadyActive".Translate().ToString()
                            : pack.Power == null || pack.Power.RemainingCharges <= 0
                                ? "SCP_Por7NoCharge".Translate().ToString()
                                : pack.Heat == null || !pack.Heat.CanStartBurst
                                    ? "SCP_Por7TooHot".Translate().ToString() : null;
                    string fireReason = operatorReason ?? activeReason;
                    string prepareReason = operatorReason ?? (pack.prepared
                        ? "SCP_Por7Prepared".Translate().ToString() : activeReason);
                    string reloadReason = pawn.DeadOrDowned || !pawn.Spawned || pack.Power == null
                        ? "SCP_Por7ReloadUnavailable".Translate().ToString()
                        : !pack.Power.NeedsReload(true)
                            ? "SCP_Por7ReloadFull".Translate().ToString()
                            : pack.deployedEmitter != null || pack.prepareEndTick > 0
                                ? "SCP_Por7AlreadyActive".Translate().ToString() : null;

                    DrawButton(new Rect(panel.x + 95f, panel.y + 13f, 48f, 48f), fireIcon,
                        "SCP_Por7Charge".Translate() + "\n" + "SCP_Por7ChargeDesc".Translate(),
                        fireReason, false, () => Find.Targeter.BeginTargeting(
                            TargetingParameters.ForAttackAny(), target => pack.DeployAndFire(pawn, target)));
                    DrawButton(new Rect(panel.x + 153f, panel.y + 13f, 48f, 48f), prepareIcon,
                        "SCP_Por7Prepare".Translate() + "\n" + "SCP_Por7PrepareDesc".Translate(),
                        prepareReason, pack.prepared, () => pack.BeginPreparation(pawn));
                    DrawButton(new Rect(panel.x + 211f, panel.y + 13f, 48f, 48f), reloadIcon,
                        "SCP_Por7Reload".Translate() + "\n" + "SCP_Por7ReloadDesc".Translate(),
                        reloadReason, false, () => pack.Reload(pawn));

                    int charges = pack.Power == null ? 0 : pack.Power.RemainingCharges;
                    int maximum = pack.Power == null ? 0 : pack.Power.MaxCharges;
                    int heat = pack.Heat == null ? 0 : pack.Heat.HeatPercent;
                    Widgets.Label(new Rect(panel.x + 280f, panel.y + 8f, 120f, 18f),
                        "SCP_Por7PanelCharges".Translate(charges, maximum));
                    Widgets.Label(new Rect(panel.x + 280f, panel.y + 27f, 120f, 18f),
                        "SCP_Por7PanelHeat".Translate(heat));
                    string state = pack.prepareEndTick > 0
                        ? "SCP_Por7PreparingTime".Translate(System.Math.Max(0,
                            (int)System.Math.Ceiling((pack.prepareEndTick - Find.TickManager.TicksGame) / 60f))).ToString()
                        : pack.prepared ? "SCP_Por7Prepared".Translate().ToString()
                        : pack.Heat != null && !pack.Heat.CanStartBurst
                            ? "SCP_Por7TooHot".Translate().ToString() : string.Empty;
                    Widgets.Label(new Rect(panel.x + 280f, panel.y + 46f, 120f, 22f), state);
                }
                finally
                {
                    Text.Font = oldFont;
                    Text.Anchor = oldAnchor;
                    GUI.color = oldColor;
                }
                return new GizmoResult(GizmoState.Clear);
            }

            private static void DrawButton(Rect rect, Texture2D icon, string tip,
                string disabledReason, bool highlighted, System.Action action)
            {
                GUI.DrawTexture(rect, Command.BGTex);
                Widgets.DrawHighlightIfMouseover(rect);
                if (highlighted)
                {
                    Widgets.DrawBoxSolid(rect, new Color(1f, 0.5f, 0f, 0.3f));
                    GUI.color = new Color(1f, 0.5f, 0f);
                    Widgets.DrawBox(rect, 2);
                    GUI.color = Color.white;
                }
                if (disabledReason == null)
                {
                    if (Widgets.ButtonImage(rect, icon))
                    {
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        action();
                    }
                }
                else
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.4f);
                    GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                    tip += "\n" + disabledReason;
                }
                TooltipHandler.TipRegion(rect, tip);
            }
        }

        private void BeginPreparation(Pawn pawn)
        {
            if (pawn != Wearer || !pawn.Spawned || pawn.DeadOrDowned || !pawn.Drafted
                || deployedEmitter != null || prepareEndTick > 0 || prepared || !CanStartBurst) return;
            pawn.jobs.StopAll();
            pawn.pather.StopDead();
            prepareCell = pawn.Position;
            prepareEndTick = Find.TickManager.TicksGame + PrepareTicks;
        }

        private void Reload(Pawn pawn)
        {
            if (pawn != Wearer || !pawn.Spawned || pawn.DeadOrDowned || Power == null
                || !Power.NeedsReload(true) || deployedEmitter != null || prepareEndTick > 0) return;
            List<Thing> ammo = ReloadableUtility.FindEnoughAmmo(pawn, pawn.Position, Power, true);
            if (ammo == null || ammo.Count == 0)
            {
                Messages.Message("SCP_Por7NoCells".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Job job = JobGiver_Reload.MakeReloadJob(Power, ammo);
            if (job != null) pawn.jobs.TryTakeOrderedJob(job);
        }

        private void DeployAndFire(Pawn pawn, LocalTargetInfo target)
        {
            if (pawn != Wearer || !pawn.Spawned || pawn.DeadOrDowned || !pawn.Drafted
                || deployedEmitter != null || !CanStartBurst) return;

            ThingDef emitterDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_Por7Emitter");
            if (emitterDef == null) return;
            ThingWithComps wand = ThingMaker.MakeThing(emitterDef) as ThingWithComps;
            if (wand == null) return;

            ThingWithComps original = pawn.equipment.Primary;
            if (original != null && !pawn.equipment.TryTransferEquipmentToContainer(original, pawn.inventory.innerContainer))
            {
                wand.Destroy(DestroyMode.Vanish);
                Messages.Message("SCP_Por7CannotStow".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            operatorPawn = pawn;
            stowedWeapon = original;
            deployedEmitter = wand;
            deploymentTick = Find.TickManager.TicksGame;
            try
            {
                pawn.equipment.AddEquipment(wand);
                pawn.jobs.StopAll();
                pawn.pather.StopDead();
                CompEquippable equippable = wand.GetComp<CompEquippable>();
                Verb beam = equippable == null ? null : equippable.PrimaryVerb;
                if (beam == null || !beam.CanHitTarget(target) || !beam.TryStartCastOn(target))
                {
                    RestoreWeapon(pawn);
                    Messages.Message("SCP_Por7InvalidTarget".Translate(), MessageTypeDefOf.RejectInput, false);
                }
            }
            catch (System.Exception error)
            {
                RestoreWeapon(pawn);
                Log.Error("[SCP] POR-7 deployment failed: " + error);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn wearer = Wearer;
            if (prepareEndTick > 0 || prepared)
            {
                if (wearer == null || !wearer.Spawned || wearer.DeadOrDowned || !wearer.Drafted
                    || prepareEndTick > 0 && wearer.Position != prepareCell)
                {
                    prepareEndTick = 0;
                    prepared = false;
                }
                else if (prepareEndTick > 0 && Find.TickManager.TicksGame >= prepareEndTick)
                {
                    prepareEndTick = 0;
                    prepared = true;
                }
            }
            if (deployedEmitter == null) return;
            Pawn pawn = operatorPawn;
            if (pawn == null || pawn != Wearer || !pawn.Spawned || pawn.DeadOrDowned
                || pawn.equipment.Primary != deployedEmitter)
            {
                RestoreWeapon(pawn);
                return;
            }
            if (Find.TickManager.TicksGame <= deploymentTick + 1) return;
            CompEquippable equippable = deployedEmitter.GetComp<CompEquippable>();
            Verb beam = equippable == null ? null : equippable.PrimaryVerb;
            if (beam == null || (beam.state != VerbState.Bursting
                && !(pawn.stances.curStance is Stance_Warmup)))
                RestoreWeapon(pawn);
        }

        private void RestoreWeapon(Pawn pawn)
        {
            ThingWithComps wand = deployedEmitter;
            ThingWithComps original = stowedWeapon;
            deployedEmitter = null;
            stowedWeapon = null;
            operatorPawn = null;
            if (pawn != null && wand != null && pawn.equipment.AllEquipmentListForReading.Contains(wand))
                pawn.equipment.Remove(wand);
            if (wand != null && !wand.Destroyed) wand.Destroy(DestroyMode.Vanish);
            if (pawn != null && original != null && !original.Destroyed
                && pawn.equipment.Primary == null && pawn.inventory.innerContainer.Contains(original))
            {
                pawn.inventory.innerContainer.Remove(original);
                pawn.equipment.AddEquipment(original);
            }
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            prepared = false;
            prepareEndTick = 0;
            RestoreWeapon(pawn);
            base.Notify_Unequipped(pawn);
        }

        public override void Notify_Downed()
        {
            prepared = false;
            prepareEndTick = 0;
            RestoreWeapon(operatorPawn);
            base.Notify_Downed();
        }

        public override void Notify_WearerDied()
        {
            prepared = false;
            prepareEndTick = 0;
            RestoreWeapon(operatorPawn);
            base.Notify_WearerDied();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            prepared = false;
            prepareEndTick = 0;
            RestoreWeapon(operatorPawn);
            base.PostDestroy(mode, previousMap);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref operatorPawn, "por7Operator");
            Scribe_References.Look(ref stowedWeapon, "por7StowedWeapon");
            Scribe_References.Look(ref deployedEmitter, "por7DeployedEmitter");
            Scribe_Values.Look(ref deploymentTick, "por7DeploymentTick", 0);
            Scribe_Values.Look(ref prepareEndTick, "por7PrepareEndTick", 0);
            Scribe_Values.Look(ref prepareCell, "por7PrepareCell", IntVec3.Invalid);
            Scribe_Values.Look(ref prepared, "por7Prepared", false);
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), "TryDropEquipment")]
    internal static class Por7PreventEmitterDropPatch
    {
        private static bool Prefix(Pawn_EquipmentTracker __instance, ThingWithComps eq,
            ref ThingWithComps resultingEq, ref bool __result)
        {
            if (eq == null || eq.def.defName != "SCP_Por7Emitter") return true;
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null || pawn.apparel == null) return true;
            foreach (Apparel worn in pawn.apparel.WornApparel)
            {
                if (worn.def.defName != "SCP_Por7PowerPack") continue;
                CompPor7Pack pack = worn.GetComp<CompPor7Pack>();
                if (pack == null || !pack.IsActiveEmitter(eq)) continue;
                pack.ReturnEmitterIfActive(eq);
                resultingEq = null;
                __result = false;
                return false;
            }
            return true;
        }
    }

    public sealed class Verb_Por7Beam : Verb_ShootBeam
    {
        public override float WarmupTime
        {
            get { CompPor7Pack pack = PowerPack; return pack != null && pack.IsPrepared ? 0.05f : base.WarmupTime; }
        }
        private CompPor7Pack PowerPack
        {
            get
            {
                Pawn pawn = CasterPawn;
                if (pawn == null || pawn.apparel == null) return null;
                foreach (Apparel worn in pawn.apparel.WornApparel)
                {
                    if (worn.def.defName == "SCP_Por7PowerPack")
                        return worn.GetComp<CompPor7Pack>();
                }
                return null;
            }
        }

        public override bool Available()
        {
            CompPor7Pack pack = PowerPack;
            return base.Available() && pack != null && pack.IsActiveEmitter(EquipmentSource)
                && (state == VerbState.Bursting || pack.CanStartBurst);
        }

        protected override bool TryCastShot()
        {
            CompPor7Pack pack = PowerPack;
            if (pack == null || !pack.IsActiveEmitter(EquipmentSource)) return false;

            bool firstShot = burstShotsLeft == ShotsPerBurst;
            if (firstShot && !pack.CanStartBurst) return false;

            bool fired = base.TryCastShot();
            if (fired && firstShot)
            {
                pack.RegisterBurst();
            }
            return fired;
        }
    }

    [HarmonyPatch(typeof(Verb_ShootBeam), "CalculatePath")]
    internal static class Por7FixedBeamPathPatch
    {
        private static void Postfix(Verb_ShootBeam __instance, Vector3 target,
            List<Vector3> pathList, HashSet<IntVec3> pathCellsList)
        {
            if (!(__instance is Verb_Por7Beam)) return;
            FillFixedPath(target, __instance.BurstShotCount, pathList, pathCellsList);
        }

        internal static void FillFixedPath(Vector3 target, int shots,
            List<Vector3> pathList, HashSet<IntVec3> pathCellsList)
        {
            pathList.Clear();
            for (int i = 0; i <= shots; i++) pathList.Add(target);
            pathCellsList.Clear();
            pathCellsList.Add(target.ToIntVec3());
        }
    }

    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(PawnRenderUtility), "DrawEquipmentAiming")]
    internal static class Por7HoseDrawPatch
    {
        private static Material outerMaterial;
        private static Material ribMaterial;

        private static void Prefix(Thing eq, Vector3 drawLoc)
        {
            ThingWithComps wand = eq as ThingWithComps;
            if (wand == null || wand.def.defName != "SCP_Por7Emitter") return;
            CompEquippable equippable = wand.GetComp<CompEquippable>();
            Verb verb = equippable == null ? null : equippable.PrimaryVerb;
            Pawn pawn = verb == null ? null : verb.CasterPawn;
            if (pawn == null || !pawn.Spawned || pawn.apparel == null || !verb.CurrentTarget.IsValid) return;

            foreach (Apparel worn in pawn.apparel.WornApparel)
            {
                if (worn.def.defName != "SCP_Por7PowerPack") continue;
                CompPor7Pack pack = worn.GetComp<CompPor7Pack>();
                if (pack != null && pack.IsActiveEmitter(wand))
                    DrawHose(pawn.DrawPos, drawLoc, verb.CurrentTarget.CenterVector3);
                return;
            }
        }

        private static void DrawHose(Vector3 pawnLoc, Vector3 gunLoc, Vector3 targetLoc)
        {
            if (outerMaterial == null)
            {
                outerMaterial = SolidColorMaterials.SimpleSolidColorMaterial(
                    new Color(0.055f, 0.055f, 0.065f), false);
                ribMaterial = SolidColorMaterials.SimpleSolidColorMaterial(
                    new Color(0.24f, 0.24f, 0.25f), false);
            }

            Vector3 forward = targetLoc - pawnLoc;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) return;
            forward.Normalize();
            Vector3 side = new Vector3(-forward.z, 0f, forward.x);
            Vector3 packPort = pawnLoc - forward * 0.08f + side * 0.18f;
            Vector3 gunPort = gunLoc - forward * 0.52f - side * 0.06f;
            packPort.y = gunPort.y = gunLoc.y - 0.001f;
            Vector3 loopStart = packPort - forward * 0.12f + side * 0.23f;
            Vector3 loopEnd = gunPort - forward * 0.13f + side * 0.30f;

            DrawSegment(packPort, loopStart);
            DrawSegment(loopStart, loopEnd);
            DrawSegment(loopEnd, gunPort);
        }

        private static void DrawSegment(Vector3 from, Vector3 to)
        {
            GenDraw.DrawLineBetween(from, to, outerMaterial, 0.085f);
            GenDraw.DrawLineBetween(from, to, ribMaterial, 0.035f);
        }
    }
}
