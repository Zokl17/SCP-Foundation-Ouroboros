using RimWorld;
using ScpFoundation.Definitions;
using Verse;

namespace ScpFoundation.Interactions
{
    [DefOf]
    public static class ScpInteractionDefOf
    {
        public static ThingDef SCP_ContainmentAnchor;
        public static ThingDef SCP_Statue;
        public static ThingDef SCP_AntimemeticObject;
        public static JobDef SCP_Capture;
        public static JobDef SCP_Observe;
        public static JobDef SCP_Study;
        public static JobDef SCP_Restore;
        public static JobDef SCP_Monitor;
        public static JobDef SCP_ChaosStealObject;
        public static JobDef SCP_882Load;
        public static JobDef SCP_882Inspect;
        public static JobDef SCP_882ReplaceRopes;
        public static JobDef SCP_882Unload;
        public static ThingDef SCPV1_GlassWall;
        public static ThingDef SCPV1_Camera;
        public static ThingDef SCPV1_MonitoringConsole;
        public static ThingDef SCPV1_MonitoringDisplay;
        public static ThingDef SCPV1_Sensor;
        public static ThingDef SCPV1_AccessTerminal;
        public static ThingDef SCPV1_DeconGate;
        public static ThingDef SCPV1_Shutter_Closed;
        public static ThingDef SCPV1_EmergencyAccumulator;
        public static EffecterDef WatchingTelevision;
        public static ThoughtDef SCP_Startled650;
        public static IncidentDef SCP_Discover650;
        public static IncidentDef SCP_Discover055;
        public static ScpDef SCP_Anomaly055;
        static ScpInteractionDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(ScpInteractionDefOf)); }
    }
}
