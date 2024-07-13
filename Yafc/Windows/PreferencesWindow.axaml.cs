using Avalonia.Controls;
using Yafc.Model;
using Yafc.ViewModels;

namespace Yafc.Windows;

internal partial class PreferencesWindow : WindowBase {
    public PreferencesWindow() {
        InitializeComponent();
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (((ComboBox)sender).SelectedItem is EntityBelt belt) {
            ((PreferencesViewModel)DataContext!).SetProductionFromBelt(belt);
            ((ComboBox)sender).SelectedItem = null;
        }
    }
}
