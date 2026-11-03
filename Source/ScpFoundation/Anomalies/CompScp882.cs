using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Storytelling;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ScpFoundation.Anomalies
{
    public sealed class Building_Scp882Tank : Building
    {
        public override Graphic Graphic
        {
            get
            {
                CompScp882 component = GetComp<CompScp882>();
                return component != null && component.ContainsObject
                    ? GraphicDatabase.Get<Graphic_Single>("Things/Item/Scp882Tank", ShaderDatabase.Cutout,
                        def.graphicData.drawSize, Color.white)
                    : base.Graphic;
            }
        }
    }

    public sealed class CompProperties_Scp882 : CompProperties
    {
        public int seawaterPercent = 100;
        public int raidGrowthThreshold = 10;
        public CompProperties_Scp882() { compClass = typeof(CompScp882); }
    }

    [StaticConstructorOnStartup]
    public sealed class CompScp882 : ThingComp
    {
        public const int RopeClothCost = 100;
        private const int ConsumptionTicks = 7500;
        private const int AttractionRadiusSquared = 100;
        private const int HourTicks = 2500;
        private static Graphic metalFragmentsGraphic;
        private static ThingDef[] cornerMotes;
        private static ThingDef centerMote;
        private static readonly string[] CornerMoteNames = {
            "SCP_882MoteNW", "SCP_882MoteNE", "SCP_882MoteSW", "SCP_882MoteSE"
        };
        private static readonly Vector3[] GrowthOffsets = {
            new Vector3(-0.65f, 0.15f, 0.35f), new Vector3(0.7f, 0.15f, -0.3f),
            new Vector3(0.35f, 0.15f, 0.7f), new Vector3(-0.35f, 0.15f, -0.7f),
            new Vector3(-1f, 0.15f, -0.45f), new Vector3(1f, 0.15f, 0.45f),
            new Vector3(-0.4f, 0.15f, 1f), new Vector3(0.4f, 0.15f, -1f)
        };
        private bool contained;
        private int absorbedUnits;
        private int growthTiles;
        private int growthQuarters;
        private int humanoidsCreated;
        private bool churchRaidTriggered;
        private bool rustIntact = true;
        private int lastInspectionTick;
        private int lastRopeTick;
        private int nextDayTick;
        private int missedDays;
        private Thing attractedMetal;
        private int attractionStartTick;
        private List<Thing> consumingTargets = new List<Thing>();
        private List<int> consumingStarts = new List<int>();

        public bool ContainsObject { get { return contained; } }
        public bool CanUnload { get { return contained && (((CompProperties_Scp882)props).seawaterPercent != 100 || rustIntact); } }

        public static Thing AvailableCloth(Pawn pawn)
        {
            return pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Cloth)
                .Where(t => t.Spawned && t.stackCount > 0 && pawn.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(t => t.Position.DistanceToSquared(pawn.Position)).FirstOrDefault();
        }

        private static bool HasEnoughCloth(Pawn pawn)
        {
            return pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Cloth)
                .Where(t => t.Spawned && pawn.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .Sum(t => t.stackCount) >= RopeClothCost;
        }

        private static bool CanWork(Pawn pawn, Thing target)
        {
            return pawn != null && pawn.IsColonistPlayerControlled && !pawn.Drafted && target.Spawned
                && pawn.CanReserveAndReach(target, PathEndMode.Touch, Danger.Some);
        }

        public override void PostExposeData()
        {
            // Older saves used this same ThingDef for a permanently occupied reservoir.
            Scribe_Values.Look(ref contained, "scp882Contained", true);
            Scribe_Values.Look(ref absorbedUnits, "scp882AbsorbedUnits", 0);
            Scribe_Values.Look(ref growthTiles, "scp882GrowthTiles", 0);
            Scribe_Values.Look(ref growthQuarters, "scp882GrowthQuarters", 0);
            Scribe_Values.Look(ref humanoidsCreated, "scp882HumanoidsCreated", 0);
            Scribe_Values.Look(ref churchRaidTriggered, "scp882ChurchRaidTriggered", false);
            Scribe_Values.Look(ref rustIntact, "scp882RustIntact", true);
            Scribe_Values.Look(ref lastInspectionTick, "scp882LastInspection", 0);
            Scribe_Values.Look(ref lastRopeTick, "scp882LastRope", 0);
            Scribe_Values.Look(ref nextDayTick, "scp882NextDay", 0);
            Scribe_Values.Look(ref missedDays, "scp882MissedDays", 0);
            Scribe_References.Look(ref attractedMetal, "scp882AttractedMetal");
            Scribe_Values.Look(ref attractionStartTick, "scp882AttractionStart", 0);
            Scribe_Collections.Look(ref consumingTargets, "scp882ConsumingTargets", LookMode.Reference);
            Scribe_Collections.Look(ref consumingStarts, "scp882ConsumingStarts", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (consumingTargets == null) consumingTargets = new List<Thing>();
                if (consumingStarts == null) consumingStarts = new List<int>();
                while (consumingTargets.Count > consumingStarts.Count) consumingTargets.RemoveAt(consumingTargets.Count - 1);
                while (consumingStarts.Count > consumingTargets.Count) consumingStarts.RemoveAt(consumingStarts.Count - 1);
                if (attractedMetal != null && !consumingTargets.Contains(attractedMetal))
                {
                    consumingTargets.Add(attractedMetal);
                    consumingStarts.Add(attractionStartTick);
                }
                attractedMetal = null;
                attractionStartTick = 0;
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (contained && map != null && parent.def.category == ThingCategory.Building)
            {
                ThingDef objectDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_882Object");
                if (objectDef != null)
                {
                    Thing objectThing = ThingMaker.MakeThing(objectDef);
                    CompScp882 objectComp = objectThing.TryGetComp<CompScp882>();
                    if (objectComp != null)
                    {
                        objectComp.CopyStateFrom(this);
                        objectComp.contained = false;
                        GenSpawn.Spawn(objectThing, parent.Position, map);
                        contained = false;
                    }
                }
            }
            base.PostDeSpawn(map, mode);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (!parent.Spawned || parent.def.category != ThingCategory.Item && !contained) return;
            int pieces = Math.Min(growthQuarters, GrowthOffsets.Length);
            if (pieces == 0) return;
            if (metalFragmentsGraphic == null)
                metalFragmentsGraphic = GraphicDatabase.Get<Graphic_Single>("Things/Mote/Scp882MetalFragments",
                    ShaderDatabase.Cutout, new Vector2(0.52f, 0.52f), Color.white);
            for (int i = 0; i < pieces; i++)
                metalFragmentsGraphic.Draw(parent.DrawPos + GrowthOffsets[i], Rot4.North, parent);
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            if (parent.Spawned && (parent.def.category == ThingCategory.Item || contained))
                GenDraw.DrawRadiusRing(parent.Position, Mathf.Sqrt(AttractionRadiusSquared));
        }

        private static void EnsureConsumptionMotes()
        {
            if (cornerMotes != null) return;
            cornerMotes = new ThingDef[4];
            for (int i = 0; i < 4; i++)
                cornerMotes[i] = DefDatabase<ThingDef>.GetNamedSilentFail(CornerMoteNames[i]);
            centerMote = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_882MoteCenter");
        }

        private void DrawConsumptionMotes(Map map, int now)
        {
            if (consumingTargets.Count == 0) return;
            EnsureConsumptionMotes();
            for (int i = 0; i < consumingTargets.Count; i++)
            {
                Thing target = consumingTargets[i];
                if (target == null || !target.Spawned || target.Map != map
                    || target.Position.DistanceToSquared(parent.Position) > AttractionRadiusSquared
                    || !GenView.ShouldSpawnMotesAt(target.Position, map, false)) continue;
                int elapsed = now - consumingStarts[i];
                float scale = elapsed >= HourTicks ? 0.66f : 0.33f;
                float spread = Math.Max(0.48f, Math.Max(target.def.size.x, target.def.size.z) * 0.4f);
                Vector3 center = target.DrawPos + new Vector3(0f, 0.15f, 0f);
                if (cornerMotes[0] != null) MoteMaker.MakeStaticMote(center + new Vector3(-spread, 0f, spread), map, cornerMotes[0], scale);
                if (cornerMotes[1] != null) MoteMaker.MakeStaticMote(center + new Vector3(spread, 0f, spread), map, cornerMotes[1], scale);
                if (cornerMotes[2] != null) MoteMaker.MakeStaticMote(center + new Vector3(-spread, 0f, -spread), map, cornerMotes[2], scale);
                if (cornerMotes[3] != null) MoteMaker.MakeStaticMote(center + new Vector3(spread, 0f, -spread), map, cornerMotes[3], scale);
                if (elapsed < HourTicks * 2) continue;
                if (cornerMotes[2] != null) MoteMaker.MakeStaticMote(center + new Vector3(0f, 0f, spread), map, cornerMotes[2], 0.33f);
                if (cornerMotes[3] != null) MoteMaker.MakeStaticMote(center + new Vector3(spread, 0f, 0f), map, cornerMotes[3], 0.33f);
                if (cornerMotes[0] != null) MoteMaker.MakeStaticMote(center + new Vector3(0f, 0f, -spread), map, cornerMotes[0], 0.33f);
                if (cornerMotes[1] != null) MoteMaker.MakeStaticMote(center + new Vector3(-spread, 0f, 0f), map, cornerMotes[1], 0.33f);
                if (elapsed >= ConsumptionTicks - 500 && centerMote != null)
                    MoteMaker.MakeStaticMote(center, map, centerMote, 0.7f);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            if (!parent.Spawned || parent.def.category == ThingCategory.Item) yield break;
            if (!contained)
            {
                yield return new Command_Action { defaultLabel = "SCP_882Load".Translate(),
                    defaultDesc = "SCP_882LoadDesc".Translate(), action = () => ChooseWorker(ScpInteractionDefOf.SCP_882Load) };
                yield break;
            }
            yield return new Command_Action { defaultLabel = "SCP_882Inspect".Translate(),
                defaultDesc = "SCP_882InspectDesc".Translate(), action = () => ChooseWorker(ScpInteractionDefOf.SCP_882Inspect) };
            yield return new Command_Action { defaultLabel = "SCP_882ReplaceRopes".Translate(),
                defaultDesc = "SCP_882ReplaceRopesDesc".Translate(), action = () => ChooseWorker(ScpInteractionDefOf.SCP_882ReplaceRopes) };
            yield return new Command_Action { defaultLabel = "SCP_882Unload".Translate(),
                defaultDesc = "SCP_882UnloadDesc".Translate(), action = () => ChooseWorker(ScpInteractionDefOf.SCP_882Unload) };
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            if (!CanWork(pawn, parent)) yield break;
            if (parent.def.category == ThingCategory.Item)
            {
                foreach (Thing tank in AvailableTanks(pawn))
                {
                    Thing destination = tank;
                    yield return new FloatMenuOption("SCP_882LoadInto".Translate(destination.LabelCap),
                        () => AssignJob(pawn, ScpInteractionDefOf.SCP_882Load, parent, destination));
                }
                yield break;
            }
            if (!contained)
            {
                Thing loose = AvailableObject(pawn);
                if (loose != null)
                    yield return new FloatMenuOption("SCP_882Load".Translate(),
                        () => AssignJob(pawn, ScpInteractionDefOf.SCP_882Load, loose, parent));
                yield break;
            }
            yield return new FloatMenuOption("SCP_882Inspect".Translate(),
                () => AssignJob(pawn, ScpInteractionDefOf.SCP_882Inspect, parent));
            if (HasEnoughCloth(pawn))
                yield return new FloatMenuOption("SCP_882ReplaceRopes".Translate(),
                    () => AssignJob(pawn, ScpInteractionDefOf.SCP_882ReplaceRopes, parent, AvailableCloth(pawn)));
            else yield return new FloatMenuOption("SCP_882NeedCloth".Translate(), null);
            if (CanUnload)
                yield return new FloatMenuOption("SCP_882Unload".Translate(),
                    () => AssignJob(pawn, ScpInteractionDefOf.SCP_882Unload, parent));
        }

        private static IEnumerable<Thing> AvailableTanks(Pawn pawn)
        {
            return pawn.Map.listerThings.AllThings.Where(t =>
                    (t.def.defName == "SCP_882Core" || t.def.defName == "SCP_882CoreFull")
                    && t.TryGetComp<CompScp882>() != null && !t.TryGetComp<CompScp882>().ContainsObject
                    && CanWork(pawn, t))
                .OrderBy(t => t.Position.DistanceToSquared(pawn.Position));
        }

        private Thing AvailableObject(Pawn pawn)
        {
            ThingDef objectDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_882Object");
            return objectDef == null ? null : pawn.Map.listerThings.ThingsOfDef(objectDef)
                .Where(t => CanWork(pawn, t)).OrderBy(t => t.Position.DistanceToSquared(parent.Position)).FirstOrDefault();
        }

        private void ChooseWorker(JobDef jobDef)
        {
            var options = new List<FloatMenuOption>();
            foreach (Pawn candidate in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                Pawn pawn = candidate;
                if (!CanWork(pawn, parent)) continue;
                if (jobDef == ScpInteractionDefOf.SCP_882Unload && !CanUnload) continue;
                if (jobDef == ScpInteractionDefOf.SCP_882ReplaceRopes && !HasEnoughCloth(pawn)) continue;
                Thing source = jobDef == ScpInteractionDefOf.SCP_882Load ? AvailableObject(pawn) : parent;
                if (source == null) continue;
                options.Add(new FloatMenuOption(pawn.LabelShortCap,
                    () => AssignJob(pawn, jobDef, source, jobDef == ScpInteractionDefOf.SCP_882Load
                        ? parent : jobDef == ScpInteractionDefOf.SCP_882ReplaceRopes ? AvailableCloth(pawn) : null)));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("SCP_882NoWorker".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void AssignJob(Pawn pawn, JobDef definition, Thing targetA, Thing targetB = null)
        {
            if (targetA == null || targetB == null && (definition == ScpInteractionDefOf.SCP_882Load
                || definition == ScpInteractionDefOf.SCP_882ReplaceRopes)) return;
            Job job = targetB == null ? JobMaker.MakeJob(definition, targetA) : JobMaker.MakeJob(definition, targetA, targetB);
            if (definition == ScpInteractionDefOf.SCP_882Load) job.count = 1;
            job.playerForced = true;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public override string CompInspectStringExtra()
        {
            string countdown = consumingStarts.Count == 0 ? "" : ("\n" + "SCP_882PullCountdown".Translate(
                (Math.Max(0, ConsumptionTicks - (Find.TickManager.TicksGame - consumingStarts.Min())) / 2500f).ToString("F1"))).ToString();
            if (parent.def.category == ThingCategory.Item)
                return "SCP_882LooseStatus".Translate(missedDays) + "\n" + "SCP_882AttractionRadius".Translate()
                    + "\n" + "SCP_882HumanoidProgress".Translate(Math.Min(2, Math.Max(0, growthTiles - humanoidsCreated * 2)))
                    + countdown;
            if (!contained) return "SCP_882Empty".Translate(((CompProperties_Scp882)props).seawaterPercent)
                + "\n" + "SCP_882AttractionRadius".Translate();
            return "SCP_882Status".Translate(((CompProperties_Scp882)props).seawaterPercent,
                rustIntact ? "SCP_882RustCovered".Translate() : "SCP_882RustExposed".Translate(), missedDays)
                + "\n" + "SCP_882AttractionRadius".Translate()
                + "\n" + "SCP_882GrowthProgress".Translate(growthQuarters, 4)
                + "\n" + "SCP_882HumanoidProgress".Translate(Math.Min(2, Math.Max(0, growthTiles - humanoidsCreated * 2)))
                + countdown;
        }

        private void CopyStateFrom(CompScp882 source)
        {
            absorbedUnits = source.absorbedUnits;
            growthTiles = source.growthTiles;
            growthQuarters = source.growthQuarters;
            humanoidsCreated = source.humanoidsCreated;
            churchRaidTriggered = source.churchRaidTriggered;
            rustIntact = source.rustIntact;
            lastInspectionTick = source.lastInspectionTick;
            lastRopeTick = source.lastRopeTick;
            nextDayTick = source.nextDayTick;
            missedDays = source.missedDays;
            consumingTargets = new List<Thing>(source.consumingTargets);
            consumingStarts = new List<int>(source.consumingStarts);
            contained = true;
        }

        public bool LoadObject(Thing objectThing)
        {
            if (contained || objectThing == null || objectThing.def.defName != "SCP_882Object") return false;
            CompScp882 objectComp = objectThing.TryGetComp<CompScp882>();
            if (objectComp == null) return false;
            CopyStateFrom(objectComp);
            bool fullSeawater = ((CompProperties_Scp882)props).seawaterPercent == 100;
            if (fullSeawater)
            {
                consumingTargets.Clear();
                consumingStarts.Clear();
                attractedMetal = null;
                attractionStartTick = 0;
            }
            objectThing.Destroy(DestroyMode.Vanish);
            RefreshTankGraphic();
            Messages.Message((fullSeawater ? "SCP_882LoadedFull" : "SCP_882Loaded").Translate(),
                parent, MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public bool UnloadObject()
        {
            if (!CanUnload)
            {
                Messages.Message("SCP_882RustMustRecover".Translate(), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            ThingDef objectDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_882Object");
            if (objectDef == null) return false;
            Map map = parent.Map;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, 4.9f, true))
            {
                if (!ScpSpawnCells.FootprintClear(cell, map, objectDef)) continue;
                Thing objectThing = ThingMaker.MakeThing(objectDef);
                objectThing.TryGetComp<CompScp882>().CopyStateFrom(this);
                GenSpawn.Spawn(objectThing, cell, map);
                contained = false;
                consumingTargets.Clear();
                consumingStarts.Clear();
                RefreshTankGraphic();
                Messages.Message("SCP_882Unloaded".Translate(), objectThing, MessageTypeDefOf.CautionInput, false);
                return true;
            }
            Messages.Message("SCP_882NoUnloadSpace".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        private void RefreshTankGraphic()
        {
            Map map = parent.Map;
            if (map != null)
                map.mapDrawer.MapMeshDirty(parent.Position,
                    (ulong)MapMeshFlagDefOf.Things | (ulong)MapMeshFlagDefOf.Buildings, true, true);
        }

        public void Inspect()
        {
            lastInspectionTick = Find.TickManager.TicksGame;
            if (!rustIntact && ((CompProperties_Scp882)props).seawaterPercent == 100)
                rustIntact = true;
            Messages.Message("SCP_882Inspected".Translate(), parent, MessageTypeDefOf.PositiveEvent, false);
        }

        public void ReplaceRopes()
        {
            lastRopeTick = Find.TickManager.TicksGame;
            Messages.Message("SCP_882RopesReplaced".Translate(), parent, MessageTypeDefOf.PositiveEvent, false);
        }

        public override void CompTickRare()
        {
            if (!parent.Spawned || parent.Map == null
                || (parent.def.category != ThingCategory.Item && !contained)) return;
            Map map = parent.Map;
            int now = Find.TickManager.TicksGame;
            if (nextDayTick == 0) nextDayTick = now + 60000;
            while (now >= nextDayTick)
            {
                int previousDay = nextDayTick - 60000;
                bool maintained = lastInspectionTick >= previousDay && lastRopeTick >= previousDay
                    && (rustIntact || ((CompProperties_Scp882)props).seawaterPercent == 100);
                missedDays = maintained ? 0 : missedDays + 1;
                nextDayTick += 60000;
                if (missedDays > 0 && Rand.Chance(1f - (float)Math.Pow(0.7, missedDays)))
                {
                    if (TryGrow(map))
                        Find.LetterStack.ReceiveLetter("SCP_882ContainmentBreachTitle".Translate(),
                            "SCP_882ContainmentBreachBody".Translate(missedDays), LetterDefOf.ThreatSmall, parent);
                }
            }
            PullMetal(map, now);

            CompProperties_Scp882 settings = (CompProperties_Scp882)props;
            if (growthQuarters >= 4 && growthTiles < 24 && TryGrow(map)) growthQuarters -= 4;
            if (growthTiles >= (humanoidsCreated + 1) * 2 && TrySpawnHumanoid(map)) humanoidsCreated++;

            if (growthTiles >= 4)
            {
                ThoughtDef hallucination = DefDatabase<ThoughtDef>.GetNamedSilentFail("SCP_882GrindingHallucination");
                if (hallucination != null)
                    foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                        if (!pawn.Dead && pawn.Position.DistanceToSquared(parent.Position) <= 64
                            && pawn.needs != null && pawn.needs.mood != null)
                            pawn.needs.mood.thoughts.memories.TryGainMemory(hallucination);
            }
            if (!churchRaidTriggered && growthTiles >= settings.raidGrowthThreshold && map.IsPlayerHome)
                TryCallChurchRaid(map);
        }

        private IEnumerable<Thing> NearbyTargets(Map map)
        {
            return map.listerThings.AllThings.Where(thing => thing != parent && thing.Spawned
                && thing.OccupiedRect().ClosestDistSquaredTo(parent.Position) <= AttractionRadiusSquared
                && IsMetalFeed(thing));
        }

        private static bool IsMetalFeed(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.def.defName == "HiddenConduit"
                || thing.def.defName == "SCP_882Core" || thing.def.defName == "SCP_882CoreFull"
                || thing.def.defName == "SCP_882GearGrowth" || thing.def.defName == "SCP_882GearGrowthQuarter"
                || (thing.def.category != ThingCategory.Item && thing.def.category != ThingCategory.Building)) return false;
            if (thing.def.defName == "AncientPodCar" || thing.def.defName == "AncientRustedCar"
                || thing.def.defName == "AncientRustedTruck" || thing.def.defName == "AncientRustedJeep"
                || thing.def.defName == "AncientRustedCarFrame" || thing.def.defName == "AncientWarwalkerTorso"
                || thing.def.defName == "AncientShoppingCart" || thing.def.defName == "AncientRustedEngineBlock"
                || thing.def.defName == "AncientLargeRustedEngineBlock" || thing.def.defName == "AncientWarwalkerClaw"
                || thing.def.defName == "AncientWarwalkerLeg" || thing.def.defName == "AncientMiniWarwalkerRemains"
                || thing.def.defName == "AncientWarwalkerFoot" || thing.def.defName == "AncientWarwalkerShell"
                || thing.def.defName == "AncientJetEngine" || thing.def.defName == "AncientDropshipEngine") return true;
            if (thing.def.defName == "ChunkSlagSteel") return true;
            return thing.Stuff != null ? thing.Stuff.IsMetal
                : thing.def.IsMetal || thing.def.costList != null
                    && thing.def.costList.Any(cost => cost.thingDef != null && cost.thingDef.IsMetal);
        }

        private void PullMetal(Map map, int now)
        {
            for (int i = consumingTargets.Count - 1; i >= 0; i--)
            {
                Thing target = consumingTargets[i];
                if (target == null || !target.Spawned || target.Map != map
                    || target.OccupiedRect().ClosestDistSquaredTo(parent.Position) > AttractionRadiusSquared
                    || !IsMetalFeed(target))
                {
                    consumingTargets.RemoveAt(i);
                    consumingStarts.RemoveAt(i);
                    continue;
                }
                if (now - consumingStarts[i] < ConsumptionTicks) continue;
                bool metal = IsMetalFeed(target);
                IntVec3 consumedAt = target.Position;
                if (metal) absorbedUnits += Math.Max(1, target.stackCount);
                target.Destroy(DestroyMode.Vanish);
                consumingTargets.RemoveAt(i);
                consumingStarts.RemoveAt(i);
                if (metal)
                {
                    growthQuarters++;
                    ThingDef quarter = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_882GearGrowthQuarter");
                    if (quarter != null && ScpSpawnCells.FootprintClear(consumedAt, map, quarter))
                        GenSpawn.Spawn(ThingMaker.MakeThing(quarter), consumedAt, map);
                    if (Rand.Chance(0.2f)) rustIntact = false;
                }
            }
            var tracked = new HashSet<Thing>(consumingTargets);
            foreach (Thing target in NearbyTargets(map))
            {
                if (!tracked.Add(target)) continue;
                consumingTargets.Add(target);
                consumingStarts.Add(now);
            }
            DrawConsumptionMotes(map, now);
        }

        private bool TryGrow(Map map)
        {
            ThingDef growthDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_882GearGrowth");
            if (growthDef == null) return false;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, 3.5f, true))
            {
                if (!ScpSpawnCells.FootprintClear(cell, map, growthDef)) continue;
                GenSpawn.Spawn(ThingMaker.MakeThing(growthDef), cell, map);
                growthTiles++;
                return true;
            }
            return false;
        }

        private bool TrySpawnHumanoid(Map map)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, 7f, true))
            {
                Pawn humanoid = SpawnHumanoidAt(map, cell);
                if (humanoid == null) continue;
                Find.LetterStack.ReceiveLetter("SCP_882HumanoidTitle".Translate(),
                    "SCP_882HumanoidBody".Translate(), LetterDefOf.ThreatSmall, humanoid);
                return true;
            }
            return false;
        }

        public static Pawn SpawnHumanoidAt(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map) || !cell.Standable(map)
                || cell.GetEdifice(map) != null || cell.GetThingList(map).Any(thing => thing is Pawn))
                return null;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_882MetalHumanoid");
            Faction faction = Faction.OfMechanoids;
            if (kind == null || faction == null) return null;
            Pawn humanoid = PawnGenerator.GeneratePawn(kind, faction);
            if (humanoid == null) return null;
            GenSpawn.Spawn(humanoid, cell, map);
            LordMaker.MakeNewLord(faction,
                new LordJob_AssaultColony(faction, false, true, false, false, false, false, false),
                map, new List<Pawn> { humanoid });
            return humanoid;
        }

        private void TryCallChurchRaid(Map map)
        {
            FactionDef definition = DefDatabase<FactionDef>.GetNamedSilentFail("SCP_ChurchBrokenGod");
            if (definition == null || Find.FactionManager == null) return;
            Faction church = Find.FactionManager.FirstFactionOfDef(definition);
            if (church == null) return;
            ScpFactionRelationsComponent.EnsureHostile(church, Faction.OfPlayer);
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.faction = church;
            parms.points = Math.Max(500f, StorytellerUtility.DefaultThreatPointsNow(map));
            parms.forced = true;
            if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(parms)) return;
            churchRaidTriggered = true;
            Find.LetterStack.ReceiveLetter("SCP_882RaidTitle".Translate(),
                "SCP_882RaidBody".Translate(), LetterDefOf.ThreatBig, parent);
        }
    }
}
