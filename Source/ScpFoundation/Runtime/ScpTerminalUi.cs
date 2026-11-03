using RimWorld;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    internal static class ScpTerminalUi
    {
        internal static readonly Color Background = new Color(0.055f, 0.085f, 0.105f);
        internal static readonly Color PanelColor = new Color(0.075f, 0.115f, 0.135f);
        internal static readonly Color BorderColor = new Color(0.18f, 0.31f, 0.34f);
        internal static readonly Color AccentColor = new Color(0.31f, 0.82f, 0.78f);
        internal static readonly Color MainText = new Color(0.88f, 0.94f, 0.92f);
        internal static readonly Color MutedText = new Color(0.56f, 0.68f, 0.69f);

        internal static void Panel(Rect rect, Color? fill = null)
        {
            Widgets.DrawBoxSolid(rect, fill ?? PanelColor);
            Color previous = GUI.color;
            GUI.color = BorderColor;
            Widgets.DrawBox(rect);
            GUI.color = previous;
        }

        internal static void Label(Rect rect, string value, Color? color = null,
            GameFont font = GameFont.Small, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Color previousColor = GUI.color;
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            GUI.color = color ?? MainText;
            Text.Font = font;
            Text.Anchor = anchor;
            Widgets.Label(rect, value);
            Text.Anchor = previousAnchor;
            Text.Font = previousFont;
            GUI.color = previousColor;
        }

        internal static bool Button(Rect rect, string value, bool active = false)
        {
            bool hovered = GUI.enabled && Mouse.IsOver(rect);
            Widgets.DrawBoxSolid(rect, active ? new Color(0.12f, 0.23f, 0.25f)
                : hovered ? new Color(0.12f, 0.18f, 0.2f) : PanelColor);
            Color previous = GUI.color;
            GUI.color = active || hovered ? AccentColor : BorderColor;
            Widgets.DrawBox(rect);
            GUI.color = previous;
            if (active) Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), AccentColor);
            Label(new Rect(rect.x + 6f, rect.y + 1f, rect.width - 12f, rect.height - 2f),
                value, GUI.enabled ? active || hovered ? AccentColor : MainText : MutedText,
                GameFont.Small, TextAnchor.MiddleCenter);
            return Widgets.ButtonInvisible(rect);
        }
    }
}
