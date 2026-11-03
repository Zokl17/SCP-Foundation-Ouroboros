using System.Collections.Generic;
using CombatExtended;
using ScpFoundation.Anomalies;
using Verse;

namespace ScpFoundation.CombatExtendedCompat
{
    public sealed class CompProperties_Scp127Ammo : CompProperties_AmmoUser
    {
        public CompProperties_Scp127Ammo() { compClass = typeof(CompAmmoUser_Scp127); }
    }

    // CE owns projectile flight and magazine display; the living gun still owns
    // the 3-5 day regrowth timer and the four-hour locker recovery.
    public sealed class CompAmmoUser_Scp127 : CompAmmoUser
    {
        public void SyncLivingMagazine()
        {
            CompScp127Equippable living = parent.GetComp<CompScp127Equippable>();
            if (living == null) return;
            AmmoDef tooth = DefDatabase<AmmoDef>.GetNamedSilentFail("SCP_Ammo_127Tooth");
            if (tooth != null && CurrentAmmo != tooth) CurrentAmmo = tooth;
            if (tooth != null && SelectedAmmo != tooth) SelectedAmmo = tooth;
            int rounds = living.Rounds;
            if (CurMagCount != rounds) CurMagCount = rounds;
        }

        public override void CompTick()
        {
            base.CompTick();
            SyncLivingMagazine();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield break;
        }
    }

    public sealed class Verb_Scp127ShootCE : Verb_ShootCE
    {
        private CompScp127Equippable LivingMagazine
        {
            get { return EquipmentSource == null ? null : EquipmentSource.GetComp<CompScp127Equippable>(); }
        }

        private void Sync()
        {
            CompAmmoUser_Scp127 ammo = CompAmmo as CompAmmoUser_Scp127;
            if (ammo != null) ammo.SyncLivingMagazine();
        }

        public override int ShotsPerBurst
        {
            get
            {
                CompScp127Equippable living = LivingMagazine;
                return living != null && living.Automatic ? 6 : 1;
            }
        }

        public override bool Available()
        {
            Sync();
            CompScp127Equippable living = LivingMagazine;
            return living != null && living.Rounds > 0 && base.Available();
        }

        public override bool TryCastShot()
        {
            Sync();
            CompScp127Equippable living = LivingMagazine;
            if (living == null || living.Rounds <= 0 || !base.TryCastShot()) return false;
            living.SpendRound();
            Sync();
            return true;
        }
    }
}
