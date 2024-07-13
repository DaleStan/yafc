using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Yafc.UI;

namespace Yafc.ViewModels {
    internal class SdlToAvaloniaIconConverter : IValueConverter {
        private static readonly Dictionary<Icon, Bitmap> icons = [];
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
