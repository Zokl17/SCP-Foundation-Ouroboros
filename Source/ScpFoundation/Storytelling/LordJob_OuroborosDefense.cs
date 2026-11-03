using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ScpFoundation.Storytelling
{
    // The vanilla colony-assistance lord leaves after 25,000 ticks even during a siege.
    public sealed class LordJob_OuroborosDefense : LordJob
    {
        private IntVec3 fallbackLocation = IntVec3.Invalid;

        public override bool AddFleeToil { get { return false; } }

        public LordJob_OuroborosDefense() { }

        public LordJob_OuroborosDefense(IntVec3 fallbackLocation)
        {
            this.fallbackLocation = fallbackLocation;
        }

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            graph.AddToil(new LordToil_HuntEnemies(fallbackLocation));
            return graph;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref fallbackLocation, "scp882DefenseFallback", IntVec3.Invalid);
        }
    }
}
