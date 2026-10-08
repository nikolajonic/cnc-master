using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CNC.App.Interop;

/// <summary>Asks the Windows desktop window manager to draw the native title bar in dark mode.</summary>
internal static partial class DarkTitleBar
{
    // Supported on Windows 10 20H1+ and Windows 11; older versions ignore the attribute.
    private const int DwmwaUseImmersiveDarkMode = 20;

    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = 1;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
