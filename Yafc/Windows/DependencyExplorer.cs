﻿using System.Collections.Generic;
using System.Linq;
using SDL2;
using Yafc.Model;
using Yafc.UI;

namespace Yafc;

public class DependencyExplorer : PseudoScreen {
    private readonly ScrollArea dependencies;
    private readonly ScrollArea dependents;
    private static readonly Padding listPad = new Padding(0.5f);

    private readonly List<FactorioObject> history = [];
    private FactorioObject current;

    private static readonly Dictionary<DependencyNode.Flags, (string name, string missingText)> dependencyListTexts = new Dictionary<DependencyNode.Flags, (string, string)>()
    {
        {DependencyNode.Flags.Fuel, ("Fuel", "There is no fuel to power this entity")},
        {DependencyNode.Flags.Ingredient, ("Ingredient", "There are no ingredients to this recipe")},
        {DependencyNode.Flags.IngredientVariant, ("Ingredient", "There are no ingredient variants for this recipe")},
        {DependencyNode.Flags.CraftingEntity, ("Crafter", "There are no crafters that can craft this item")},
        {DependencyNode.Flags.Source, ("Source", "This item have no sources")},
        {DependencyNode.Flags.TechnologyUnlock, ("Research", "This recipe is disabled and there are no technologies to unlock it")},
        {DependencyNode.Flags.TechnologyPrerequisites, ("Research", "There are no technology prerequisites")},
        {DependencyNode.Flags.ItemToPlace, ("Item", "This entity cannot be placed")},
        {DependencyNode.Flags.SourceEntity, ("Source", "This recipe requires another entity")},
        {DependencyNode.Flags.Disabled, ("", "This technology is disabled")},
        {DependencyNode.Flags.Location, ("Location", "There are no locations that spawn this entity")},
    };

    public DependencyExplorer(FactorioObject current) : base(60f) {
        dependencies = new ScrollArea(30f, DrawDependencies);
        dependents = new ScrollArea(30f, DrawDependents);
        this.current = current;
    }

    public static void Show(FactorioObject target) => _ = MainScreen.Instance.ShowPseudoScreen(new DependencyExplorer(target));

    private void DrawFactorioObject(ImGui gui, FactorioId id) {
        FactorioObject obj = Database.objects[id];
        using (gui.EnterGroup(listPad, RectAllocator.LeftRow)) {
            gui.BuildFactorioObjectIcon(obj);
            string text = obj.locName + " (" + obj.type + ")";
            gui.RemainingRow(0.5f).BuildText(text, TextBlockDisplayStyle.WrappedText with { Color = obj.IsAccessible() ? SchemeColor.BackgroundText : SchemeColor.BackgroundTextFaint });
        }
        if (gui.BuildFactorioObjectButtonBackground(gui.lastRect, obj, tooltipOptions: new() { ShowTypeInHeader = true }) == Click.Left) {
            Change(obj);
        }
    }

    private void DrawDependencies(ImGui gui) {
        gui.spacing = 0f;

        Dependencies.dependencyList[current].Draw(gui, (gui, elements, flags) => {
            if (!dependencyListTexts.TryGetValue(flags, out var dependencyType)) {
                dependencyType = (flags.ToString(), "Missing " + flags);
            }

            if (elements.Count > 0) {
                gui.AllocateSpacing(0.5f);
                if (elements.Count == 1) {
                    gui.BuildText("Require this " + dependencyType.name + ":");
                }
                else if (flags.HasFlags(DependencyNode.Flags.RequireEverything)) {
                    gui.BuildText("Require ALL of these " + dependencyType.name + "s:");
                }
                else {
                    gui.BuildText("Require ANY of these " + dependencyType.name + "s:");
                }

                gui.AllocateSpacing(0.5f);
                foreach (var id in elements.OrderByDescending(x => CostAnalysis.Instance.flow[x])) {
                    DrawFactorioObject(gui, id);
                }
            }
            else {
                string text = dependencyType.missingText;
                if (Database.rootAccessible.Contains(current)) {
                    text += ", but it is inherently accessible.";
                }
                else {
                    text += ", and it is inaccessible.";
                }

                gui.BuildText(text, TextBlockDisplayStyle.WrappedText);
            }
        });
    }

    private void DrawDependents(ImGui gui) {
        gui.spacing = 0f;
        foreach (var reverseDependency in Dependencies.reverseDependencies[current].OrderByDescending(x => CostAnalysis.Instance.flow[x])) {
            DrawFactorioObject(gui, reverseDependency);
        }
    }

    private void SetFlag(ProjectPerItemFlags flag, bool set) {
        Project.current.settings.SetFlag(current, flag, set);
        Analysis.Do<Milestones>(Project.current);
        Rebuild();
        dependents.Rebuild();
        dependencies.Rebuild();
    }

    public override void Build(ImGui gui) {
        gui.allocator = RectAllocator.Center;
        BuildHeader(gui, "Dependency explorer");
        using (gui.EnterRow()) {
            gui.BuildText("Currently inspecting:", Font.subheader);
            if (gui.BuildFactorioObjectButtonWithText(current) == Click.Left) {
                SelectSingleObjectPanel.Select(Database.objects.explorable, "Select something", Change);
            }

            gui.DrawText(gui.lastRect, "(Click to change)", RectAlignment.MiddleRight, color: TextBlockDisplayStyle.HintText.Color);
        }
        using (gui.EnterRow()) {
            var settings = Project.current.settings;
            if (current.IsAccessible()) {
                if (current.IsAutomatable()) {
                    gui.BuildText("Status: Automatable");
                }
                else {
                    gui.BuildText("Status: Accessible, Not automatable");
                }

                if (settings.Flags(current).HasFlags(ProjectPerItemFlags.MarkedAccessible)) {
                    gui.BuildText("Manually marked as accessible.");
                    if (gui.BuildLink("Clear mark")) {
                        SetFlag(ProjectPerItemFlags.MarkedAccessible, false);
                        NeverEnoughItemsPanel.Refresh();
                    }
                }
                else {
                    if (gui.BuildLink("Mark as inaccessible")) {
                        SetFlag(ProjectPerItemFlags.MarkedInaccessible, true);
                        NeverEnoughItemsPanel.Refresh();
                    }

                    if (gui.BuildLink("Mark as accessible without milestones")) {
                        SetFlag(ProjectPerItemFlags.MarkedAccessible, true);
                        NeverEnoughItemsPanel.Refresh();
                    }
                }
            }
            else {
                if (settings.Flags(current).HasFlags(ProjectPerItemFlags.MarkedInaccessible)) {
                    gui.BuildText("Status: Marked as inaccessible");
                    if (gui.BuildLink("Clear mark")) {
                        SetFlag(ProjectPerItemFlags.MarkedInaccessible, false);
                        NeverEnoughItemsPanel.Refresh();
                    }
                }
                else {
                    gui.BuildText("Status: Not accessible. Wrong?");
                    if (gui.BuildLink("Manually mark as accessible")) {
                        SetFlag(ProjectPerItemFlags.MarkedAccessible, true);
                        NeverEnoughItemsPanel.Refresh();
                    }
                }
            }
        }
        gui.AllocateSpacing(2f);
        using var split = gui.EnterHorizontalSplit(2);
        split.Next();
        gui.BuildText("Dependencies:", Font.subheader);
        dependencies.Build(gui);
        split.Next();
        gui.BuildText("Dependents:", Font.subheader);
        dependents.Build(gui);
    }

    public void Change(FactorioObject target) {
        history.Add(current);
        if (history.Count > 100) {
            history.RemoveRange(0, 20);
        }

        current = target;
        dependents.Rebuild();
        dependencies.Rebuild();
        Rebuild();
    }

    public override bool KeyDown(SDL.SDL_Keysym key) {
        if (key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE && history.Count > 0) {
            var last = history[^1];
            Change(last);
            history.RemoveRange(history.Count - 2, 2);
            return true;
        }
        return base.KeyDown(key);
    }
}
