﻿using Avalonia;
using Avalonia.Markup.Xaml;

namespace Yafc;

public partial class App : Application {
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
