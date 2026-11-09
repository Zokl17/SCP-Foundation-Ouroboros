using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using ScpFoundation.Runtime;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ScpFoundation.Anomalies
{
    [StaticConstructorOnStartup]
    public sealed class Pawn_Scp049 : Pawn
    {
        private int nextVisitTick;
        private int nextWanderTick;
        public const int MaximumIntegrity = 1600;
        private const int RecoveryStepTicks = 30000;
        private const int RecoveryAmount = 100;
        private const float MinimumRoomImpressiveness = 30f;
        private int integrity = MaximumIntegrity;
        private int vanishTick;
        private int nextRecoveryTick;
        private Thing containedPost;
        private static Graphic portrait;

        public int Integrity { get { return integrity; } }
        public bool CanBeContained { get { return integrity <= 0 && Downed && !Dead && containedPost == null; } }
        public bool IsContained { get { return containedPost != null; } }
        public bool IsContainedAt(Thing post) { return post != null && containedPost == post; }

        public override string Label { get { return "SCP-049"; } }
        public override string LabelNoCount { get { return "SCP-049"; } }
        public override string LabelShort { get { return "SCP-049"; } }

        public override void DrawGUIOverlay() { }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            gender = Gender.Male;
        }

        public override string GetInspectString()
        {
            if (containedPost == null)
                return (integrity <= 0 ? "SCP_049InspectDowned" : "SCP_049InspectActive").Translate();
            bool enclosed, sized;
            float light, impressiveness;
            bool ready = RoomReady(out enclosed, out sized, out light, out impressiveness);
            return "SCP_049InspectContained".Translate() + "\n"
                + "SCP_049RoomEnclosed".Translate(YesNo(enclosed)) + "\n"
                + "SCP_049RoomSize".Translate(YesNo(sized)) + "\n"
                + "SCP_049RoomLight".Translate(light.ToString("P0")) + "\n"
                + "SCP_049RoomImpressiveness".Translate(impressiveness.ToString("0.#"),
                    MinimumRoomImpressiveness.ToString("0")) + "\n"
                + (ready ? "SCP_049ReadyForDialogue" : "SCP_049AggressiveAtPost").Translate();
        }

        private static string YesNo(bool value)
        {
            return (value ? "SCP_Yes" : "SCP_No").Translate();
        }

        private bool RoomReady(out bool enclosed, out bool sized, out float light, out float impressiveness)
        {
            Room room = containedPost != null && containedPost.Spawned && Map == containedPost.Map
                ? containedPost.Position.GetRoom(Map) : null;
            enclosed = room != null && !room.TouchesMapEdge && room.OpenRoofCount == 0;
            sized = enclosed && HasSixBySix(room.Cells);
            light = enclosed ? Map.glowGrid.GroundGlowAt(containedPost.Position) : 0f;
            impressiveness = enclosed ? room.GetStat(RoomStatDefOf.Impressiveness) : 0f;
            return MeetsRoomThresholds(enclosed, sized, light, impressiveness);
        }

        internal static bool MeetsRoomThresholds(bool enclosed, bool sized, float light, float impressiveness)
        {
            return enclosed && sized && light >= 0.3f && impressiveness >= MinimumRoomImpressiveness;
        }

        internal static bool HasSixBySix(IEnumerable<IntVec3> cells)
        {
            HashSet<IntVec3> floor = new HashSet<IntVec3>(cells);
            if (floor.Count < 36) return false;
            foreach (IntVec3 origin in floor)
            {
                bool square = true;
                for (int x = 0; x < 6 && square; x++)
                    for (int z = 0; z < 6; z++)
                        if (!floor.Contains(new IntVec3(origin.x + x, 0, origin.z + z)))
                        {
                            square = false;
                            break;
                        }
                if (square) return true;
            }
            return false;
        }

        public override IEnumerable<InspectTabBase> GetInspectTabs() { yield break; }

        public override IEnumerable<Gizmo> GetGizmos() { yield break; }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            if (!CanBeContained && containedPost == null)
                foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn)) yield return option;
            if (!CanBeContained || !Spawned || selPawn == null || selPawn.Downed
                || selPawn.Faction != Faction.OfPlayer || selPawn.Map != Map) yield break;
            ThingDef postDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_ContainmentAnchor");
            if (postDef == null) yield break;
            List<Thing> posts = Map.listerThings.ThingsOfDef(postDef)
                .Where(thing => AvailablePost(thing))
                .OrderBy(thing => thing.Position.DistanceToSquared(Position)).ToList();
            if (posts.Count == 0)
            {
                yield return new FloatMenuOption("SCP_049CarryNoPost".Translate(), null);
                yield break;
            }
            Thing post = posts.FirstOrDefault(thing => selPawn.CanReserveAndReach(
                thing.Position, PathEndMode.OnCell, Danger.Deadly));
            if (post == null || !selPawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption("SCP_049CarryUnreachable".Translate(), null);
                yield break;
            }
            yield return new FloatMenuOption("SCP_049CarryToPost".Translate(), () =>
                DispatchToPost(selPawn, post));
        }

        public static Pawn_Scp049 Generate()
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_049Kind");
            Faction faction = ScpAnomalyFactionUtility.GetDoctorFaction();
            if (kind == null || faction == null) return null;
            Pawn_Scp049 pawn = PawnGenerator.GeneratePawn(kind, faction) as Pawn_Scp049;
            if (pawn != null) pawn.Name = new NameSingle("SCP-049", false);
            return pawn;
        }

        public static bool IsInjuredPatient(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && !pawn.Dead && pawn.Downed
                && pawn.RaceProps.Humanlike && pawn.DevelopmentalStage == DevelopmentalStage.Adult
                && !pawn.IsMutant && pawn.health != null
                && pawn.health.hediffSet != null && pawn.health.hediffSet.hediffs
                    .OfType<Hediff_Injury>().Any(injury => injury.Severity > 0f);
        }

        private static bool IsFreshCorpse(Corpse corpse)
        {
            CompRottable rot = corpse == null ? null : corpse.GetComp<CompRottable>();
            Pawn pawn = corpse == null ? null : corpse.InnerPawn;
            return corpse != null && corpse.Spawned && pawn != null && pawn.Dead
                && pawn.RaceProps.Humanlike && pawn.DevelopmentalStage == DevelopmentalStage.Adult
                && !pawn.IsMutant && rot != null
                && rot.Stage == RotStage.Fresh;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextVisitTick, "scp049NextVisit", 0);
            Scribe_Values.Look(ref nextWanderTick, "scp049NextWander", 0);
            Scribe_Values.Look(ref integrity, "scp049Integrity", MaximumIntegrity);
            Scribe_Values.Look(ref vanishTick, "scp049VanishTick", 0);
            Scribe_Values.Look(ref nextRecoveryTick, "scp049NextRecoveryTick", 0);
            Scribe_References.Look(ref containedPost, "scp049ContainedPost");
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (integrity > 0)
            {
                integrity = System.Math.Max(0, integrity - ScpIntegrityDamage.From(dinfo));
                if (integrity == 0) Collapse();
            }
            absorbed = true;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (portrait == null)
                portrait = GraphicDatabase.Get<Graphic_Single>("Things/Item/Scp049", ShaderDatabase.Cutout,
                    new Vector2(1.3f, 1.7f), Color.white);
            if (CanBeContained || containedPost != null)
                drawLoc.y = Altitudes.AltitudeFor(AltitudeLayer.MoteOverhead);
            portrait.Draw(drawLoc, integrity <= 0 && containedPost == null ? Rot4.East : Rot4.North, this);
        }

        internal void DrawCarriedAt(Vector3 drawLoc)
        {
            DrawAt(drawLoc);
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            if (phase == DrawPhase.Draw) DrawAt(drawLoc, flip);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned || Dead || Map == null) return;
            if (Find.TickManager.TicksGame % 250 == 0)
            {
                Faction hostile = ScpAnomalyFactionUtility.GetDoctorFaction();
                if (hostile != null && Faction != hostile) SetFaction(hostile);
            }
            if (Find.TickManager.TicksGame % 250 == 0 && needs != null)
            {
                if (needs.food != null) needs.food.CurLevel = needs.food.MaxLevel;
                if (needs.rest != null) needs.rest.CurLevel = needs.rest.MaxLevel;
            }
            if (containedPost != null)
            {
                if (!containedPost.Spawned || containedPost.Map != Map || Position != containedPost.Position)
                {
                    containedPost = null;
                    // A pawn moved away from a post must not silently vanish a day later.
                    vanishTick = 0;
                    if (integrity < MaximumIntegrity)
                    {
                        integrity = 0;
                        Collapse();
                        vanishTick = 0;
                    }
                    return;
                }
                vanishTick = 0;
                if (integrity < MaximumIntegrity && Find.TickManager.TicksGame >= nextRecoveryTick)
                {
                    integrity = System.Math.Min(MaximumIntegrity, integrity + RecoveryAmount);
                    nextRecoveryTick = Find.TickManager.TicksGame + RecoveryStepTicks;
                    if (integrity == MaximumIntegrity) FinishRecovery();
                }
                if (integrity == MaximumIntegrity && Find.TickManager.TicksGame % 180 == 0)
                {
                    bool enclosed, sized;
                    float light, impressiveness;
                    if (!RoomReady(out enclosed, out sized, out light, out impressiveness)) StrikeVisitor();
                }
                return;
            }
            if (integrity <= 0)
            {
                if (!Downed) Collapse();
                // Older delivery jobs could drop 049 on the standable post without
                // completing the binding. Recover that state before the vanish timer.
                ThingDef postDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_ContainmentAnchor");
                Thing post = postDef == null ? null : Position.GetThingList(Map)
                    .FirstOrDefault(thing => thing.def == postDef && AvailablePost(thing));
                if (post != null && ContainAtPost(post)) return;
                if (containedPost == null && vanishTick != 0
                    && Find.TickManager.TicksGame >= vanishTick) Destroy(DestroyMode.Vanish);
                return;
            }
            if (Find.TickManager.TicksGame % 250 != 0 || Find.TickManager.TicksGame < nextVisitTick) return;
            nextVisitTick = Find.TickManager.TicksGame + 250;
            Map map = Map;
            Thing patient = map.mapPawns.AllPawnsSpawned.Where(IsInjuredPatient).Cast<Thing>()
                .Concat(map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                    .OfType<Corpse>().Where(IsFreshCorpse))
                .Where(thing => map.reachability.CanReach(Position, thing.Position,
                    PathEndMode.Touch, TraverseParms.For(this)))
                .OrderBy(thing => thing.Position.DistanceToSquared(Position)).FirstOrDefault();
            if (patient == null)
            {
                if (!HuntPawn(map)) Wander(map);
                return;
            }
            if (Position.DistanceToSquared(patient.Position) > 2)
            {
                if (jobs != null && (jobs.curJob == null || jobs.curJob.def != JobDefOf.Goto
                    || jobs.curJob.targetA.Cell != patient.Position))
                    jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, patient.Position), JobCondition.InterruptForced);
                return;
            }

            ConvertPatient(patient, map);
        }

        private bool HuntPawn(Map map)
        {
            Pawn target = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn != this && !pawn.Dead && !pawn.Downed
                    && pawn.RaceProps.Humanlike && !pawn.IsMutant && pawn.HostileTo(this)
                    && map.reachability.CanReach(Position, pawn.Position,
                        PathEndMode.Touch, TraverseParms.For(this)))
                .OrderBy(pawn => pawn.Position.DistanceToSquared(Position)).FirstOrDefault();
            if (target == null || jobs == null) return false;
            if (jobs.curJob == null || jobs.curJob.def != JobDefOf.AttackMelee
                || jobs.curJob.targetA.Thing != target)
                jobs.StartJob(JobMaker.MakeJob(JobDefOf.AttackMelee, target), JobCondition.InterruptForced);
            return true;
        }

        private void Collapse()
        {
            if (vanishTick == 0 && Find.TickManager != null)
                vanishTick = Find.TickManager.TicksGame + 60000;
            if (jobs != null) jobs.StopAll();
            if (pather != null) pather.StopDead();
            health.forceDowned = true;
            HediffDef collapse = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_049Incapacitated");
            if (collapse != null && !health.hediffSet.HasHediff(collapse))
                health.AddHediff(collapse);
            else
                health.CheckForStateChange(null, null);
        }

        private void FinishRecovery()
        {
            health.forceDowned = false;
            HediffDef collapse = DefDatabase<HediffDef>.GetNamedSilentFail("SCP_049Incapacitated");
            if (collapse != null)
            {
                Hediff injury = health.hediffSet.hediffs.FirstOrDefault(hediff => hediff.def == collapse);
                if (injury != null) health.RemoveHediff(injury);
            }
            health.CheckForStateChange(null, null);
        }

        private void StrikeVisitor()
        {
            Pawn visitor = Map.mapPawns.AllPawnsSpawned.FirstOrDefault(pawn =>
                pawn != this && !pawn.Dead && !pawn.Downed && pawn.RaceProps.Humanlike
                && (pawn.Faction == Faction.OfPlayer || pawn.IsPrisonerOfColony)
                && pawn.Position.DistanceToSquared(Position) <= 2);
            if (visitor != null)
                visitor.TakeDamage(new DamageInfo(DamageDefOf.Cut, 12f, 0f, -1f, this));
        }

        private bool AvailablePost(Thing post)
        {
            return post != null && post.Spawned && post.Faction == Faction.OfPlayer
                && !post.Position.GetThingList(post.Map).Any(thing => thing != this && (thing is Pawn
                    || thing.def.category == ThingCategory.Item))
                && !post.Map.mapPawns.AllPawnsSpawned.OfType<Pawn_Scp049>()
                    .Any(pawn => pawn != this && pawn.containedPost == post);
        }

        private void DispatchToPost(Pawn worker, Thing post)
        {
            if (!CanBeContained || !AvailablePost(post) || worker == null || worker.jobs == null
                || !worker.CanReserveAndReach(this, PathEndMode.Touch, Danger.Deadly)
                || !worker.CanReserveAndReach(post.Position, PathEndMode.OnCell, Danger.Deadly)) return;
            JobDef def = DefDatabase<JobDef>.GetNamed("SCP_049CarryToPost");
            Job job = JobMaker.MakeJob(def, this, post.Position, post);
            job.count = 1;
            worker.jobs.TryTakeOrderedJob(job);
        }

        public bool ContainAtPost(Thing post)
        {
            if (!CanBeContained || !AvailablePost(post) || !Spawned
                || Position != post.Position) return false;
            containedPost = post;
            vanishTick = 0;
            integrity = RecoveryAmount;
            nextRecoveryTick = Find.TickManager.TicksGame + RecoveryStepTicks;
            ScpEventRecord contained = ScpEventJournal.Add("SCP-049", this, "SCP_049CarryCompleteBody".Translate());
            Find.LetterStack.ReceiveLetter(ScpEventJournal.Notice(contained, "SCP_049CarryCompleteTitle".Translate()),
                "SCP_049CarryCompleteBody".Translate(), LetterDefOf.PositiveEvent, this);
            return true;
        }

        private void Wander(Map map)
        {
            int now = Find.TickManager.TicksGame;
            if (now < nextWanderTick || jobs == null || pather.Moving) return;
            nextWanderTick = now + 600;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(Position, 12f, false).InRandomOrder())
            {
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.Fogged(map)
                    || cell.DistanceToSquared(Position) < 16
                    || !map.reachability.CanReach(Position, cell, PathEndMode.OnCell,
                        TraverseParms.For(this))) continue;
                jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobCondition.InterruptForced);
                return;
            }
        }

        private void ConvertPatient(Thing target, Map map)
        {
            Faction hostile = ScpAnomalyFactionUtility.GetHostileFaction();
            MutantDef ghoul = DefDatabase<MutantDef>.GetNamedSilentFail("Ghoul");
            if (!ModsConfig.AnomalyActive || hostile == null || ghoul == null || !target.Spawned) return;
            Corpse corpse = target as Corpse;
            Pawn patient = corpse == null ? target as Pawn : corpse.InnerPawn;
            if (corpse != null)
            {
                if (!IsFreshCorpse(corpse) || !ResurrectionUtility.TryResurrect(patient,
                    new ResurrectionParams { restoreMissingParts = true, noLord = true })) return;
            }
            else if (!IsInjuredPatient(patient)) return;
            MutantUtility.SetPawnAsMutantInstantly(patient, ghoul, RotStage.Fresh);
            if (!patient.IsGhoul) return;
            patient.SetFaction(hostile);
            if (patient.Downed) MutantUtility.RestoreUntilNotDowned(patient);
            LordMaker.MakeNewLord(hostile,
                new LordJob_AssaultColony(hostile, false, true, false, false, false, false, false),
                map, new List<Pawn> { patient });
            ScpEventRecord turned = ScpEventJournal.Add("SCP-049", this,
                "SCP_049TurnBody".Translate(patient.Named("PAWN")));
            Find.LetterStack.ReceiveLetter(ScpEventJournal.Notice(turned, "SCP_049TurnTitle".Translate()),
                "SCP_049TurnBody".Translate(patient.Named("PAWN")), LetterDefOf.NegativeEvent, this);
        }
    }

    // Carried pawns use PawnRenderer directly, bypassing Pawn.DrawAt.
    [HarmonyPatch(typeof(PawnRenderer), "RenderPawnAt")]
    internal static class Scp049CarriedRendererPatch
    {
        private static readonly FieldInfo PawnField = AccessTools.Field(typeof(PawnRenderer), "pawn");

        private static bool Prefix(PawnRenderer __instance, Vector3 drawLoc)
        {
            if (PawnField == null) return true;
            Pawn_Scp049 doctor = PawnField.GetValue(__instance) as Pawn_Scp049;
            if (doctor == null || !(doctor.ParentHolder is Pawn_CarryTracker)) return true;
            doctor.DrawCarriedAt(drawLoc);
            return false;
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), "GetOptions")]
    internal static class Scp049DownedMenuPatch
    {
        private static void Postfix(ref List<FloatMenuOption> __result, ref FloatMenuContext context)
        {
            if (__result == null || context.ClickedThings == null) return;
            Pawn_Scp049 entity = context.ClickedThings.OfType<Pawn_Scp049>()
                .FirstOrDefault(pawn => pawn.CanBeContained || pawn.IsContained);
            if (entity == null) return;
            string carry = "SCP_049CarryToPost".Translate();
            string noPost = "SCP_049CarryNoPost".Translate();
            string unreachable = "SCP_049CarryUnreachable".Translate();
            Texture2D icon = ContentFinder<Texture2D>.Get("Things/Item/Scp049");
            for (int index = __result.Count - 1; index >= 0; index--)
            {
                FloatMenuOption option = __result[index];
                if (option == null) continue;
                string label = option.Label ?? string.Empty;
                if (label == carry || label == noPost || label == unreachable)
                {
                    option.iconThing = null;
                    Traverse.Create(option).Field("iconTex").SetValue(icon);
                    option.revalidateClickTarget = null;
                    continue;
                }
                if (option.iconThing == entity || option.revalidateClickTarget == entity
                    || label.IndexOf("scp-049", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    __result.RemoveAt(index);
            }
        }
    }

    [StaticConstructorOnStartup]
    public sealed class Pawn_Scp0492 : Pawn
    {
        private static Graphic portrait;

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (portrait == null)
                portrait = GraphicDatabase.Get<Graphic_Single>("Things/Item/Scp0492", ShaderDatabase.Cutout,
                    new Vector2(1.2f, 1.6f), Color.white);
            portrait.Draw(drawLoc, Rotation, this);
        }
    }
}
