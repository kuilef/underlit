using Underlit.Display;

internal static class Program
{
    private static readonly DisplayInfo[] Displays =
    {
        new() { DeviceName = "DISPLAY1", IsPrimary = true },
        new() { DeviceName = "DISPLAY2" }
    };

    public static int Main()
    {
        var tests = new (string Name, Action<GammaRampApplier> Run)[]
        {
            ("Unchanged gamma avoids repeated native writes", gamma =>
            {
                gamma.Apply(1, 2734);
                for (int i = 0; i < 20; i++)
                {
                    var result = gamma.Apply(1, 2734);
                    Equal(2, result.Count);
                }
                Equal(2, NativeMethods.Writes.Count);
            }),
            ("Warmth change reaches both monitors", gamma =>
            {
                gamma.Apply(1, 2734);
                gamma.Apply(1, 3000);
                Equal(4, NativeMethods.Writes.Count);
            }),
            ("Gamma brightness change reaches both monitors", gamma =>
            {
                gamma.Apply(1, 2734);
                gamma.Apply(0.8, 2734);
                Equal(4, NativeMethods.Writes.Count);
            }),
            ("Rejected writes are retried without rewriting successful monitor", gamma =>
            {
                NativeMethods.Failures.Add(new IntPtr(2));
                Equal(1, gamma.Apply(1, 2734).Count);
                NativeMethods.Failures.Clear();
                Equal(2, gamma.Apply(1, 2734).Count);
                Equal(3, NativeMethods.Writes.Count);
            }),
            ("Display change reasserts unchanged gamma", gamma =>
            {
                gamma.Apply(1, 2734);
                gamma.Register(Displays);
                gamma.Apply(1, 2734);
                Equal(4, NativeMethods.Writes.Count);
            }),
            ("Restore allows same target to be reapplied", gamma =>
            {
                gamma.Apply(1, 2734);
                gamma.Restore();
                NativeMethods.Writes.Clear();
                gamma.Apply(1, 2734);
                Equal(2, NativeMethods.Writes.Count);
            }),
            ("Resume invalidation reasserts unchanged gamma", gamma =>
            {
                gamma.Apply(1, 2734);
                gamma.Invalidate();
                gamma.Apply(1, 2734);
                Equal(4, NativeMethods.Writes.Count);
            }),
            ("External gamma reset is repaired after cache freshness expires", gamma =>
            {
                gamma.Apply(1, 2734);
                var expected = (ushort[])NativeMethods.CurrentRamps[new IntPtr(2)].Clone();
                NativeMethods.CurrentRamps[new IntPtr(2)] = new ushort[768];
                Thread.Sleep(550);
                gamma.Apply(1, 2734);
                if (!NativeMethods.CurrentRamps[new IntPtr(2)].SequenceEqual(expected))
                    throw new Exception("Externally overwritten gamma was never reasserted");
            }),
        };
        int failed = 0;
        foreach (var test in tests)
        {
            NativeMethods.Writes.Clear();
            NativeMethods.Failures.Clear();
            NativeMethods.CurrentRamps.Clear();
            using var gamma = new GammaRampApplier();
            gamma.Register(Displays);
            try { test.Run(gamma); Console.WriteLine($"PASS {test.Name}"); }
            catch (Exception ex) { failed++; Console.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
        }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} passed");
        return failed == 0 ? 0 : 1;
    }

    private static void Equal(int expected, int actual)
    {
        if (actual != expected) throw new Exception($"Expected {expected}, got {actual}");
    }
}
