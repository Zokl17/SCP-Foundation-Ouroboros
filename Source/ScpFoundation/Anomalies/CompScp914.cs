using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Runtime;
using UnityEngine;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp914 : CompProperties
    {
        public CompProperties_Scp914() { compClass = typeof(CompScp914); }
    }

    [StaticConstructorOnStartup]
    public sealed class CompScp914 : ThingComp, IThingHolder
    {
        private static readonly string[] Settings = { "Rough", "Coarse", "1:1", "Fine", "Very Fine" };
        private static readonly StatDef[] ArmorStats =
            { StatDefOf.ArmorRating_Sharp, StatDefOf.ArmorRating_Blunt, StatDefOf.ArmorRating_Heat };
        private static readonly float[][] ArmorMultipliers =
        {
            null, null,
            new[] { 1.2f, 1.2f, 1.2f },
            new[] { 1.6f, 2f, 2f },
            new[] { 2.5f, 4f, 4f }
        };
        private static readonly QualityCategory[] ArmorQualities =
            (QualityCategory[])Enum.GetValues(typeof(QualityCategory));
        private static Graphic closedInputGraphic;
        private int setting = 2;
        private int finishTick;
        private Thing input;
        private ThingOwner<Thing> heldInput;

        public CompScp914() { heldInput = new ThingOwner<Thing>(this); }

        public ThingOwner GetDirectlyHeldThings() { return heldInput; }
        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, heldInput);
        }

        private IntVec3 InputCell
        {
            get { CellRect rect = parent.OccupiedRect(); return new IntVec3(rect.minX - 1, 0, (rect.minZ + rect.maxZ) / 2); }
        }

        private IntVec3 OutputCell
        {
            get { CellRect rect = parent.OccupiedRect(); return new IntVec3(rect.maxX + 1, 0, (rect.minZ + rect.maxZ) / 2); }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref setting, "scp914Setting", 2);
            Scribe_Values.Look(ref finishTick, "scp914FinishTick", 0);
            Scribe_Deep.Look(ref heldInput, "scp914HeldInput", this);
            Scribe_References.Look(ref input, "scp914Input");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                setting = Math.Max(0, Math.Min(Settings.Length - 1, setting));
                if (heldInput == null) heldInput = new ThingOwner<Thing>(this);
                if (heldInput.Count > 0) input = heldInput[0];
            }
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (!parent.Spawned || finishTick == 0 || heldInput == null || heldInput.Count == 0) return;
            if (closedInputGraphic == null)
                closedInputGraphic = GraphicDatabase.Get<Graphic_Single>(
                    "Things/Item/ScpArtifacts/Scp914IntakeClosed", ShaderDatabase.Cutout,
                    new Vector2(1f, 1f), Color.white);
            Vector3 position = InputCell.ToVector3Shifted();
            position.y = AltitudeLayer.ItemImportant.AltitudeFor();
            closedInputGraphic.Draw(position, Rot4.North, parent);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (heldInput != null && heldInput.Count > 0)
                heldInput.TryDropAll(InputCell.InBounds(map) ? InputCell : parent.Position,
                    map, ThingPlaceMode.Near);
            input = null;
            finishTick = 0;
            base.PostDeSpawn(map, mode);
        }

        public override string CompInspectStringExtra()
        {
            return "SCP_914Status".Translate(("SCP_914Mode" + setting).Translate(), finishTick > 0
                ? "SCP_914Running".Translate() : "SCP_914Ready".Translate()) + "\n"
                + "SCP_914InputPosition".Translate() + " " + "SCP_914OutputPosition".Translate();
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            if (!parent.Spawned) return;
            if (InputCell.InBounds(parent.Map))
                GenDraw.DrawFieldEdges(new List<IntVec3> { InputCell }, Color.yellow, null, null, 0);
            if (OutputCell.InBounds(parent.Map))
                GenDraw.DrawFieldEdges(new List<IntVec3> { OutputCell }, Color.green, null, null, 0);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = "SCP_914SetMode".Translate(("SCP_914Mode" + setting).Translate()),
                defaultDesc = "SCP_914SetModeDesc".Translate(),
                icon = ScpIcons.Get(4),
                action = () =>
                {
                    if (finishTick > 0) return;
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    for (int index = 0; index < Settings.Length; index++)
                    {
                        int chosen = index;
                        options.Add(new FloatMenuOption((chosen == setting ? "● " : "")
                            + ("SCP_914Mode" + chosen).Translate(), () => setting = chosen));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
            yield return new Command_Action
            {
                defaultLabel = "SCP_914Start".Translate(),
                defaultDesc = "SCP_914StartDesc".Translate(),
                icon = ScpIcons.Get(5),
                action = StartProcess
            };
        }

        private void StartProcess()
        {
            if (!parent.Spawned || finishTick > 0 || heldInput.Count > 0) return;
            input = parent.Map.listerThings.AllThings
                .Where(thing => thing != parent && thing.Spawned && thing.def.category == ThingCategory.Item
                    && thing.def.stackLimit > 0 && thing.Position == InputCell
                    && (!thing.def.defName.StartsWith("SCP_", StringComparison.Ordinal)
                        || (setting == 3 && thing.def.defName == "SCP_500Pill"))
                    && thing.TryGetComp<ScpFoundation.Runtime.CompScp>() == null
                    && thing.GetStatValue(StatDefOf.MarketValue) >= ThingDefOf.Steel.BaseMarketValue)
                .FirstOrDefault();
            if (input == null)
            {
                Messages.Message("SCP_914NeedsInput".Translate(), parent, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (IsArmor(input) && setting >= 2
                && !Scp914ArmorPolicy.ShouldRepairOnOneToOne(setting, input.HitPoints, input.MaxHitPoints))
            {
                ThingDef result;
                ThingDef stuff;
                QualityCategory quality;
                if (!TryFindArmorResult(input, out result, out stuff, out quality))
                {
                    Messages.Message("SCP_914NoArmorMatch".Translate(), parent, MessageTypeDefOf.RejectInput, false);
                    input = null;
                    return;
                }
            }
            if (!StoreInput(input))
            {
                input = null;
                Messages.Message("SCP_914IntakeFailed".Translate(), parent, MessageTypeDefOf.RejectInput, false);
                return;
            }
            finishTick = Find.TickManager.TicksGame + (setting == 3
                ? GenDate.TicksPerDay / 2 : setting == 4 ? GenDate.TicksPerDay : 2500);
            Messages.Message("SCP_914Started".Translate(input.LabelCap, ("SCP_914Mode" + setting).Translate()), parent,
                MessageTypeDefOf.NeutralEvent, false);
        }

        private bool StoreInput(Thing source)
        {
            if (source == null || !source.Spawned || heldInput.Count > 0) return false;
            Map map = source.Map;
            IntVec3 cell = source.Position;
            Thing stored = source.stackCount > 1 ? source.SplitOff(1) : source;
            if (stored.Spawned) stored.DeSpawnOrDeselect();
            if (!heldInput.TryAdd(stored, true))
            {
                GenPlace.TryPlaceThing(stored, cell, map, ThingPlaceMode.Near);
                return false;
            }
            input = stored;
            return true;
        }

        public override void CompTickRare()
        {
            if (!parent.Spawned) return;
            if (finishTick == 0)
            {
                if (heldInput.Count > 0)
                    heldInput.TryDropAll(InputCell, parent.Map, ThingPlaceMode.Near);
                return;
            }
            // Move an input from an old save into the new sealed intake before the cycle finishes.
            if (heldInput.Count == 0 && (input == null || !input.Spawned || input.Map != parent.Map
                || (input.Position != InputCell && input.Position.DistanceToSquared(parent.Position) > 16)
                || !StoreInput(input)))
            {
                finishTick = 0;
                input = null;
                return;
            }
            if (Find.TickManager.TicksGame < finishTick) return;
            finishTick = 0;
            Thing source = heldInput[0];
            heldInput.Remove(source);
            input = null;
            if (setting == 3 && source.def.defName == "SCP_500Pill")
            {
                source.Destroy(DestroyMode.Vanish);
                ThingDef amulet = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_427Amulet");
                if (amulet == null) return;
                Thing result = ThingMaker.MakeThing(amulet);
                if (!GenPlace.TryPlaceThing(result, OutputCell, parent.Map, ThingPlaceMode.Near))
                    result.Destroy(DestroyMode.Vanish);
                else
                {
                    CompScp scp = result.TryGetComp<CompScp>();
                    if (scp != null) scp.NotifyCaptured();
                    Messages.Message("SCP_914Finished".Translate(result.LabelCap), parent,
                        MessageTypeDefOf.PositiveEvent, false);
                }
                return;
            }
            if (IsArmor(source))
            {
                ProcessArmor(source);
                return;
            }
            float value = Math.Max(0f, source.GetStatValue(StatDefOf.MarketValue));
            Thing output = source.stackCount > 1 ? source.SplitOff(1) : source;
            if (output.Spawned) output.DeSpawn();
            if (setting <= 1)
            {
                output.Destroy(DestroyMode.Vanish);
                ThingDef steel = ThingDefOf.Steel;
                output = ThingMaker.MakeThing(steel);
                float fraction = setting == 0 ? 0.25f : 0.5f;
                output.stackCount = Math.Max(1, Math.Min(steel.stackLimit,
                    (int)(value * fraction / steel.BaseMarketValue)));
            }
            else if (setting >= 3)
            {
                CompQuality quality = output.TryGetComp<CompQuality>();
                if (quality != null)
                    quality.SetQuality((QualityCategory)Math.Min((int)QualityCategory.Legendary,
                        (int)quality.Quality + (setting == 3 ? 1 : 2)), ArtGenerationContext.Colony);
                else if (setting == 4 && value >= 300f)
                {
                    output.Destroy(DestroyMode.Vanish);
                    output = ThingMaker.MakeThing(ThingDefOf.ComponentSpacer);
                }
                else if (setting == 3 && value >= 50f)
                {
                    output.Destroy(DestroyMode.Vanish);
                    output = ThingMaker.MakeThing(ThingDefOf.ComponentIndustrial);
                }
            }
            if (output.def.useHitPoints) output.HitPoints = output.MaxHitPoints;
            if (!GenPlace.TryPlaceThing(output, OutputCell, parent.Map,
                ThingPlaceMode.Near)) output.Destroy(DestroyMode.Vanish);
            else Messages.Message("SCP_914Finished".Translate(output.LabelCap), parent,
                MessageTypeDefOf.PositiveEvent, false);
        }

        private static bool IsArmor(Thing thing)
        {
            if (!thing.def.IsApparel || thing.def.apparel == null) return false;
            return ArmorStats.Any(stat => thing.GetStatValue(stat) >= 0.05f);
        }

        private static bool SameArmorSlot(ThingDef source, ThingDef candidate)
        {
            return source.apparel.layers != null && candidate.apparel.layers != null
                && source.apparel.layers.Intersect(candidate.apparel.layers).Any()
                && source.apparel.bodyPartGroups != null && candidate.apparel.bodyPartGroups != null
                && source.apparel.bodyPartGroups.Intersect(candidate.apparel.bodyPartGroups).Any();
        }

        private bool TryFindArmorResult(Thing source, out ThingDef result, out ThingDef resultStuff,
            out QualityCategory resultQuality)
        {
            result = null;
            resultStuff = null;
            resultQuality = QualityCategory.Normal;
            float[] original = ArmorStats.Select(stat => source.GetStatValue(stat)).ToArray();
            CompQuality sourceQuality = source.TryGetComp<CompQuality>();
            QualityCategory originalQuality = sourceQuality != null ? sourceQuality.Quality : QualityCategory.Normal;
            int bestRadius = int.MaxValue;
            float bestError = float.PositiveInfinity;

            foreach (ThingDef candidate in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!candidate.IsApparel || candidate.apparel == null
                    || candidate.category != ThingCategory.Item || candidate.stackLimit != 1
                    || candidate.defName.StartsWith("SCP_", StringComparison.Ordinal)
                    || !SameArmorSlot(source.def, candidate)) continue;

                ThingDef stuff = null;
                if (candidate.MadeFromStuff)
                {
                    stuff = source.Stuff != null && GenStuff.AllowedStuffsFor(candidate).Contains(source.Stuff)
                        ? source.Stuff : GenStuff.DefaultStuffFor(candidate);
                    if (stuff == null) continue;
                }
                bool hasQuality = candidate.HasComp(typeof(CompQuality));
                foreach (QualityCategory quality in hasQuality ? ArmorQualities
                    : new[] { QualityCategory.Normal })
                {
                    if (setting == 2 && candidate == source.def && stuff == source.Stuff
                        && quality == originalQuality) continue;
                    StatRequest request = StatRequest.For(candidate, stuff, quality);
                    float[] protection = ArmorStats.Select(stat => stat.Worker.GetValue(request, true)).ToArray();
                    int radius;
                    float error;
                    if (!Scp914ArmorPolicy.TryMeasure(original, protection, ArmorMultipliers[setting],
                        out radius, out error)) continue;
                    if (radius < bestRadius || (radius == bestRadius && (error < bestError - 0.0001f
                        || (Math.Abs(error - bestError) <= 0.0001f && result != null
                            && (string.CompareOrdinal(candidate.defName, result.defName) < 0
                                || (candidate == result && quality < resultQuality))))))
                    {
                        result = candidate;
                        resultStuff = stuff;
                        resultQuality = quality;
                        bestRadius = radius;
                        bestError = error;
                    }
                }
            }
            return result != null;
        }

        private void ProcessArmor(Thing source)
        {
            if (setting >= 2)
            {
                if (Scp914ArmorPolicy.ShouldRepairOnOneToOne(setting, source.HitPoints, source.MaxHitPoints))
                {
                    source.HitPoints = source.MaxHitPoints;
                    PlaceResult(source);
                    return;
                }
                ThingDef resultDef;
                ThingDef resultStuff;
                QualityCategory quality;
                if (!TryFindArmorResult(source, out resultDef, out resultStuff, out quality))
                {
                    Messages.Message("SCP_914NoArmorMatch".Translate(), parent, MessageTypeDefOf.RejectInput, false);
                    GenPlace.TryPlaceThing(source, InputCell, parent.Map, ThingPlaceMode.Near);
                    return;
                }
                Thing result = ThingMaker.MakeThing(resultDef, resultStuff);
                CompQuality resultQuality = result.TryGetComp<CompQuality>();
                if (resultQuality != null)
                    resultQuality.SetQuality(quality, ArtGenerationContext.Colony);
                if (result.def.useHitPoints) result.HitPoints = result.MaxHitPoints;
                if (source.Spawned) source.DeSpawn();
                source.Destroy(DestroyMode.Vanish);
                PlaceResult(result);
                return;
            }

            Dictionary<ThingDef, int> materials = new Dictionary<ThingDef, int>();
            if (setting == 0) materials.Add(ThingDefOf.Steel, 25);
            else
            {
                bool craftable = source.def.recipeMaker != null
                    || DefDatabase<RecipeDef>.AllDefsListForReading.Any(recipe => recipe.ProducedThingDef == source.def);
                if (craftable)
                {
                    if (source.def.CostList != null)
                        foreach (ThingDefCountClass cost in source.def.CostList)
                            AddMaterial(materials, cost.thingDef, cost.count);
                    if (source.Stuff != null)
                        AddMaterial(materials, source.Stuff, source.def.CostStuffCount);
                }
                if (materials.Count == 0)
                {
                    AddMaterial(materials, ThingDefOf.Steel, 50);
                    AddMaterial(materials, ThingDefOf.ComponentIndustrial, 2);
                }
            }
            if (source.Spawned) source.DeSpawn();
            source.Destroy(DestroyMode.Vanish);
            foreach (KeyValuePair<ThingDef, int> material in materials)
            {
                int remaining = material.Value;
                while (remaining > 0)
                {
                    ThingDef stuff = material.Key.MadeFromStuff ? GenStuff.DefaultStuffFor(material.Key) : null;
                    Thing result = ThingMaker.MakeThing(material.Key, stuff);
                    result.stackCount = Math.Min(remaining, Math.Max(1, material.Key.stackLimit));
                    remaining -= result.stackCount;
                    PlaceResult(result);
                }
            }
        }

        private static void AddMaterial(Dictionary<ThingDef, int> materials, ThingDef def, int count)
        {
            if (def == null || count <= 0) return;
            int existing;
            materials.TryGetValue(def, out existing);
            materials[def] = existing + count;
        }

        private void PlaceResult(Thing result)
        {
            if (!GenPlace.TryPlaceThing(result, OutputCell, parent.Map, ThingPlaceMode.Near))
                result.Destroy(DestroyMode.Vanish);
            else Messages.Message("SCP_914Finished".Translate(result.LabelCap), parent,
                MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
