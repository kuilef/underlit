using Underlit.Display;
using Underlit.UI;
using Underlit.Sys;

internal static class Program
{
    private static readonly DisplayInfo Primary = new() { DeviceName = "MAIN", IsPrimary = true };
    private static readonly DisplayInfo Second = new() { DeviceName = "SECOND" };

    [STAThread]
    public static int MainEntry()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Stopping capture releases the retained full-monitor frame", () =>
            {
                using var capture = new WgcCapture();
                var frame = typeof(WgcCapture).GetField("<LatestFrame>k__BackingField",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                frame.SetValue(capture, new byte[2560 * 1440 * 4]);
                capture.StopCapture();
                if (capture.LatestFrame != null) throw new Exception("Full-monitor frame retained after stop");
                capture.StopCapture(); // Idempotent, including before first capture.
            }),
            ("Monitor discovery does not allocate transparent windows", () =>
            {
                using var overlays = new OverlayManager();
                overlays.Sync(new[] { Primary, Second });
                Equal(0, OverlayWindow.Created.Count);
            }),
            ("Only dimmed monitors allocate windows; clearing dim releases them", () =>
            {
                using var overlays = new OverlayManager();
                overlays.Sync(new[] { Primary, Second });
                var levels = new Dictionary<string, double> { [Second.DeviceName] = 0.3 };
                overlays.SetOpacityPerMonitor(levels, 0);
                Equal(1, OverlayWindow.Created.Count);
                var second = OverlayWindow.Created.Single();
                Equal(0.3, second.Opacity);
                if (second.Display.DeviceName != Second.DeviceName) throw new Exception("Wrong monitor dimmed");
                overlays.SetOpacityPerMonitor(levels, 0);
                Equal(1, OverlayWindow.Created.Count);
                overlays.SetOpacity(0);
                if (!second.Closed) throw new Exception("Invisible window retained");
                overlays.SetOpacityPerMonitor(levels, 0);
                Equal(2, OverlayWindow.Created.Count);
                Equal(0.3, OverlayWindow.Created.Last().Opacity);
            }),
            ("Active overlays follow bounds, disconnect and reconnect", () =>
            {
                using var overlays = new OverlayManager();
                overlays.Sync(new[] { Primary, Second });
                overlays.SetOpacity(0.4);
                var old = OverlayWindow.Created.Single(w => w.Display.DeviceName == Second.DeviceName);
                var moved = new DisplayInfo { DeviceName = Second.DeviceName, Bounds = new(-1920, 0, 1920, 1080) };
                overlays.Sync(new[] { Primary, moved });
                if (old.Display.Bounds.Left != -1920) throw new Exception("Bounds not updated");
                overlays.Sync(new[] { Primary });
                if (!old.Closed) throw new Exception("Disconnected overlay retained");
                overlays.Sync(new[] { Primary, moved });
                overlays.SetOpacity(0.4);
                Equal(3, OverlayWindow.Created.Count);
                overlays.SetOpacity(1);
                Equal(0.92, OverlayWindow.Created.Last().Opacity);
            }),
            ("WMI support is probed again after display changes", () =>
            {
                using var hardware = new HardwareBrightness();
                WmiBrightness.Value = null;
                hardware.Register(new[] { Primary, Second });
                if (hardware.PathFor(Second) != HardwareBrightness.Path.None) throw new Exception("Unexpected WMI support");
                WmiBrightness.Value = 50;
                hardware.Register(new[] { Primary, Second });
                if (hardware.PathFor(Second) != HardwareBrightness.Path.Wmi) throw new Exception("WMI panel did not recover");
                if (hardware.PathFor(Primary) != HardwareBrightness.Path.DdcCi) throw new Exception("DDC path changed");
                WmiBrightness.Value = null;
                hardware.Register(new[] { Primary, Second });
                if (hardware.PathFor(Second) != HardwareBrightness.Path.None) throw new Exception("Stale WMI support");
            }),
            ("Transient WMI failure on resume preserves a known laptop panel", () =>
            {
                using var hardware = new HardwareBrightness();
                WmiBrightness.Value = 50;
                hardware.Register(new[] { Primary, Second });
                WmiBrightness.Value = null;
                WmiBrightness.Unsupported = false; // Temporary WMI service/driver failure.
                hardware.Register(new[] { Primary, Second });
                if (hardware.PathFor(Second) != HardwareBrightness.Path.Wmi)
                    throw new Exception("Known WMI panel disabled by transient probe failure");
                WmiBrightness.Value = 60;
                hardware.Register(new[] { Primary, Second });
                if (hardware.PathFor(Second) != HardwareBrightness.Path.Wmi)
                    throw new Exception("WMI did not recover");
            }),
        };
        int failed = 0;
        foreach (var test in tests)
        {
            OverlayWindow.Created.Clear();
            DdcCiMonitorSet.Supported.Clear();
            WmiBrightness.Unsupported = true;
            DdcCiMonitorSet.Supported.Add(Primary.DeviceName);
            try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
            catch (Exception ex) { failed++; Console.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
        }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} passed");
        return failed == 0 ? 0 : 1;
    }

    public static int Main(string[] args) => MainEntry();

    private static void Equal(double expected, double actual)
    {
        if (Math.Abs(expected - actual) > 0.0001) throw new Exception($"Expected {expected}, got {actual}");
    }
}
