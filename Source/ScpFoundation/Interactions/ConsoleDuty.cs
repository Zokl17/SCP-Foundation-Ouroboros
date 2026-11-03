using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ScpFoundation.Interactions
{
    public sealed class ConsoleDutyShift : IExposable
    {
        public int hours;
        public Pawn employee;
        public void ExposeData()
        {
            Scribe_Values.Look(ref hours, "hours", 0);
            Scribe_References.Look(ref employee, "employee");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) hours &= ShiftPolicy.AllHours;
        }
    }
    public sealed partial class CompMonitoringConsole
    {
        public bool useShifts;
        public List<ConsoleDutyShift> shifts = Defaults();
        private static List<ConsoleDutyShift> Defaults()
        {
            return new List<ConsoleDutyShift> {
                new ConsoleDutyShift { hours = ShiftPolicy.Range(0,8) },
                new ConsoleDutyShift { hours = ShiftPolicy.Range(8,16) },
                new ConsoleDutyShift { hours = ShiftPolicy.Range(16,24) } };
        }
        public static IEnumerable<CompMonitoringConsole> All(Map map)
        {
            return map.listerThings.ThingsOfDef(ScpInteractionDefOf.SCPV1_MonitoringConsole)
                .Where(t => t.Faction == Faction.OfPlayer).Select(t => t.TryGetComp<CompMonitoringConsole>()).Where(c => c != null);
        }
        public bool Due(Pawn pawn, bool early)
        {
            return pawn != null && parent.Spawned && shifts.Any(s => s.employee == pawn
                && ShiftPolicy.Due(s.hours, GenLocalDate.HourFloat(parent.Map), early));
        }
        public bool Eligible(Pawn pawn)
        {
            if (!useShifts) return true;
            if (!Due(pawn,true)) return false;
            // Active post duties have priority; contradictory console rosters use a stable winner.
            if (CompScpPost.All(parent.Map).Any(p => p.useShifts && p.BoundObject != null
                && (p.OnDuty(pawn,false,false) || p.OnDuty(pawn,true,false)))) return false;
            return !All(parent.Map).Any(c => c != this && c.useShifts && c.HasAssignments && c.Due(pawn,true)
                && ((!Due(pawn,false) && c.Due(pawn,false)) || (Due(pawn,false) == c.Due(pawn,false) && c.parent.thingIDNumber < parent.thingIDNumber)));
        }
        public Pawn Relief
        {
            get
            {
                Pawn currentOperator = Operator;
                List<Pawn> colonists = parent.Map.mapPawns.FreeColonistsSpawned.ToList();
                return colonists.FirstOrDefault(p => p != currentOperator && ScpSurveillance.FitOperator(p)
                    && Due(p,false) && Eligible(p) && !p.pather.Moving
                    && p.Position.DistanceToSquared(parent.InteractionCell) <= 2
                    && p.CurJob != null && p.CurJob.def == ScpInteractionDefOf.SCP_Monitor && p.CurJob.targetA.Thing == parent);
            }
        }
        public void AssignDuty(ConsoleDutyShift shift, Pawn pawn)
        {
            if (shift == null) return;
            shift.employee = pawn;
            if (pawn == null) return;
            useShifts = true;
            WorkTypeDef work = DefDatabase<WorkTypeDef>.GetNamed("BasicWorker");
            if (pawn.workSettings != null && !pawn.WorkTypeIsDisabled(work) && pawn.workSettings.GetPriority(work) == 0)
                pawn.workSettings.SetPriority(work, 3);
        }
        public IntVec3 WaitingCell(Pawn pawn)
        {
            foreach (IntVec3 offset in GenAdj.AdjacentCells)
            {
                IntVec3 cell = parent.InteractionCell + offset;
                if (cell.InBounds(parent.Map) && cell.Standable(parent.Map) && cell.GetEdifice(parent.Map) == null
                    && cell.GetRoom(parent.Map) == parent.InteractionCell.GetRoom(parent.Map)
                    && ScpSurveillance.Sight(cell,parent.InteractionCell,parent.Map)
                    && (cell.GetFirstPawn(parent.Map) == null || cell.GetFirstPawn(parent.Map) == pawn)
                    && pawn.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.Some)) return cell;
            }
            return IntVec3.Invalid;
        }
        public IntVec3 EntryCell(Pawn pawn, bool ignoreRoster = false)
        {
            if (!ignoreRoster && !Eligible(pawn)) return IntVec3.Invalid;
            if (Operator == null && pawn.CanReserveAndReach(parent.InteractionCell, PathEndMode.OnCell, Danger.Some)) return parent.InteractionCell;
            return useShifts ? WaitingCell(pawn) : IntVec3.Invalid;
        }
        public string RosterSummary()
        {
            if (!useShifts) return "SCP_ShiftsOff".Translate();
            string people = string.Join(", ", shifts.Where(s => ShiftPolicy.Active(s.hours, GenLocalDate.HourInteger(parent.Map)))
                .Select(s => CompScpPost.Name(s.employee)).Distinct().ToArray());
            return "SCP_ConsoleCurrentShift".Translate(people.Length == 0 ? "SCP_ShiftNobody".Translate().ToString() : people);
        }
        public string ValidateRoster()
        {
            var gaps = new List<string>();
            var conflicts = new HashSet<string>();
            for (int hour = 0; hour < 24; hour++)
            {
                var workers = shifts.Where(s => ShiftPolicy.Active(s.hours,hour) && s.employee != null && !s.employee.Dead).Select(s => s.employee).Distinct().ToList();
                if (workers.Count == 0) gaps.Add(hour.ToString("00"));
                if (workers.Count > 1) foreach(Pawn worker in workers) conflicts.Add(CompScpPost.Name(worker));
                foreach (Pawn worker in workers)
                {
                    if (All(parent.Map).Any(c => c != this && c.useShifts && c.shifts.Any(s => s.employee == worker && ShiftPolicy.Active(s.hours,hour)))
                        || CompScpPost.All(parent.Map).Any(p => p.useShifts && p.shifts.Any(s => ShiftPolicy.Active(s.hours,hour) && (s.Has(worker,false) || s.Has(worker,true)))))
                        conflicts.Add(CompScpPost.Name(worker));
                }
            }
            return (gaps.Count == 0 && conflicts.Count == 0 ? "SCP_ShiftRosterValid".Translate().ToString() :
                (gaps.Count > 0 ? "SCP_ShiftGaps".Translate(string.Join(", ",gaps.ToArray())).ToString()+"\n" : "")
                + (conflicts.Count > 0 ? "SCP_ShiftConflicts".Translate(string.Join(", ",conflicts.ToArray())).ToString() : ""))
                + "\n\n" + "SCP_ConsoleRosterHelp".Translate();
        }
    }
}
