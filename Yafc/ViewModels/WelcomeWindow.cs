using System;
using System.Collections.Generic;
using System.Reactive;
using System.Windows.Input;
using ReactiveUI;

namespace Yafc.ViewModels;

internal class WelcomeWindow : ViewModelBase {
    public string ProjectFileLocation { get; set; } = "";
    public string FactorioDataLocation { get; set; } = "";
    public string ModFolderLocation { get; set; } = "";
    public string OpenCaption { get; set; } = "Create New Project";
    public bool ExpensiveRecipes { get; set; }
    public IReadOnlyDictionary<string, string> SupportedLanguages { get; private set; } = new Dictionary<string, string>(){
        { "en", "English" },
        { "ca", "Catalan" },
        { "cs", "Czech" },
        { "da", "Danish" },
        { "nl", "Dutch" },
        { "de", "German" },
        { "fi", "Finnish" },
        { "fr", "French" },
        { "hu", "Hungarian" },
        { "it", "Italian" },
        { "no", "Norwegian" },
        { "pl", "Polish" },
        { "pt-PT", "Portuguese" },
        { "pt-BR", "Portuguese (Brazilian)" },
        { "ro", "Romanian" },
        { "ru", "Russian" },
        { "es-ES", "Spanish" },
        { "sv-SE", "Swedish" },
        { "tr", "Turkish" },
        { "uk", "Ukrainian" },
        { "ja", "Japanese" },
        { "zh-CN", "Chinese (Simplified)" },
        { "zh-TW", "Chinese (Traditional)" },
        { "ko", "Korean" },
    };

    public KeyValuePair<string, string> Language { get; set; }

    public ReactiveCommand<string, Unit> BrowseCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    public WelcomeWindow() {
        BrowseCommand = ReactiveCommand.Create<string>(Browse);
        OpenCommand = ReactiveCommand.Create(Open);
    }

    private void Browse(string property) {
    }

    private void Open() {
    }

    public class CommandBrowse : ICommand {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => throw new NotImplementedException();
    }
}
