using Underlit.Display;

namespace Underlit
{
    internal static class Logger
    {
        public static void Info(string message) { }
        public static void Warn(string message, Exception? ex = null) { }
    }
}

namespace Underlit.UI
{
    // Exercise the real manager without painting over the user's desktop.
    public sealed class OverlayWindow
    {
        public static readonly List<OverlayWindow> Created = new();
        public DisplayInfo Display { get; private set; }
        public bool Closed { get; private set; }
        public double Opacity { get; private set; }
        public OverlayWindow(DisplayInfo display) { Display = display; Created.Add(this); }
        public void ApplyBounds(DisplayInfo display) => Display = display;
        public void SetDimOpacity(double opacity) => Opacity = Math.Clamp(opacity, 0, 0.92);
        public void Show() { }
        public void Close() => Closed = true;
        public void ReassertTopmost() { }
    }
}

namespace Underlit.Display
{
    public static class WmiBrightness
    {
        public static int? Value;
        public static bool Unsupported = true;
        public static int? TryGet() => Value;
        public static int? TryGet(out bool unsupported)
        {
            unsupported = !Value.HasValue && Unsupported;
            return Value;
        }
        public static bool TrySet(int percent) => Value.HasValue;
    }

    public sealed class DdcCiMonitorSet : IDisposable
    {
        public sealed class Entry { public bool Supported { get; init; } }
        public static readonly HashSet<string> Supported = new();
        public Dictionary<string, Entry> Entries { get; } = new();
        public void Register(IEnumerable<DisplayInfo> displays)
        {
            Entries.Clear();
            foreach (var display in displays)
                Entries[display.DeviceName] = new Entry { Supported = Supported.Contains(display.DeviceName) };
        }
        public bool TrySet(string device, int percent) => Supported.Contains(device);
        public void Dispose() { }
    }
}
