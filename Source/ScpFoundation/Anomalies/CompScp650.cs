using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Containment;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using System.Collections.Generic;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp650 : CompProperties
    {
        public int relocationCooldownTicks = 5000;
        public CompProperties_Scp650() { compClass = typeof(CompScp650); }
    }

    public sealed class CompScp650 : ThingComp
    {
        private int nextRelocationTick;
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            QuestPart_Scp650Return transfer = QuestPart_Scp650Return.For(parent);
            FloatMenuOption option = transfer == null ? null : transfer.LoadOption(pawn);
            if (option != null) yield return option;
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            // A physical placement starts a fresh setup interval; loading a save preserves the deadline.
            if (!respawningAfterLoad || nextRelocationTick == 0)
                nextRelocationTick = Find.TickManager.TicksGame + ((CompProperties_Scp650)props).relocationCooldownTicks;
        }
        public override void PostExposeData() { Scribe_Values.Look(ref nextRelocationTick, "scp650NextRelocation", 0); }

        public override void CompTickRare()
        {
            CompScp scp = parent.TryGetComp<CompScp>();
            // Captured objects cannot bypass the warning/response window through physical relocation.
            if (scp != null && !ContainmentProtection.AllowsEscape(scp.IsInCustody, scp.IsCaptureGraceActive, scp.IsBreached)) return;
            if (!parent.Spawned || Find.TickManager.TicksGame < nextRelocationTick || ScpObservation.Observers(parent).Count > 0 || ScpSurveillance.Observed(parent)) return;
            nextRelocationTick = Find.TickManager.TicksGame + ((CompProperties_Scp650)props).relocationCooldownTicks;
            Pawn victim = parent.Map.mapPawns.FreeColonistsSpawned.Where(pawn => !pawn.Dead && !pawn.Downed && pawn.Awake()).RandomElementWithFallback();
            if (victim == null) return;
            Map map = parent.Map;
            IntVec3 behind = victim.Position - victim.Rotation.FacingCell;
            foreach (IntVec3 destination in GenRadial.RadialCellsAround(behind, 1.5f, true))
            {
                if (!destination.InBounds(map) || !destination.Standable(map) || destination.Fogged(map)
                    || destination.GetThingList(map).Count != 0 || !GenSight.LineOfSight(destination, victim.Position, map)) continue;
                // Move the same saved instance; never wipe occupants or clone the anomaly.
                parent.DeSpawn();
                GenSpawn.Spawn(parent, destination, map);
                if (victim.needs.mood != null) victim.needs.mood.thoughts.memories.TryGainMemory(ScpInteractionDefOf.SCP_Startled650);
                break;
            }
        }
    }
}
