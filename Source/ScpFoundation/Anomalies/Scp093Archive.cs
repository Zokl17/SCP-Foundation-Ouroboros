using System.Collections.Generic;
using RimWorld;
using ScpFoundation.Definitions;
using ScpFoundation.Knowledge;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ScpFoundation.Anomalies
{
    public sealed class CompProperties_Scp093Archive : CompProperties
    {
        public CompProperties_Scp093Archive() { compClass = typeof(CompScp093Archive); }
    }

    public sealed class CompScp093Archive : ThingComp
    {
        private bool recovered;
        private Scp093WorldVariant variant = Scp093WorldVariant.Red;

        public void SetVariant(Scp093WorldVariant value) { variant = value; }
        private string BodyKey { get { return "SCP_093Archive" + variant + "Body"; } }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref recovered, "scp093ArchiveRecovered", false);
            Scribe_Values.Look(ref variant, "scp093ArchiveVariant", Scp093WorldVariant.Red);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && recovered && Current.Game != null)
            {
                ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
                if (knowledge != null) knowledge.ClaimScp093ArchiveReward((int)variant);
            }
        }

        public override string CompInspectStringExtra()
        {
            return (recovered ? "SCP_093ArchiveRecovered" : "SCP_093ArchiveUnread").Translate();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action {
                defaultLabel = (recovered ? "SCP_093ArchiveReview" : "SCP_093ArchiveRecover").Translate(),
                defaultDesc = "SCP_093ArchiveActionDesc".Translate(),
                icon = parent.def.uiIcon,
                action = () => { if (recovered) Review(); else ChooseReader(); }
            };
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            if (!parent.Spawned || pawn.Map != parent.Map || !pawn.IsColonistPlayerControlled)
                yield break;
            if (recovered)
            {
                yield return new FloatMenuOption("SCP_093ArchiveReview".Translate(), Review);
                yield break;
            }
            if (CanRead(pawn))
                yield return new FloatMenuOption("SCP_093ArchiveRecover".Translate(), () => Assign(pawn));
        }

        private bool CanRead(Pawn pawn)
        {
            return parent.Spawned && pawn != null && pawn.Spawned && !pawn.Downed
                && pawn.IsColonistPlayerControlled && pawn.Map == parent.Map && pawn.jobs != null
                && pawn.CanReserveAndReach(parent, PathEndMode.Touch, Danger.Some);
        }

        private void ChooseReader()
        {
            if (!parent.Spawned) return;
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (Pawn candidate in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                if (!CanRead(candidate)) continue;
                Pawn reader = candidate;
                options.Add(new FloatMenuOption(reader.LabelShortCap, () => Assign(reader)));
            }
            if (options.Count == 0)
                Messages.Message("SCP_093ArchiveNoReader".Translate(), parent,
                    MessageTypeDefOf.RejectInput, false);
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        private void Assign(Pawn pawn)
        {
            if (!CanRead(pawn)) return;
            JobDef jobDef = DefDatabase<JobDef>.GetNamed("SCP_093ReadArchive");
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, parent), JobTag.Misc);
        }

        private void Review()
        {
            Find.WindowStack.Add(new Dialog_MessageBox(BodyKey.Translate(),
                title: "SCP_093ArchiveTitle".Translate()));
        }

        public void Complete(Pawn reader)
        {
            if (recovered || !parent.Spawned || reader == null || reader.Map != parent.Map) return;
            recovered = true;
            ScpDef anomaly = DefDatabase<ScpDef>.GetNamedSilentFail("SCP_Anomaly093");
            ScpKnowledgeComponent knowledge = Current.Game.GetComponent<ScpKnowledgeComponent>();
            bool firstRecovery = knowledge != null && knowledge.ClaimScp093ArchiveReward((int)variant);
            if (anomaly != null && firstRecovery) knowledge.AddResearch(anomaly, 10000f);
            Find.LetterStack.ReceiveLetter("SCP_093ArchiveTitle".Translate(),
                (firstRecovery ? BodyKey : "SCP_093ArchiveRepeatedBody").Translate(),
                LetterDefOf.NeutralEvent, parent);
            if (variant == Scp093WorldVariant.Violet) WakeSentry();
        }

        private void WakeSentry()
        {
            Map map = parent.Map;
            Faction faction = Faction.OfMechanoids;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther");
            IntVec3 spot = new IntVec3(map.Size.x / 2 + 17, 0, map.Size.z / 2 - 1);
            if (faction == null || kind == null || !spot.InBounds(map) || !spot.Standable(map))
                return;
            Pawn sentry = PawnGenerator.GeneratePawn(kind, faction);
            GenSpawn.Spawn(sentry, spot, map);
            LordMaker.MakeNewLord(faction,
                new LordJob_DefendBase(faction, spot, 10, false), map,
                new List<Pawn> { sentry });
            Find.LetterStack.ReceiveLetter("SCP_093SentryTitle".Translate(),
                "SCP_093SentryBody".Translate(), LetterDefOf.ThreatSmall, sentry);
        }
    }

    public sealed class JobDriver_Scp093ReadArchive : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(420, TargetIndex.A);
            yield return Toils_General.Do(() => {
                Thing archive = job.targetA.Thing;
                CompScp093Archive record = archive == null ? null : archive.TryGetComp<CompScp093Archive>();
                if (record != null) record.Complete(pawn);
            });
        }
    }
}
