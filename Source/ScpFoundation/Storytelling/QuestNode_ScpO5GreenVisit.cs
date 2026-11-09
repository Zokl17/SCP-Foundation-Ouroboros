using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using RimWorld.Planet;
using ScpFoundation.Anomalies;
using ScpFoundation.Runtime;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    public sealed class QuestNode_ScpO5GreenVisit : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
            return map != null && map.IsPlayerHome && support.FoundationFaction != null
                && !support.FoundationFaction.HostileTo(Faction.OfPlayer)
                && DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_O5Green") != null
                && Current.Game.GetComponent<ScpOuroborosArchiveComponent>().AnalysisTick >= 0;
        }

        protected override void RunInt()
        {
            Faction foundation = Current.Game.GetComponent<FoundationSupportComponent>().FoundationFaction;
            PawnKindDef greenKind = DefDatabase<PawnKindDef>.GetNamed("SCP_O5Green");
            PawnKindDef guardKind = DefDatabase<PawnKindDef>.GetNamed("SCP_MTF_354ExosuitGuard");
            Pawn green = QuestGen_Pawns.GeneratePawn(QuestGen.quest, greenKind, foundation);
            green.gender = Gender.Female;
            green.Name = new NameSingle("SCP_O5GreenName".Translate());
            var guards = new List<Pawn>();
            for (int i = 0; i < 6; i++)
                guards.Add(QuestGen_Pawns.GeneratePawn(QuestGen.quest, guardKind, foundation));
            var delegation = new List<Pawn> { green };
            delegation.AddRange(guards);
            QuestGen.slate.Set("scpO5GreenFaction", foundation);
            QuestGen.slate.Set("scpO5GreenPawns", delegation);
            QuestGen.slate.Set("scpO5GreenContents", delegation.Cast<Thing>().ToList());
            QuestGen.quest.AddPart(new QuestPart_RequirementsToAcceptScpO5Scientist {
                map = QuestGen.slate.Get<Map>("map")
            });
            QuestGen.quest.AddPart(new QuestPart_ScpO5GreenVisit {
                map = QuestGen.slate.Get<Map>("map"),
                visitor = green,
                guards = guards,
                inSignalEnable = QuestGen.quest.InitiateSignal,
                arrivalSignal = QuestGenUtility.HardcodedSignalWithQuestID("O5GreenVisitArrive"),
                pickupSignal = QuestGenUtility.HardcodedSignalWithQuestID("O5GreenVisitPickup"),
                completedSignal = QuestGenUtility.HardcodedSignalWithQuestID("O5GreenVisitCompleted"),
                failedSignal = QuestGenUtility.HardcodedSignalWithQuestID("O5GreenVisitFailed")
            });
        }
    }

    public sealed class QuestPart_RequirementsToAcceptScpO5Scientist : QuestPart_RequirementsToAccept
    {
        public Map map;

        public override AcceptanceReport CanAccept()
        {
            if (map != null && map.mapPawns != null && Current.Game != null)
            {
                FoundationPersonnelComponent personnel = Current.Game.GetComponent<FoundationPersonnelComponent>();
                if (map.mapPawns.FreeColonistsSpawned.Any(pawn => pawn != null && !pawn.Dead
                    && pawn.skills != null && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Research)
                    && personnel.LevelFor(pawn) >= 4
                    && pawn.skills.GetSkill(SkillDefOf.Intellectual).Level >= 12)) return true;
            }
            return "SCP_O5GreenNeedScientist".Translate();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "scpO5GreenRequirementMap");
        }
    }

    // The quest uses vanilla shuttles, room stats, pawn jobs and a visiting lord. No terminal reply
    // can complete the conversation: both people must actually meet in the prepared room.
    public sealed class QuestPart_ScpO5GreenVisit : QuestPartActivable
    {
        public Map map;
        public Pawn visitor;
        public List<Pawn> guards = new List<Pawn>();
        public string arrivalSignal;
        public string pickupSignal;
        public string completedSignal;
        public string failedSignal;
        private Thing meetingTable;
        private Pawn scientist;
        private IntVec3 visitorSpot = IntVec3.Invalid;
        private IntVec3 scientistSpot = IntVec3.Invalid;
        private ThingOverlaysHandle meetingMarkerOwner;
        private OverlayHandle? meetingMarker;
        private bool preparationLetterSent;
        private bool arrivalRequested;
        private bool arrived;
        private bool meetingStarted;
        private bool meetingAuthorized;
        private bool recorded;
        private bool finished;
        private bool escortSecured;
        private bool escortJobsCleared;
        private bool visitorDetached;
        private bool extraGuardsReleased;
        private bool departureFallbackStarted;
        private bool strandedDelegationReleased;
        private bool retaliationTriggered;
        private int meetingTicks;
        private int arrivalRequestTick = -1;
        private int departureTick = -1;
        public bool RetaliationTriggered { get { return retaliationTriggered; } }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "scpO5GreenVisitMap");
            Scribe_References.Look(ref meetingTable, "scpO5GreenVisitTable");
            Scribe_References.Look(ref scientist, "scpO5GreenVisitScientist");
            Scribe_Values.Look(ref visitorSpot, "scpO5GreenVisitorSpot", IntVec3.Invalid);
            Scribe_Values.Look(ref scientistSpot, "scpO5GreenScientistSpot", IntVec3.Invalid);
            Scribe_References.Look(ref visitor, "scpO5GreenVisitor");
            Scribe_Collections.Look(ref guards, "scpO5GreenGuards", LookMode.Reference);
            Scribe_Values.Look(ref arrivalSignal, "scpO5GreenArrivalSignal");
            Scribe_Values.Look(ref pickupSignal, "scpO5GreenPickupSignal");
            Scribe_Values.Look(ref completedSignal, "scpO5GreenCompletedSignal");
            Scribe_Values.Look(ref failedSignal, "scpO5GreenFailedSignal");
            Scribe_Values.Look(ref preparationLetterSent, "scpO5GreenPreparationLetterSent", false);
            Scribe_Values.Look(ref arrivalRequested, "scpO5GreenArrivalRequested", false);
            Scribe_Values.Look(ref arrived, "scpO5GreenArrived", false);
            Scribe_Values.Look(ref meetingStarted, "scpO5GreenMeetingStarted", false);
            Scribe_Values.Look(ref meetingAuthorized, "scpO5GreenMeetingAuthorized", false);
            Scribe_Values.Look(ref recorded, "scpO5GreenRecorded", false);
            Scribe_Values.Look(ref finished, "scpO5GreenFinished", false);
            Scribe_Values.Look(ref escortSecured, "scpO5GreenEscortSecured", false);
            Scribe_Values.Look(ref escortJobsCleared, "scpO5GreenEscortJobsCleared", false);
            Scribe_Values.Look(ref visitorDetached, "scpO5GreenVisitorDetached", false);
            Scribe_Values.Look(ref extraGuardsReleased, "scpO5GreenExtraGuardsReleased", false);
            Scribe_Values.Look(ref departureFallbackStarted, "scpO5GreenDepartureFallbackStarted", false);
            Scribe_Values.Look(ref strandedDelegationReleased, "scpO5GreenStrandedDelegationReleased", false);
            Scribe_Values.Look(ref retaliationTriggered, "scpO5GreenRetaliationTriggered", false);
            Scribe_Values.Look(ref meetingTicks, "scpO5GreenMeetingTicks", 0);
            Scribe_Values.Look(ref arrivalRequestTick, "scpO5GreenArrivalRequestTick", -1);
            Scribe_Values.Look(ref departureTick, "scpO5GreenDepartureTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (guards == null) guards = new List<Pawn>();
                if (meetingStarted || recorded) meetingAuthorized = true;
            }
        }

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            if (signal.tag == inSignalEnable)
                foreach (Pawn pawn in Delegation())
                    if (pawn != null && !pawn.Spawned && !Find.WorldPawns.Contains(pawn))
                        Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            base.Notify_QuestSignalReceived(signal);
        }

        public override void QuestPartTick()
        {
            int now = Find.TickManager.TicksGame;
            if (State != QuestPartState.Enabled || finished) return;
            if (arrived && !recorded && !visitorDetached && visitor != null
                && visitor.Spawned && visitor.Map == map) DetachVisitor();
            if (arrivalRequested && !recorded && (!arrived || !escortJobsCleared))
                HoldUnloadedDelegation();
            if (now % 250 != 0) return;
            if (!preparationLetterSent)
            {
                preparationLetterSent = true;
                Find.LetterStack.ReceiveLetter("SCP_O5GreenPrepTitle".Translate(),
                    "SCP_O5GreenPrepBody".Translate(), LetterDefOf.NeutralEvent);
            }
            if (map == null || !map.IsPlayerHome) { Fail(); return; }
            Faction foundation = Current.Game.GetComponent<FoundationSupportComponent>().FoundationFaction;
            if (arrived && visitor != null && (visitor.IsPrisonerOfColony || visitor.Dead
                || foundation != null && foundation.HostileTo(Faction.OfPlayer)))
            {
                TriggerRetaliation(foundation);
                Fail();
                return;
            }
            if (foundation == null || foundation.HostileTo(Faction.OfPlayer)) { Fail(); return; }
            if (arrived && !recorded && (!escortSecured || !escortJobsCleared)) SecureEscort(foundation);
            if (recorded)
            {
                if (!departureFallbackStarted) TryBoardShuttle();
                if (Delegation().All(pawn => pawn == null || !pawn.Spawned)) Finish();
                else if (!departureFallbackStarted && now - departureTick >= 20000)
                {
                    departureFallbackStarted = true;
                    SendDelegationHome(now);
                }
                if (now - departureTick >= 90000) Finish();
                return;
            }
            if (!arrivalRequested)
            {
                Thing table = FindMeetingTable(map);
                Pawn candidate = FindScientist(map, table);
                if (candidate != null && table != null)
                {
                    meetingTable = table;
                    scientist = candidate;
                    arrivalRequested = true;
                    arrivalRequestTick = now;
                    Find.SignalManager.SendSignal(new Signal(arrivalSignal));
                }
                return;
            }
            if (visitor == null || visitor.Dead || visitor.Destroyed || visitor.IsPrisonerOfColony) { Fail(); return; }
            if (!visitor.Spawned)
            {
                if (!arrived && now - arrivalRequestTick < 120000) return;
                Fail(); return;
            }
            if (visitor.Map != map)
            { Fail(); return; }
            if (!arrived)
            {
                if (Delegation().Any(pawn => !pawn.Spawned || pawn.Map != map))
                {
                    if (now - arrivalRequestTick >= 120000) Fail();
                    return;
                }
                if (meetingTable == null || !meetingTable.Spawned)
                    meetingTable = FindMeetingTable(map);
                if (meetingTable == null) return;
                BeginVisit(foundation);
            }
            if (!meetingAuthorized)
            {
                EnsureMeetingMarker();
                if (meetingTable != null && RoomReady(meetingTable))
                {
                    visitorSpot = MeetingSpot(visitor, meetingTable, visitorSpot, IntVec3.Invalid);
                    GuideToMeeting(visitor, visitorSpot);
                }
                return;
            }
            if (meetingTable == null || !meetingTable.Spawned || scientist == null
                || scientist.Dead || scientist.Destroyed || scientist.Map != map)
            {
                ReleaseMeetingLord();
                meetingAuthorized = false;
                meetingStarted = false;
                meetingTicks = 0;
                scientist = null;
                visitorSpot = IntVec3.Invalid;
                scientistSpot = IntVec3.Invalid;
                meetingTable = FindMeetingTable(map);
                EnsureMeetingMarker();
                return;
            }
            if (!RoomReady(meetingTable) || scientist.Downed || scientist.Drafted
                || scientist.skills == null || scientist.WorkTypeIsDisabled(WorkTypeDefOf.Research)
                || Current.Game.GetComponent<FoundationPersonnelComponent>().LevelFor(scientist) < 4
                || scientist.skills.GetSkill(SkillDefOf.Intellectual).Level < 12) return;
            Room room = meetingTable.Position.GetRoom(map);
            visitorSpot = MeetingSpot(visitor, meetingTable, visitorSpot, IntVec3.Invalid);
            scientistSpot = MeetingSpot(scientist, meetingTable, scientistSpot, visitorSpot);
            if (!visitorSpot.IsValid || !scientistSpot.IsValid) return;
            EnsureMeetingLord(foundation);
            bool together = visitor.Position.GetRoom(map) == room
                && scientist.Position.GetRoom(map) == room
                && visitor.Position.DistanceToSquared(scientist.Position) <= 16
                && visitor.Position.DistanceToSquared(meetingTable.Position) <= 25;
            if (!together) return;
            if (!meetingStarted)
            {
                meetingStarted = true;
                Messages.Message("SCP_O5GreenMeetingStarted".Translate(scientist.LabelShortCap),
                    MessageTypeDefOf.NeutralEvent, false);
            }
            meetingTicks += 250;
            if (meetingTicks % 2500 == 0 && scientist.interactions != null)
                scientist.interactions.TryInteractWith(visitor, InteractionDefOf.Chitchat);
            if (meetingTicks < 7500) return;
            recorded = true;
            departureTick = now;
            ReleaseMeetingLord();
            if (scientist != null && scientist.jobs != null) scientist.jobs.StopAll();
            Current.Game.GetComponent<ScpOuroborosArchiveComponent>().UnlockGreenVisit(now, scientist);
            Find.LetterStack.ReceiveLetter("SCP_O5GreenRecordTitle".Translate(),
                "SCP_O5GreenRecordBody".Translate(), LetterDefOf.PositiveEvent);
            SetPickupLanding();
            Find.SignalManager.SendSignal(new Signal(pickupSignal));
        }

        private IEnumerable<Pawn> Delegation()
        {
            if (visitor != null) yield return visitor;
            if (guards != null)
                foreach (Pawn guard in guards) if (guard != null) yield return guard;
        }

        public static QuestPart_ScpO5GreenVisit ActiveFor(Pawn pawn)
        {
            if (pawn == null || Find.QuestManager == null) return null;
            return Find.QuestManager.QuestsListForReading
                .SelectMany(current => current.PartsListForReading)
                .OfType<QuestPart_ScpO5GreenVisit>()
                .FirstOrDefault(part => part.visitor == pawn && part.State == QuestPartState.Enabled
                    && part.arrived && !part.finished && !part.recorded && !part.meetingAuthorized);
        }

        public bool CanBeginMeeting(Pawn candidate, out string reason)
        {
            reason = null;
            Thing table = RoomReady(meetingTable) ? meetingTable : map == null ? null : FindMeetingTable(map);
            if (candidate == null || candidate.Faction != Faction.OfPlayer || candidate.Map != map)
                reason = "SCP_O5GreenSelectColonist".Translate();
            else if (candidate.Dead || candidate.Downed || candidate.Drafted)
                reason = "SCP_O5GreenSelectedUnavailable".Translate();
            else if (candidate.skills == null || candidate.WorkTypeIsDisabled(WorkTypeDefOf.Research))
                reason = "SCP_O5GreenSelectedCannotResearch".Translate();
            else if (Current.Game.GetComponent<FoundationPersonnelComponent>().LevelFor(candidate) < 4)
                reason = "SCP_O5GreenSelectedClearance".Translate();
            else if (candidate.skills.GetSkill(SkillDefOf.Intellectual).Level < 12)
                reason = "SCP_O5GreenSelectedIntellectual".Translate();
            else if (table == null)
                reason = "SCP_O5GreenRoomNotReady".Translate();
            else if (visitor == null || !visitor.Spawned || visitor.Map != map
                || !candidate.CanReach(table, PathEndMode.Touch, Danger.Some)
                || !visitor.CanReach(table, PathEndMode.Touch, Danger.Some))
                reason = "SCP_O5GreenCannotReach".Translate();
            return reason == null;
        }

        public void BeginMeeting(Pawn candidate)
        {
            string reason;
            if (!CanBeginMeeting(candidate, out reason)) return;
            if (!RoomReady(meetingTable)) meetingTable = FindMeetingTable(map);
            scientist = candidate;
            scientistSpot = IntVec3.Invalid;
            meetingTicks = 0;
            meetingAuthorized = true;
            ClearMeetingMarker();
        }

        private void BeginVisit(Faction foundation)
        {
            arrived = true;
            if (visitor.apparel != null)
                foreach (Apparel garment in visitor.apparel.WornApparel)
                    garment.DrawColor = new Color(0.18f, 0.35f, 0.24f);
            visitor.Drawer.renderer.SetAllGraphicsDirty();
            SecureEscort(foundation);
            EnsureMeetingMarker();
            Find.LetterStack.ReceiveLetter("SCP_O5GreenArrivedTitle".Translate(),
                "SCP_O5GreenArrivedBody".Translate(), LetterDefOf.PositiveEvent, visitor);
        }

        private void HoldUnloadedDelegation()
        {
            foreach (Pawn pawn in Delegation())
            {
                if (pawn == null || !pawn.Spawned || pawn.Map != map) continue;
                if (pawn.mindState != null) pawn.mindState.exitMapAfterTick = int.MaxValue;
                Lord previous = LordUtility.GetLord(pawn);
                if (previous != null) previous.RemovePawn(pawn);
                if (pawn.jobs == null || pawn.CurJob != null && pawn.CurJob.def == JobDefOf.Wait) continue;
                Job wait = JobMaker.MakeJob(JobDefOf.Wait);
                wait.expiryInterval = 3000;
                pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
            }
        }

        private void DetachVisitor()
        {
            Lord previous = LordUtility.GetLord(visitor);
            if (previous != null) previous.RemovePawn(visitor);
            if (visitor.mindState != null) visitor.mindState.exitMapAfterTick = int.MaxValue;
            if (visitor.jobs != null)
            {
                visitor.jobs.StopAll();
                Job wait = JobMaker.MakeJob(JobDefOf.Wait);
                wait.expiryInterval = 12000;
                visitor.jobs.StartJob(wait, JobCondition.InterruptForced);
            }
            visitorDetached = true;
        }

        private void EnsureMeetingMarker()
        {
            if (meetingAuthorized || meetingMarker.HasValue || visitor == null || !visitor.Spawned
                || visitor.Map != map || map == null) return;
            meetingMarkerOwner = map.overlayDrawer.GetOverlaysHandle(visitor);
            meetingMarker = meetingMarkerOwner.Enable(OverlayTypes.QuestionMark);
        }

        private void ClearMeetingMarker()
        {
            if (meetingMarkerOwner != null && meetingMarker.HasValue
                && visitor != null && visitor.Spawned)
                meetingMarkerOwner.Disable(ref meetingMarker);
            meetingMarker = null;
            meetingMarkerOwner = null;
        }

        private void SecureEscort(Faction foundation)
        {
            if (visitor == null || !visitor.Spawned || visitor.Map != map || meetingTable == null) return;
            PawnKindDef guardKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_MTF_354ExosuitGuard");
            int presentGuards = guards.Count(guard => guard != null && guard.Spawned
                && !guard.Dead && guard.Map == map);
            if (guardKind != null)
                while (presentGuards < 6)
                {
                    Pawn guard = PawnGenerator.GeneratePawn(guardKind, foundation);
                    IntVec3 cell = CellFinder.RandomSpawnCellForPawnNear(visitor.Position, map, 6);
                    GenSpawn.Spawn(guard, cell, map);
                    guards.Add(guard);
                    presentGuards++;
                }
            foreach (Pawn pawn in Delegation())
            {
                if (pawn == null || !pawn.Spawned || pawn.Map != map) continue;
                Lord previous = LordUtility.GetLord(pawn);
                if (previous != null) previous.RemovePawn(pawn);
                if (pawn.mindState != null) pawn.mindState.exitMapAfterTick = int.MaxValue;
                if (pawn.jobs != null) pawn.jobs.StopAll();
            }
            LordMaker.MakeNewLord(foundation,
                new LordJob_DefendPoint(meetingTable.Position, 5f, 14f, false, false), map,
                guards.Where(pawn => pawn != null && pawn.Spawned && pawn.Map == map).ToList());
            if (visitor.jobs != null)
            {
                Job wait = JobMaker.MakeJob(JobDefOf.Wait);
                wait.expiryInterval = 12000;
                visitor.jobs.StartJob(wait, JobCondition.InterruptForced);
            }
            escortSecured = true;
            escortJobsCleared = true;
            visitorDetached = true;
        }

        private static bool IsTable(Thing thing)
        {
            string name = thing.def.defName;
            return name == "Table1x2c" || name == "Table2x2c"
                || name == "Table2x4c" || name == "Table3x3c";
        }

        private static bool IsSeat(Thing thing)
        {
            string name = thing.def.defName;
            return name == "Stool" || name == "DiningChair" || name == "Armchair";
        }

        private static bool RoomReady(Thing table)
        {
            if (table == null || !table.Spawned || !IsTable(table)) return false;
            Map map = table.Map;
            Room room = table.Position.GetRoom(map);
            if (room == null || room.TouchesMapEdge || room.OpenRoofCount > 0
                || room.Cells.Count() < 30 || room.GetStat(RoomStatDefOf.Impressiveness) < 60f
                || map.glowGrid.GroundGlowAt(table.Position) < 0.3f) return false;
            return map.listerBuildings.allBuildingsColonist.Count(building => building.Spawned
                && IsSeat(building) && building.Position.GetRoom(map) == room
                && building.Position.DistanceToSquared(table.Position) <= 36) >= 2;
        }

        private static Thing FindMeetingTable(Map map)
        {
            return map.listerBuildings.allBuildingsColonist.Cast<Thing>()
                .Where(RoomReady)
                .OrderByDescending(table => table.Position.GetRoom(map)
                    .GetStat(RoomStatDefOf.Impressiveness)).FirstOrDefault();
        }

        private static Pawn FindScientist(Map map, Thing table)
        {
            if (table == null) return null;
            FoundationPersonnelComponent personnel = Current.Game.GetComponent<FoundationPersonnelComponent>();
            return map.mapPawns.FreeColonistsSpawned.FirstOrDefault(pawn => pawn != null
                && !pawn.Dead && !pawn.Downed && !pawn.Drafted && pawn.skills != null
                && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Research)
                && personnel.LevelFor(pawn) >= 4
                && pawn.skills.GetSkill(SkillDefOf.Intellectual).Level >= 12
                && pawn.CanReach(table, PathEndMode.Touch, Danger.Some));
        }

        private static IntVec3 MeetingSpot(Pawn pawn, Thing table, IntVec3 current, IntVec3 avoid)
        {
            if (pawn == null || table == null || !table.Spawned) return IntVec3.Invalid;
            Map map = table.Map;
            Room room = table.Position.GetRoom(map);
            if (current.IsValid && current.InBounds(map) && current.GetRoom(map) == room
                && current.DistanceToSquared(table.Position) <= 16
                && current != avoid
                && (!avoid.IsValid || current.DistanceToSquared(avoid) <= 16)
                && current.Standable(map) && current.GetEdifice(map) == null
                && pawn.CanReach(current, PathEndMode.OnCell, Danger.Some)) return current;
            if (pawn.Position.GetRoom(map) == room && pawn.Position.DistanceToSquared(table.Position) <= 16
                && pawn.Position != avoid
                && (!avoid.IsValid || pawn.Position.DistanceToSquared(avoid) <= 16)) return pawn.Position;
            return GenRadial.RadialCellsAround(table.Position, 3f, true)
                .Where(cell => cell.InBounds(map) && cell.GetRoom(map) == room
                    && cell.Standable(map) && cell.GetEdifice(map) == null
                    && cell.DistanceToSquared(table.Position) <= 16 && cell != avoid
                    && (!avoid.IsValid || cell.DistanceToSquared(avoid) <= 16)
                    && (cell.GetFirstPawn(map) == null || cell.GetFirstPawn(map) == pawn)
                    && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some))
                .OrderBy(cell => cell.DistanceToSquared(table.Position))
                .DefaultIfEmpty(IntVec3.Invalid).First();
        }

        private static void GuideToMeeting(Pawn pawn, IntVec3 destination)
        {
            if (pawn == null || pawn.Drafted || pawn.Downed || pawn.jobs == null
                || !destination.IsValid) return;
            if (pawn.Position == destination)
            {
                JobDef meetingWait = DefDatabase<JobDef>.GetNamed("SCP_O5MeetingWait");
                if (pawn.CurJob != null && pawn.CurJob.def == meetingWait) return;
                Job wait = JobMaker.MakeJob(meetingWait);
                wait.expiryInterval = 12000;
                pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
                return;
            }
            JobDef meetingGoto = DefDatabase<JobDef>.GetNamed("SCP_O5MeetingGoto");
            if (pawn.CurJob != null && pawn.CurJob.def == meetingGoto
                && pawn.CurJob.targetA.Cell == destination) return;
            Job move = JobMaker.MakeJob(meetingGoto, destination);
            move.expiryInterval = 3000;
            pawn.jobs.StartJob(move, JobCondition.InterruptForced);
        }

        private void EnsureMeetingLord(Faction foundation)
        {
            Lord existing = LordUtility.GetLord(visitor);
            LordJob_ScpO5Meeting meeting = existing == null ? null
                : existing.LordJob as LordJob_ScpO5Meeting;
            if (meeting != null && meeting.Scientist == scientist)
            {
                if (LordUtility.GetLord(scientist) != existing) existing.AddPawn(scientist);
                meeting.UpdateSpots(visitorSpot, scientistSpot);
                return;
            }
            foreach (Pawn pawn in new[] { visitor, scientist })
            {
                Lord previous = LordUtility.GetLord(pawn);
                if (previous != null) previous.RemovePawn(pawn);
                if (pawn.jobs != null) pawn.jobs.StopAll();
            }
            if (visitor.mindState != null) visitor.mindState.exitMapAfterTick = int.MaxValue;
            LordMaker.MakeNewLord(foundation,
                new LordJob_ScpO5Meeting(visitor, scientist, visitorSpot, scientistSpot),
                map, new[] { visitor, scientist });
        }

        private void ReleaseMeetingLord()
        {
            Lord meeting = visitor == null ? null : LordUtility.GetLord(visitor);
            if (meeting == null || !(meeting.LordJob is LordJob_ScpO5Meeting)) return;
            foreach (Pawn pawn in meeting.ownedPawns.ToList()) meeting.RemovePawn(pawn);
        }

        private void SetPickupLanding()
        {
            IntVec3 landing;
            if (visitor == null || !visitor.Spawned || !DropCellFinder.TryFindDropSpotNear(
                visitor.Position, map, out landing, false, false, false, ThingDefOf.Shuttle.size, true)
                || !visitor.CanReach(landing, PathEndMode.Touch, Danger.Deadly)) return;
            foreach (QuestPart_AddShipJob_Arrive arrival in quest.PartsListForReading
                .OfType<QuestPart_AddShipJob_Arrive>())
                if (arrival.inSignal == pickupSignal) arrival.cell = landing;
        }

        private void TryBoardShuttle()
        {
            List<Pawn> present = Delegation().Where(pawn => pawn.Spawned && !pawn.Dead
                && pawn.Map == map).ToList();
            if (present.Count == 0) return;
            Thing shuttle = map.listerThings.ThingsOfDef(ThingDefOf.Shuttle).FirstOrDefault(thing =>
            {
                CompShuttle comp = thing.TryGetComp<CompShuttle>();
                return comp != null && comp.RequiredPawns != null
                    && comp.RequiredPawns.Any(required => Delegation().Contains(required));
            });
            CompShuttle shuttleComp = shuttle == null ? null : shuttle.TryGetComp<CompShuttle>();
            if (shuttleComp == null)
            {
                if (Find.TickManager.TicksGame - departureTick >= 18000) return;
                foreach (Pawn pawn in present)
                {
                    Lord currentLord = LordUtility.GetLord(pawn);
                    if (pawn != visitor && currentLord != null
                        && currentLord.LordJob is LordJob_ExitMapBest) continue;
                    if (pawn.jobs == null || pawn.CurJob != null
                        && pawn.CurJob.def == JobDefOf.Wait) continue;
                    Job wait = JobMaker.MakeJob(JobDefOf.Wait);
                    wait.expiryInterval = 1500;
                    pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
                }
                return;
            }
            if (!extraGuardsReleased)
            {
                List<Pawn> extras = present.Where(pawn => pawn != visitor
                    && !shuttleComp.RequiredPawns.Contains(pawn)).ToList();
                if (extras.Count > 0)
                {
                    foreach (Pawn pawn in extras)
                    {
                        Lord previous = LordUtility.GetLord(pawn);
                        if (previous != null) previous.RemovePawn(pawn);
                        if (pawn.jobs != null) pawn.jobs.StopAll();
                    }
                    LordMaker.MakeNewLord(extras[0].Faction,
                        new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, false), map, extras);
                }
                extraGuardsReleased = true;
            }
            foreach (Pawn pawn in present)
            {
                if (!shuttleComp.RequiredPawns.Contains(pawn) || !shuttleComp.IsAllowed(pawn)
                    || !pawn.CanReach(shuttle, PathEndMode.Touch, Danger.Deadly)) continue;
                if (pawn.mindState != null) pawn.mindState.exitMapAfterTick = int.MaxValue;
                if (pawn.jobs == null || pawn.CurJob != null
                    && pawn.CurJob.def == JobDefOf.EnterTransporter
                    && pawn.CurJob.targetA.Thing == shuttle) continue;
                Lord lord = LordUtility.GetLord(pawn);
                if (lord != null) lord.RemovePawn(pawn);
                pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.EnterTransporter, shuttle),
                    JobCondition.InterruptForced);
            }
        }

        private void TriggerRetaliation(Faction foundation)
        {
            if (retaliationTriggered || foundation == null || map == null) return;
            retaliationTriggered = true;
            ScpFactionRelationsComponent.EnsureHostile(foundation, Faction.OfPlayer);
            List<Pawn> escort = guards.Where(pawn => pawn != null && pawn.Spawned
                && !pawn.Dead && pawn.Map == map).ToList();
            foreach (Pawn guard in escort)
            {
                Lord previous = LordUtility.GetLord(guard);
                if (previous != null) previous.RemovePawn(guard);
            }
            if (escort.Count > 0)
                LordMaker.MakeNewLord(foundation,
                    new LordJob_AssaultColony(foundation, false, true, false, false, false, false, false),
                    map, escort);

            PawnKindDef vanguard = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_FoundationChimera");
            if (vanguard != null)
                for (int i = 0; i < 2; i++)
                {
                    IntVec3 cell;
                    if (!CellFinder.TryFindRandomEdgeCellWith(candidate =>
                        ScpSpawnCells.FootprintClear(candidate, map, vanguard.race), map, 0f, out cell)) break;
                    Pawn_FoundationChimera machine = PawnGenerator.GeneratePawn(vanguard, foundation)
                        as Pawn_FoundationChimera;
                    if (machine == null) break;
                    GenSpawn.Spawn(machine, cell, map);
                    machine.EnableAutonomousEscort(120000);
                }

            PawnKindDef rifleman = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_MTF_Omega7Rifleman");
            PawnKindDef gunner = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_MTF_Omega7Gunner");
            ThingDef rifle = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_Nu7HKCarbine");
            ThingDef machineGun = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_Nu7LightMachineGun");
            if (rifleman == null || gunner == null || rifle == null || machineGun == null) return;
            IntVec3 entry;
            if (!CellFinder.TryFindRandomEdgeCellWith(candidate => candidate.Standable(map)
                && !candidate.Fogged(map) && candidate.GetFirstPawn(map) == null, map, 0f, out entry)) return;
            var squad = new List<Pawn>();
            for (int i = 0; i < 8; i++)
                squad.Add(FoundationSupportComponent.GenerateEquippedPawn(
                    i < 6 ? rifleman : gunner, foundation, i < 6 ? rifle : machineGun));
            Pawn_Scp076 able = Pawn_Scp076.Generate(foundation);
            if (able != null) squad.Add(able);
            foreach (Pawn pawn in squad)
            {
                IntVec3 cell = CellFinder.RandomSpawnCellForPawnNear(entry, map, 6);
                GenSpawn.Spawn(pawn, cell, map);
            }
            LordMaker.MakeNewLord(foundation,
                new LordJob_AssaultColony(foundation, false, true, false, false, false, false, false), map, squad);
        }

        private void SendDelegationHome(int now)
        {
            var lords = new HashSet<Lord>();
            foreach (Pawn pawn in Delegation())
            {
                if (pawn == null || !pawn.Spawned || pawn.Map != map || pawn.Dead) continue;
                Lord lord = LordUtility.GetLord(pawn);
                if (lord != null && lords.Add(lord))
                {
                    lord.SetJob(new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, false));
                    lord.GotoToil(lord.Graph.StartingToil);
                }
                else if (lord == null && pawn.mindState != null)
                    pawn.mindState.exitMapAfterTick = now + 1;
            }
        }

        public void ReleaseStrandedDelegation()
        {
            if (strandedDelegationReleased || map == null) return;
            Faction foundation = Current.Game.GetComponent<FoundationSupportComponent>().FoundationFaction;
            if (foundation == null) return;
            strandedDelegationReleased = true;
            List<Pawn> stranded = Delegation().Where(pawn => pawn.Spawned && !pawn.Dead
                && pawn.Map == map && pawn.Faction == foundation && !pawn.IsPrisonerOfColony).ToList();
            if (stranded.Count == 0) return;
            foreach (Pawn pawn in stranded)
            {
                Lord previous = LordUtility.GetLord(pawn);
                if (previous != null) previous.RemovePawn(pawn);
                if (pawn.jobs != null) pawn.jobs.StopAll();
                if (pawn.mindState != null) pawn.mindState.exitMapAfterTick = Find.TickManager.TicksGame + 1;
            }
            LordMaker.MakeNewLord(foundation,
                new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, false), map, stranded);
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            ClearMeetingMarker();
            Find.SignalManager.SendSignal(new Signal(completedSignal));
        }

        private void Fail()
        {
            if (finished) return;
            finished = true;
            ClearMeetingMarker();
            ReleaseMeetingLord();
            if (!retaliationTriggered) SendDelegationHome(Find.TickManager.TicksGame);
            Find.SignalManager.SendSignal(new Signal(failedSignal));
        }
    }

    [HarmonyPatch(typeof(FloatMenuMakerMap), "GetOptions")]
    internal static class ScpO5GreenMeetingMenuPatch
    {
        private static void Postfix(ref List<FloatMenuOption> __result, ref FloatMenuContext context)
        {
            if (__result == null || context == null || context.ClickedPawns == null) return;
            Pawn selected = context.FirstSelectedPawn;
            if (selected == null || selected.Faction != Faction.OfPlayer) return;
            QuestPart_ScpO5GreenVisit visit = context.ClickedPawns
                .Select(QuestPart_ScpO5GreenVisit.ActiveFor).FirstOrDefault(part => part != null);
            if (visit == null) return;
            string label = "SCP_O5GreenBeginMeeting".Translate();
            string reason;
            __result.Add(visit.CanBeginMeeting(selected, out reason)
                ? new FloatMenuOption(label, () => visit.BeginMeeting(selected))
                : new FloatMenuOption(label + ": " + reason, null));
        }
    }

}
