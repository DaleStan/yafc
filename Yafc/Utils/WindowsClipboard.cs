using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Yafc.UI;

namespace Yafc {
    public static class WindowsClipboard {
        [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr handle);
        [DllImport("user32.dll")] private static extern bool EmptyClipboard();
        [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint format, IntPtr data);
        [DllImport("user32.dll")] private static extern bool CloseClipboard();

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

        public static void CopySurfaceToClipboard(MemoryDrawingSurface surface) {
            (BitmapInfoHeader header, byte[] bytes) = RenderingUtils.GetBitmapInfoForClipboard(surface.surface);
            CopyToClipboard(8, header, bytes);
        }
    }
}
