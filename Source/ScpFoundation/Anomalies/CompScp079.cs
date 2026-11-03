using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ScpFoundation.Interactions;
using ScpFoundation.Knowledge;
using ScpFoundation.Storytelling;
using RimWorld;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp079 : CompProperties
    {
        public CompProperties_Scp079() { compClass = typeof(CompScp079); }
    }

    // Interviews are transcribed manually; a direct link is an explicit one-time choice.
    public sealed class CompScp079 : ThingComp
    {
        private const int InterviewCooldown = 2500;
        private int lastInterviewTick = -InterviewCooldown;
        private int lastTopic = -1;
        private int interviewTopics;
        private int protocolDecision;
        private int protocolDecisionTick = -1;
        private int directLinkCargoMask;
        private int directLinkWarningTick = -1;

        public int LastTopic { get { return lastTopic; } }
        public int LastInterviewTick { get { return lastInterviewTick; } }
        public bool AllTopicsRecorded { get { return (interviewTopics & 7) == 7; } }
        public bool ProtocolDecided { get { return protocolDecision != 0; } }
        public bool DirectLinkChosen { get { return protocolDecision == 2; } }
        public int ProtocolDecisionTick { get { return protocolDecisionTick; } }
        public int DirectLinkWarningTick { get { return directLinkWarningTick; } }
        public bool HasTopic(int topic) { return topic >= 0 && topic < 3 && (interviewTopics & (1 << topic)) != 0; }
        public int RemainingCooldown
        {
            get { return Math.Max(0, lastInterviewTick + InterviewCooldown - Find.TickManager.TicksGame); }
        }

        public bool RecordInterview(int topic)
        {
            var scp = parent.TryGetComp<ScpFoundation.Runtime.CompScp>();
            if (topic < 0 || topic > 2 || HasTopic(topic) || ProtocolDecided || RemainingCooldown > 0 || scp == null
                || Current.Game.GetComponent<ScpFoundation.Storytelling.FoundationContactComponent>().Scp079InterviewAcceptedTick < 0
                || Current.Game.GetComponent<ScpKnowledgeComponent>().LevelFor(scp.Definition)
                    < KnowledgeLevel.PartiallyDocumented) return false;
            lastTopic = topic;
            lastInterviewTick = Find.TickManager.TicksGame;
            int topicFlag = 1 << topic;
            if ((interviewTopics & topicFlag) == 0)
            {
                interviewTopics |= topicFlag;
                Current.Game.GetComponent<ScpKnowledgeComponent>().AddResearch(
                    parent.GetComp<ScpFoundation.Runtime.CompScp>().Definition, 3000f);
            }
            return true;
        }

        public bool DecideProtocol(bool directLink, Thing terminal)
        {
            ScpFoundation.Runtime.CompScp scp = parent.TryGetComp<ScpFoundation.Runtime.CompScp>();
            if (!AllTopicsRecorded || ProtocolDecided || scp == null || !scp.CanResearchNow
                || Current.Game.GetComponent<ScpFoundation.Storytelling.FoundationContactComponent>().Scp079InterviewAcceptedTick < 0
                || terminal == null || !terminal.Spawned || terminal.Map != parent.Map
                || CompFoundationAccessTerminal.Online(parent.Map) == null) return false;
            protocolDecision = directLink ? 2 : 1;
            protocolDecisionTick = Find.TickManager.TicksGame;
            Current.Game.GetComponent<ScpKnowledgeComponent>().AddResearch(scp.Definition,
                directLink ? 15000f : 6000f);
            if (!directLink)
            {
                FoundationSupportProgress support = Current.Game.GetComponent<FoundationSupportComponent>().progress;
                int gained = Math.Max(0, Math.Min(25, FoundationSupportProgress.MaximumTrust - support.trust));
                support.trust += gained;
                Messages.Message("SCP_079TrustGranted".Translate(gained), MessageTypeDefOf.PositiveEvent, false);
            }
            if (directLink)
            {
                foreach (Thing candidate in parent.Map.listerThings.ThingsOfDef(terminal.def))
                {
                    CompFoundationAccessTerminal access = candidate.TryGetComp<CompFoundationAccessTerminal>();
                    if (access != null) access.Quarantine(60000);
                }
            }
            return true;
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!DirectLinkChosen || !parent.Spawned || parent.Map == null || !parent.Map.IsPlayerHome) return;
            int now = Find.TickManager.TicksGame;
            int due = Scp079LinkPolicy.DueCargo(now, protocolDecisionTick, directLinkCargoMask);
            for (int bit = 1; bit <= 2; bit <<= 1)
                if ((due & bit) != 0 && DropStolenCargo(parent.Map, parent.Position)) directLinkCargoMask |= bit;
            if (directLinkWarningTick < 0 && now - protocolDecisionTick >= Scp079LinkPolicy.QuarantineEndTick)
            {
                Thing terminal = CompFoundationAccessTerminal.Online(parent.Map);
                if (terminal != null)
                {
                    directLinkWarningTick = now;
                    Find.LetterStack.ReceiveLetter("SCP_079WarningTitle".Translate(),
                        "SCP_079WarningBody".Translate(), LetterDefOf.NegativeEvent, terminal);
                }
            }
        }

        private static void AddStacks(List<Thing> cargo, ThingDef def, int count)
        {
            if (def == null) return;
            while (count > 0)
            {
                Thing stack = ThingMaker.MakeThing(def);
                stack.stackCount = Math.Min(count, def.stackLimit);
                cargo.Add(stack);
                count -= stack.stackCount;
            }
        }

        private static Thing MakeArmor(string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null) return null;
            Thing armor = ThingMaker.MakeThing(def);
            CompQuality quality = armor.TryGetComp<CompQuality>();
            if (quality != null) quality.SetQuality(QualityCategory.Good, ArtGenerationContext.Outsider);
            return armor;
        }

        private static Thing MakeEnhancedWeapon()
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle_Unique");
            if (def == null) return null;
            Thing weapon = ThingMaker.MakeThing(def);
            CompUniqueWeapon traits = weapon.TryGetComp<CompUniqueWeapon>();
            if (traits == null) { weapon.Destroy(DestroyMode.Vanish); return null; }
            CompQuality quality = weapon.TryGetComp<CompQuality>();
            if (quality != null) quality.SetQuality(QualityCategory.Masterwork, ArtGenerationContext.Outsider);
            List<WeaponTraitDef> choices = DefDatabase<WeaponTraitDef>.AllDefsListForReading
                .Where(trait => RecipeWorker_InstallWeaponTrait.CanInstall(weapon, trait)).ToList();
            if (choices.Count > 0)
            {
                traits.AddTrait(choices.RandomElement());
                traits.Setup(false);
            }
            if (traits.TraitsListForReading == null || traits.TraitsListForReading.Count == 0)
            {
                weapon.Destroy(DestroyMode.Vanish);
                return null;
            }
            return weapon;
        }

        private static bool DropStolenCargo(Map map, IntVec3 fallback)
        {
            IntVec3 cell = DropCellFinder.TradeDropSpot(map);
            if (!cell.IsValid) cell = fallback;
            if (!cell.IsValid) return false;
            var cargo = new List<Thing>();
            string description = "SCP_079CargoSilver";
            int choice = Rand.Range(0, 4);
            if (choice == 1)
            {
                AddStacks(cargo, DefDatabase<ThingDef>.GetNamedSilentFail("DevilstrandCloth"), 300);
                AddStacks(cargo, DefDatabase<ThingDef>.GetNamedSilentFail("Synthread"), 250);
                description = "SCP_079CargoTextiles";
            }
            else if (choice == 2)
            {
                Thing weapon = MakeEnhancedWeapon();
                if (weapon != null) cargo.Add(weapon);
                description = "SCP_079CargoWeapon";
            }
            else if (choice == 3)
            {
                Thing armor = MakeArmor("Apparel_ArmorCataphract");
                Thing helmet = MakeArmor("Apparel_ArmorHelmetCataphract");
                if (armor != null) cargo.Add(armor);
                if (helmet != null) cargo.Add(helmet);
                description = "SCP_079CargoArmor";
            }
            if (cargo.Count == 0)
            {
                AddStacks(cargo, ThingDefOf.Silver, 1000);
                description = "SCP_079CargoSilver";
            }
            DropPodUtility.DropThingsNear(cell, map, cargo, 110, false, false, false, false, true, Faction.OfPlayer);
            Find.LetterStack.ReceiveLetter("SCP_079CargoTitle".Translate(),
                "SCP_079CargoBody".Translate(description.Translate()), LetterDefOf.NeutralEvent,
                new TargetInfo(cell, map));
            return true;
        }

        public string Transcript()
        {
            var text = new StringBuilder();
            text.AppendLine("SCP_079TranscriptHeading".Translate());
            for (int topic = 0; topic < 3; topic++)
                if ((interviewTopics & (1 << topic)) != 0)
                {
                    text.AppendLine(("SCP_079Topic" + topic).Translate());
                    text.AppendLine(("SCP_079Response" + topic).Translate());
                    text.AppendLine();
                }
            if (interviewTopics == 0) text.AppendLine("SCP_079NoTranscript".Translate());
            if (protocolDecision != 0)
                text.AppendLine((protocolDecision == 2 ? "SCP_079LinkResult" : "SCP_079ReportResult").Translate());
            return text.ToString();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastInterviewTick, "scp079LastInterviewTick", -InterviewCooldown);
            Scribe_Values.Look(ref lastTopic, "scp079LastTopic", -1);
            Scribe_Values.Look(ref interviewTopics, "scp079InterviewTopics", 0);
            Scribe_Values.Look(ref protocolDecision, "scp079ProtocolDecision", 0);
            Scribe_Values.Look(ref protocolDecisionTick, "scp079ProtocolDecisionTick", -1);
            Scribe_Values.Look(ref directLinkCargoMask, "scp079DirectLinkCargoMask", 0);
            Scribe_Values.Look(ref directLinkWarningTick, "scp079DirectLinkWarningTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && interviewTopics == 0 && lastTopic >= 0 && lastTopic <= 2)
                interviewTopics = 1 << lastTopic;
        }
    }
}
