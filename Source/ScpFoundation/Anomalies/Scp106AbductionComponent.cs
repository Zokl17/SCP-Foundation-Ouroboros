using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class Scp106AbductionComponent : MapComponent, IThingHolder
    {
        private const int StainTicks = 300;
        private const int LarryReturnTicks = 600;
        private const int VictimReturnTicks = 7500;
        private ThingOwner<Pawn> pocket;
        private List<Abduction> abductions = new List<Abduction>();
        private readonly Dictionary<Pawn, Mote> stains = new Dictionary<Pawn, Mote>();

        public Scp106AbductionComponent(Map map) : base(map)
        {
            pocket = new ThingOwner<Pawn>(this);
        }

        public IThingHolder ParentHolder { get { return null; } }
        public ThingOwner GetDirectlyHeldThings() { return pocket; }
        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, pocket);
        }

        public bool IsBusy(Pawn_Scp106 larry)
        {
            return abductions.Exists(entry => entry.larry == larry && !entry.larryReturned);
        }

        public bool TryBegin(Pawn_Scp106 larry, Pawn victim)
        {
            if (larry == null || victim == null || !larry.Spawned || larry.CanBeContained || larry.Dead
                || !victim.Spawned || !victim.Downed || victim.Dead || !victim.RaceProps.Humanlike
                || (victim.Faction != Faction.OfPlayer && !victim.IsPrisonerOfColony)
                || larry.Map != map || victim.Map != map
                || larry.Position.DistanceToSquared(victim.Position) > 4
                || IsBusy(larry) || abductions.Exists(entry => entry.victim == victim)) return false;
            abductions.Add(new Abduction
            {
                larry = larry,
                victim = victim,
                larryCell = larry.Position,
                victimCell = victim.Position,
                captureTick = Find.TickManager.TicksGame + StainTicks,
                victimReturns = Rand.Chance(0.5f)
            });
            larry.pather.StopDead();
            return true;
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int index = abductions.Count - 1; index >= 0; index--)
            {
                Abduction entry = abductions[index];
                if (!entry.captured)
                {
                    if (entry.larry == null || !entry.larry.Spawned || entry.larry.CanBeContained
                        || entry.victim == null || !entry.victim.Spawned || !entry.victim.Downed
                        || entry.victim.Dead || entry.larry.Position.DistanceToSquared(entry.victim.Position) > 4)
                    {
                        if (entry.victim != null) stains.Remove(entry.victim);
                        abductions.RemoveAt(index);
                        continue;
                    }
                    MaintainStain(entry);
                    entry.larry.pather.StopDead();
                    if (now < entry.captureTick) continue;
                    entry.victimCell = entry.victim.Position;
                    entry.larryCell = entry.larry.Position;
                    entry.victim.DeSpawnOrDeselect();
                    if (!pocket.TryAdd(entry.victim, true))
                    {
                        GenSpawn.Spawn(entry.victim, entry.victimCell, map);
                        stains.Remove(entry.victim);
                        abductions.RemoveAt(index);
                        continue;
                    }
                    entry.larry.DeSpawnOrDeselect();
                    if (!pocket.TryAdd(entry.larry, true))
                    {
                        ReturnPawn(entry.victim, entry.victimCell);
                        GenSpawn.Spawn(entry.larry, entry.larryCell, map);
                        stains.Remove(entry.victim);
                        abductions.RemoveAt(index);
                        continue;
                    }
                    entry.captured = true;
                    entry.larryReturnTick = now + LarryReturnTicks;
                    entry.victimReturnTick = now + VictimReturnTicks;
                    stains.Remove(entry.victim);
                    Messages.Message("SCP_106Abducted".Translate(entry.victim.LabelShort),
                        MessageTypeDefOf.NegativeEvent, false);
                }
                if (entry.captured && !entry.larryReturned && now >= entry.larryReturnTick)
                {
                    if (!ReturnPawn(entry.larry, entry.larryCell)) continue;
                    entry.larryReturned = true;
                    entry.larry.DelayHunt(120);
                }
                if (entry.captured && entry.larryReturned && now >= entry.victimReturnTick)
                {
                    if (entry.victimReturns)
                    {
                        if (!ReturnPawn(entry.victim, entry.victimCell)) continue;
                        Messages.Message("SCP_106Returned".Translate(entry.victim.LabelShort),
                            MessageTypeDefOf.PositiveEvent, false);
                    }
                    else
                    {
                        pocket.Remove(entry.victim);
                        Find.WorldPawns.PassToWorld(entry.victim, RimWorld.Planet.PawnDiscardDecideMode.KeepForever);
                        Messages.Message("SCP_106Lost".Translate(entry.victim.LabelShort),
                            MessageTypeDefOf.NegativeEvent, false);
                    }
                    abductions.RemoveAt(index);
                }
            }
        }

        private void MaintainStain(Abduction entry)
        {
            Mote mote;
            if (!stains.TryGetValue(entry.victim, out mote) || mote == null || mote.Destroyed)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_106CapturePortal");
                if (def != null) mote = MoteMaker.MakeStaticMote(entry.victim.Position, map, def, 1f);
                stains[entry.victim] = mote;
            }
            if (mote != null) mote.Maintain();
        }

        private bool ReturnPawn(Pawn pawn, IntVec3 origin)
        {
            if (pawn == null || pawn.Destroyed) return true;
            IntVec3 cell = IntVec3.Invalid;
            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(origin, 8f, true))
            {
                if (candidate.InBounds(map) && candidate.Standable(map)) { cell = candidate; break; }
            }
            if (!cell.IsValid) return false;
            pocket.Remove(pawn);
            GenSpawn.Spawn(pawn, cell, map);
            return true;
        }

        public override void MapRemoved()
        {
            foreach (Pawn pawn in pocket.InnerListForReading.ToList())
            {
                pocket.Remove(pawn);
                Find.WorldPawns.PassToWorld(pawn, RimWorld.Planet.PawnDiscardDecideMode.KeepForever);
            }
            base.MapRemoved();
        }

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref pocket, "scp106Pocket", this);
            Scribe_Collections.Look(ref abductions, "scp106Abductions", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pocket == null) pocket = new ThingOwner<Pawn>(this);
                if (abductions == null) abductions = new List<Abduction>();
            }
        }

        public sealed class Abduction : IExposable
        {
            public Pawn_Scp106 larry;
            public Pawn victim;
            public IntVec3 larryCell;
            public IntVec3 victimCell;
            public int captureTick;
            public int larryReturnTick;
            public int victimReturnTick;
            public bool victimReturns;
            public bool captured;
            public bool larryReturned;

            public void ExposeData()
            {
                Scribe_References.Look(ref larry, "larry");
                Scribe_References.Look(ref victim, "victim");
                Scribe_Values.Look(ref larryCell, "larryCell", IntVec3.Invalid);
                Scribe_Values.Look(ref victimCell, "victimCell", IntVec3.Invalid);
                Scribe_Values.Look(ref captureTick, "captureTick");
                Scribe_Values.Look(ref larryReturnTick, "larryReturnTick");
                Scribe_Values.Look(ref victimReturnTick, "victimReturnTick");
                Scribe_Values.Look(ref victimReturns, "victimReturns");
                Scribe_Values.Look(ref captured, "captured");
                Scribe_Values.Look(ref larryReturned, "larryReturned");
            }
        }
    }
}
