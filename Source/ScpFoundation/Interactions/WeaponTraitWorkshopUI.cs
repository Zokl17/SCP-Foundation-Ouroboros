using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Runtime;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public sealed class CompProperties_WeaponTraitWorkshop : CompProperties
    {
        public CompProperties_WeaponTraitWorkshop() { compClass = typeof(CompWeaponTraitWorkshop); }
    }

    public sealed class CompWeaponTraitWorkshop : ThingComp
    {
        public override void CompTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0 || !parent.Spawned) return;
            Building_WorkTable bench = parent as Building_WorkTable;
            if (bench == null || !bench.CurrentlyUsableForBills()) return;
            foreach (Bill_WeaponTrait bill in bench.BillStack.Bills.OfType<Bill_WeaponTrait>())
            {
                Thing weapon = bill.TargetWeapon;
                if (weapon == null || weapon.Spawned || weapon.Destroyed) continue;
                Pawn owner = bench.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p =>
                    p.equipment != null && p.equipment.Primary == weapon && !p.Drafted && !p.Downed
                    && p.Position.InHorDistOf(bench.Position, 3f));
                if (owner == null) continue;
                ThingWithComps dropped;
                owner.equipment.TryDropEquipment((ThingWithComps)weapon, out dropped, owner.Position, false);
            }
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            if (parent.Faction != Faction.OfPlayer || pawn == null || pawn.Faction != Faction.OfPlayer) yield break;
            yield return new FloatMenuOption("SCP_WeaponWorkshopButton".Translate(), () =>
                StartWorkshopJob(pawn));
        }

        private void StartWorkshopJob(Pawn pawn)
        {
            if (pawn == null || pawn.Drafted || pawn.Downed || !parent.Spawned)
            {
                Messages.Message("SCP_WeaponWorkshopPawn".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            if (pawn.Map != parent.Map || !pawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly))
            {
                Messages.Message("SCP_WeaponWorkshopReach".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            JobDef definition = DefDatabase<JobDef>.GetNamedSilentFail("SCP_OpenWeaponWorkshop");
            if (definition == null) return;
            Job job = JobMaker.MakeJob(definition, parent);
            job.playerForced = true;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }

    public sealed class JobDriver_OpenWeaponWorkshop : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Do(() =>
            {
                Building_WorkTable bench = job.targetA.Thing as Building_WorkTable;
                if (bench != null && bench.Spawned && pawn.Map == bench.Map)
                    Find.WindowStack.Add(new Dialog_WeaponTraitWorkshop(bench, pawn));
            });
        }
    }

    public sealed class Bill_WeaponTrait : Bill_Production
    {
        private Thing targetWeapon;

        public Thing TargetWeapon { get { return targetWeapon; } }

        public Bill_WeaponTrait() { }

        public Bill_WeaponTrait(RecipeDef recipe, Thing weapon) : base(recipe, null)
        {
            targetWeapon = weapon;
            repeatMode = BillRepeatModeDefOf.RepeatCount;
            repeatCount = 1;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref targetWeapon, "scpWorkshopTargetWeapon");
        }

        public override bool ShouldDoNow()
        {
            return targetWeapon != null && targetWeapon.Spawned
                && RecipeWorker_InstallWeaponTrait.CanInstall(targetWeapon,
                    RecipeWorker_InstallWeaponTrait.TraitFor(recipe))
                && base.ShouldDoNow();
        }

        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            base.Notify_IterationCompleted(billDoer, ingredients);
            if (billStack != null) billStack.Delete(this);
        }

        public override bool PawnAllowedToStartAnew(Pawn pawn)
        {
            WorkTypeDef work = Dialog_WeaponTraitWorkshop.WorkTypeFor(recipe);
            return pawn != null && work != null && !pawn.WorkTypeIsDisabled(work)
                && base.PawnAllowedToStartAnew(pawn);
        }

        public override bool IsFixedOrAllowedIngredient(Thing thing)
        {
            if (thing != null && thing.def.IsRangedWeapon && thing.def.IsWeapon)
                return thing == targetWeapon
                    && RecipeWorker_InstallWeaponTrait.CanInstall(thing, RecipeWorker_InstallWeaponTrait.TraitFor(recipe))
                    && base.IsFixedOrAllowedIngredient(thing);
            return base.IsFixedOrAllowedIngredient(thing);
        }
    }

    public sealed class WorkGiver_DoBillArmoryInlay : WorkGiver_DoBill
    {
        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            Job job = base.JobOnThing(pawn, thing, forced);
            return job != null && job.bill != null && job.bill.recipe.workSkill == SkillDefOf.Artistic
                ? job : null;
        }
    }

    public sealed class Dialog_WeaponTraitWorkshop : Window
    {
        private readonly Building_WorkTable bench;
        private Thing selectedWeapon;
        private string selectedGroup = "Barrel";
        private Vector2 traitScroll;
        private Vector2 buildScroll;

        private static readonly string[] Groups = {
            "Barrel", "Grip", "Launcher", "Ammo", "Material", "Stock", "Sights", "Mechanism"
        };

        public override Vector2 InitialSize { get { return new Vector2(Mathf.Min(1120f, UI.screenWidth - 24f),
            Mathf.Min(740f, UI.screenHeight - 24f)); } }

        public Dialog_WeaponTraitWorkshop(Building_WorkTable bench, Pawn preferredPawn = null)
        {
            this.bench = bench;
            selectedWeapon = preferredPawn == null || preferredPawn.equipment == null
                ? null : preferredPawn.equipment.Primary;
            // Remove completed one-shot bills left in saves by older versions.
            foreach (Bill_WeaponTrait bill in bench.BillStack.Bills.OfType<Bill_WeaponTrait>().ToList())
            {
                CompUniqueWeapon comp = bill.TargetWeapon == null
                    ? null : bill.TargetWeapon.TryGetComp<CompUniqueWeapon>();
                WeaponTraitDef trait = RecipeWorker_InstallWeaponTrait.TraitFor(bill.recipe);
                if (bill.repeatCount <= 0 || comp != null && trait != null
                    && comp.TraitsListForReading.Contains(trait)) bench.BillStack.Delete(bill);
            }
            doCloseX = true;
            doCloseButton = true;
            forcePause = true;
        }

        private IEnumerable<Thing> AvailableWeapons()
        {
            IEnumerable<Thing> equipped = bench.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p.equipment != null && p.equipment.Primary != null
                    && p.Position.InHorDistOf(bench.Position, 3f))
                .Select(p => p.equipment.Primary);
            IEnumerable<Thing> nearby = bench.Map.listerThings.AllThings
                .Where(thing => thing.Spawned && thing.Position.InHorDistOf(bench.Position, 3f));
            return equipped.Concat(nearby)
                .Where(thing => thing.def.IsWeapon && thing.def.IsRangedWeapon
                    && thing.TryGetComp<CompUniqueWeapon>() != null).Distinct();
        }

        private static string GroupFor(WeaponTraitDef trait)
        {
            switch (trait.defName)
            {
                case "ExtendedBarrel": case "ShortenedBarrel": case "ReinforcedLimbs":
                case "SCP_Compensator": case "SCP_Bayonet":
                case "SCP_Suppressor": case "SCP_FlashHider": return "Barrel";
                case "CustomGrip": case "SCP_Bipod": case "SCP_LaserTargetingUnit": return "Grip";
                case "StabilizerBrace": case "SCP_FoldingStock":
                case "SCP_WeightedStock": return "Stock";
                case "GoldInlay": case "JadeInlay": case "SCP_CarbonInserts":
                case "Ornamental": case "Ugly": case "Lightweight": return "Material";
                case "AimAssistance": case "ImprovedSights": case "ShoddySights":
                case "PrecisionScope": case "RangefinderScope":
                case "SCP_ThermalSight": case "SCP_MotionPredictor":
                case "SCP_EnemyAnalysis": return "Sights";
            }
            if (trait.weaponCategory.defName == "Attachable") return "Launcher";
            switch (trait.weaponCategory.defName)
            {
                case "BulletFiring": case "PelletFiring": case "Bow":
                case "Pistol": case "LowStoppingPower": return "Ammo";
                default: return "Mechanism";
            }
        }

        private List<WeaponTraitDef> TraitsFor(string group, Thing weapon)
        {
            return DefDatabase<WeaponTraitDef>.AllDefsListForReading
                .Where(t => GroupFor(t) == group && RecipeWorker_InstallWeaponTrait.TraitFor(RecipeFor(t)) == t)
                .Where(t => weapon == null || RecipeWorker_InstallWeaponTrait.CategoryMatches(weapon, t))
                .OrderBy(t => t.label).ToList();
        }

        private static RecipeDef RecipeFor(WeaponTraitDef trait)
        {
            return DefDatabase<RecipeDef>.GetNamedSilentFail(RecipeWorker_InstallWeaponTrait.Prefix + trait.defName);
        }

        private string WeaponChoiceLabel(Thing candidate)
        {
            Pawn owner = bench.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p =>
                p.equipment != null && p.equipment.Primary == candidate);
            string location = owner != null ? owner.LabelShortCap
                : candidate.Position.x + ", " + candidate.Position.z;
            return candidate.LabelCap + " (" + location + ")";
        }

        public override void DoWindowContents(Rect rect)
        {
            if (bench == null || !bench.Spawned) { Close(); return; }
            List<Thing> weapons = AvailableWeapons().ToList();
            if (selectedWeapon == null || !weapons.Contains(selectedWeapon)) selectedWeapon = weapons.FirstOrDefault();
            Thing weapon = selectedWeapon;

            Widgets.Label(new Rect(0f, 0f, rect.width, 30f), "SCP_WeaponWorkshopTitle".Translate());
            Widgets.Label(new Rect(0f, 34f, rect.width, 52f), "SCP_WeaponWorkshopHelp".Translate());

            Rect selectorIcon = new Rect(0f, 96f, 42f, 42f);
            Texture2D weaponIcon = weapon == null ? ScpIcons.Get(3) : weapon.def.uiIcon;
            bool chooseWeapon = Widgets.ButtonImage(selectorIcon, weaponIcon);
            Widgets.Label(new Rect(48f, 100f, 166f, 38f), weapon == null
                ? "SCP_WeaponWorkshopNoWeapon".Translate().ToString() : weapon.LabelCap);
            if (chooseWeapon)
            {
                List<FloatMenuOption> options = weapons.Select(candidate => new FloatMenuOption(
                    WeaponChoiceLabel(candidate),
                    () => { selectedWeapon = candidate; buildScroll = Vector2.zero; traitScroll = Vector2.zero; }))
                    .ToList();
                if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
            }
            Widgets.Label(new Rect(0f, 149f, 214f, 30f), "SCP_WeaponWorkshopCurrentBuild".Translate());
            CompUniqueWeapon installedComp = weapon == null ? null : weapon.TryGetComp<CompUniqueWeapon>();
            List<WeaponTraitDef> installedTraits = installedComp == null
                ? new List<WeaponTraitDef>() : installedComp.TraitsListForReading;
            if (installedTraits.Count == 0)
                Widgets.Label(new Rect(0f, 184f, 214f, 50f), "SCP_WeaponWorkshopNoTraits".Translate());
            else
            {
                Rect buildView = new Rect(0f, 184f, 214f, rect.height - 252f);
                const float buildRowHeight = 100f;
                Rect buildContent = new Rect(0f, 0f, buildView.width - 18f,
                    Mathf.Max(buildView.height, installedTraits.Count * buildRowHeight));
                Widgets.BeginScrollView(buildView, ref buildScroll, buildContent);
                for (int i = 0; i < installedTraits.Count; i++)
                {
                    WeaponTraitDef trait = installedTraits[i];
                    float y = i * buildRowHeight;
                    Widgets.Label(new Rect(4f, y, buildContent.width - 8f, 28f), trait.LabelCap);
                    Widgets.Label(new Rect(4f, y + 28f, buildContent.width - 8f, 24f),
                        ("SCP_WeaponWorkshop" + GroupFor(trait)).Translate());
                    Rect description = new Rect(4f, y + 52f, buildContent.width - 8f, 44f);
                    Widgets.Label(description, trait.description);
                    TooltipHandler.TipRegion(description, trait.description);
                }
                Widgets.EndScrollView();
            }

            float rightX = 230f;
            float rightWidth = rect.width - rightX;
            Widgets.Label(new Rect(rightX, 95f, rightWidth, 28f), weapon == null
                ? "SCP_WeaponWorkshopNoWeapon".Translate().ToString()
                : weapon.LabelCap);
            if (weapon != null)
            {
                CompUniqueWeapon comp = weapon.TryGetComp<CompUniqueWeapon>();
                Widgets.Label(new Rect(rightX, 122f, rightWidth - 180f, 35f),
                    "SCP_WeaponWorkshopInstalled".Translate() + ": "
                    + (comp == null ? 0 : comp.TraitsListForReading.Count) + "/5");
                Bill_WeaponTrait queued = bench.BillStack.Bills.OfType<Bill_WeaponTrait>()
                    .FirstOrDefault(b => b.TargetWeapon == weapon);
                if (queued != null && Widgets.ButtonText(
                    new Rect(rightX + rightWidth - 175f, 120f, 175f, 30f),
                    "SCP_WeaponWorkshopCancel".Translate()))
                    bench.BillStack.Delete(queued);
            }

            for (int i = 0; i < Groups.Length; i++)
            {
                string group = Groups[i];
                int available = TraitsFor(group, weapon).Count;
                Rect tab = new Rect(rightX + (i % 4) * (rightWidth / 4f), 162f + (i / 4) * 36f,
                    rightWidth / 4f - 4f, 32f);
                if (group == selectedGroup) Widgets.DrawHighlight(tab);
                if (Widgets.ButtonText(tab, ("SCP_WeaponWorkshop" + group).Translate() + " (" + available + ")"))
                { selectedGroup = group; traitScroll = Vector2.zero; }
            }

            List<WeaponTraitDef> traits = TraitsFor(selectedGroup, weapon);
            float listWidth = rightWidth;
            Rect listView = new Rect(rightX, 245f, listWidth, rect.height - 313f);
            Rect listContent = new Rect(0f, 0f, listView.width - 18f, Mathf.Max(listView.height, traits.Count * 112f));
            Widgets.BeginScrollView(listView, ref traitScroll, listContent);
            for (int i = 0; i < traits.Count; i++)
            {
                WeaponTraitDef trait = traits[i];
                float y = i * 112f;
                Widgets.DrawHighlightIfMouseover(new Rect(0f, y, listContent.width, 108f));
                Widgets.Label(new Rect(5f, y + 2f, listContent.width - 165f, 24f), trait.LabelCap);
                Rect descriptionRect = new Rect(5f, y + 28f, listContent.width - 165f, 78f);
                Widgets.Label(descriptionRect, trait.description);
                TooltipHandler.TipRegion(descriptionRect, trait.description);
                if (weapon != null)
                {
                    bool usable = CanQueue(weapon, trait);
                    bool enabled = GUI.enabled;
                    GUI.enabled = enabled && usable;
                    string reason = usable ? null : Reason(weapon, trait);
                    string buttonLabel = usable ? "SCP_WeaponWorkshopInstall".Translate().ToString()
                        : reason != null && reason.StartsWith("SCP_WeaponWorkshopResearch".Translate().ToString())
                            ? "SCP_WeaponWorkshopResearch".Translate().ToString() : reason;
                    if (Widgets.ButtonText(new Rect(listContent.width - 155f, y + 7f, 150f, 30f), buttonLabel))
                        Queue(weapon, trait);
                    GUI.enabled = enabled;
                    if (!usable)
                        TooltipHandler.TipRegion(new Rect(listContent.width - 155f, y + 7f, 150f, 30f),
                            reason);
                }
                RecipeDef recipe = RecipeFor(trait);
                string requirements = Requirements(recipe);
                Rect requirementsRect = new Rect(listContent.width - 155f, y + 42f, 150f, 66f);
                Widgets.Label(requirementsRect, requirements);
                TooltipHandler.TipRegion(requirementsRect, requirements);
            }
            Widgets.EndScrollView();
            if (traits.Count == 0)
                Widgets.Label(new Rect(rightX, 260f, listWidth, 48f), "SCP_WeaponWorkshopNoCategory".Translate());

        }

        internal static WorkTypeDef WorkTypeFor(RecipeDef recipe)
        {
            return recipe != null && recipe.workSkill == SkillDefOf.Artistic
                ? DefDatabase<WorkTypeDef>.GetNamedSilentFail("Art") : WorkTypeDefOf.Crafting;
        }

        private static string Requirements(RecipeDef recipe)
        {
            if (recipe == null) return "";
            string materials = string.Join(" + ", recipe.ingredients.Skip(1).Select(ingredient =>
            {
                ThingDef def = ingredient.filter.AllowedThingDefs.FirstOrDefault();
                return ingredient.GetBaseCount().ToString("0.##") + " × " + (def == null ? "?" : def.label);
            }).ToArray());
            SkillRequirement skill = recipe.skillRequirements.FirstOrDefault();
            string research = recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished
                ? "\n" + "SCP_WeaponWorkshopResearch".Translate().ToString() + ": " + recipe.researchPrerequisite.LabelCap.ToString()
                : "";
            return "SCP_WeaponWorkshopCost".Translate() + ": " + materials + "\n"
                + (skill == null ? "" : skill.skill.LabelCap + " " + skill.minLevel) + research;
        }

        private bool CanQueue(Thing weapon, WeaponTraitDef trait)
        {
            RecipeDef recipe = RecipeFor(trait);
            bool accessible = weapon != null && (weapon.Spawned && weapon.Map == bench.Map
                || bench.Map.mapPawns.FreeColonistsSpawned.Any(p => p.equipment != null
                    && p.equipment.Primary == weapon && !p.Downed
                    && p.Position.InHorDistOf(bench.Position, 3f)));
            return recipe != null && recipe.AvailableNow && bench.CurrentlyUsableForBills()
                && accessible
                && RecipeWorker_InstallWeaponTrait.CanInstall(weapon, trait)
                && !bench.BillStack.Bills.OfType<Bill_WeaponTrait>().Any(b => b.TargetWeapon == weapon);
        }

        private string Reason(Thing weapon, WeaponTraitDef trait)
        {
            if (!RecipeWorker_InstallWeaponTrait.CanInstall(weapon, trait)) return "SCP_WeaponWorkshopIncompatible".Translate();
            if (bench.BillStack.Bills.OfType<Bill_WeaponTrait>().Any(b => b.TargetWeapon == weapon))
                return "SCP_WeaponWorkshopQueued".Translate();
            RecipeDef recipe = RecipeFor(trait);
            if (recipe == null) return "SCP_WeaponWorkshopUnavailable".Translate();
            if (!recipe.AvailableNow)
                return "SCP_WeaponWorkshopResearch".Translate() + ": "
                    + (recipe.researchPrerequisite == null ? "?" : recipe.researchPrerequisite.LabelCap.ToString());
            if (!bench.CurrentlyUsableForBills()) return "SCP_WeaponWorkshopBench".Translate();
            return "SCP_WeaponWorkshopUnavailable".Translate();
        }

        private void Queue(Thing weapon, WeaponTraitDef trait)
        {
            if (!CanQueue(weapon, trait)) return;
            Bill_WeaponTrait bill = new Bill_WeaponTrait(RecipeFor(trait), weapon);
            bench.BillStack.AddBill(bill);
            bench.BillStack.Reorder(bill, 0);
            Close();
        }
    }
}
