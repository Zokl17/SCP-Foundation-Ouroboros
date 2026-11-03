using RimWorld;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class Pawn_Scp076 : Pawn
    {
        private const string BladeDefName = "SCP_076VoidBlade";

        public override string Label { get { return "SCP-076-2 (Able)"; } }
        public override string LabelNoCount { get { return "SCP-076-2 (Able)"; } }
        public override string LabelShort { get { return "Able"; } }

        public static Pawn_Scp076 Generate(Faction faction)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("SCP_076Kind");
            if (kind == null || faction == null) return null;
            Pawn_Scp076 pawn = PawnGenerator.GeneratePawn(kind, faction) as Pawn_Scp076;
            if (pawn == null) return null;
            pawn.Name = new NameSingle("Able", false);
            pawn.RemoveUnwantedBackpacks();
            pawn.EquipBlade();
            return pawn;
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned || Dead || !this.IsHashIntervalTick(250)) return;
            RemoveUnwantedBackpacks();
            if (!Downed) EquipBlade();
        }

        private void RemoveUnwantedBackpacks()
        {
            if (apparel == null) return;
            for (int i = apparel.WornApparel.Count - 1; i >= 0; i--)
            {
                Apparel worn = apparel.WornApparel[i];
                if (worn.def.defName != "SCP_MTFNavyBackpack" && worn.def.defName != "SCP_MTFBlackBackpack") continue;
                apparel.Remove(worn);
                worn.Destroy(DestroyMode.Vanish);
            }
        }

        private void EquipBlade()
        {
            if (equipment == null || equipment.Primary != null) return;
            ThingDef bladeDef = DefDatabase<ThingDef>.GetNamedSilentFail(BladeDefName);
            ThingWithComps blade = bladeDef == null ? null : ThingMaker.MakeThing(bladeDef) as ThingWithComps;
            if (blade != null) equipment.AddEquipment(blade);
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            dinfo.SetAmount(dinfo.Amount * 0.1f);
            base.PreApplyDamage(ref dinfo, out absorbed);
        }
    }

    public sealed class Thing_Scp076Blade : ThingWithComps
    {
        public override void TickRare()
        {
            base.TickRare();
            if (Spawned) Destroy(DestroyMode.Vanish);
        }
    }
}
