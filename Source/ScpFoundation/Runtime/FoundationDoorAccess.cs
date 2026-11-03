using System.Collections.Generic;
using System.Linq;
using RimWorld;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class CompProperties_FoundationDoorAccess : CompProperties
    {
        public CompProperties_FoundationDoorAccess()
        {
            compClass = typeof(CompFoundationDoorAccess);
        }
    }

    public sealed class CompFoundationDoorAccess : ThingComp
    {
        private bool restricted;
        private bool emergencyAccess;
        private int minimumClearance;
        private List<Pawn> allowedPawns = new List<Pawn>();

        public bool Restricted { get { return restricted; } }
        public bool EmergencyAccess { get { return emergencyAccess; } }
        public int MinimumClearance { get { return minimumClearance; } }
        public IEnumerable<Pawn> AllowedPawns { get { return allowedPawns.Where(pawn => pawn != null && !pawn.Destroyed); } }

        public bool IsAllowed(Pawn pawn)
        {
            if (pawn == null) return false;
            if (allowedPawns.Contains(pawn)) return true;
            return minimumClearance > 0 && Current.Game != null
                && Current.Game.GetComponent<FoundationPersonnelComponent>().LevelFor(pawn) >= minimumClearance;
        }

        public void SetAllowed(Pawn pawn, bool allowed)
        {
            if (pawn == null) return;
            if (allowed)
            {
                if (!allowedPawns.Contains(pawn)) allowedPawns.Add(pawn);
            }
            else allowedPawns.Remove(pawn);
        }

        public void AllowAllCurrentColonists()
        {
            if (!parent.Spawned) return;
            foreach (Pawn pawn in parent.Map.mapPawns.FreeColonistsSpawned) SetAllowed(pawn, true);
        }

        public void ClearAllowed()
        {
            allowedPawns.Clear();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref restricted, "scpDoorRestricted", false);
            Scribe_Values.Look(ref emergencyAccess, "scpDoorEmergencyAccess", false);
            Scribe_Values.Look(ref minimumClearance, "scpDoorMinimumClearance", 0);
            Scribe_Collections.Look(ref allowedPawns, "scpDoorAllowedPawns", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                allowedPawns = (allowedPawns ?? new List<Pawn>()).Where(pawn => pawn != null && !pawn.Destroyed).Distinct().ToList();
                if (!restricted) emergencyAccess = false;
                minimumClearance = System.Math.Max(0, System.Math.Min(FoundationAuthorityPolicy.MaximumEarnedLevel, minimumClearance));
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!parent.Spawned || parent.Faction != Faction.OfPlayer) yield break;

            yield return new Command_Action
            {
                defaultLabel = (restricted ? "SCP_DoorAccessDisable" : "SCP_DoorAccessEnable").Translate(),
                defaultDesc = "SCP_DoorAccessToggleDesc".Translate(),
                icon = ScpIcons.Get(2),
                action = delegate
                {
                    restricted = !restricted;
                    emergencyAccess = false;
                    if (restricted && allowedPawns.Count == 0) AllowAllCurrentColonists();
                }
            };

            if (!restricted) yield break;

            yield return new Command_Action
            {
                defaultLabel = "SCP_DoorAccessRoster".Translate(),
                defaultDesc = "SCP_DoorAccessRosterDesc".Translate(),
                icon = ScpIcons.Get(3),
                action = () => Find.WindowStack.Add(new Dialog_FoundationDoorAccess(this))
            };
            yield return new Command_Action
            {
                defaultLabel = "SCP_DoorClearance".Translate(minimumClearance),
                defaultDesc = "SCP_DoorClearanceDesc".Translate(),
                icon = ScpIcons.Get(4),
                action = delegate
                {
                    minimumClearance = minimumClearance >= FoundationAuthorityPolicy.MaximumEarnedLevel ? 0 : minimumClearance + 1;
                    if (minimumClearance == 1) allowedPawns.Clear();
                }
            };
            yield return new Command_Action
            {
                defaultLabel = (emergencyAccess ? "SCP_DoorEmergencyClose" : "SCP_DoorEmergencyOpen").Translate(),
                defaultDesc = "SCP_DoorEmergencyDesc".Translate(),
                icon = ScpIcons.Get(5),
                action = () => emergencyAccess = !emergencyAccess
            };
        }

        public override string CompInspectStringExtra()
        {
            if (!restricted) return "SCP_DoorAccessUnrestricted".Translate();
            if (emergencyAccess) return "SCP_DoorAccessEmergency".Translate();
            return "SCP_DoorAccessRestrictedClearance".Translate(AllowedPawns.Count(), minimumClearance);
        }
    }
}
