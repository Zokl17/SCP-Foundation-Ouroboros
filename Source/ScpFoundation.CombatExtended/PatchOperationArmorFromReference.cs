using System;
using System.Globalization;
using System.Xml;
using Verse;

namespace ScpFoundation.CombatExtendedCompat
{
    // Runs after CE's XML patches, while the vanilla abstract armor bases are still available.
    public sealed class PatchOperationArmorFromReference : PatchOperation
    {
        public string sourceDef;
        public string fallbackSourceDef;
        public string targetDef;
        public float multiplier = 1f;
        public float bulk;
        public float wornBulk;

        protected override bool ApplyWorker(XmlDocument xml)
        {
            if (xml == null || string.IsNullOrEmpty(sourceDef) || string.IsNullOrEmpty(targetDef)
                || multiplier <= 0f || bulk <= 0f || wornBulk <= 0f)
                return false;

            XmlNode source = FindStats(xml, sourceDef, true);
            if (source == null && !string.IsNullOrEmpty(fallbackSourceDef))
                source = FindStats(xml, fallbackSourceDef, true);
            XmlNode target = FindStats(xml, targetDef, false);
            if (source == null || target == null)
                return false;

            // A missing CE patch leaves vanilla fractions on the reference; never treat them as RHA.
            decimal sharp;
            decimal blunt;
            decimal heat;
            if (!ReadRating(source, "ArmorRating_Sharp", out sharp) || sharp < 2m
                || !ReadRating(source, "ArmorRating_Blunt", out blunt) || blunt < 2m
                || !ReadRating(source, "ArmorRating_Heat", out heat))
                return false;

            decimal factor = Convert.ToDecimal(multiplier);
            SetStat(target, "ArmorRating_Sharp", sharp * factor);
            SetStat(target, "ArmorRating_Blunt", blunt * factor);
            SetStat(target, "ArmorRating_Heat", heat * factor);
            SetStat(target, "Bulk", Convert.ToDecimal(bulk));
            SetStat(target, "WornBulk", Convert.ToDecimal(wornBulk));
            return true;
        }

        private static XmlNode FindStats(XmlDocument xml, string defName, bool abstractBase)
        {
            // These names come from our own patch file, but keep XPath quoting unambiguous.
            if (defName.IndexOfAny(new[] { '\'', '"', '[', ']' }) >= 0) return null;
            string predicate = abstractBase ? "@Name" : "defName";
            return xml.SelectSingleNode("/Defs/ThingDef[" + predicate + "='" + defName + "']/statBases");
        }

        private static bool ReadRating(XmlNode stats, string name, out decimal value)
        {
            XmlNode node = stats.SelectSingleNode(name);
            return decimal.TryParse(node == null ? null : node.InnerText, NumberStyles.Float,
                CultureInfo.InvariantCulture, out value);
        }

        private static void SetStat(XmlNode stats, string name, decimal value)
        {
            XmlNode node = stats.SelectSingleNode(name);
            if (node == null)
            {
                node = stats.OwnerDocument.CreateElement(name);
                stats.AppendChild(node);
            }
            node.InnerText = value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
