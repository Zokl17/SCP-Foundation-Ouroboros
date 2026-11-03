using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ScpFoundation.Interactions
{
    [DefOf]
    public static class ScpGameConditionDefOf
    {
        public static GameConditionDef SolarFlare;
        static ScpGameConditionDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(ScpGameConditionDefOf)); }
    }

    public sealed class CompProperties_EmergencyAccumulator : CompProperties
    {
        public CompProperties_EmergencyAccumulator() { compClass = typeof(CompEmergencyAccumulator); }
    }

    public sealed class CompEmergencyAccumulator : ThingComp
    {
        private int reserveTicks;
        private bool backupAllEquipment;
        private PowerNet previousNetwork;
        private bool previouslyActive, previousAllEquipment;
        private readonly List<CompPowerTrader> previousConsumers = new List<CompPowerTrader>();

        private bool SolarFlare
        {
            get { return parent.Spawned && parent.Map.gameConditionManager.ConditionIsActive(ScpGameConditionDefOf.SolarFlare); }
        }

        private bool SwitchedOn
        {
            get
            {
                CompFlickable flick = parent.TryGetComp<CompFlickable>();
                return flick == null || flick.SwitchIsOn;
            }
        }

        public bool ActiveReserve
        {
            get
            {
                CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
                return EmergencyPowerPolicy.CanBackUp(reserveTicks, SolarFlare, SwitchedOn,
                    power != null && power.PowerNet != null);
            }
        }
        public float Charge { get { return (float)reserveTicks / EmergencyPowerPolicy.MaximumReserveTicks; } }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            UpdateConsumption(parent.TryGetComp<CompPowerTrader>());
        }

        private void UpdateConsumption(CompPowerTrader power)
        {
            if (power == null) return;
            float requested = -EmergencyPowerPolicy.PowerConsumption(reserveTicks);
            if (power.PowerOutput != requested) power.PowerOutput = requested;
        }

        public override void CompTick()
        {
            if (!parent.Spawned || parent.Faction != Faction.OfPlayer) return;
            CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
            reserveTicks = EmergencyPowerPolicy.Update(reserveTicks, 1,
                SwitchedOn && power != null && power.PowerOn,
                SolarFlare && SwitchedOn && power != null && power.PowerNet != null);
            UpdateConsumption(power);
            RefreshBackupSignals(power);
        }

        private void RefreshBackupSignals(CompPowerTrader power)
        {
            PowerNet network = power == null ? null : power.PowerNet;
            bool active = ActiveReserve;
            if (network == previousNetwork && active == previouslyActive && backupAllEquipment == previousAllEquipment) return;
            // Lamps and other event-driven appliances also need a native power signal
            // when reserve coverage changes, including a mode change during a flare.
            var changed = new List<CompPowerTrader>(previousConsumers);
            if (network != null && (active || previouslyActive))
                foreach (CompPowerTrader consumer in network.powerComps)
                    if (!changed.Contains(consumer)) changed.Add(consumer);
            previousNetwork = network;
            previouslyActive = active;
            previousAllEquipment = backupAllEquipment;
            previousConsumers.Clear();
            if (active && network != null) previousConsumers.AddRange(network.powerComps);
            foreach (CompPowerTrader consumer in changed)
                if (consumer != null && consumer.parent.Spawned)
                    consumer.parent.BroadcastCompSignal(consumer.PowerOn ? "PowerTurnedOn" : "PowerTurnedOff");
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            base.PostDeSpawn(map, mode);
            foreach (CompPowerTrader consumer in previousConsumers)
                if (consumer != null && consumer.parent.Spawned)
                    consumer.parent.BroadcastCompSignal(consumer.PowerOn ? "PowerTurnedOn" : "PowerTurnedOff");
            previousConsumers.Clear();
            previousNetwork = null;
            previouslyActive = false;
        }

        public bool ProvidesBackup(Thing target)
        {
            if (!ActiveReserve || target == null || !target.Spawned || target.Map != parent.Map
                || target.Faction != Faction.OfPlayer || !CanReceiveBackup(target)
                || !backupAllEquipment && !Supported(target.def)) return false;
            return SamePowerNetwork(parent.TryGetComp<CompPowerTrader>(), target.TryGetComp<CompPowerTrader>());
        }

        public static bool SamePowerNetwork(CompPower source, CompPower target)
        {
            return source != null && target != null && source.PowerNet != null && source.PowerNet == target.PowerNet;
        }

        private static bool CanReceiveBackup(Thing target)
        {
            CompPowerTrader power = target.TryGetComp<CompPowerTrader>();
            // Never charge another reserve or turn a generator into a protected consumer.
            if (power == null || power.PowerNet == null || power is CompPowerPlant
                || power.PowerOutput > 0f || power.Props.PowerConsumption < 0f
                || target.TryGetComp<CompEmergencyAccumulator>() != null) return false;
            CompFlickable flick = target.TryGetComp<CompFlickable>();
            CompBreakdownable breakdown = target.TryGetComp<CompBreakdownable>();
            CompStunnable stun = target.TryGetComp<CompStunnable>();
            CompRefuelable fuel = target.TryGetComp<CompRefuelable>();
            return (flick == null || flick.SwitchIsOn) && (breakdown == null || !breakdown.BrokenDown)
                && (stun == null || !stun.StunHandler.Stunned) && (fuel == null || fuel.HasFuel)
                && !target.Map.GetComponent<FoundationEmpShutdownComponent>().IsDisabled(target);
        }

        public static bool BackupFor(Thing target)
        {
            if (target == null || !target.Spawned || target.Faction != Faction.OfPlayer
                || ScpInteractionDefOf.SCPV1_EmergencyAccumulator == null
                || !target.Map.gameConditionManager.ConditionIsActive(ScpGameConditionDefOf.SolarFlare)) return false;
            foreach (Thing thing in target.Map.listerThings.ThingsOfDef(ScpInteractionDefOf.SCPV1_EmergencyAccumulator))
            {
                if (thing.Faction != Faction.OfPlayer) continue;
                CompEmergencyAccumulator comp = thing.TryGetComp<CompEmergencyAccumulator>();
                if (comp != null && comp.ProvidesBackup(target)) return true;
            }
            return false;
        }

        private static bool Supported(ThingDef definition)
        {
            return definition == ScpInteractionDefOf.SCPV1_AccessTerminal
                || definition == ScpInteractionDefOf.SCPV1_MonitoringConsole
                || definition == ScpInteractionDefOf.SCPV1_MonitoringDisplay
                || definition == ScpInteractionDefOf.SCPV1_Camera
                || definition == ScpInteractionDefOf.SCPV1_Sensor
                || definition == ScpInteractionDefOf.SCPV1_Shutter_Closed;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref reserveTicks, "scpEmergencyReserveTicks", 0);
            Scribe_Values.Look(ref backupAllEquipment, "scpEmergencyBackupAllEquipment", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                reserveTicks = System.Math.Max(0, System.Math.Min(EmergencyPowerPolicy.MaximumReserveTicks, reserveTicks));
        }

        public override string CompInspectStringExtra()
        {
            CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
            string state = !SwitchedOn ? "SCP_EmergencyPowerOff"
                : power == null || power.PowerNet == null ? "SCP_EmergencyPowerDisconnected"
                : ActiveReserve ? "SCP_EmergencyPowerActive" : SolarFlare ? "SCP_EmergencyPowerEmpty"
                : reserveTicks >= EmergencyPowerPolicy.MaximumReserveTicks ? "SCP_EmergencyPowerReady" : "SCP_EmergencyPowerCharging";
            double shownCharge = System.Math.Floor(Charge * 100f) / 100d;
            return "SCP_EmergencyPowerCharge".Translate(shownCharge.ToString("P0")) + "\n" + state.Translate()
                + "\n" + "SCP_EmergencyPowerMode".Translate(ModeLabel);
        }

        private string ModeLabel
        {
            get { return (backupAllEquipment ? "SCP_EmergencyPowerModeAll" : "SCP_EmergencyPowerModeFoundation").Translate(); }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Toggle {
                defaultLabel = ModeLabel,
                defaultDesc = "SCP_EmergencyPowerModeDesc".Translate(),
                icon = TexCommand.DesirePower,
                isActive = () => backupAllEquipment,
                toggleAction = () => {
                    backupAllEquipment = !backupAllEquipment;
                    RefreshBackupSignals(parent.TryGetComp<CompPowerTrader>());
                }
            };
        }
    }

    // Ordinary appliances consult the native PowerOn property; Foundation devices
    // also retain the existing explicit reserve fallback in ScpSurveillance.
    [HarmonyPatch(typeof(CompPowerTrader), "PowerOn", MethodType.Getter)]
    internal static class EmergencyPowerTraderPatch
    {
        private static void Postfix(CompPowerTrader __instance, ref bool __result)
        {
            if (!__result && __instance.parent != null)
                __result = CompEmergencyAccumulator.BackupFor(__instance.parent);
        }
    }
}
