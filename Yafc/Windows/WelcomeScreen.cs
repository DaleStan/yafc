﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using SDL2;
using Serilog;
using Yafc.Model;
using Yafc.Parser;
using Yafc.UI;

namespace Yafc;

/// <summary>
/// Contains information about a language supported by Factorio.
/// </summary>
/// <param name="NativeName">The name of the language, expressed in its language. To select this language, the current font must contain glyphs
/// for all characters in this string.</param>
/// <param name="LatinName"><paramref name="NativeName"/>, if that uses only well-supported Latin characters, or the English name of the
/// language.</param>
/// <param name="BaseFontName">The base name of the default font to use for this language.</param>
internal sealed record LanguageInfo(string LatinName, string NativeName, string BaseFontName) {
    public LanguageInfo(string latinName, string nativeName) : this(latinName, nativeName, "Roboto") { }
    public LanguageInfo(string name) : this(name, name, "Roboto") { }

    /// <summary>
    /// If not <see langword="null"/>, the current font must also have glyphs for these characters. Some fonts have enough glyphs for the
    /// language name, but don't have glyphs for all characters used by the language. (e.g. Some fonts have glyphs for türkmençe but are missing
    /// the Turkish ı.)
    /// </summary>
    public string? CheckExtraCharacters { get; set; }
}

public class WelcomeScreen : WindowUtility, IProgress<(string, string)>, IKeyboardFocus {
    private readonly ILogger logger = Logging.GetLogger<WelcomeScreen>();
    private bool loading, downloading;
    private string? currentLoad1, currentLoad2;
    private string path = "", dataPath = "", _modsPath = "";
    private string modsPath {
        get => _modsPath;
        set {
            _modsPath = value;
            FactorioDataSource.ClearDisabledMods();
        }
    }
    private bool netProduction;
    private string createText;
    private bool canCreate;
    private readonly ScrollArea errorScroll;
    private readonly ScrollArea recentProjectScroll;
    private readonly ScrollArea languageScroll;
    private string? errorMod;
    private string? errorMessage;
    private string? tip;
    private readonly string[] tips;
    private bool useMostRecentSave = true;

    internal static readonly SortedList<string, LanguageInfo> languageMapping = new()
    {
        {"en", new("English")},
        {"af", new("Afrikaans")},
        {"ar", new("Arabic", "العربية", "noto-sans-arabic")},
        {"be", new("Belarusian", "беларуская")},
        {"bg", new("Bulgarian", "български")},
        {"ca", new("català")},
        {"cs", new("Czech", "čeština")},
        {"da", new("dansk")},
        {"de", new("Deutsch")},
        {"el", new("Greek", "Ελληνικά")},
        {"es-ES", new("español")},
        {"et", new("eesti") },
        {"eu", new("euskara") },
        {"fi", new("suomi")},
        {"fil", new("Filipino")},
        {"fr", new("français")},
        {"fy-NL", new("Frysk")},
        {"ga-IE", new("Gaeilge")},
        {"he", new("Hebrew", "עברית", "noto-sans-hebrew")},
        {"hr", new("hrvatski")},
        {"hu", new("magyar")},
        {"id", new("Bahasa Indonesia")},
        {"is", new("íslenska")},
        {"it", new("italiano")},
        {"ja", new("Japanese", "日本語", "noto-sans-jp") { CheckExtraCharacters = "軽気処鉱砕" }},
        {"ka", new("Georgian", "ქართული", "noto-sans-georgian")},
        {"kk", new("Kazakh", "Қазақша")},
        {"ko", new("Korean", "한국어", "noto-sans-kr")},
        {"lt", new("Lithuanian", "lietuvių")},
        {"lv", new("Latvian", "latviešu")},
        {"nl", new("Nederlands")},
        {"no", new("norsk")},
        {"pl", new("polski")},
        {"pt-PT", new("português")},
        {"pt-BR", new("português (Brazil)")},
        {"ro", new("română")},
        {"ru", new("Russian", "русский")},
        {"sk", new("Slovak", "slovenčina")},
        {"sl", new("slovenski")},
        {"sq", new("shqip")},
        {"sr", new("Serbian", "српски")},
        {"sv-SE", new("svenska")},
        {"th", new("Thai", "ไทย", "noto-sans-thai")},
        {"tr", new("Turkish","türkmençe") { CheckExtraCharacters = "ığş" }},
        {"uk", new("Ukranian", "українська")},
        {"vi", new("Vietnamese", "Tiếng Việt")},
        {"zh-CN", new("Chinese (Simplified)", "汉语", "noto-sans-sc")},
        {"zh-TW", new("Chinese (Traditional)", "漢語", "noto-sans-tc") { CheckExtraCharacters = "鈾礦" }},
    };

    private enum EditType {
        Workspace, Factorio, Mods
    }

    public WelcomeScreen(ProjectDefinition? cliProject = null) : base(ImGuiUtils.DefaultScreenPadding) {
        tips = File.ReadAllLines("Data/Tips.txt");

        IconCollection.ClearCustomIcons();
        RenderingUtils.SetColorScheme(Preferences.Instance.darkMode);

        recentProjectScroll = new ScrollArea(20f, BuildRecentProjectList, collapsible: true);
        languageScroll = new ScrollArea(20f, LanguageSelection, collapsible: true);
        errorScroll = new ScrollArea(20f, BuildError, collapsible: true);
        Create("Welcome to YAFC CE v" + YafcLib.version.ToString(3), 45, null);

        if (cliProject != null && !string.IsNullOrEmpty(cliProject.dataPath)) {
            SetProject(cliProject);
            LoadProject();
        }
        else {
            ProjectDefinition? lastProject = Preferences.Instance.recentProjects.FirstOrDefault();
            SetProject(lastProject);
            _ = InputSystem.Instance.SetDefaultKeyboardFocus(this);
        }
    }

    private void BuildError(ImGui gui) {
        if (errorMod != null) {
            gui.BuildText($"Error while loading mod {errorMod}.", TextBlockDisplayStyle.Centered with { Color = SchemeColor.Error });
        }

        gui.allocator = RectAllocator.Stretch;
        gui.BuildText(errorMessage, TextBlockDisplayStyle.ErrorText with { Color = SchemeColor.ErrorText });
        gui.DrawRectangle(gui.lastRect, SchemeColor.Error);
    }

    protected override void BuildContents(ImGui gui) {
        gui.spacing = 1.5f;
        gui.BuildText("Yet Another Factorio Calculator", new TextBlockDisplayStyle(Font.header, Alignment: RectAlignment.Middle));
        if (loading) {
            gui.BuildText(currentLoad1, TextBlockDisplayStyle.Centered);
            gui.BuildText(currentLoad2, TextBlockDisplayStyle.Centered);
            gui.AllocateSpacing(15f);
            gui.BuildText(tip, new TextBlockDisplayStyle(WrapText: true, Alignment: RectAlignment.Middle));
            gui.SetNextRebuild(Ui.time + 30);
        }
        else if (downloading) {
            gui.BuildText("Please wait . . .", TextBlockDisplayStyle.Centered);
            gui.BuildText("Yafc is downloading the fonts for your language.", TextBlockDisplayStyle.Centered);
        }
        else if (errorMessage != null) {
            errorScroll.Build(gui);
            bool thereIsAModToDisable = (errorMod != null);

            using (gui.EnterRow()) {
                if (thereIsAModToDisable) {
                    gui.BuildWrappedText("YAFC was unable to load the project. You can disable the problematic mod once by clicking on 'Disable & reload' button, or you can disable it " +
                                         "permanently for YAFC by copying the mod-folder, disabling the mod in the copy by editing mod-list.json, and pointing YAFC to the copy.");
                }
                else {
                    gui.BuildWrappedText("YAFC cannot proceed because it was unable to load the project.");
                }
            }

            using (gui.EnterRow()) {
                if (gui.BuildLink("More info")) {
                    ShowDropDown(gui, gui.lastRect, ProjectErrorMoreInfo, new Padding(0.5f), 30f);
                }
            }

            using (gui.EnterRow()) {
                if (gui.BuildButton("Copy to clipboard", SchemeColor.Grey)) {
                    _ = SDL.SDL_SetClipboardText(errorMessage);
                }
                if (thereIsAModToDisable && gui.BuildButton("Disable & reload").WithTooltip(gui, "Disable this mod until you close YAFC or change the mod folder.")) {
                    FactorioDataSource.DisableMod(errorMod!);
                    errorMessage = null;
                    LoadProject();
                }
                if (gui.RemainingRow().BuildButton("Back")) {
                    errorMessage = null;
                    Rebuild();
                }
            }
        }
        else {
            BuildPathSelect(gui, path, "Project file location", "You can leave it empty for a new project", EditType.Workspace);
            BuildPathSelect(gui, dataPath, "Factorio Data location*\nIt should contain folders 'base' and 'core'",
                "e.g. C:/Games/Steam/SteamApps/common/Factorio/data", EditType.Factorio);
            BuildPathSelect(gui, modsPath, "Factorio Mods location (optional)\nIt should contain file 'mod-list.json'",
                "If you don't use separate mod folder, leave it empty", EditType.Mods);

            using (gui.EnterRow()) {
                gui.allocator = RectAllocator.RightRow;
                string lang = Preferences.Instance.language;
                if (languageMapping.TryGetValue(Preferences.Instance.language, out LanguageInfo? mapped)) {
                    lang = mapped.NativeName;
                }

                if (gui.BuildLink(lang)) {
                    gui.ShowDropDown(languageScroll.Build);
                }

                gui.BuildText("In-game objects language:");
            }

            using (gui.EnterRowWithHelpIcon("""When enabled it will try to find a more recent autosave. Disable if you want to load your manual save only.""", false)) {
                if (gui.BuildCheckBox("Load most recent (auto-)save", Preferences.Instance.useMostRecentSave,
                        out useMostRecentSave)) {
                    Preferences.Instance.useMostRecentSave = useMostRecentSave;
                    Preferences.Instance.Save();
                }
            }

            using (gui.EnterRowWithHelpIcon("""
                    If checked, YAFC will only suggest production or consumption recipes that have a net production or consumption of that item or fluid.
                    For example, kovarex enrichment will not be suggested when adding recipes that produce U-238 or consume U-235.
                    """, false)) {
                _ = gui.BuildCheckBox("Use net production/consumption when analyzing recipes", netProduction, out netProduction);
            }

            string softwareRenderHint = "If checked, the main project screen will not use hardware-accelerated rendering.\n\n" +
                "Enable this setting if YAFC crashes after loading without an error message, or if you know that your computer's " +
                "graphics hardware does not support modern APIs (e.g. DirectX 12 on Windows).";

            using (gui.EnterRowWithHelpIcon(softwareRenderHint, false)) {
                bool forceSoftwareRenderer = Preferences.Instance.forceSoftwareRenderer;
                _ = gui.BuildCheckBox("Force software rendering in project screen", forceSoftwareRenderer, out forceSoftwareRenderer);

                if (forceSoftwareRenderer != Preferences.Instance.forceSoftwareRenderer) {
                    Preferences.Instance.forceSoftwareRenderer = forceSoftwareRenderer;
                    Preferences.Instance.Save();
                }
            }

            using (gui.EnterRow()) {
                if (Preferences.Instance.recentProjects.Length > 1) {
                    if (gui.BuildButton("Recent projects", SchemeColor.Grey)) {
                        gui.ShowDropDown(BuildRecentProjectsDropdown, 35f);
                    }
                }
                if (gui.BuildButton(Icon.Help).WithTooltip(gui, "About YAFC")) {
                    _ = new AboutScreen(this);
                }

                if (gui.BuildButton(Icon.DarkMode).WithTooltip(gui, "Toggle dark mode")) {
                    Preferences.Instance.darkMode = !Preferences.Instance.darkMode;
                    RenderingUtils.SetColorScheme(Preferences.Instance.darkMode);
                    Preferences.Instance.Save();
                }
                if (gui.RemainingRow().BuildButton(createText, active: canCreate)) {
                    LoadProject();
                }
            }
        }
    }

    private void ProjectErrorMoreInfo(ImGui gui) {

        gui.BuildWrappedText("Check that these mods load in Factorio.");
        gui.BuildWrappedText("YAFC only supports loading mods that were loaded in Factorio before. If you add or remove mods or change startup settings, " +
            "you need to load those in Factorio and then close the game because Factorio saves mod-list.json only when exiting.");
        gui.BuildWrappedText("Check that Factorio loads mods from the same folder as YAFC.");
        gui.BuildWrappedText("If that doesn't help, try removing the mods that have several versions, or are disabled, or don't have the required dependencies.");

        // The whole line is underlined if the allocator is not set to LeftAlign
        gui.allocator = RectAllocator.LeftAlign;
        if (gui.BuildLink("If all else fails, then create an issue on GitHub")) {
            Ui.VisitLink(AboutScreen.Github);
        }

        gui.BuildWrappedText("Please attach a new-game save file to sync mods, versions, and settings.");
    }

    private void DoLanguageList(ImGui gui, SortedList<string, LanguageInfo> list, bool listFontSupported) {
        foreach (var (k, v) in list) {
            if (!Font.text.CanDraw(v.NativeName + v.CheckExtraCharacters) && !listFontSupported) {
                bool result;
                if (Font.text.CanDraw(v.NativeName)) {
                    result = gui.BuildLink($"{v.NativeName} ({k})");
                }
                else {
                    result = gui.BuildLink($"{v.LatinName} ({k})");
                }

                if (result) {
                    if (Font.FilesExist(v.BaseFontName) || Program.hasOverriddenFont) {
                        Preferences.Instance.language = k;
                        Preferences.Instance.Save();
                        gui.CloseDropdown();
                        restartIfNecessary();
                    }
                    else {
                        gui.ShowDropDown(async gui => {
                            gui.BuildText("Yafc will download a suitable font before it restarts.\nThis may take a minute or two.", TextBlockDisplayStyle.WrappedText);
                            gui.allocator = RectAllocator.Center;
                            if (gui.BuildButton("Confirm")) {
                                gui.CloseDropdown();
                                downloading = true;
                                // Jump through several hoops to download an appropriate Noto Sans font.
                                // Google Webfonts Helper is the first place I found that would (eventually) let me directly download ttf files
                                // for the static fonts. It can also provide links to Google, but that would take three requests, instead of two.
                                // Variable fonts can be acquired from github, but SDL_ttf doesn't support those yet.
                                // See https://gwfh.mranftl.com/fonts, https://github.com/majodev/google-webfonts-helper, and
                                // https://github.com/libsdl-org/SDL_ttf/pull/506
                                HttpClient client = new();
                                // Get the character subsets supported by this font
                                string query = "https://gwfh.mranftl.com/api/fonts/" + v.BaseFontName;
                                dynamic response = JObject.Parse(await client.GetStringAsync(query));
                                // Request a zip containing ttf files and all character subsets
                                query += "?variants=300,regular&download=zip&formats=ttf&subsets=";
                                foreach (string item in response["subsets"]) {
                                    query += item + ",";
                                }
                                ZipArchive archive = new(await client.GetStreamAsync(query));
                                // Extract the two ttf files into the expected locations
                                foreach (var entry in archive.Entries) {
                                    if (entry.Name.Contains("300")) {
                                        entry.ExtractToFile($"Data/{v.BaseFontName}-Light.ttf");
                                    }
                                    else {
                                        entry.ExtractToFile($"Data/{v.BaseFontName}-Regular.ttf");
                                    }
                                }
                                // Save and restart
                                Preferences.Instance.language = k;
                                Preferences.Instance.Save();
                                restartIfNecessary();
                            }
                        });
                    }
                }
            }
            else if (Font.text.CanDraw(v.NativeName + v.CheckExtraCharacters) && listFontSupported && gui.BuildLink(v.NativeName + " (" + k + ")")) {
                Preferences.Instance.language = k;
                Preferences.Instance.Save();
                _ = gui.CloseDropdown();
            }
        }

        static void restartIfNecessary() {
            if (!Program.hasOverriddenFont) {
                Process.Start("dotnet", Assembly.GetEntryAssembly()!.Location);
                Environment.Exit(0);
            }
        }
    }

    private void LanguageSelection(ImGui gui) {
        gui.spacing = 0f;
        gui.allocator = RectAllocator.LeftAlign;
        gui.BuildText("Mods may not support your language, using English as a fallback.", TextBlockDisplayStyle.WrappedText);
        gui.AllocateSpacing(0.5f);

        DoLanguageList(gui, languageMapping, true);
        if (!Program.hasOverriddenFont) {
            gui.AllocateSpacing(0.5f);

            string unsupportedLanguageMessage = "These languages are not supported by the current font. Click the language to restart with a suitable font, or click 'Select font' to select a custom font.";
            gui.BuildText(unsupportedLanguageMessage, TextBlockDisplayStyle.WrappedText);
            gui.AllocateSpacing(0.5f);
        }
        DoLanguageList(gui, languageMapping, false);

        gui.AllocateSpacing(0.5f);
        if (gui.BuildButton("Select font")) {
            SelectFont();
        }

        if (Preferences.Instance.overrideFont != null) {
            gui.BuildText(Preferences.Instance.overrideFont, TextBlockDisplayStyle.WrappedText);
            if (gui.BuildLink("Reset font to default")) {
                Preferences.Instance.overrideFont = null;
                languageScroll.RebuildContents();
                Preferences.Instance.Save();
            }
        }
        gui.BuildText("Restart Yafc to switch to the selected font.", TextBlockDisplayStyle.WrappedText);
    }

    private async void SelectFont() {
        string? result = await new FilesystemScreen("Override font", "Override font that YAFC uses", "Ok", null, FilesystemScreen.Mode.SelectFile, null, this, null, null);
        if (result == null) {
            return;
        }

        if (SDL_ttf.TTF_OpenFont(result, 16) != IntPtr.Zero) {
            Preferences.Instance.overrideFont = result;
            languageScroll.RebuildContents();
            Preferences.Instance.Save();
        }
    }

    public void Report((string, string) value) => (currentLoad1, currentLoad2) = value;

    private bool FactorioValid(string factorio) => !string.IsNullOrEmpty(factorio) && Directory.Exists(Path.Combine(factorio, "core"));

    private bool ModsValid(string mods) => string.IsNullOrEmpty(mods) || File.Exists(Path.Combine(mods, "mod-list.json"));

    [MemberNotNull(nameof(createText))]
    private void ValidateSelection() {
        bool factorioValid = FactorioValid(dataPath);
        bool modsValid = ModsValid(modsPath);
        bool projectExists = File.Exists(path);

        if (projectExists) {
            createText = "Load '" + Path.GetFileNameWithoutExtension(path) + "'";
        }
        else if (path != "") {
            string? directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory)) {
                createText = "Project directory does not exist";
                canCreate = false;
                return;
            }
            createText = "Create '" + Path.GetFileNameWithoutExtension(path) + "'";
        }
        else {
            createText = "Create new project";
        }

        canCreate = factorioValid && modsValid;
    }

    private void BuildPathSelect(ImGui gui, string path, string description, string placeholder, EditType editType) {
        gui.BuildText(description, TextBlockDisplayStyle.WrappedText);
        gui.spacing = 0.5f;
        using (gui.EnterGroup(default, RectAllocator.RightRow)) {
            if (gui.BuildButton("...")) {
                ShowFileSelect(description, path, editType);
            }

            if (gui.RemainingRow(0f).BuildTextInput(path, out path, placeholder)) {
                switch (editType) {
                    case EditType.Workspace:
                        this.path = path;
                        break;
                    case EditType.Factorio:
                        dataPath = path;
                        break;
                    case EditType.Mods:
                        modsPath = path;
                        break;
                }
                ValidateSelection();
            }
        }
        gui.spacing = 1.5f;
    }

    /// <summary>
    /// Initializes different input fields with the supplied project definition. <br/>
    /// If the project is null, the fields are cleared. <br/>
    /// If the user is on Windows, it also tries to infer the installation directory of Factorio.
    /// </summary>
    /// <param name="project">A project definition with paths and options. Can be null.</param>
    [MemberNotNull(nameof(createText))]
    private void SetProject(ProjectDefinition? project) {
        if (project != null) {
            dataPath = project.dataPath;
            modsPath = project.modsPath;
            path = project.path;
            netProduction = project.netProduction;
        }
        else {
            dataPath = "";
            modsPath = "";
            path = "";
            netProduction = false;
        }

        if (dataPath == "" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            string possibleDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam/steamApps/common/Factorio/data");
            if (FactorioValid(possibleDataPath)) {
                dataPath = possibleDataPath;
            }
        }

        ValidateSelection();
        rootGui.Rebuild();
    }

    private async void LoadProject() {
        try {
            // TODO (shpaass/yafc-ce/issues/249): Why does WelcomeScreen.cs need the internals of ProjectDefinition?
            // Why not take or copy the whole object? The parts are used only in WelcomeScreen.cs, so I see no reason
            // to disassemble ProjectDefinition and drag it piece by piece.
            var (dataPath, modsPath, projectPath) = (this.dataPath, this.modsPath, path);
            Preferences.Instance.AddProject(dataPath, modsPath, projectPath, netProduction);
            Preferences.Instance.Save();
            tip = tips.Length > 0 ? tips[DataUtils.random.Next(tips.Length)] : "";

            loading = true;
            rootGui.Rebuild();

            await Ui.ExitMainThread();

            ErrorCollector collector = new ErrorCollector();
            var project = FactorioDataSource.Parse(dataPath, modsPath, projectPath, netProduction, this, collector, Preferences.Instance.language, Preferences.Instance.useMostRecentSave);

            await Ui.EnterMainThread();
            logger.Information("Opening main screen");
            _ = new MainScreen(displayIndex, project);

            if (collector.severity > ErrorSeverity.None) {
                ErrorListPanel.Show(collector);
            }

            Close();
            GC.Collect();
            logger.Information("GC: {TotalMemory}", GC.GetTotalMemory(false));
        }
        catch (Exception ex) {
            await Ui.EnterMainThread();
            while (ex.InnerException != null) {
                ex = ex.InnerException;
            }

            errorScroll.RebuildContents();
            errorMod = FactorioDataSource.CurrentLoadingMod;
            if (ex is LuaException lua) {
                errorMessage = lua.Message;
            }
            else {
                errorMessage = ex.Message + "\n" + ex.StackTrace;
            }
        }
        finally {
            loading = false;
            rootGui.Rebuild();
        }
    }

    private Func<string, bool>? GetFolderFilter(EditType type) => type switch {
        EditType.Mods => ModsValid,
        EditType.Factorio => FactorioValid,
        _ => null,
    };

    private async void ShowFileSelect(string description, string path, EditType type) {
        string buttonText;
        string? location, fileExtension;
        FilesystemScreen.Mode fsMode;

        if (type == EditType.Workspace) {
            buttonText = "Select";
            location = Path.GetDirectoryName(path);
            fsMode = FilesystemScreen.Mode.SelectOrCreateFile;
            fileExtension = "yafc";
        }
        else {
            buttonText = "Select folder";
            location = path;
            fsMode = FilesystemScreen.Mode.SelectFolder;
            fileExtension = null;
        }

        string? result = await new FilesystemScreen("Select folder", description, buttonText, location, fsMode, "", this, GetFolderFilter(type), fileExtension);

        if (result != null) {
            if (type == EditType.Factorio) {
                dataPath = result;
            }
            else if (type == EditType.Mods) {
                modsPath = result;
            }
            else {
                this.path = result;
            }

            Rebuild();
            ValidateSelection();
        }
    }

    private void BuildRecentProjectsDropdown(ImGui gui) => recentProjectScroll.Build(gui);

    private void BuildRecentProjectList(ImGui gui) {
        gui.spacing = 0f;
        foreach (var project in Preferences.Instance.recentProjects) {
            if (string.IsNullOrEmpty(project.path)) {
                continue;
            }

            using (gui.EnterGroup(new Padding(0.5f, 0.25f), RectAllocator.LeftRow)) {
                gui.BuildIcon(Icon.Settings);
                gui.RemainingRow(0.5f).BuildText(project.path);
            }

            if (gui.BuildButton(gui.lastRect, SchemeColor.None, SchemeColor.Grey)) {
                WelcomeScreen owner = (WelcomeScreen)gui.window!; // null-forgiving: gui.window has been set earlier in the render loop.
                owner.SetProject(project);
                _ = gui.CloseDropdown();
            }
        }
    }

    public bool KeyDown(SDL.SDL_Keysym key) {
        if (canCreate && !loading && errorMessage == null
            && key.scancode is SDL.SDL_Scancode.SDL_SCANCODE_RETURN or SDL.SDL_Scancode.SDL_SCANCODE_RETURN2 or SDL.SDL_Scancode.SDL_SCANCODE_KP_ENTER) {

            LoadProject();
            return true;
        }
        if (errorMessage != null && key.scancode == SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE) {
            errorMessage = null;
            Rebuild();
            return true;
        }
        return false;
    }
    public bool TextInput(string input) => false;
    public bool KeyUp(SDL.SDL_Keysym key) => false;
    public void FocusChanged(bool focused) { }
}
