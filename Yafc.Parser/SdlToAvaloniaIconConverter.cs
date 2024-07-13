using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Yafc.UI;

namespace Yafc.Parser;

public class SdlToAvaloniaIconConverter : IValueConverter {
    private static readonly Dictionary<Icon, (nint?, Bitmap?)> icons = [];

    public static void AddConversion(Icon icon, nint sdlSurface) => icons[icon] = (sdlSurface, null);

    public static void ClearConversions() => icons.Clear();

    public unsafe object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is Icon icon) {
            (nint? surface, Bitmap? image) = icons[icon];
            if (image != null) {
                return image;
            }

            (BitmapInfoHeader header, byte[] data) = RenderingUtils.GetBitmapInfoForAvalonia(surface!.Value); // null-forgiving: If the bitmap is null, the surface is not.
            fixed (byte* ptr = data) {
                icons[icon] = (null, new Bitmap(PixelFormat.Rgba8888, AlphaFormat.Unpremul, (nint)ptr, new PixelSize(header.biWidth, header.biHeight), new Vector(72, 72), header.biWidth * 4));
            }

            return icons[icon].Item2;
        }
        return null;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();

    public static unsafe void CopyToClipboard<T>(uint format, in T header, Span<byte> data) where T : unmanaged {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            return;
        }

        int headerSize = Unsafe.SizeOf<T>();
        nint ptr = Marshal.AllocHGlobal(headerSize + data.Length);
        _ = OpenClipboard(IntPtr.Zero);
        try {
            Marshal.StructureToPtr(header, ptr, false);
            Span<byte> targetSpan = new Span<byte>((void*)(ptr + headerSize), data.Length);
            data.CopyTo(targetSpan);
            _ = EmptyClipboard();
            _ = SetClipboardData(format, ptr);
            ptr = IntPtr.Zero;
        }
        finally {
            Marshal.FreeHGlobal(ptr);
            _ = CloseClipboard();
        }
    }

    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr data);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();

}
