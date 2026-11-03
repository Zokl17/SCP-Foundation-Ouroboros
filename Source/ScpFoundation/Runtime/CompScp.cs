using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ScpFoundation.Anomalies;
using ScpFoundation.Containment;
using ScpFoundation.Definitions;
using ScpFoundation.Knowledge;
using ScpFoundation.Interactions;
using ScpFoundation.Storytelling;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class CompScp : ThingComp
    {
        private ContainmentState state = new ContainmentState();
        private readonly List<string> failedRequirements = new List<string>();
        private int lastEvaluationTick;
        private int lastWarningLetterTick = -ContainmentWarningCooldown.Ticks;
        private int pending650WarningTick;
        private bool hasSample;
        private bool debugViolation;
        private bool briefingSent;
        private KnowledgeLevel briefedKnowledge;
        private bool captured;
        private int captureGraceUntilTick;
        private bool discoveryAnnounced;
        private bool informationUnlocked;
        private bool requirementsViolated;

        public ScpDef Definition { get { return ((CompProperties_Scp)props).anomaly; } }
        public bool IsContained { get { return state.status == ContainmentStatus.Contained; } }
        public bool IsInCustody { get { return captured || (!((CompProperties_Scp)props).requireCapture && parent.Faction == Faction.OfPlayer); } }
        public bool IsBreached { get { return state.status == ContainmentStatus.Breached; } }
        public bool IsCaptureGraceActive { get { return !IsContained && Find.TickManager.TicksGame < captureGraceUntilTick; } }
        public string StatusLabel { get { return (IsCaptureGraceActive && !IsContained ? "SCP_Status_Preparing" : "SCP_Status_" + state.status).Translate(); } }
        private bool ReadyAtPost { get { return parent.GetComp<CompScpInteraction>() == null
            || parent.GetComp<CompScpInteraction>().CanWorkAtPost
            || CompScp093Mirror.IsDiscAtActiveMirror(parent); } }
        public ScpOperationalFlags OperationalFlags
        {
            get
            {
                bool atPost = parent.Spawned && (CompScpPost.IsObjectAtPost(parent)
                    || CompScp093Mirror.IsDiscAtActiveMirror(parent));
                bool warning = requirementsViolated || state.status != ContainmentStatus.Contained;
                return ScpOperationalPolicy.State(atPost, warning);
            }
        }
        public bool CanResearchNow
        {
            get
            {
                if (!parent.Spawned || !IsInCustody || !ScpOperationalPolicy.IsAtPost(OperationalFlags)) return false;
                EvaluateRequirements();
                return ScpOperationalPolicy.AllowsResearch(OperationalFlags);
            }
        }
        public string OperationalLabel
        {
            get
            {
                ScpOperationalFlags flags = OperationalFlags;
                if ((flags & ScpOperationalFlags.EscapedRed) != 0) return "SCP_OperationalRed".Translate();
                if ((flags & ScpOperationalFlags.WarningYellow) != 0) return "SCP_OperationalYellow".Translate();
                return "SCP_OperationalBlack".Translate();
            }
        }
        private ScpKnowledgeComponent Knowledge { get { return Current.Game.GetComponent<ScpKnowledgeComponent>(); } }
        public int Clearance { get { return DiscoveryPolicy.Clearance(informationUnlocked || (IsInCustody && ReadyAtPost && IsContained), (int)Knowledge.LevelFor(Definition)); } }
        public string PublicLabel { get { return Clearance == 0 ? "SCP_UnknownObject".Translate().ToString()
            : Definition.scpId == "SCP-650" ? "SCP_650DisplayName".Translate().ToString() : parent.def.LabelCap.ToString(); } }
        public string ClearanceHint { get { return (parent.Map != null && !parent.Map.IsPlayerHome
            ? "SCP_ClearanceZeroAwayHint" : "SCP_ClearanceZeroHint").Translate(); } }

        public void NotifyDiscovered(bool showLetter = true)
        {
            if (Definition == null || !DiscoveryPolicy.ShouldAnnounce(discoveryAnnounced, parent.Spawned,
                parent.Spawned && !parent.Position.Fogged(parent.Map), Definition.source.originalFixture)) return;
            discoveryAnnounced = true;
            Knowledge.RecordEncounter(Definition);
            if (showLetter)
                Find.LetterStack.ReceiveLetter("SCP_ObjectDiscoveredTitle".Translate(), "SCP_ObjectDiscoveredBody".Translate(), LetterDefOf.NeutralEvent, parent);
        }

        private string RestrictedInformation()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("SCP_UnknownObject".Translate());
            text.AppendLine("SCP_Clearance".Translate(0));
            text.AppendLine(ClearanceHint);
            text.AppendLine();
            text.AppendLine("SCP_RequirementsButton".Translate());
            ContainmentContext context = parent.Spawned && ReadyAtPost ? new ContainmentContext(parent) : null;
            foreach (ContainmentRequirement requirement in Definition.requirements)
                text.AppendLine(context == null ? "• " + requirement.Description()
                    : "SCP_RequirementLine".Translate((requirement.IsSatisfied(context) ? "SCP_CheckMet" : "SCP_CheckUnmet").Translate(), requirement.Description()).ToString());
            return text.ToString();
        }

        // Capture jobs can call this even when the physical Thing has no faction.
        public void NotifyCaptured()
        {
            captured = true;
            briefingSent = false;
            BeginCaptureGrace();
            Knowledge.RecordEncounter(Definition);
            ScpEventJournal.Add(Definition.scpId, parent, "SCP_JournalCaptured".Translate());
        }

        private void BeginCaptureGrace()
        {
            captureGraceUntilTick = CaptureGracePeriod.Deadline(Find.TickManager.TicksGame, Definition.objectClass);
            state.Recover();
            state.status = ContainmentStatus.Grace;
        }

        public bool TryRecover()
        {
            if (!parent.Spawned || !ReadyAtPost || (((CompProperties_Scp)props).requireCapture && !IsInCustody)
                || EvaluateRequirements()) return false;
            bool wasContained = IsContained;
            state.Recover();
            informationUnlocked = true;
            if (!wasContained) ScpEventJournal.Add(Definition.scpId, parent, "SCP_JournalRecovered".Translate());
            return true;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            lastEvaluationTick = Find.TickManager.TicksGame;
            hasSample = false;
            parent.Map.GetComponent<ScpStoryStateComponent>().Register(this);
            if (!respawningAfterLoad && !((CompProperties_Scp)props).requireCapture && !captured && parent.Faction == Faction.OfPlayer)
            {
                captured = true;
                BeginCaptureGrace();
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            map.GetComponent<ScpStoryStateComponent>().Unregister(this);
            base.PostDeSpawn(map, mode);
        }

        public override void CompTick()
        {
            if (parent.def.tickerType == TickerType.Normal
                && Find.TickManager.TicksGame % GenTicks.TickRareInterval == 0)
                CompTickRare();
        }

        public override void CompTickRare()
        {
            if (!parent.Spawned || Definition == null) return;
            NotifyDiscovered();
            if (((CompProperties_Scp)props).requireCapture && !IsInCustody) return;
            int now = Find.TickManager.TicksGame;
            int delta = Math.Min(GenTicks.TickRareInterval, Math.Max(0, now - lastEvaluationTick));
            lastEvaluationTick = now;
            bool violated = EvaluateRequirements();
            if (!violated && ReadyAtPost && IsInCustody && !IsContained) TryRecover();
            KnowledgeLevel currentKnowledge = Knowledge.LevelFor(Definition);
            if ((captured || parent.Faction == Faction.OfPlayer) && ReadyAtPost && parent.Map.IsPlayerHome
                && BriefingPolicy.ShouldNotify(briefingSent, (int)briefedKnowledge, (int)currentKnowledge,
                    Definition.requirements.Select(requirement => (int)requirement.visibleFrom)))
            {
                Find.LetterStack.ReceiveLetter("SCP_BriefingTitle".Translate(), BuildRequirementsReport(), LetterDefOf.NeutralEvent, parent);
                briefingSent = true;
                if (currentKnowledge > briefedKnowledge) briefedKnowledge = currentKnowledge;
            }
            if (IsCaptureGraceActive) return;
            delta = CaptureGracePeriod.ExposedTicks(now, delta, captureGraceUntilTick);
            if (!violated) pending650WarningTick = 0;
            if (state.BeginWarning(violated))
            {
                if (Definition.scpId == "SCP-650") pending650WarningTick = now + 1250;
                else SendContainmentWarning(now);
                // A new violation still receives its full grace period when its letter is muted.
                delta = 0;
            }
            if (pending650WarningTick > 0 && now >= pending650WarningTick && violated
                && state.status != ContainmentStatus.Breached)
            {
                pending650WarningTick = 0;
                SendContainmentWarning(now);
            }
            if (state.Advance(Definition.breach, violated, delta, () => Rand.Value)) ApplyBreachEffects();
            if (IsInCustody && ReadyAtPost && IsContained) informationUnlocked = true;
        }

        private void SendContainmentWarning(int now)
        {
            string warning = "SCP_ContainmentWarningHint".Translate()
                + "\n\n" + string.Join("\n", failedRequirements.Select(failure => "• " + failure).ToArray());
            ScpEventJournal.Add(Definition.scpId, parent, warning);
            if (!ContainmentWarningCooldown.CanSend(now, lastWarningLetterTick)) return;
            Find.LetterStack.ReceiveLetter(Definition.scpId + ": "
                + "SCP_ContainmentWarningTitle".Translate(), warning, LetterDefOf.NegativeEvent, parent);
            lastWarningLetterTick = now;
        }

        private bool EvaluateRequirements()
        {
            failedRequirements.Clear();
            ContainmentContext context = new ContainmentContext(parent);
            bool violated = false;
            bool hiddenFailure = false;
            KnowledgeLevel knowledge = Knowledge.LevelFor(Definition);
            foreach (ContainmentRequirement requirement in Definition.requirements)
            {
                if (requirement.IsSatisfied(context)) continue;
                violated = true;
                if (knowledge >= requirement.visibleFrom) failedRequirements.Add(FailureDescription(requirement, context));
                else hiddenFailure = true;
            }
            if (hiddenFailure) failedRequirements.Add("SCP_HiddenRequirements".Translate());
            if (debugViolation) failedRequirements.Add("SCP_DebugViolationActive".Translate());
            hasSample = true;
            requirementsViolated = violated || debugViolation;
            return requirementsViolated;
        }

        private string FailureDescription(ContainmentRequirement requirement, ContainmentContext context)
        {
            RequirementLight light = requirement as RequirementLight;
            if (light != null)
                return "SCP_AlarmLight".Translate(context.thing.Map.glowGrid.GroundGlowAt(context.thing.Position).ToString("P0"),
                    light.min.ToString("P0"), light.max.ToString("P0"));
            RequirementTemperature temperature = requirement as RequirementTemperature;
            if (temperature != null)
                return "SCP_AlarmTemperature".Translate(context.thing.AmbientTemperature.ToString("F1"), temperature.min, temperature.max);
            RequirementPower power = requirement as RequirementPower;
            if (power != null) return "SCP_AlarmPower".Translate();
            RequirementObservers observers = requirement as RequirementObservers;
            if (observers != null)
                return "SCP_AlarmObservers".Translate(ScpObservation.PairedCount(parent), observers.count);
            RequirementRoom room = requirement as RequirementRoom;
            if (room != null) return "SCP_AlarmRoom".Translate(room.minCells, (room.roofed ? "SCP_Yes" : "SCP_No").Translate());
            RequirementDoor door = requirement as RequirementDoor;
            if (door != null) return "SCP_AlarmDoor".Translate();
            return requirement.Description();
        }

        private void ApplyBreachEffects()
        {
            ScpEventJournal.Add(Definition.scpId, parent, "SCP_JournalBreached".Translate());
            foreach (BreachEffect effect in Definition.breachEffects) effect.Apply(this);
        }

        public string BuildRequirementsReport()
        {
            if (!Definition.source.originalFixture && Clearance == 0) return RestrictedInformation();
            StringBuilder text = new StringBuilder();
            text.AppendLine("SCP_BriefingIntro".Translate(PublicLabel));
            if (!ReadyAtPost) text.AppendLine("SCP_MoveToPostFirst".Translate());
            AppendCaptureGrace(text);
            if (((CompProperties_Scp)props).requireCapture && !IsInCustody) text.AppendLine("SCP_NotCaptured".Translate());
            text.AppendLine();
            ContainmentContext context = parent.Spawned ? new ContainmentContext(parent) : null;
            KnowledgeLevel knowledge = Knowledge.LevelFor(Definition);
            bool hidden = false;
            foreach (ContainmentRequirement requirement in Definition.requirements)
            {
                if (knowledge < requirement.visibleFrom) { hidden = true; continue; }
                string status = context == null ? "SCP_CheckUnavailable" : requirement.IsSatisfied(context) ? "SCP_CheckMet" : "SCP_CheckUnmet";
                text.AppendLine("SCP_RequirementLine".Translate(status.Translate(), requirement.Description()));
                RequirementObservers observation = requirement as RequirementObservers;
                if (context != null && observation != null) text.AppendLine(ScpObservation.Report(parent, observation.count));
            }
            if (hidden) text.AppendLine("SCP_HiddenRequirements".Translate());
            if (debugViolation) text.AppendLine("SCP_DebugViolationActive".Translate());
            text.AppendLine();
            text.AppendLine("SCP_ContainmentWarningHint".Translate());
            text.AppendLine("SCP_ReportRefresh".Translate());
            return text.ToString();
        }

        public void ShowRequirements()
        {
            Find.WindowStack.Add(new Dialog_MessageBox(BuildRequirementsReport(), title: "SCP_BriefingTitle".Translate()));
        }

        private void AppendCaptureGrace(StringBuilder text)
        {
            if (IsCaptureGraceActive)
                text.AppendLine("SCP_CaptureGraceActive".Translate(GenDate.ToStringTicksToPeriod(captureGraceUntilTick - Find.TickManager.TicksGame, allowSeconds: false)));
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref state.status, "scpStatus", ContainmentStatus.Unassessed);
            Scribe_Values.Look(ref state.violationTicks, "scpViolationTicks", 0);
            Scribe_Values.Look(ref state.risk, "scpRisk", 0f);
            Scribe_Values.Look(ref state.breachCount, "scpBreachCount", 0);
            Scribe_Values.Look(ref state.warningSent, "scpContainmentWarningSent", false);
            Scribe_Values.Look(ref lastWarningLetterTick, "scpLastContainmentWarningLetterTick", -ContainmentWarningCooldown.Ticks);
            Scribe_Values.Look(ref pending650WarningTick, "scp650PendingWarningTick", 0);
            Scribe_Values.Look(ref debugViolation, "scpDebugViolation", false);
            Scribe_Values.Look(ref briefingSent, "scpBriefingSent", false);
            Scribe_Values.Look(ref captured, "scpCaptured", false);
            Scribe_Values.Look(ref discoveryAnnounced, "scpDiscoveryAnnounced", false);
            Scribe_Values.Look(ref informationUnlocked, "scpInformationUnlocked", false);
            Scribe_Values.Look(ref requirementsViolated, "scpRequirementsViolated", false);
            Scribe_Values.Look(ref captureGraceUntilTick, "scpCaptureGraceUntilTick", 0);
            Scribe_Values.Look(ref briefedKnowledge, "scpBriefedKnowledge", KnowledgeLevel.Unknown);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                state.violationTicks = Math.Max(0, state.violationTicks);
                state.risk = float.IsNaN(state.risk) ? 0f : Math.Max(0f, Math.Min(1f, state.risk));
                state.breachCount = Math.Max(0, state.breachCount);
                if (!Enum.IsDefined(typeof(ContainmentStatus), state.status)) state.status = ContainmentStatus.Unassessed;
            }
        }

        public override string CompInspectStringExtra()
        {
            if (Definition == null) return "SCP_MissingDefinition".Translate();
            if (!Definition.source.originalFixture && Clearance == 0) return "SCP_Clearance".Translate(0) + "\n" + ClearanceHint;
            if (!ReadyAtPost) return "SCP_MoveToPostFirst".Translate();
            if (((CompProperties_Scp)props).requireCapture && !IsInCustody) return "SCP_NotCaptured".Translate();
            KnowledgeLevel level = Knowledge.LevelFor(Definition);
            StringBuilder text = new StringBuilder();
            AppendCaptureGrace(text);
            if (!IsCaptureGraceActive) text.AppendLine("SCP_ContainmentStatus".Translate(StatusLabel));
            text.AppendLine("SCP_KnowledgeStatus".Translate(("SCP_Knowledge_" + level).Translate()));
            if (level >= KnowledgeLevel.Documented && Definition.research != null)
                text.AppendLine(ScpArticleCatalog.ExperimentProgressText(Definition, Knowledge));
            if (level >= KnowledgeLevel.Observed) text.AppendLine("SCP_Identification".Translate(Definition.scpId));
            if (level >= KnowledgeLevel.PartiallyDocumented)
            {
                text.AppendLine("SCP_Risk".Translate(state.risk.ToString("P0"), state.violationTicks));
                text.AppendLine("SCP_BreachCount".Translate(state.breachCount));
                if (!hasSample) text.AppendLine("SCP_AwaitingCheck".Translate());
                else if (failedRequirements.Count == 0) text.AppendLine("SCP_RequirementsMet".Translate());
                else foreach (string failure in failedRequirements) text.AppendLine("SCP_FailedRequirement".Translate(failure));
            }
            else text.AppendLine("SCP_RequirementsUnknown".Translate());
            if (debugViolation) text.AppendLine("SCP_DebugViolationActive".Translate());
            return text.ToString().TrimEnd();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Definition == null) yield break;
            yield return new Command_Action { defaultLabel = "SCP_RequirementsButton".Translate(), defaultDesc = "SCP_RequirementsButtonDesc".Translate(),
                icon = ScpIcons.Get(2), action = ShowRequirements };
        }

    }
}
