using System.Linq;
using ScpFoundation.Storytelling;
using UnityEngine;
using Verse;

namespace ScpFoundation.Runtime
{
    public sealed class Dialog_FoundationDoorAccess : Window
    {
        private readonly CompFoundationDoorAccess access;
        private Vector2 scroll;

        public override Vector2 InitialSize { get { return new Vector2(620f, 620f); } }

        public Dialog_FoundationDoorAccess(CompFoundationDoorAccess access)
        {
            this.access = access;
            doCloseX = true;
            doCloseButton = true;
            forcePause = true;
        }

        public override void DoWindowContents(Rect rect)
        {
            if (!access.parent.Spawned) { Close(); return; }

            Widgets.Label(new Rect(0f, 0f, rect.width - 40f, 30f), "SCP_DoorAccessRoster".Translate());
            Widgets.Label(new Rect(0f, 34f, rect.width, 58f), "SCP_DoorAccessWindowDesc".Translate());

            if (Widgets.ButtonText(new Rect(0f, 98f, 190f, 30f), "SCP_DoorAccessAllowAll".Translate())) access.AllowAllCurrentColonists();
            if (Widgets.ButtonText(new Rect(200f, 98f, 190f, 30f), "SCP_DoorAccessClear".Translate())) access.ClearAllowed();

            var colonists = access.parent.Map.mapPawns.FreeColonistsSpawned.OrderBy(pawn => pawn.LabelShort).ToList();
            Rect view = new Rect(0f, 140f, rect.width, rect.height - 220f);
            Rect content = new Rect(0f, 0f, view.width - 20f, Mathf.Max(view.height, colonists.Count * 36f));
            Widgets.BeginScrollView(view, ref scroll, content);
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                bool allowed = access.IsAllowed(pawn);
                FoundationPersonnelComponent personnel = Current.Game.GetComponent<FoundationPersonnelComponent>();
                Widgets.CheckboxLabeled(new Rect(4f, i * 36f, content.width - 8f, 30f),
                    pawn.LabelShortCap + " — " + Dialog_FoundationPersonnel.RankFor(pawn, personnel), ref allowed);
                access.SetAllowed(pawn, allowed);
            }
            Widgets.EndScrollView();

            if (colonists.Count == 0) Widgets.Label(new Rect(0f, 150f, rect.width, 40f), "SCP_DoorAccessNoColonists".Translate());
        }
    }
}
