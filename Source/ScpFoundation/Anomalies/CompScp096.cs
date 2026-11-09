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
    public sealed class Thing_Scp096 : ThingWithComps, IAttackTarget
    {
        private Graphic agitatedGraphic;
        private Graphic incapacitatedGraphic;

        Thing IAttackTarget.Thing { get { return this; } }
        public LocalTargetInfo TargetCurrentlyAimingAt { get { return LocalTargetInfo.Invalid; } }
        public float TargetPriorityFactor { get { return 1f; } }

        public bool ThreatDisabled(IAttackTargetSearcher searcher)
        {
            CompScp scp = GetComp<CompScp>();
            CompScp096 behavior = GetComp<CompScp096>();
            return !Spawned || behavior == null || !behavior.IsAgitated
                || scp != null && scp.IsInCustody && !scp.IsBreached;
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            map.attackTargetsCache.UpdateTarget(this);
        }

        public override Vector3 DrawPos
        {
            get
            {
                Vector3 position = base.DrawPos;
                position.z += 0.68f;
                CompScp096 behavior = GetComp<CompScp096>();
                if (behavior != null && behavior.IsPursuing)
                    position.x += Mathf.Sin(Find.TickManager.TicksGame * 0.2f) * 0.04f;
                return position;
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            CompScp096 behavior = GetComp<CompScp096>();
            Graphic current = def.graphicData.Graphic;
            Vector2 size = def.graphicData.drawSize;
            if (behavior != null && behavior.IsIncapacitated)
            {
                if (incapacitatedGraphic == null)
                    incapacitatedGraphic = GraphicDatabase.Get<Graphic_Single>(
                        "Things/Item/Scp096Down", ShaderDatabase.Cutout,
                        new Vector2(1.8f, 2.1f), Color.white);
                current = incapacitatedGraphic;
                size = new Vector2(1.8f, 2.1f);
                drawLoc.z += Mathf.Sin(Find.TickManager.TicksGame * 0.035f) * 0.012f;
            }
            else if (behavior != null && behavior.IsAgitated)
            {
                if (agitatedGraphic == null)
                    agitatedGraphic = GraphicDatabase.Get<Graphic_Multi>(
                        "Things/Item/Scp096Aggro", ShaderDatabase.Cutout,
                        def.graphicData.drawSize, Color.white);
                current = agitatedGraphic;
            }
            Graphics.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(drawLoc, Quaternion.identity, new Vector3(size.x, 1f, size.y)),
                current.MatAt(Rotation, this), 0);
            Comps_PostDraw();
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            CompScp096 behavior = GetComp<CompScp096>();
            if (behavior != null)
            {
                behavior.TakeIntegrityDamage(ScpIntegrityDamage.From(dinfo));
                behavior.ProvokeByAttacker(dinfo.Instigator as Pawn);
            }
            absorbed = true;
        }
    }

    public sealed class CompProperties_Scp096 : CompProperties
    {
        public CompProperties_Scp096() { compClass = typeof(CompScp096); }
    }

    public sealed class CompScp096 : ThingComp
    {
        private const int MaximumIntegrity = 1200;
        private const int DistressTicks = 180;
        private const int StepTicks = 10;
        private const int RecoveryTicks = 60000;
        private int integrity = MaximumIntegrity;
        private int phase; // 0 calm, 1 distressed, 2 pursuing, 3 incapacitated
        private int distressUntil;
        private int recoveryTick;
        private int nextStepTick;
        private int nextAttackTick;
        private Pawn target;
        private List<Pawn> witnesses = new List<Pawn>();
        private IntVec3 previousCell = IntVec3.Invalid;
        private int previousStepTick;
        private int lightingGraceUntilTick;

        public bool IsIncapacitated { get { return phase == 3; } }
        public bool IsPursuing { get { return phase == 2; } }
        public bool IsAgitated { get { return phase == 1 || phase == 2; } }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad) UpdateHaulingRestriction();
        }

        private void UpdateHaulingRestriction()
        {
            if (parent.Spawned)
                parent.SetForbidden(phase != 3 && !CompScpPost.IsObjectAtPost(parent), false);
        }

        public override string CompInspectStringExtra()
        {
            string status = phase == 3 ? "SCP_096Incapacitated".Translate(integrity, MaximumIntegrity)
                : phase == 2 ? "SCP_096Pursuing".Translate(target == null ? "?" : target.LabelShortCap)
                : (phase == 1 ? "SCP_096Distressed" : "SCP_096Calm").Translate();
            if (phase != 3)
                status += "\n" + "SCP_096IntegrityHint".Translate(integrity, MaximumIntegrity);
            if (parent.Map != null && !parent.Map.IsPlayerHome)
                status += "\n" + (phase == 3 ? "SCP_096ExtractionReady" : "SCP_096ExtractionHint").Translate();
            else if (phase == 3)
                status += "\n" + "SCP_096HomeTransportHint".Translate();
            return status;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref integrity, "scp096Integrity", MaximumIntegrity);
            Scribe_Values.Look(ref phase, "scp096Phase", 0);
            Scribe_Values.Look(ref distressUntil, "scp096DistressUntil", 0);
            Scribe_Values.Look(ref recoveryTick, "scp096RecoveryTick", 0);
            Scribe_Values.Look(ref nextStepTick, "scp096NextStep", 0);
            Scribe_Values.Look(ref nextAttackTick, "scp096NextAttack", 0);
            Scribe_Values.Look(ref previousCell, "scp096PreviousCell", IntVec3.Invalid);
            Scribe_Values.Look(ref previousStepTick, "scp096PreviousStepTick", 0);
            Scribe_Values.Look(ref lightingGraceUntilTick, "scp096LightingGraceUntilTick", -1);
            Scribe_References.Look(ref target, "scp096Target");
            Scribe_Collections.Look(ref witnesses, "scp096Witnesses", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && witnesses == null) witnesses = new List<Pawn>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && lightingGraceUntilTick < 0)
                lightingGraceUntilTick = Find.TickManager.TicksGame + 60000;
        }

        public void TakeIntegrityDamage(int amount)
        {
            if (amount <= 0 || phase == 3) return;
            integrity = System.Math.Max(0, integrity - amount);
            if (integrity != 0) return;
            phase = 3;
            UpdateHaulingRestriction();
            target = null;
            witnesses.Clear();
            recoveryTick = Find.TickManager.TicksGame + RecoveryTicks;
            Find.LetterStack.ReceiveLetter("SCP_096DownTitle".Translate(),
                (parent.Map != null && parent.Map.IsPlayerHome
                    ? "SCP_096DownHomeBody" : "SCP_096DownBody").Translate(),
                LetterDefOf.NeutralEvent, parent);
        }

        public void ProvokeByAttacker(Pawn attacker)
        {
            if (phase == 3 || attacker == null || !attacker.Spawned || attacker.Dead
                || !attacker.RaceProps.Humanlike || attacker.Map != parent.Map) return;
            if (!witnesses.Contains(attacker)) witnesses.Insert(0, attacker);
            target = attacker;
            phase = 2;
            distressUntil = 0;
            nextStepTick = Find.TickManager.TicksGame;
            UpdateHaulingRestriction();
            if (parent.Spawned) parent.Map.attackTargetsCache.UpdateTarget((IAttackTarget)parent);
        }

        public override void CompTick()
        {
            if (!parent.Spawned || parent.Map == null) return;
            int now = Find.TickManager.TicksGame;
            if (phase == 3)
            {
                if (now >= recoveryTick)
                {
                    phase = 0;
                    integrity = MaximumIntegrity;
                    UpdateHaulingRestriction();
                    parent.Rotation = Rot4.South;
                    CompScp recovered = parent.TryGetComp<CompScp>();
                    if (recovered == null || !recovered.IsInCustody || !recovered.CanResearchNow)
                        Find.LetterStack.ReceiveLetter("SCP_096RecoveredTitle".Translate(),
                            "SCP_096RecoveredBody".Translate(), LetterDefOf.ThreatBig, parent);
                }
                return;
            }
            CompScp scp = parent.TryGetComp<CompScp>();
            if (phase == 0)
            {
                bool oldChamberGrace = now < lightingGraceUntilTick && scp != null
                    && scp.IsInCustody && CompScpPost.IsObjectAtPost(parent);
                if (now % 60 == 0 && !oldChamberGrace
                    && (scp == null || !scp.IsInCustody || !scp.CanResearchNow))
                    ScanForViewers();
                return;
            }
            if (phase == 1)
            {
                if (now < distressUntil) return;
                phase = 2;
                nextStepTick = now;
            }
            if (now < nextStepTick) return;
            nextStepTick = now + StepTicks;
            if (target == null || !target.Spawned || target.Dead || target.Map != parent.Map)
            {
                target = witnesses.FirstOrDefault(pawn => pawn != null && pawn.Spawned
                    && !pawn.Dead && pawn.Map == parent.Map);
                if (target == null)
                {
                    phase = 0;
                    witnesses.Clear();
                    parent.Rotation = Rot4.South;
                    return;
                }
            }
            FaceToward(target.Position);
            if (parent.Position.DistanceToSquared(target.Position) <= 2)
            {
                if (now >= nextAttackTick)
                {
                    nextAttackTick = now + 90;
                    target.TakeDamage(new DamageInfo(DamageDefOf.Crush, 75f, 999f, -1f, parent));
                }
                return;
            }
            MoveToward(target.Position, now);
        }

        private void ScanForViewers()
        {
            Map map = parent.Map;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike
                    || pawn.health == null) continue;
                IntVec3 delta = parent.Position - pawn.Position;
                if (delta.LengthHorizontalSquared > 400) continue;
                IntVec3 viewerFacing = pawn.Rotation.FacingCell;
                IntVec3 faceFacing = parent.Rotation.FacingCell;
                if (!Scp096SightPolicy.SeesFace(delta.x, delta.z, viewerFacing.x,
                    viewerFacing.z, faceFacing.x, faceFacing.z,
                    pawn.Awake() && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Sight),
                    ScpSurveillance.Sight(pawn.Position, parent.Position, map),
                    map.glowGrid.GroundGlowAt(parent.Position))) continue;
                if (!witnesses.Contains(pawn)) witnesses.Add(pawn);
            }
            if (witnesses.Count == 0) return;
            target = witnesses[0];
            phase = 1;
            distressUntil = Find.TickManager.TicksGame + DistressTicks;
            Find.LetterStack.ReceiveLetter("SCP_096TriggeredTitle".Translate(),
                "SCP_096TriggeredBody".Translate(target.Named("PAWN")), LetterDefOf.ThreatBig, parent);
        }

        private void MoveToward(IntVec3 destination, int now)
        {
            Map map = parent.Map;
            IntVec3 origin = parent.Position;
            int dx = System.Math.Sign(destination.x - origin.x);
            int dz = System.Math.Sign(destination.z - origin.z);
            IntVec3[] directions = { new IntVec3(dx, 0, dz), new IntVec3(dx, 0, 0),
                new IntVec3(0, 0, dz) };
            foreach (IntVec3 direction in directions)
            {
                if (direction == IntVec3.Zero) continue;
                IntVec3 cell = origin + direction;
                if (!cell.InBounds(map)) continue;
                Building obstacle = cell.GetEdifice(map);
                if (obstacle != null && !cell.Standable(map))
                {
                    obstacle.TakeDamage(new DamageInfo(DamageDefOf.Crush, 90f, 999f, -1f, parent));
                    return;
                }
                if (!cell.Standable(map) || cell.GetFirstPawn(map) != null) continue;
                Rot4 facing = parent.Rotation;
                parent.DeSpawn();
                GenSpawn.Spawn(parent, cell, map, facing);
                previousCell = origin;
                previousStepTick = now;
                return;
            }
        }

        private void FaceToward(IntVec3 destination)
        {
            int dx = destination.x - parent.Position.x;
            int dz = destination.z - parent.Position.z;
            if (dx == 0 && dz == 0) return;
            parent.Rotation = System.Math.Abs(dx) > System.Math.Abs(dz)
                ? (dx > 0 ? Rot4.East : Rot4.West)
                : (dz > 0 ? Rot4.North : Rot4.South);
        }
    }

    // Keep vanilla's forbidden flag so automatic haulers cannot pick up an active SCP-096.
    // Its Allow button and red X are misleading for an anomaly that must first be incapacitated.
    [HarmonyPatch(typeof(CompForbiddable), "CompGetGizmosExtra")]
    internal static class Scp096ForbidGizmoPatch
    {
        private static void Postfix(CompForbiddable __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance.parent is Thing_Scp096) __result = new Gizmo[0];
        }
    }

    [HarmonyPatch(typeof(OverlayDrawer), "RenderForbiddenOverlay")]
    internal static class Scp096ForbidOverlayPatch
    {
        private static bool Prefix(Thing __0) { return !(__0 is Thing_Scp096); }
    }

    [HarmonyPatch(typeof(OverlayDrawer), "RenderForbiddenBigOverlay")]
    internal static class Scp096ForbidBigOverlayPatch
    {
        private static bool Prefix(Thing __0) { return !(__0 is Thing_Scp096); }
    }
}
