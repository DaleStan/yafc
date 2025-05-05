﻿using System.Linq;
using System.Numerics;
using Yafc.I18n;
using Yafc.Model;
using Yafc.UI;

namespace Yafc;

public class MilestonesEditor : PseudoScreen {
    private static readonly MilestonesEditor Instance = new MilestonesEditor();
    private readonly VirtualScrollList<FactorioObject> milestoneList;

    public MilestonesEditor() : base(50) {
        milestoneList = new VirtualScrollList<FactorioObject>(30f, new Vector2(float.PositiveInfinity, 3f), MilestoneDrawer);
        cleanupCallback = () => {
            // Force rebuild the panel to update its contents and show the possibly-modified milestones.
            MilestonesPanel.Rebuild();

            // Recalculate the project with the  possibly-modified milestones.
            Milestones.Instance.Compute(Project.current, new());
        };
    }

    public override void Open() {
        base.Open();
        milestoneList.data = Project.current.settings.milestones;
    }

    public static void Show() => _ = MainScreen.Instance.ShowPseudoScreen(Instance);

    private void MilestoneDrawer(ImGui gui, FactorioObject element, int index) {
        using (gui.EnterRow()) {
            var settings = Project.current.settings;
            gui.BuildFactorioObjectIcon(element, new IconDisplayStyle(3f, MilestoneDisplay.None, false));
            gui.BuildText(element.locName, maxWidth: width - 16.6f); // Experimentally determined width of the non-text parts of the editor.
            if (gui.BuildButton(Icon.Close, size: 1f)) {
                _ = settings.RecordUndo().milestones.Remove(element);
                Rebuild();
                milestoneList.data = settings.milestones;
            }
        }
        if (gui.DoListReordering(gui.lastRect, gui.lastRect, index, out int moveFrom)) {
            Project.current.settings.RecordUndo().milestones.MoveListElementIndex(moveFrom, index);
        }
    }

    public override void Build(ImGui gui) {
        BuildHeader(gui, LSs.MilestoneEditor);
        milestoneList.Build(gui);

        string milestoneHintText = LSs.MilestoneDescription;
        gui.BuildText(milestoneHintText, TextBlockDisplayStyle.WrappedText with { Color = SchemeColor.BackgroundTextFaint });

        using (gui.EnterRow()) {
            if (gui.BuildButton(LSs.MilestoneAutoSort, SchemeColor.Grey)) {
                ErrorCollector collector = new ErrorCollector();
                Milestones.Instance.ComputeWithParameters(Project.current, collector, [.. Project.current.settings.milestones], true);

                if (collector.severity > ErrorSeverity.None) {
                    ErrorListPanel.Show(collector);
                }

                milestoneList.RebuildContents();
            }
            if (gui.BuildButton(LSs.MilestoneAdd)) {
                SelectMultiObjectPanel.Select(Database.objects.explorable.Except(Project.current.settings.milestones), LSs.MilestoneAddNew, AddMilestone);
            }
        }
    }

    private void AddMilestone(FactorioObject obj) {
        var settings = Project.current.settings;

        if (settings.milestones.Contains(obj)) {
            MessageBox.Show(LSs.MilestoneCannotAdd, LSs.MilestoneCannotAddAlreadyExists, LSs.Ok);
            return;
        }

        var lockedMask = Milestones.Instance.GetMilestoneResult(obj);

        if (lockedMask.IsClear()) {
            settings.RecordUndo().milestones.Add(obj);
        }
        else {
            int bestIndex = 0;
            for (int i = 1; i < lockedMask.length; i++) {
                if (lockedMask[i]) {
                    lockedMask[i] = false;
                    var milestone = Milestones.Instance.currentMilestones[i - 1];
                    int index = settings.milestones.IndexOf(milestone);

                    if (index >= bestIndex) {
                        bestIndex = index + 1;
                    }

                    if (lockedMask.IsClear()) {
                        break;
                    }
                }
            }
            settings.RecordUndo().milestones.Insert(bestIndex, obj);
        }

        Rebuild();
        milestoneList.data = settings.milestones;
    }
}
