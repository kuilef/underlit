using System;
using System.Management;

namespace Underlit.Display;

/// <summary>
/// Controls built-in laptop-panel brightness via WMI (WmiMonitorBrightness /
/// WmiMonitorBrightnessMethods) in the root\WMI namespace.
/// Only applies to the internal panel; external monitors go through DDC/CI.
/// </summary>
public static class WmiBrightness
{
    /// <summary>Returns 0–100, or null if no WMI-controllable monitor exists.</summary>
    public static int? TryGet() => TryGet(out _);

    /// <summary>Distinguishes missing WMI brightness support from a temporary
    /// service/driver failure, so resume cannot disable a known laptop panel.</summary>
    public static int? TryGet(out bool unsupported)
    {
        unsupported = false;
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                using (obj)
                    return Convert.ToInt32(obj["CurrentBrightness"]);
            }
            unsupported = true; // The query succeeded, but there is no panel.
        }
        catch (ManagementException ex) when (ex.ErrorCode is ManagementStatus.NotSupported or ManagementStatus.InvalidClass)
        {
            unsupported = true;
            Logger.Info("WMI brightness is not supported on this display configuration");
        }
        catch (Exception ex)
        {
            Logger.Warn("WMI brightness get failed", ex);
        }
        return null;
    }

    /// <summary>Set brightness 0–100. Returns true on success. Harmless no-op if no WMI monitor.</summary>
    public static bool TrySet(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                using (obj)
                {
                    // timeout=0 (apply for this session, not permanently)
                    obj.InvokeMethod("WmiSetBrightness", new object[] { (uint)0, (byte)percent });
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("WMI brightness set failed", ex);
        }
        return false;
    }
}
