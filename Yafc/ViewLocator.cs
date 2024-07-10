using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Yafc.ViewModels;

namespace Yafc;

public class ViewLocator : IDataTemplate {

    public Control? Build(object? data) {
        if (data is null) {
            return null;
        }

        string name = data.GetType().FullName!.Replace("ViewModel", "Window", StringComparison.Ordinal);
        Type? type = Type.GetType(name);

        if (type != null) {
            Control control = (Control)Activator.CreateInstance(type)!;
            control.DataContext = data;
            return control;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
