using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class Thing_Scp131 : ThingWithComps
    {
        private Pawn bondedPawn;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref bondedPawn, "scp131BondedPawn");
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            return (text.NullOrEmpty() ? "" : text + "\n") + (bondedPawn == null
                ? "SCP_131Unbonded".Translate() : "SCP_131Bonded".Translate(bondedPawn.LabelShortCap));
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(pawn)) yield return option;
            if (!Spawned || pawn == null || !pawn.IsColonistPlayerControlled || !pawn.RaceProps.Humanlike
                || pawn.Dead || pawn.Downed
                || pawn.Map != Map || pawn.Position.DistanceToSquared(Position) > 2) yield break;
            yield return new FloatMenuOption("SCP_131Befriend".Translate(), () => bondedPawn = pawn);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) return;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(def.defName + "Pawn");
            if (kind == null) return;
            Map map = Map;
            IntVec3 position = Position;
            Pawn_Scp131 pod = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer) as Pawn_Scp131;
            if (pod == null) return;
            pod.BondedPawn = bondedPawn;
            GenSpawn.Spawn(pod, position, map);
            Destroy(DestroyMode.Vanish);
        }

        public static bool Watches173(Thing statue)
        {
            if (statue == null || !statue.Spawned || statue.Map.glowGrid.GroundGlowAt(statue.Position) < 0.15f)
                return false;
            Map map = statue.Map;
            IEnumerable<Thing> pods = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn.def.defName == "SCP_131APawn" || pawn.def.defName == "SCP_131BPawn")
                .Cast<Thing>()
                .Concat(new[] { "SCP_131A", "SCP_131B" }
                .Select(DefDatabase<ThingDef>.GetNamedSilentFail)
                .Where(def => def != null)
                .SelectMany(def => map.listerThings.ThingsOfDef(def)));
            return pods.Any(pod => pod.Spawned && pod.Position.DistanceToSquared(statue.Position) <= 400
                    && ScpSurveillance.Sight(pod.Position, statue.Position, map));
        }
    }
}
