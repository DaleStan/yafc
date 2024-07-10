using Avalonia.Styling;
using Yafc.UI;
using Window = Avalonia.Controls.Window;

namespace Yafc.Windows {
    internal abstract class WindowBase : Window {
        protected WindowBase() => RequestedThemeVariant = RenderingUtils.darkMode ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
