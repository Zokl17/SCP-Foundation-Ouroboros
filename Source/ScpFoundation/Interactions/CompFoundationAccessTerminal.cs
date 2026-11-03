using System.Collections.Generic;
using System.Linq;
using ScpFoundation.Runtime;
using ScpFoundation.Storytelling;
using RimWorld;
using Verse;

namespace ScpFoundation.Interactions
{
    public sealed class CompProperties_FoundationAccessTerminal : CompProperties
    {
        public CompProperties_FoundationAccessTerminal() { compClass = typeof(CompFoundationAccessTerminal); }
    }
    public sealed class CompFoundationAccessTerminal : ThingComp
    {
        private int quarantinedUntilTick;
        public bool IsQuarantined { get { return Find.TickManager.TicksGame < quarantinedUntilTick; } }

        public void Quarantine(int ticks)
        {
            quarantinedUntilTick = System.Math.Max(quarantinedUntilTick, Find.TickManager.TicksGame + ticks);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref quarantinedUntilTick, "scp079QuarantinedUntilTick", 0);
        }

        public static Thing Online(Map map)
        {
            if (map == null || !Find.Maps.Contains(map) || !map.IsPlayerHome) return null;
            return map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("SCPV1_AccessTerminal"))
                .FirstOrDefault(thing => ScpSurveillance.Powered(thing)
                    && thing.TryGetComp<CompFoundationAccessTerminal>() != null
                    && !thing.TryGetComp<CompFoundationAccessTerminal>().IsQuarantined);
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Action {
                defaultLabel = "SCP_ContactButton".Translate(), defaultDesc = "SCP_ContactHelp".Translate(),
                icon = ScpIcons.Get(3), action = () => Find.WindowStack.Add(new Dialog_FoundationContact(parent.Map))
            };
        }
        public override string CompInspectStringExtra()
        {
            string status = IsQuarantined
                ? "SCP_079TerminalQuarantine".Translate(
                    ((quarantinedUntilTick - Find.TickManager.TicksGame) / 2500f).ToString("0.0"))
                : (ScpSurveillance.Powered(parent)
                    ? Current.Game.GetComponent<FoundationContactComponent>().StatueDiscovered
                        ? "SCP_ContactOnline" : "SCP_ContactUnavailable"
                    : "SCP_ContactOffline").Translate();
            string text = status
                + "\n" + ("SCP_ContactStage_" + Current.Game.GetComponent<FoundationContactComponent>().progress.stage).Translate();
            FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
            if (support.progress.unlocked) text += "\n" + "SCP_SupportTrustShort".Translate(support.progress.trust);
            return text;
        }
    }
}
