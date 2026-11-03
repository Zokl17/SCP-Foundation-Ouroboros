using System.Collections.Generic;
using System.Linq;
using ScpFoundation.Interactions;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class Dialog_ConsoleShifts : Window
    {
        private readonly CompMonitoringConsole console;
        private Vector2 scroll;
        private bool painting, paintValue;
        public override Vector2 InitialSize { get { return new Vector2(960,700); } }
        public Dialog_ConsoleShifts(CompMonitoringConsole console)
        {
            this.console = console;
            doCloseX = true; doCloseButton = true; forcePause = true;
        }
        public override void DoWindowContents(Rect rect)
        {
            if (!console.parent.Spawned) { Close(); return; }
            Widgets.Label(new Rect(0,0,rect.width-40,30),"SCP_ConsoleShifts".Translate());
            Widgets.CheckboxLabeled(new Rect(0,34,rect.width,28),"SCP_ShiftsEnable".Translate(), ref console.useShifts);
            Widgets.Label(new Rect(0,68,rect.width,85),"SCP_ConsoleRosterHelp".Translate());
            Widgets.Label(new Rect(0,155,rect.width,28),console.RosterSummary());
            Rect view = new Rect(0,190,rect.width,rect.height-280);
            Rect content = new Rect(0,0,view.width-20,Mathf.Max(view.height,console.shifts.Count*125));
            Widgets.BeginScrollView(view,ref scroll,content);
            ConsoleDutyShift remove = null;
            for (int i=0;i<console.shifts.Count;i++)
            {
                ConsoleDutyShift shift = console.shifts[i];
                float y=i*125, hourWidth=(content.width-16)/24f;
                Widgets.Label(new Rect(0,y,200,26),"SCP_ShiftNumber".Translate(i+1));
                if (Widgets.ButtonText(new Rect(210,y,content.width-320,28),CompScpPost.Name(shift.employee))) Choose(shift);
                if (Widgets.ButtonText(new Rect(content.width-100,y,95,28),"SCP_ShiftDelete".Translate())) remove=shift;
                for (int hour=0;hour<24;hour++)
                {
                    Rect cell=new Rect(8+hour*hourWidth,y+39,hourWidth-2,36);
                    bool active=ShiftPolicy.Active(shift.hours,hour);
                    Widgets.DrawBoxSolid(cell,active ? new Color(.16f,.56f,.51f) : new Color(.24f,.26f,.28f));
                    Widgets.Label(new Rect(cell.x+5,cell.y+5,cell.width,cell.height),hour.ToString("00"));
                    Event input=Event.current;
                    if(input.button==0 && cell.Contains(input.mousePosition))
                    {
                        if(input.type==EventType.MouseDown){painting=true;paintValue=!active;}
                        if(painting && (input.type==EventType.MouseDown || input.type==EventType.MouseDrag))
                        {
                            if(paintValue)shift.hours|=1<<hour;else shift.hours&=~(1<<hour);
                            input.Use();
                        }
                    }
                }
                if (Widgets.ButtonText(new Rect(8,y+83,200,27),"SCP_ShiftClearHours".Translate())) shift.hours=0;
            }
            Widgets.EndScrollView();
            if (remove != null) console.shifts.Remove(remove);
            if(Event.current.rawType==EventType.MouseUp)painting=false;
            if (console.shifts.Count<12 && Widgets.ButtonText(new Rect(0,rect.height-78,240,28),"SCP_ShiftAdd".Translate())) console.shifts.Add(new ConsoleDutyShift());
            if (Widgets.ButtonText(new Rect(250,rect.height-78,260,28),"SCP_ShiftValidate".Translate()))
                Find.WindowStack.Add(new Dialog_MessageBox(console.ValidateRoster(),title:"SCP_ConsoleShifts".Translate()));
        }
        private void Choose(ConsoleDutyShift shift)
        {
            var options=new List<FloatMenuOption>();
            options.Add(new FloatMenuOption("SCP_ShiftNobody".Translate(),()=>console.AssignDuty(shift,null)));
            foreach(Pawn pawn in console.parent.Map.mapPawns.FreeColonistsSpawned.OrderBy(p=>p.LabelShort))
            {
                Pawn candidate=pawn;
                options.Add(new FloatMenuOption(candidate.LabelShortCap,()=>console.AssignDuty(shift,candidate)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
