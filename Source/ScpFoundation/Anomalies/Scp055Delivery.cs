using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Containment;
using ScpFoundation.Interactions;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public static class Scp055Delivery
    {
        public const string CrateDefName = "SCP_055ShippingCrate";

        public static bool BlocksCargoCell(Thing occupant, Thing anchor, Thing cargo)
        {
            return occupant != null && occupant != anchor && occupant != cargo && !(occupant is Pawn)
                && occupant.def.category == ThingCategory.Item;
        }

        private static string PostBlockKey(CompScpPost post, Thing cargo)
        {
            if (post == null || !post.parent.Spawned || !post.parent.Map.IsPlayerHome
                || post.parent.def != ScpInteractionDefOf.SCP_ContainmentAnchor
                || post.parent.Faction != Faction.OfPlayer) return "SCP_055CargoNoPost";
            if (post.BoundObject != null || post.ObjectCell.GetThingList(post.parent.Map)
                .Any(thing => BlocksCargoCell(thing, post.parent, cargo))) return "SCP_055CargoPostOccupied";
            if (!post.ObjectCell.Standable(post.parent.Map)) return "SCP_055CargoNoPath";
            return null;
        }

        public static bool PostReady(CompScpPost post, Thing cargo = null)
        {
            if (PostBlockKey(post, cargo) != null) return false;
            var context = new ContainmentContext(post.parent);
            return ScpInteractionDefOf.SCP_Anomaly055.requirements.All(requirement => requirement.IsSatisfied(context));
        }

        public static string PostProblem(CompScpPost post, Thing cargo = null)
        {
            string blocked = PostBlockKey(post, cargo);
            if (blocked != null) return blocked.Translate();
            var context = new ContainmentContext(post.parent);
            foreach (ContainmentRequirement requirement in ScpInteractionDefOf.SCP_Anomaly055.requirements)
            {
                if (requirement.IsSatisfied(context)) continue;
                RequirementRoom room = requirement as RequirementRoom;
                if (room != null)
                {
                    if (context.room == null || context.room.TouchesMapEdge || context.room.IsDoorway)
                        return "SCP_055CargoRoomNotClosed".Translate();
                    if (context.room.CellCount < room.minCells)
                        return "SCP_055CargoRoomSmall".Translate(context.room.CellCount, room.minCells);
                    if (room.roofed && context.room.OpenRoofCount > 0)
                        return "SCP_055CargoRoofMissing".Translate(context.room.OpenRoofCount);
                }
                return "SCP_055CargoRequirementMissing".Translate(requirement.Description());
            }
            return null;
        }

        public static CompScpPost ReadyPost(Map map)
        {
            return CompScpPost.All(map).FirstOrDefault(post => PostReady(post));
        }

        public static bool Deliver(Map map)
        {
            if (map == null || !map.IsPlayerHome) return false;
            ThingDef crateDef = DefDatabase<ThingDef>.GetNamedSilentFail(CrateDefName);
            if (crateDef == null) return false;
            IntVec3 dropSpot = DropCellFinder.TradeDropSpot(map);
            if (!dropSpot.IsValid || !dropSpot.InBounds(map)) return false;
            Thing crate = ThingMaker.MakeThing(crateDef);
            if (ModsConfig.RoyaltyActive)
            {
                Faction faction = Current.Game.GetComponent<FoundationSupportComponent>().FoundationFaction;
                TransportShip ship = TransportShipMaker.MakeTransportShip(
                    TransportShipDefOf.Ship_Shuttle, new List<Thing> { crate }, null);
                if (faction != null) ship.shipThing.SetFaction(faction);
                ShipJob_Arrive arrive = (ShipJob_Arrive)ShipJobMaker.MakeShipJob(ShipJobDefOf.Arrive);
                arrive.mapParent = map.Parent;
                arrive.cell = IntVec3.Invalid;
                arrive.factionForArrival = Faction.OfPlayer;
                ShipJob_Unload unload = (ShipJob_Unload)ShipJobMaker.MakeShipJob(ShipJobDefOf.Unload);
                unload.dropMode = TransportShipDropMode.All;
                unload.unforbidAll = true;
                ShipJob_WaitTime wait = (ShipJob_WaitTime)ShipJobMaker.MakeShipJob(ShipJobDefOf.WaitTime);
                wait.duration = 600;
                ship.AddJob(arrive);
                ship.AddJob(unload);
                ship.AddJob(wait);
                ship.AddJob(ShipJobDefOf.FlyAway);
                ship.Start();
            }
            else
                DropPodUtility.DropThingsNear(dropSpot, map, new List<Thing> { crate },
                    110, false, false, false, false, true, Faction.OfPlayer);
            Current.Game.GetComponent<ScpArrivalRegistry>().RecordArrival("055");
            return true;
        }
    }

    // The cargo is an ordinary sealed item, with no SCP comp, dossier or ticking.
    // Only physical placement in a prepared chamber creates the contained anomaly.
    public sealed class Thing_Scp055ShippingCrate : ThingWithComps
    {
        private bool receiptSent;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref receiptSent, "scp055CargoReceiptSent", false);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (respawningAfterLoad || receiptSent || !map.IsPlayerHome) return;
            receiptSent = true;
            Find.LetterStack.ReceiveLetter("SCP_055CargoArrivedTitle".Translate(),
                "SCP_055CargoArrivedBody".Translate(), LetterDefOf.NeutralEvent, this);
        }

        public override string GetInspectString()
        {
            return "SCP_055CargoInspect".Translate();
        }

        private IEnumerable<CompScpPost> AvailablePosts(Pawn pawn)
        {
            return CompScpPost.All(Map).Where(post => Scp055Delivery.PostReady(post, this)
                && pawn.CanReserveAndReach(post.ObjectCell, PathEndMode.OnCell, Danger.Some))
                .OrderBy(post => post.ObjectCell.DistanceToSquared(Position));
        }

        private bool CanCarry(Pawn pawn)
        {
            return Spawned && pawn != null && pawn.Map == Map && pawn.IsColonistPlayerControlled
                && !pawn.Drafted && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
                && pawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some);
        }

        private string UnavailableReason(Pawn pawn = null)
        {
            List<CompScpPost> posts = CompScpPost.All(Map)
                .Where(post => post.parent.def == ScpInteractionDefOf.SCP_ContainmentAnchor)
                .OrderBy(post => post.ObjectCell.DistanceToSquared(Position)).ToList();
            if (posts.Count == 0) return "SCP_055CargoNoPost".Translate();
            if (posts.Any(post => Scp055Delivery.PostReady(post, this)))
                return (pawn == null ? "SCP_055CargoNoCarrier" : "SCP_055CargoNoPath").Translate();
            return string.Join("\n", posts.Take(3).Select(post => Scp055Delivery.PostProblem(post, this))
                .Where(reason => reason != null).Distinct().ToArray());
        }

        private void Assign(Pawn pawn, CompScpPost post)
        {
            if (!CanCarry(pawn) || !Scp055Delivery.PostReady(post, this)) return;
            Job job = JobMaker.MakeJob(ScpInteractionDefOf.SCP_Capture, this, post.ObjectCell, post.parent);
            job.count = 1;
            job.haulMode = HaulMode.ToCellNonStorage;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(pawn)) yield return option;
            if (!CanCarry(pawn)) yield break;
            CompScpPost post = AvailablePosts(pawn).FirstOrDefault();
            yield return post == null ? new FloatMenuOption(UnavailableReason(pawn), null)
                : new FloatMenuOption("SCP_MoveToPostButton".Translate(), () => Assign(pawn, post));
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (!Spawned || !Map.IsPlayerHome) yield break;
            yield return new Command_Action {
                defaultLabel = "SCP_MoveToPostButton".Translate(),
                defaultDesc = "SCP_055CargoMoveDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("Things/Item/Scp055ShippingCrate"),
                action = () => {
                    var options = new List<FloatMenuOption>();
                    foreach (Pawn candidate in Map.mapPawns.FreeColonistsSpawned)
                    {
                        Pawn pawn = candidate;
                        if (!CanCarry(pawn)) continue;
                        CompScpPost post = AvailablePosts(pawn).FirstOrDefault();
                        if (post != null) options.Add(new FloatMenuOption(pawn.LabelShortCap,
                            () => Assign(pawn, post)));
                    }
                    if (options.Count == 0) options.Add(new FloatMenuOption(UnavailableReason(), null));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }

        public Thing InstallAt(CompScpPost post)
        {
            if (!Spawned || post == null || post.parent.Map != Map || post.ObjectCell != Position
                || !Scp055Delivery.PostReady(post, this)) return null;
            Map map = Map;
            IntVec3 cell = Position;
            Thing thing = ThingMaker.MakeThing(ScpInteractionDefOf.SCP_AntimemeticObject);
            DeSpawn();
            GenSpawn.Spawn(thing, cell, map);
            post.Bind(thing);
            CompScp anomaly = thing.TryGetComp<CompScp>();
            anomaly.NotifyDiscovered(false);
            anomaly.NotifyCaptured();
            Destroy(DestroyMode.Vanish);
            Find.LetterStack.ReceiveLetter("SCP_055CargoPlacedTitle".Translate(),
                "SCP_055CargoPlacedBody".Translate(), LetterDefOf.NeutralEvent, thing);
            return thing;
        }
    }
}
