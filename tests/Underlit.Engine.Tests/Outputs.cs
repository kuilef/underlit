using System.Collections.Concurrent;
using Underlit.Display;

// Only the output boundaries are replaced. The actual engine and settings
// sources are compiled into this executable; tests never touch real screens.
namespace Underlit
{
    internal static class Logger
    {
        public static void Info(string message) { }
        public static void Warn(string message, Exception? ex = null) { }
    }
}

namespace Underlit.Display
{
    public sealed class GammaRampApplier
    {
        public HashSet<string> Unavailable { get; } = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<DisplayInfo> _displays = Array.Empty<DisplayInfo>();
        public void Register(IReadOnlyList<DisplayInfo> displays) => _displays = displays;
        public IReadOnlySet<string> Apply(double brightness, int kelvin)
            => _displays.Where(d => !Unavailable.Contains(d.DeviceName))
                .Select(d => d.DeviceName).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class HardwareBrightness
    {
        public enum Path { None, Wmi, DdcCi }
        public ConcurrentDictionary<string, Path> Paths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentDictionary<string, bool> Results { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentQueue<(string Device, int Percent)> Writes { get; } = new();
        public void Register(IReadOnlyList<DisplayInfo> displays) { }
        public Path PathFor(DisplayInfo display) => Paths.GetValueOrDefault(display.DeviceName, Path.None);
        public bool TrySet(DisplayInfo display, int percent)
        {
            Writes.Enqueue((display.DeviceName, percent));
            return PathFor(display) != Path.None && Results.GetValueOrDefault(display.DeviceName, true);
        }
    }
}

namespace Underlit.UI
{
    public sealed class OverlayManager
    {
        public Dictionary<string, double> Opacities { get; } = new(StringComparer.OrdinalIgnoreCase);
        public void Sync(IReadOnlyList<DisplayInfo> displays)
        {
            Opacities.Clear();
            foreach (var display in displays) Opacities[display.DeviceName] = 0;
        }
        public void SetOpacity(double opacity)
        {
            foreach (var key in Opacities.Keys.ToArray()) Opacities[key] = opacity;
        }
        public void SetOpacityPerMonitor(IDictionary<string, double> perMonitor, double fallback)
        {
            foreach (var key in Opacities.Keys.ToArray())
                Opacities[key] = perMonitor.TryGetValue(key, out var opacity) ? opacity : fallback;
        }
    }
}
