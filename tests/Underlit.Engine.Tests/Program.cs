using System.Windows.Threading;
using Underlit.Core;
using Underlit.Display;
using Underlit.Settings;
using Underlit.UI;

internal static class Program
{
    private static readonly DisplayInfo Primary = new() { DeviceName = "DISPLAY1", IsPrimary = true };
    private static readonly DisplayInfo Second = new() { DeviceName = "DISPLAY2" };

    [STAThread]
    public static int Main(string[] args) => RunTests();

    private static int RunTests()
    {
        var tests = new (string Name, Action Test)[]
        {
            ("Unsupported secondary dims in positive range", () =>
            {
                using var f = new Fixture(50);
                Equal(0, f.Overlays.Opacities[Primary.DeviceName]);
                Equal(0.325, f.Overlays.Opacities[Second.DeviceName]);
            }),
            ("Both software-only monitors dim", () =>
            {
                using var f = new Fixture(50, mainHardware: false);
                Equal(0.325, f.Overlays.Opacities[Primary.DeviceName]);
                Equal(0.325, f.Overlays.Opacities[Second.DeviceName]);
            }),
            ("Supported monitors do not get double dimmed", () =>
            {
                using var f = new Fixture(50, secondHardware: true);
                Equal(0, f.Overlays.Opacities[Primary.DeviceName]);
                Equal(0, f.Overlays.Opacities[Second.DeviceName]);
            }),
            ("Primary OSD does not inherit secondary fallback", () =>
            {
                using var f = new Fixture(50);
                Equal(0, f.Engine.CurrentOverlayOpacity);
            }),
            ("Failed hardware write activates fallback", () =>
            {
                using var f = new Fixture(100, secondHardware: true);
                f.Hardware.Results[Second.DeviceName] = false;
                f.Engine.SetSignedBrightness(50);
                PumpUntil(() => f.Overlays.Opacities[Second.DeviceName] > 0);
                Equal(0.325, f.Overlays.Opacities[Second.DeviceName]);
                Equal(0, f.Overlays.Opacities[Primary.DeviceName]);
            }),
            ("Successful hardware retry removes software fallback", () =>
            {
                using var f = new Fixture(100, secondHardware: true);
                f.Hardware.Results[Second.DeviceName] = false;
                f.Engine.SetSignedBrightness(50);
                PumpUntil(() => f.Overlays.Opacities[Second.DeviceName] > 0);
                f.Hardware.Results[Second.DeviceName] = true;
                f.Engine.SetSignedBrightness(60);
                PumpUntil(() => f.Overlays.Opacities[Second.DeviceName] == 0);
                Equal(0, f.Overlays.Opacities[Primary.DeviceName]);
            }),
            ("Gamma failure is compensated before extended overlay starts", () =>
            {
                using var f = new Fixture(100, secondHardware: true);
                f.Gamma.Unavailable.Add(Second.DeviceName);
                f.Engine.SetSignedBrightness(-25);
                Equal(0, f.Overlays.Opacities[Primary.DeviceName]);
                Equal(0.1125, f.Overlays.Opacities[Second.DeviceName]);
            }),
            ("Hardware and gamma failures compose in overlay", () =>
            {
                using var f = new Fixture(100);
                f.Gamma.Unavailable.Add(Second.DeviceName);
                f.Engine.SetSignedBrightness(-25);
                Equal(0.689375, f.Overlays.Opacities[Second.DeviceName]);
                f.Engine.SetSignedBrightness(-100);
                Equal(0.92, f.Overlays.Opacities[Second.DeviceName]);
            }),
            ("Software fallback respects secondary offset", () =>
            {
                using var f = new Fixture(100);
                f.Settings.PerMonitor[Second.StableId] = new PerMonitor { BrightnessOffset = -20 };
                f.Engine.SetSignedBrightness(50);
                Equal(0.455, f.Overlays.Opacities[Second.DeviceName]);
            }),
            ("Boost and pause clear fallback and restore dimming", () =>
            {
                using var f = new Fixture(50);
                f.Engine.Boost();
                Equal(0, f.Overlays.Opacities[Second.DeviceName]);
                f.Engine.Boost();
                Equal(0.325, f.Overlays.Opacities[Second.DeviceName]);
                f.Engine.TogglePaused();
                Equal(0, f.Overlays.Opacities[Second.DeviceName]);
                Equal(0, f.Engine.CurrentOverlayOpacity);
                f.Engine.TogglePaused();
                Equal(0.325, f.Overlays.Opacities[Second.DeviceName]);
            }),
            ("Fallback stays continuous through hardware minimum", () =>
            {
                using var f = new Fixture(0);
                Equal(0.65, f.Overlays.Opacities[Second.DeviceName]);
                f.Engine.SetSignedBrightness(-25);
                Equal(0.65, f.Overlays.Opacities[Second.DeviceName]);
                f.Engine.SetSignedBrightness(-100);
                Equal(0.92, f.Overlays.Opacities[Second.DeviceName]);
            }),
            ("Reconnected hardware receives unchanged target again", () =>
            {
                using var f = new Fixture(50, secondHardware: true);
                PumpFor(100);
                int before = f.Hardware.Writes.Count;
                f.Engine.RefreshDisplays(new() { Primary, Second });
                PumpUntil(() => f.Hardware.Writes.Count >= before + 2);
            }),
            ("External laptop change also updates external primary", () =>
            {
                using var f = new Fixture(50, secondHardware: true);
                f.Hardware.Paths[Second.DeviceName] = HardwareBrightness.Path.Wmi;
                PumpFor(1600); // Real hardware-settling grace period.
                f.Engine.SyncFromExternalHardware(70);
                PumpUntil(() => f.Hardware.Writes.Any(w => w.Device == Primary.DeviceName && w.Percent == 70));
                Equal(70, f.Engine.HardwareLevel);
            }),
            ("External WMI change is not overwritten by its monitor offset", () =>
            {
                using var f = new Fixture(50, secondHardware: true);
                f.Hardware.Paths[Second.DeviceName] = HardwareBrightness.Path.Wmi;
                f.Settings.PerMonitor[Second.StableId] = new PerMonitor { BrightnessOffset = 10 };
                PumpFor(1600);
                int before = f.Hardware.Writes.Count(w => w.Device == Second.DeviceName);
                f.Engine.SyncFromExternalHardware(70);
                PumpUntil(() => f.Hardware.Writes.Any(w => w.Device == Primary.DeviceName && w.Percent == 70));
                PumpFor(50);
                Equal(before, f.Hardware.Writes.Count(w => w.Device == Second.DeviceName));
            }),
            ("External WMI change clears a previous failed-write fallback", () =>
            {
                using var f = new Fixture(100, secondHardware: true);
                f.Hardware.Paths[Second.DeviceName] = HardwareBrightness.Path.Wmi;
                f.Hardware.Results[Second.DeviceName] = false;
                f.Engine.SetSignedBrightness(50);
                PumpUntil(() => f.Overlays.Opacities[Second.DeviceName] > 0);
                PumpFor(1600);
                f.Engine.SyncFromExternalHardware(70);
                Equal(0, f.Overlays.Opacities[Second.DeviceName]);
            }),
        };
        int failed = 0;
        foreach (var test in tests)
        {
            try { test.Test(); Console.WriteLine($"PASS {test.Name}"); }
            catch (Exception ex) { failed++; Console.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
        }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} passed");
        return failed == 0 ? 0 : 1;
    }

    private static void Equal(double expected, double actual)
    {
        if (Math.Abs(expected - actual) > 0.0001)
            throw new InvalidOperationException($"Expected {expected:F4}, got {actual:F4}");
    }

    private static void PumpUntil(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!done() && DateTime.UtcNow < deadline) PumpFor(10);
        if (!done()) throw new InvalidOperationException("Timed out waiting for engine output");
    }

    private static void PumpFor(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromMilliseconds(milliseconds)
        };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private sealed class Fixture : IDisposable
    {
        public AppSettings Settings { get; } = new() { SmoothRamping = false };
        public GammaRampApplier Gamma { get; } = new();
        public OverlayManager Overlays { get; } = new();
        public HardwareBrightness Hardware { get; } = new();
        public UnderlitEngine Engine { get; }
        public Fixture(double brightness, bool mainHardware = true, bool secondHardware = false)
        {
            Settings.BrightnessLevel = brightness;
            if (mainHardware) Hardware.Paths[Primary.DeviceName] = HardwareBrightness.Path.DdcCi;
            if (secondHardware) Hardware.Paths[Second.DeviceName] = HardwareBrightness.Path.DdcCi;
            Engine = new(Settings, Dispatcher.CurrentDispatcher, Gamma, Overlays, Hardware);
            Engine.RefreshDisplays(new() { Primary, Second });
            PumpFor(50);
        }
        public void Dispose() { Engine.Dispose(); PumpFor(20); }
    }
}
