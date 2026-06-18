using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace StandUpBro;

/// <summary>
/// Application entry point with single-instance enforcement.
///
/// Uses a named Mutex to detect duplicates and a system-wide
/// EventWaitHandle as a safe IPC signal. When a second instance
/// launches, it signals the event and shuts down. The first instance
/// has a background Task waiting on that event, which uses
/// Dispatcher.Invoke to safely restore the window on the UI thread.
///
/// No Win32 FindWindow/ShowWindow calls — avoids the rendering crash
/// caused by cross-process window manipulation.
/// </summary>
public partial class App : Application
{
    // ── Named system objects for single-instance IPC ──────────────────
    private const string MutexName = "StandUpBroSingleInstanceMutex";
    private const string EventName = "StandUpBroShowWindowEvent";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showWindowEvent;
    private CancellationTokenSource? _listenerCts;

    // ═══════════════════════════════════════════════════════════════════
    //  Startup
    // ═══════════════════════════════════════════════════════════════════

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Try to create / acquire the named mutex
        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance already owns the mutex.
            // Signal it to show its window, then exit immediately.
            try
            {
                using var signal = EventWaitHandle.OpenExisting(EventName);
                signal.Set();
            }
            catch
            {
                // Event doesn't exist yet — the first instance might still
                // be initializing. Nothing more we can do.
            }

            Current.Shutdown();
            return;
        }

        // We are the first (and only) instance.
        // Create the system-wide event that duplicate instances will signal.
        _showWindowEvent = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: EventName);

        // Start a background listener that waits for the signal
        _listenerCts = new CancellationTokenSource();
        StartShowWindowListener(_listenerCts.Token);

        // Show the main window
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Background listener — waits for "show window" signal from
    //  duplicate instances and safely restores the UI via Dispatcher.
    // ═══════════════════════════════════════════════════════════════════

    private void StartShowWindowListener(CancellationToken ct)
    {
        Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                // Block until a duplicate instance signals, or cancellation
                // is requested. WaitOne with a timeout avoids blocking forever
                // so we can check the cancellation token periodically.
                bool signaled = _showWindowEvent!.WaitOne(millisecondsTimeout: 500);

                if (signaled && !ct.IsCancellationRequested)
                {
                    // Marshal back to the UI thread to safely show the window.
                    // Call ShowFromTray() so _isHiddenToTray is reset and the
                    // timer display refreshes properly.
                    Current.Dispatcher.Invoke(() =>
                    {
                        if (MainWindow is MainWindow mw)
                        {
                            mw.BringToFront();
                        }
                    });
                }
            }
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Shutdown — clean up mutex, event, and listener
    // ═══════════════════════════════════════════════════════════════════

    protected override void OnExit(ExitEventArgs e)
    {
        // Stop the background listener
        _listenerCts?.Cancel();

        // Release the mutex so another instance can start fresh
        if (_instanceMutex != null)
        {
            try { _instanceMutex.ReleaseMutex(); } catch { /* already released */ }
            _instanceMutex.Dispose();
        }

        _showWindowEvent?.Dispose();

        base.OnExit(e);
    }
}
