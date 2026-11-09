using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using ScpFoundation.Anomalies;
using ScpFoundation.Interactions;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Debugging
{
    public static class ScpSliceDebugActions
    {
        [DebugAction("SCP Foundation", "Spawn SCP-650 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn650() { ScpAnomalyDebugSpawn.Spawn("SCP_Statue"); }

        [DebugAction("SCP Foundation", "Spawn SCP-067 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn067() { ScpAnomalyDebugSpawn.Spawn("SCP_067Pen"); }

        [DebugAction("SCP Foundation", "Spawn SCP-262 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn262() { ScpAnomalyDebugSpawn.Spawn("SCP_262Coat"); }

        [DebugAction("SCP Foundation", "Spawn SCP-316 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn316() { ScpAnomalyDebugSpawn.Spawn("SCP_316Lamp"); }

        [DebugAction("SCP Foundation", "Spawn SCP-131-A at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn131A() { ScpAnomalyDebugSpawn.Spawn("SCP_131A"); }

        [DebugAction("SCP Foundation", "Spawn SCP-131-B at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn131B() { ScpAnomalyDebugSpawn.Spawn("SCP_131B"); }

        [DebugAction("SCP Foundation", "Spawn SCP-012 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn012() { ScpAnomalyDebugSpawn.Spawn("SCP_012Score"); }

        [DebugAction("SCP Foundation", "Spawn SCP-093 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn093() { ScpAnomalyDebugSpawn.Spawn("SCP_093Disc"); }

        [DebugAction("SCP Foundation", "Spawn SCP-914 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn914() { ScpAnomalyDebugSpawn.Spawn("SCP_914Machine"); }

        [DebugAction("SCP Foundation", "Spawn SCP-513 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn513() { ScpAnomalyDebugSpawn.Spawn("SCP_513Bell"); }

        [DebugAction("SCP Foundation", "Spawn SCP-207 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn207() { ScpAnomalyDebugSpawn.Spawn("SCP_207Bottle"); }

        [DebugAction("SCP Foundation", "Spawn SCP-458 at cursor", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn458() { ScpAnomalyDebugSpawn.Spawn("SCP_458Box"); }

        public static void SpawnAnsweringSignalTransponder()
        { ScpAnomalyDebugSpawn.Spawn("SCP_OuroborosTransponder"); }

        public static List<DebugActionNode> TriggerArrivalMenu()
        {
            var nodes = new List<DebugActionNode>();
            AddArrival(nodes, "SCP-131 warning, then SCP-173", "SCP_Visit131");
            AddArrival(nodes, "SCP-207 accelerated visitor", "SCP_Discover207");
            AddArrival(nodes, "SCP-513 lost muffalo", "SCP_Discover513");
            AddArrival(nodes, "SCP-999 (colony mood below 30%)", "SCP_Visit999");
            AddArrival(nodes, "SCP-049 (3 down, 2 standing)", "SCP_Visit049");
            AddArrival(nodes, "SCP-106 breach (bait station)", "SCP_Breach106");
            AddArrival(nodes, "SCP-173 direct sighting", "SCP_Discover173");
            AddArrival(nodes, "SCP-354 pool", "SCP_Discover354");
            AddArrival(nodes, "SCP-882 discovery", "SCP_Discover882");
            AddArrival(nodes, "SCP-012 sheet music", "SCP_Discover012");
            nodes.Add(new DebugActionNode("SCP-330 village quest", action: ScpDebugActions.OfferScp330VillageQuest));
            AddArrival(nodes, "SCP-914 clockwork", "SCP_Discover914");
            return nodes;
        }

        private static void AddArrival(List<DebugActionNode> nodes, string label, string incidentName)
        {
            nodes.Add(new DebugActionNode(label, action: () => TriggerArrival(incidentName)));
        }

        private static void TriggerArrival(string incidentName)
        {
            Map map = Find.CurrentMap;
            if (!Prefs.DevMode || map == null) return;
            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail(incidentName);
            IncidentParms parms = incident == null ? null
                : StorytellerUtility.DefaultParmsNow(incident.category, map);
            if (incident == null || !incident.Worker.TryExecute(parms))
                Messages.Message("SCP_DebugArrivalUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        public static void AddFoundationTrust()
        {
            if (!Prefs.DevMode || Current.Game == null || Find.CurrentMap == null) return;
            FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
            if (support == null) return;
            int previous = support.progress.trust;
            support.progress.trust = System.Math.Min(FoundationSupportProgress.MaximumTrust, previous + 10);
            Messages.Message("SCP_DebugTrustAdded".Translate(support.progress.trust - previous,
                support.progress.trust), MessageTypeDefOf.PositiveEvent, false);
        }

        public static void Discover()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, Find.CurrentMap);
            if (!ScpInteractionDefOf.SCP_Discover650.Worker.TryExecute(parms))
                Messages.Message("SCP_DiscoveryUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        public static void Discover055()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            IncidentWorker_Discover055 worker = ScpInteractionDefOf.SCP_Discover055.Worker as IncidentWorker_Discover055;
            if (worker == null || !worker.TryExecuteDebug(Find.CurrentMap))
                Messages.Message("SCP_DiscoveryUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        public static void Discover504()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("SCP_Discover504");
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, Find.CurrentMap);
            if (incident == null || !incident.Worker.TryExecute(parms))
                Messages.Message("SCP_504DiscoveryUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("SCP Foundation", "Spawn SCP-504 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn504()
        {
            ScpAnomalyDebugSpawn.Spawn("SCP_504Bush");
        }

        public static void Ripen504()
        {
            Map map = Find.CurrentMap;
            if (!Prefs.DevMode || map == null) return;
            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map)) return;
            foreach (Thing thing in cell.GetThingList(map))
            {
                CompScp504Bush bush = thing.TryGetComp<CompScp504Bush>();
                if (bush == null) continue;
                if (!CompScpPost.IsObjectAtPost(thing))
                {
                    Messages.Message("SCP_504NeedsPost".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }
                bool ripened = bush.DebugRipenOneFruit();
                Messages.Message("SCP_504RipeCount".Translate(bush.RipeCount, Scp504Policy.FruitCapacity), thing,
                    ripened ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            Messages.Message("SCP_504DebugNoBush".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        public static void Discover939()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("SCP_Discover939");
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, Find.CurrentMap);
            if (incident == null || !incident.Worker.TryExecute(parms))
                Messages.Message("SCP_939DiscoveryUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("SCP Foundation", "Spawn SCP-939 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn939()
        {
            ScpAnomalyDebugSpawn.Spawn("SCP_939Object");
        }

        public static void Discover682()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("SCP_Discover682");
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, Find.CurrentMap);
            if (incident == null || !incident.Worker.TryExecute(parms))
                Messages.Message("SCP_682DiscoveryUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("SCP Foundation", "Spawn SCP-682 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn682()
        {
            ScpAnomalyDebugSpawn.Spawn("SCP_682Object");
        }

        [DebugAction("SCP Foundation", "Spawn SCP-882 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Discover882()
        {
            ScpAnomalyDebugSpawn.Spawn("SCP_882Object");
        }

        public static void Spawn882MetalHumanoid()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            if (CompScp882.SpawnHumanoidAt(Find.CurrentMap, UI.MouseCell()) == null)
                Messages.Message("Cannot spawn SCP-882 metal humanoid here.", MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("SCP Foundation", "Spawn SCP-127 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn127()
        {
            ScpAnomalyDebugSpawn.Spawn("SCP_127Gun");
        }

        [DebugAction("SCP Foundation", "Spawn SCP-106 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Breach106()
        {
            ScpAnomalyDebugSpawn.Spawn("SCP_106Entity");
        }

        [DebugAction("SCP Foundation", "Spawn SCP-049 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Visit049()
        {
            ScpAnomalyDebugSpawn.Spawn("SCP_049Entity");
        }

        [DebugAction("SCP Foundation", "Spawn SCP-999 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn999()
        {
            ScpAnomalyDebugSpawn.Spawn("SCP_999Object");
        }

        [DebugAction("SCP Foundation", "Spawn SCP-294 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn294()
        {
            ScpAnomalyDebugSpawn.Spawn("SCP_294Machine");
        }

        [DebugAction("SCP Foundation", "Spawn SCP-096 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn096()
        {
            ScpAnomalyDebugSpawn.Spawn("SCP_096Object");
        }

        [DebugAction("SCP Foundation", "Spawn SCP-173 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn173()
        {
            ScpAnomalyDebugSpawn.Spawn("SCP_173Object");
        }

        [DebugAction("SCP Foundation", "Spawn SCP-354 at cursor", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Spawn354()
        {
            if (!Prefs.DevMode || Find.CurrentMap == null) return;
            Map map = Find.CurrentMap;
            ThingDef lake = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_354Lake");
            IntVec3 cell = UI.MouseCell();
            if (lake == null || !IncidentWorker_Discover354.SafeSite(cell, map, lake))
            {
                Messages.Message("SCP_354NoSite".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Thing pool = GenSpawn.Spawn(ThingMaker.MakeThing(lake), cell, map);
            Find.Selector.Select(pool);
        }
    }
}
