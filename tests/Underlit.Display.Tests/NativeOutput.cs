namespace Underlit
{
    internal static class Logger
    {
        public static void Warn(string message) { }
    }
}

namespace Underlit.Display
{
    // Only GDI calls are replaced. Tests compile and exercise the real applier.
    internal static class NativeMethods
    {
        public static List<IntPtr> Writes { get; } = new();
        public static HashSet<IntPtr> Failures { get; } = new();
        public static Dictionary<IntPtr, ushort[]> CurrentRamps { get; } = new();
        public static IntPtr CreateDC(string driver, string device, string? output, IntPtr data)
            => device == "DISPLAY1" ? new IntPtr(1) : new IntPtr(2);
        public static bool DeleteDC(IntPtr dc) => true;
        public static bool GetDeviceGammaRamp(IntPtr dc, ushort[] ramp)
        {
            for (int i = 0; i < 256; i++)
                ramp[i] = ramp[i + 256] = ramp[i + 512] = (ushort)(i * 257);
            return true;
        }
        public static bool SetDeviceGammaRamp(IntPtr dc, ushort[] ramp)
        {
            Writes.Add(dc);
            if (!Failures.Contains(dc)) CurrentRamps[dc] = (ushort[])ramp.Clone();
            return !Failures.Contains(dc);
        }
    }
}
