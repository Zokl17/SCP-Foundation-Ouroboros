using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ScpFoundation.Anomalies
{
    public sealed class Building_Scp127Locker : Building_Storage
    {
        public ThingWithComps StoredGun
        {
            get
            {
                if (!Spawned || Map == null) return null;
                foreach (Thing thing in Position.GetThingList(Map))
                    if (thing.def.defName == "SCP_127Gun") return thing as ThingWithComps;
                return null;
            }
        }

        public override Graphic Graphic
        {
            get
            {
                if (StoredGun != null)
                    return GraphicDatabase.Get<Graphic_Single>("Things/Building/Scp127LockerOccupied",
                        ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white, Color.white,
                        def.graphicData, null);
                return base.Graphic;
            }
        }

        public override string GetInspectString()
        {
            string normal = base.GetInspectString();
            ThingWithComps gun = StoredGun;
            string status = gun == null ? "SCP_127LockerEmpty".Translate()
                : "SCP_127LockerFull".Translate() + "\n" + gun.GetComp<CompScp127Equippable>().CompInspectStringExtra();
            return string.IsNullOrEmpty(normal) ? status : normal + "\n" + status;
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn pawn)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(pawn)) yield return option;
            if (pawn == null || !pawn.IsColonistPlayerControlled || pawn.Downed || !Spawned
                || !pawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Some)) yield break;

            if (StoredGun != null)
                yield return new FloatMenuOption("SCP_127Take".Translate(),
                    () => StartJob(pawn, "SCP_127Take"));
            else if (pawn.equipment != null && pawn.equipment.Primary != null
                && pawn.equipment.Primary.def.defName == "SCP_127Gun")
                yield return new FloatMenuOption("SCP_127Store".Translate(),
                    () => StartJob(pawn, "SCP_127Store"));
        }

        private void StartJob(Pawn pawn, string defName)
        {
            JobDef definition = DefDatabase<JobDef>.GetNamedSilentFail(defName);
            if (definition == null) return;
            Job job = JobMaker.MakeJob(definition, this);
            job.playerForced = true;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }
}
