using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Underlit.Display;

namespace Underlit.UI;

/// <summary>
/// Owns one OverlayWindow per monitor. Rebuilds when displays change.
/// All calls marshalled to the UI thread by the caller (engine).
/// </summary>
public sealed class OverlayManager : IDisposable
{
    private readonly Dictionary<string, OverlayWindow> _overlays = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DisplayInfo> _displays = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>v0.6.74: while dim is active, re-assert HWND_TOPMOST
    /// every 500 ms so shell UI like Windows Search (which uses a
    /// higher-priority topmost class) can't render above the overlay.
    /// Stopped when opacity returns to 0 so we don't burn cycles when
    /// there's nothing to dim.</summary>
    private DispatcherTimer? _topmostTimer;
    private const int TopmostReassertIntervalMs = 500;

    public void Sync(IReadOnlyList<DisplayInfo> displays)
    {
        if (_disposed) return;

        var currentNames = new HashSet<string>(displays.Select(d => d.DeviceName), StringComparer.OrdinalIgnoreCase);

        // Remove overlays for monitors that vanished.
        var stale = _overlays.Keys.Where(k => !currentNames.Contains(k)).ToList();
        foreach (var name in stale)
        {
            try { _overlays[name].Close(); } catch { /* ignore */ }
            _overlays.Remove(name);
        }

        _displays.Clear();
        foreach (var d in displays) _displays[d.DeviceName] = d;

        // Only update existing windows here. Creating even an invisible WPF
        // layered window can allocate full-monitor rendering surfaces.
        // SetOpacity/SetOpacityPerMonitor create a window when it is needed.
        foreach (var d in displays)
        {
            if (_overlays.TryGetValue(d.DeviceName, out var existing))
            {
                existing.ApplyBounds(d);
            }
        }
        UpdateTopmostTimerState(_overlays.Count > 0);
    }

    /// <summary>Set opacity for all overlays (0 = invisible, 0.92 = almost black).</summary>
    public void SetOpacity(double opacity)
    {
        foreach (var d in _displays.Values)
            SetMonitorOpacity(d, opacity);
        UpdateTopmostTimerState(_overlays.Count > 0);
    }

    /// <summary>Per-monitor opacity override. Falls back to global opacity if not specified.</summary>
    public void SetOpacityPerMonitor(IDictionary<string, double> perMonitor, double fallback)
    {
        foreach (var d in _displays.Values)
        {
            var opacity = perMonitor.TryGetValue(d.DeviceName, out var v) ? v : fallback;
            SetMonitorOpacity(d, opacity);
        }
        UpdateTopmostTimerState(_overlays.Count > 0);
    }

    private void SetMonitorOpacity(DisplayInfo display, double opacity)
    {
        if (_disposed) return;
        if (opacity <= 0)
        {
            if (_overlays.Remove(display.DeviceName, out var unused))
                unused.Close();
            return;
        }

        if (!_overlays.TryGetValue(display.DeviceName, out var window))
        {
            window = new OverlayWindow(display);
            window.SetDimOpacity(opacity);
            window.Show();
            _overlays[display.DeviceName] = window;
        }
        else window.SetDimOpacity(opacity);
    }

    /// <summary>v0.6.74: start the periodic topmost re-assertion timer
    /// when at least one overlay is visible; stop it when everything
    /// fades back to 0. Cheap when active (a single SWP per monitor
    /// every 500 ms), zero-cost when stopped.</summary>
    private void UpdateTopmostTimerState(bool active)
    {
        if (active)
        {
            if (_topmostTimer == null)
            {
                _topmostTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(TopmostReassertIntervalMs),
                };
                _topmostTimer.Tick += (_, _) =>
                {
                    foreach (var w in _overlays.Values) w.ReassertTopmost();
                };
            }
            if (!_topmostTimer.IsEnabled) _topmostTimer.Start();
        }
        else
        {
            _topmostTimer?.Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _topmostTimer?.Stop();
        _topmostTimer = null;
        foreach (var w in _overlays.Values)
        {
            try { w.Close(); } catch { /* ignore */ }
        }
        _overlays.Clear();
        _displays.Clear();
    }
}
