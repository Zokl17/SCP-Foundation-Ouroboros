using System.Collections.Generic;
using System.Linq;
using System.Text;
using ScpFoundation.Anomalies;
using ScpFoundation.Definitions;
using ScpFoundation.Storytelling;
using Verse;

namespace ScpFoundation.Knowledge
{
    // Standalone articles, plus the retained dossier of SCP-650 after its return.
    public static class ScpArticleCatalog
    {
        private static readonly int[] Numbers = { 49, 106, 127, 131, 354, 500, 882, 914, 999 };

        public static IEnumerable<ScpDef> Articles
        {
            get
            {
                foreach (int number in Numbers)
                {
                    ScpDef definition = ForNumber(number);
                    if (definition != null) yield return definition;
                }
                if (Current.Game != null && Current.Game.GetComponent<Scp650ReturnComponent>().returned)
                {
                    ScpDef returnedStatue = ForNumber(650);
                    if (returnedStatue != null) yield return returnedStatue;
                }
            }
        }

        public static ScpDef ForNumber(int number)
        {
            return DefDatabase<ScpDef>.GetNamedSilentFail("SCP_Anomaly" + number.ToString("000"));
        }

        public static Thing Representative(Map map, ScpDef article)
        {
            if (map == null || article == null) return null;
            Thing representative;
            switch (article.scpId)
            {
                case "SCP-049":
                    representative = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p is Pawn_Scp049 && !p.Dead && !p.Position.Fogged(map));
                    break;
                case "SCP-106":
                    Thing roaming = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p is Pawn_Scp106 && !p.Dead && !p.Position.Fogged(map));
                    representative = roaming ?? OfDef(map, "SCP_106BaitStation").FirstOrDefault(t =>
                    {
                        CompLarryProtocol protocol = t.TryGetComp<CompLarryProtocol>();
                        return protocol != null && protocol.Contains106;
                    });
                    break;
                case "SCP-500":
                    Thing pill = OfDef(map, "SCP_500Pill").FirstOrDefault();
                    ThingDef pillDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_500Pill");
                    representative = pill ?? (pillDef == null ? null : map.mapPawns.FreeColonistsSpawned
                        .Where(p => p.inventory != null)
                        .SelectMany(p => p.inventory.innerContainer)
                        .FirstOrDefault(t => t.def == pillDef));
                    break;
                case "SCP-127":
                    Thing gun = OfDef(map, "SCP_127Gun").FirstOrDefault();
                    ThingDef gunDef = DefDatabase<ThingDef>.GetNamedSilentFail("SCP_127Gun");
                    representative = gun ?? (gunDef == null ? null : map.mapPawns.AllPawnsSpawned
                        .Where(p => p.equipment != null || p.inventory != null)
                        .Select(p => p.equipment != null && p.equipment.Primary != null
                                && p.equipment.Primary.def == gunDef ? p.equipment.Primary
                            : p.inventory == null ? null : p.inventory.innerContainer.FirstOrDefault(t => t.def == gunDef))
                        .FirstOrDefault(t => t != null));
                    break;
                case "SCP-882":
                    Thing exposed = OfDef(map, "SCP_882Object").FirstOrDefault();
                    representative = exposed ?? OfDef(map, "SCP_882Core").Concat(OfDef(map, "SCP_882CoreFull"))
                        .FirstOrDefault(t =>
                        {
                            CompScp882 tank = t.TryGetComp<CompScp882>();
                            return tank != null && tank.ContainsObject;
                        });
                    break;
                case "SCP-999":
                    representative = OfDef(map, "SCP_999Object").FirstOrDefault();
                    break;
                case "SCP-131":
                    representative = (Thing)map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.def.defName == "SCP_131APawn" || p.def.defName == "SCP_131BPawn")
                        ?? OfDef(map, "SCP_131A").Concat(OfDef(map, "SCP_131B")).FirstOrDefault();
                    break;
                case "SCP-914":
                    representative = OfDef(map, "SCP_914Machine").FirstOrDefault();
                    break;
                case "SCP-354":
                    representative = OfDef(map, "SCP_354Lake").FirstOrDefault();
                    break;
                default:
                    return null;
            }
            return representative != null && (!representative.Spawned || !representative.Position.Fogged(map))
                ? representative : null;
        }

        private static IEnumerable<Thing> OfDef(Map map, string name)
        {
            ThingDef definition = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            return definition == null ? Enumerable.Empty<Thing>()
                : map.listerThings.ThingsOfDef(definition).Where(t => t.Spawned && !t.Position.Fogged(map));
        }

        public static string Dossier(ScpDef article, ScpKnowledgeComponent knowledge)
        {
            if (article == null || knowledge == null || article.research == null) return "SCP_DossierUnknown".Translate();
            KnowledgeLevel level = knowledge.LevelFor(article);
            ScpResearchSpec spec = article.research;
            if (level == KnowledgeLevel.Unknown)
                return "SCP_ArchiveUnknown".Translate() + "\n\n" + Progress(article, knowledge, level);
            string dossier = spec.observedReportKey.Translate();
            if (level >= KnowledgeLevel.PartiallyDocumented) dossier += "\n\n" + spec.partialReportKey.Translate();
            if (level >= KnowledgeLevel.Documented) dossier += "\n\n" + spec.documentedReportKey.Translate();
            if (article.scpId == "SCP-650" && Current.Game != null
                && Current.Game.GetComponent<Scp650ReturnComponent>().returned)
                dossier = "SCP_650ReturnArchived".Translate() + "\n\n" + dossier;
            return dossier + "\n\n" + (level == KnowledgeLevel.Documented
                ? "SCP_ResearchComplete".Translate().ToString() + ExperimentDossierText(article, knowledge)
                : Progress(article, knowledge, level));
        }

        public static string ExperimentDossierText(ScpDef article, ScpKnowledgeComponent knowledge)
        {
            var text = new StringBuilder();
            int completed = knowledge.ExperimentStageFor(article);
            List<string> keys = article.research.experimentReportKeys;
            if (keys != null)
                for (int stage = 0; stage < completed && stage < keys.Count; stage++)
                    if (!string.IsNullOrEmpty(keys[stage])) text.Append("\n\n").Append(keys[stage].Translate());
            return text.Append("\n\n").Append(ExperimentProgressText(article, knowledge)).ToString();
        }

        public static string ExperimentProgressText(ScpDef article, ScpKnowledgeComponent knowledge)
        {
            int stage = knowledge.ExperimentStageFor(article);
            if (stage >= 3)
            {
                int first = ResearchModel.ExperimentTrust(article.objectClass, 1);
                int second = ResearchModel.ExperimentTrust(article.objectClass, 2);
                int third = ResearchModel.ExperimentTrust(article.objectClass, 3);
                return "SCP_ExperimentsComplete".Translate(first, second, third, first + second + third);
            }
            float required = article.research.observedWork;
            float work = ResearchModel.ExperimentStageWork(knowledge.ExperimentWork(article), required);
            return "SCP_ExperimentProgress".Translate(stage + 1, (int)work, (int)required,
                ResearchModel.ExperimentTrust(article.objectClass, stage + 1));
        }

        private static string Progress(ScpDef article, ScpKnowledgeComponent knowledge, KnowledgeLevel level)
        {
            ScpResearchSpec spec = article.research;
            float work = ResearchModel.CurrentStageWork(knowledge.ResearchWork(article), (int)level,
                spec.observedWork, spec.partialWork, spec.fullWork);
            float required = ResearchModel.NextStageWork((int)level,
                spec.observedWork, spec.partialWork, spec.fullWork);
            return "SCP_ArchiveProgress".Translate((int)level + 1, (int)work, (int)required);
        }
    }
}
