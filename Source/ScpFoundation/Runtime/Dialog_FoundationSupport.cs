using System;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Storytelling;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class Dialog_FoundationSupport : Window
    {
        private readonly Map map;
        private Vector2 scrollPosition;
        public override Vector2 InitialSize { get { return new Vector2(880, 700); } }

        public Dialog_FoundationSupport(Map map)
        {
            this.map = map;
            doCloseX = true;
            doCloseButton = false;
        }

        public override void DoWindowContents(Rect rect)
        {
            FoundationSupportComponent support = Current.Game.GetComponent<FoundationSupportComponent>();
            bool unlocked = support.EnsureUnlocked();
            bool online = CompFoundationAccessTerminal.Online(map) != null;
            int now = Find.TickManager.TicksGame;
            int cooldown = support.progress.SupplyCooldownRemaining(now);

            Text.Font = GameFont.Small;
            ScpTerminalUi.Panel(rect, ScpTerminalUi.Background);
            Rect header = new Rect(12f, 9f, rect.width - 24f, 54f);
            ScpTerminalUi.Panel(header);
            ScpTerminalUi.Label(header.ContractedBy(13f), "SCP_SupportTitle".Translate(),
                ScpTerminalUi.AccentColor, GameFont.Medium);
            if (!unlocked)
            {
                Rect locked = new Rect(12f, 73f, rect.width - 24f, rect.height - 136f);
                ScpTerminalUi.Panel(locked);
                Rect notice = new Rect(locked.x + 42f, locked.y + (locked.height - 138f) / 2f,
                    locked.width - 84f, 138f);
                ScpTerminalUi.Panel(notice, ScpTerminalUi.Background);
                ScpTerminalUi.Label(new Rect(notice.x + 16f, notice.y + 12f, notice.width - 32f, 36f),
                    "SCP_SupportLockedHeader".Translate(), ScpTerminalUi.AccentColor,
                    GameFont.Medium, TextAnchor.MiddleCenter);
                ScpTerminalUi.Label(new Rect(notice.x + 22f, notice.y + 54f, notice.width - 44f, 69f),
                    "SCP_SupportLocked".Translate(), ScpTerminalUi.MainText,
                    GameFont.Small, TextAnchor.MiddleCenter);
                if (ScpTerminalUi.Button(new Rect((rect.width - 140f) / 2f, rect.height - 51f, 140f, 34f),
                    "SCP_TerminalClose".Translate())) Close();
                return;
            }

            Rect statusPanel = new Rect(12f, 72f, rect.width - 24f, 90f);
            ScpTerminalUi.Panel(statusPanel);
            ScpTerminalUi.Label(new Rect(statusPanel.x + 13f, statusPanel.y + 4f, statusPanel.width - 26f, 25f),
                "SCP_SupportTrust".Translate(support.progress.trust), ScpTerminalUi.AccentColor);
            ScpTerminalUi.Label(new Rect(statusPanel.x + 13f, statusPanel.y + 33f, statusPanel.width - 26f, 47f),
                "SCP_SupportTrustGain".Translate(support.StableObjectCount), ScpTerminalUi.MutedText,
                GameFont.Small, TextAnchor.UpperLeft);

            Rect requests = new Rect(12f, 172f, rect.width - 24f, rect.height - 235f);
            bool showAnomalyRequest = !support.progress.dossierRequested && !support.NextAnomalyDiscovered;
            Rect content = new Rect(0f, 0f, requests.width - 16f,
                Mathf.Max(requests.height, showAnomalyRequest ? 495f : 389f));
            Widgets.BeginScrollView(requests, ref scrollPosition, content);
            Rect supply = new Rect(0f, 0f, content.width, 143f);
            ScpTerminalUi.Panel(supply);
            ScpTerminalUi.Label(new Rect(supply.x + 13f, supply.y + 7f, supply.width - 26f, 27f),
                "SCP_SupportSupplyTitle".Translate(), ScpTerminalUi.AccentColor);
            ScpTerminalUi.Label(new Rect(supply.x + 13f, supply.y + 36f, supply.width - 26f, 48f),
                "SCP_SupportSupplyDesc".Translate(), ScpTerminalUi.MainText,
                GameFont.Small, TextAnchor.UpperLeft);
            string status = SupplyStatus(online, support.progress, now, cooldown);
            ScpTerminalUi.Label(new Rect(supply.x + 13f, supply.y + 91f, supply.width - 238f, 37f),
                status, ScpTerminalUi.MutedText);
            bool previous = GUI.enabled;
            GUI.enabled = previous && online && support.progress.CanRequestSupplies(now);
            if (ScpTerminalUi.Button(new Rect(supply.xMax - 219f, supply.y + 92f, 205f, 35f),
                "SCP_SupportRequest".Translate()))
                support.RequestSupplies(map);
            GUI.enabled = previous;

            float securityY = supply.yMax + 10f;
            if (showAnomalyRequest)
            {
                Rect anomaly = new Rect(0f, securityY, content.width, 96f);
                DrawAnomaly(anomaly, support, online);
                securityY = anomaly.yMax + 10f;
            }
            DrawSecurity(new Rect(0f, securityY, content.width, 236f), support);
            Widgets.EndScrollView();
            if (ScpTerminalUi.Button(new Rect((rect.width - 140f) / 2f, rect.height - 51f, 140f, 34f),
                "SCP_TerminalClose".Translate())) Close();
        }

        private static string SupplyStatus(bool online, FoundationSupportProgress progress, int now, int cooldown)
        {
            if (!online) return "SCP_SupportOffline".Translate();
            if (progress.trust < FoundationSupportProgress.SupplyTrustCost)
                return "SCP_SupportInsufficient".Translate(FoundationSupportProgress.SupplyTrustCost);
            if (cooldown > 0) return "SCP_SupportCooldown".Translate((cooldown / 60000f).ToString("0.0"));
            return "SCP_SupportSupplyCost".Translate(FoundationSupportProgress.SupplyTrustCost);
        }

        private static void DrawPending(Rect rect, string titleKey, string bodyKey)
        {
            ScpTerminalUi.Panel(rect);
            ScpTerminalUi.Label(new Rect(rect.x + 12, rect.y + 10, rect.width - 24, 26),
                titleKey.Translate(), ScpTerminalUi.AccentColor);
            ScpTerminalUi.Label(new Rect(rect.x + 12, rect.y + 38, rect.width - 24, 34),
                bodyKey.Translate(), ScpTerminalUi.MutedText);
        }

        private void DrawSecurity(Rect rect, FoundationSupportComponent support)
        {
            ScpTerminalUi.Panel(rect);
            ScpTerminalUi.Label(new Rect(rect.x + 13, rect.y + 7, rect.width - 26, 27),
                "SCP_SupportSecurityTitle".Translate(), ScpTerminalUi.AccentColor);
            ScpTerminalUi.Label(new Rect(rect.x + 13, rect.y + 35, rect.width - 26, 41),
                "SCP_SupportSecurityDesc".Translate(), ScpTerminalUi.MainText,
                GameFont.Small, TextAnchor.UpperLeft);
            string nu7Reason;
            bool nu7Available = support.CanRequestNu7(map, out nu7Reason);
            ScpTerminalUi.Label(new Rect(rect.x + 13, rect.y + 78, rect.width - 245, 45),
                nu7Reason ?? "", ScpTerminalUi.MutedText);
            bool previous = GUI.enabled;
            GUI.enabled = previous && nu7Available;
            if (ScpTerminalUi.Button(new Rect(rect.xMax - 219, rect.y + 77, 205, 35),
                "SCP_SupportSecurityRequest".Translate()))
                support.RequestNu7(map);
            GUI.enabled = previous;

            Widgets.DrawBoxSolid(new Rect(rect.x + 10, rect.y + 128, rect.width - 20, 1), ScpTerminalUi.BorderColor);
            ScpTerminalUi.Label(new Rect(rect.x + 13, rect.y + 136, rect.width - 26, 41),
                "SCP_SupportEpsilonDesc".Translate(), ScpTerminalUi.MainText,
                GameFont.Small, TextAnchor.UpperLeft);
            string epsilonReason;
            bool epsilonAvailable = support.CanRequestEpsilon11(map, out epsilonReason);
            ScpTerminalUi.Label(new Rect(rect.x + 13, rect.y + 180, rect.width - 245, 45),
                epsilonReason ?? "", ScpTerminalUi.MutedText);
            GUI.enabled = previous && epsilonAvailable;
            if (ScpTerminalUi.Button(new Rect(rect.xMax - 219, rect.y + 180, 205, 35),
                "SCP_SupportEpsilonRequest".Translate()))
                support.RequestEpsilon11(map);
            GUI.enabled = previous;
        }

        private void DrawAnomaly(Rect rect, FoundationSupportComponent support, bool online)
        {
            ScpTerminalUi.Panel(rect);
            ScpTerminalUi.Label(new Rect(rect.x + 13, rect.y + 7, rect.width - 26, 27),
                "SCP_SupportAnomalyTitle".Translate(), ScpTerminalUi.AccentColor);
            TooltipHandler.TipRegion(rect, "SCP_055CargoNeedRoom".Translate());
            string status = support.NextAnomalyDiscovered ? "SCP_055CargoRegistered".Translate().ToString()
                : support.progress.dossierRequested ? "SCP_SupportAnomalyDone".Translate().ToString()
                : support.progress.trust < FoundationSupportProgress.DossierTrustCost
                    ? "SCP_SupportInsufficient".Translate(FoundationSupportProgress.DossierTrustCost).ToString()
                    : "SCP_SupportAnomalyCost".Translate(FoundationSupportProgress.DossierTrustCost).ToString();
            ScpTerminalUi.Label(new Rect(rect.x + 13, rect.y + 39, rect.width - 245, 45),
                status, ScpTerminalUi.MutedText);
            bool previous = GUI.enabled;
            GUI.enabled = previous && online && support.progress.CanRequestDossier(support.NextAnomalyDiscovered);
            if (ScpTerminalUi.Button(new Rect(rect.xMax - 219, rect.y + 38, 205, 35),
                "SCP_SupportAnomalyRequest".Translate()))
                support.RequestNextAnomaly(map);
            GUI.enabled = previous;
        }
    }
}
