using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Anomalies
{
    public sealed class Thing_Scp127Gun : ThingWithComps
    {
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!CompScp127Equippable.InLocker(this)) base.DrawAt(drawLoc, flip);
        }

        public override void Print(SectionLayer layer)
        {
            if (!CompScp127Equippable.InLocker(this)) base.Print(layer);
        }
    }

    public sealed class CompScp127Equippable : CompEquippable
    {
        private const int MagazineSize = 60;
        private const int LockerRegrowthTicks = 10000;
        private int rounds = MagazineSize;
        private int regrowthTick = -1;
        private int lockerStartTick = -1;
        private bool automatic;

        public int Rounds
        {
            get
            {
                RefreshMagazine();
                return rounds;
            }
        }

        public bool Automatic { get { return automatic; } }

        internal static bool InLocker(Thing gun)
        {
            if (gun == null || !gun.Spawned || gun.Map == null) return false;
            foreach (Thing thing in gun.Position.GetThingList(gun.Map))
                if (thing is Building_Scp127Locker) return true;
            return false;
        }

        public void SpendRound()
        {
            RefreshMagazine();
            if (rounds <= 0) return;
            rounds--;
            if (rounds == 0)
                regrowthTick = Find.TickManager.TicksGame + Rand.RangeInclusive(3, 5) * 60000;
        }

        private void RefreshMagazine()
        {
            if (rounds != 0) return;
            int now = Find.TickManager.TicksGame;
            if (regrowthTick >= 0 && now >= regrowthTick)
            {
                Refill();
                return;
            }
            if (InLocker(parent))
            {
                if (lockerStartTick < 0) lockerStartTick = now;
                if (now - lockerStartTick >= LockerRegrowthTicks) Refill();
            }
            else lockerStartTick = -1;
        }

        private void Refill()
        {
            rounds = MagazineSize;
            regrowthTick = -1;
            lockerStartTick = -1;
        }

        public override void CompTick()
        {
            base.CompTick();
            RefreshMagazine();
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            base.PostDeSpawn(map, mode);
            lockerStartTick = -1;
        }

        public override string CompInspectStringExtra()
        {
            return Status;
        }

        private string Status
        {
            get
            {
                int available = Rounds;
                if (available > 0) return "SCP_127Ammo".Translate(available, MagazineSize);
                if (lockerStartTick >= 0)
                    return "SCP_127LockerRegrowing".Translate(Math.Max(1,
                        (int)Math.Ceiling((lockerStartTick + LockerRegrowthTicks - Find.TickManager.TicksGame) / 2500f)));
                return "SCP_127Regrowing".Translate(Math.Max(1,
                    (int)Math.Ceiling((regrowthTick - Find.TickManager.TicksGame) / 60000f)));
            }
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra()) yield return gizmo;
            yield return new Command_Toggle
            {
                defaultLabel = "SCP_127FireMode".Translate(),
                defaultDesc = Status + "\n" + "SCP_127FireModeDesc".Translate(),
                icon = parent.def.uiIcon,
                isActive = () => automatic,
                toggleAction = () => automatic = !automatic
            };
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref rounds, "scp127Rounds", MagazineSize);
            Scribe_Values.Look(ref regrowthTick, "scp127RegrowthTick", -1);
            Scribe_Values.Look(ref lockerStartTick, "scp127LockerStartTick", -1);
            Scribe_Values.Look(ref automatic, "scp127Automatic", false);
        }
    }

    public sealed class Verb_Scp127Shoot : Verb_Shoot
    {
        private CompScp127Equippable Magazine
        {
            get { return EquipmentSource == null ? null : EquipmentSource.GetComp<CompScp127Equippable>(); }
        }

        protected override int ShotsPerBurst
        {
            get
            {
                CompScp127Equippable magazine = Magazine;
                return magazine != null && magazine.Automatic ? 6 : 1;
            }
        }

        public override bool Available()
        {
            CompScp127Equippable magazine = Magazine;
            return magazine != null && magazine.Rounds > 0 && base.Available();
        }

        protected override bool TryCastShot()
        {
            CompScp127Equippable magazine = Magazine;
            if (magazine == null || magazine.Rounds <= 0) return false;
            if (!base.TryCastShot()) return false;
            magazine.SpendRound();
            return true;
        }
    }
}
