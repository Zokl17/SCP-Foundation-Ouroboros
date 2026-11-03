using System.Linq;
using RimWorld;
using ScpFoundation.Interactions;
using ScpFoundation.Storytelling;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class Dialog_ScpShifts : Window
    {
        private readonly CompScpPost post;
        private Vector2 scroll;
        private bool painting, paintValue;
        public override Vector2 InitialSize { get { return new Vector2(1000, 740); } }
        public Dialog_ScpShifts(CompScpPost post)
        {
            this.post = post;
            doCloseX = true;
            doCloseButton = false;
            forcePause = true;
            closeOnClickedOutside = false;
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!post.parent.Spawned) { Close(); return; }
            Text.Font = GameFont.Small;
            ScpTerminalUi.Panel(rect, ScpTerminalUi.Background);
            ScpTerminalUi.Panel(new Rect(10f, 8f, rect.width - 20f, 36f));
            ScpTerminalUi.Label(new Rect(20f, 10f, rect.width - 60f, 31f),
                "SCP_ShiftsHeading".Translate(),
                ScpTerminalUi.AccentColor, GameFont.Medium);
            Widgets.CheckboxLabeled(new Rect(0, 42, rect.width, 28), "SCP_ShiftsEnable".Translate(), ref post.useShifts);
            Thing bound = post.BoundObject;
            ScpTerminalUi.Label(new Rect(10, 78, rect.width - 270, 30),
                bound == null ? "SCP_ShiftNoObject".Translate()
                    : "SCP_ShiftBound".Translate(bound.TryGetComp<CompScp>().PublicLabel));
            if (ScpTerminalUi.Button(new Rect(rect.width - 260, 76, 250, 30), "SCP_ShiftChooseObject".Translate())) ChooseObject();
            ScpTerminalUi.Panel(new Rect(10f, 113f, rect.width - 20f, 84f));
            ScpTerminalUi.Label(new Rect(20f, 118f, rect.width - 40f, 74f),
                "SCP_ShiftsHelp".Translate(), ScpTerminalUi.MutedText, GameFont.Small, TextAnchor.UpperLeft);
            ScpTerminalUi.Label(new Rect(10, 202, rect.width - 20, 25),
                "SCP_ShiftLocalHour".Translate(GenLocalDate.HourInteger(post.parent.Map)), ScpTerminalUi.AccentColor);
            Rect view = new Rect(0, 236, rect.width, rect.height - 320);
            ScpTerminalUi.Panel(view);
            Rect content = new Rect(0, 0, view.width - 20, Mathf.Max(view.height, post.shifts.Count * 172));
            Widgets.BeginScrollView(view, ref scroll, content);
            ScpDutyShift remove = null;
            for (int i = 0; i < post.shifts.Count; i++)
            {
                ScpDutyShift shift = post.shifts[i];
                float y = i * 172;
                bool activeShift = post.useShifts && ShiftPolicy.Active(shift.hours, GenLocalDate.HourInteger(post.parent.Map));
                ScpTerminalUi.Panel(new Rect(0, y, content.width, 163),
                    activeShift ? new Color(0.12f, 0.27f, 0.26f) : ScpTerminalUi.PanelColor);
                ScpTerminalUi.Label(new Rect(8, y + 5, 250, 26),
                    "SCP_ShiftNumber".Translate(i + 1), ScpTerminalUi.AccentColor);
                if (ScpTerminalUi.Button(new Rect(content.width - 276, y + 3, 82, 27), "SCP_ShiftAllDay".Translate())) shift.hours = ShiftPolicy.AllHours;
                if (ScpTerminalUi.Button(new Rect(content.width - 188, y + 3, 82, 27), "SCP_ShiftClearHours".Translate())) shift.hours = 0;
                if (ScpTerminalUi.Button(new Rect(content.width - 100, y + 3, 92, 27), "SCP_ShiftDelete".Translate())) remove = shift;
                float width = (content.width - 16) / 24f;
                for (int hour = 0; hour < 24; hour++)
                {
                    Rect cell = new Rect(8 + hour * width, y + 36, width - 2, 30);
                    bool active = ShiftPolicy.Active(shift.hours, hour);
                    Widgets.DrawBoxSolid(cell, active ? new Color(0.16f, 0.56f, 0.51f) : new Color(0.24f, 0.26f, 0.28f));
                    ScpTerminalUi.Label(cell, hour.ToString("00"), ScpTerminalUi.MainText,
                        GameFont.Tiny, TextAnchor.MiddleCenter);
                    Event input = Event.current;
                    if (input.button == 0 && cell.Contains(input.mousePosition))
                    {
                        if (input.type == EventType.MouseDown) { painting = true; paintValue = !active; }
                        if (painting && (input.type == EventType.MouseDown || input.type == EventType.MouseDrag))
                        {
                            if (paintValue) shift.hours |= 1 << hour; else shift.hours &= ~(1 << hour);
                            input.Use();
                        }
                    }
                }
                float roleWidth = (content.width - 24) / 3f;
                for (int role = 0; role < 3; role++)
                {
                    Pawn pawn = role == 0 ? shift.observerA : role == 1 ? shift.observerB : shift.researcher;
                    string label = (role == 0 ? "SCP_ShiftObserverA" : role == 1 ? "SCP_ShiftObserverB" : "SCP_ShiftResearcher").Translate();
                    if (ScpTerminalUi.Button(new Rect(8 + role * (roleWidth + 4), y + 77, roleWidth, 78), label + "\n" + CompScpPost.Name(pawn)
                        + "\n" + post.DutyStatus(pawn, role == 2))) ChoosePawn(shift, role);
                }
            }
            Widgets.EndScrollView();
            if (remove != null) post.shifts.Remove(remove);
            if (Event.current.rawType == EventType.MouseUp) painting = false;
            if (post.shifts.Count < 12 && ScpTerminalUi.Button(new Rect(0, rect.height - 76, 245, 29), "SCP_ShiftAdd".Translate())) post.shifts.Add(new ScpDutyShift());
            if (ScpTerminalUi.Button(new Rect(255, rect.height - 76, 280, 29), "SCP_ShiftValidate".Translate()))
                Find.WindowStack.Add(new Dialog_ScpShiftValidation(post.ValidateRoster()));
            if (ScpTerminalUi.Button(new Rect(545, rect.height - 76, rect.width - 545, 29), "SCP_DutyEnableRoster".Translate())) post.EnableRoster();
            if (ScpTerminalUi.Button(new Rect((rect.width - 140f) / 2f, rect.height - 41f, 140f, 32f),
                "SCP_TerminalClose".Translate())) Close();
        }

        private void ChooseObject()
        {
            var choices = post.parent.Map.GetComponent<ScpStoryStateComponent>().Anomalies
                .Where(comp => comp.IsInCustody && comp.parent.Spawned && comp.parent.TryGetComp<CompScpInteraction>() != null)
                .Select(comp => new FloatMenuOption(comp.PublicLabel + " • " + comp.parent.Position, () => post.Bind(comp.parent))).ToList();
            choices.Insert(0, new FloatMenuOption("SCP_ShiftUnbind".Translate(), () => post.Bind(null)));
            Find.WindowStack.Add(new FloatMenu(choices));
        }

        private void ChoosePawn(ScpDutyShift shift, int role)
        {
            var choices = new System.Collections.Generic.List<FloatMenuOption>();
            choices.Add(new FloatMenuOption("SCP_ShiftNobody".Translate(), () => Assign(shift, role, null)));
            foreach (Pawn pawn in post.parent.Map.mapPawns.FreeColonistsSpawned.OrderBy(pawn => pawn.LabelShort))
            {
                Pawn candidate = pawn;
                bool occupied = role != 0 && shift.observerA == pawn || role != 1 && shift.observerB == pawn || role != 2 && shift.researcher == pawn;
                choices.Add(new FloatMenuOption(candidate.LabelShortCap + (occupied ? " — " + "SCP_ShiftAlreadyAssigned".Translate().ToString() : ""),
                    occupied ? (System.Action)null : () => Assign(shift, role, candidate)));
            }
            Find.WindowStack.Add(new FloatMenu(choices));
        }
        private void Assign(ScpDutyShift shift, int role, Pawn pawn)
        {
            post.AssignDuty(shift, role, pawn);
        }
    }

    public sealed class Dialog_ScpShiftValidation : Window
    {
        private readonly string report;
        private Vector2 scroll;

        public override Vector2 InitialSize { get { return new Vector2(690f, 430f); } }

        public Dialog_ScpShiftValidation(string report)
        {
            this.report = report;
            doCloseX = true;
            forcePause = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            ScpTerminalUi.Panel(rect, ScpTerminalUi.Background);
            Rect header = new Rect(12f, 10f, rect.width - 24f, 48f);
            ScpTerminalUi.Panel(header);
            ScpTerminalUi.Label(header.ContractedBy(12f), "SCP_ShiftsButton".Translate(),
                ScpTerminalUi.AccentColor, GameFont.Medium);
            Rect body = new Rect(12f, 68f, rect.width - 24f, rect.height - 128f);
            ScpTerminalUi.Panel(body);
            Color previous = GUI.color;
            GUI.color = ScpTerminalUi.MainText;
            Widgets.LabelScrollable(body.ContractedBy(14f), report, ref scroll);
            GUI.color = previous;
            if (ScpTerminalUi.Button(new Rect((rect.width - 140f) / 2f, rect.height - 47f, 140f, 32f),
                "SCP_TerminalClose".Translate())) Close();
        }
    }
}
