using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace StandUpBro;

/// <summary>
/// Main application window — manages the countdown timer, system tray icon,
/// toast notifications, and the autostart registry entry.
///
/// Optimizations:
///   • Strictly event-driven (DispatcherTimer only, no loops/sleeps).
///   • Timer display updates are skipped when the window is hidden to tray
///     so the app uses 0 % CPU while backgrounded.
///   • Supports a "--silent" launch flag: hides to tray immediately and
///     defers the first timer start by 10 seconds so it doesn't compete
///     with other startup programs for CPU.
/// </summary>
public partial class MainWindow : Window
{
    // ── Constants ──────────────────────────────────────────────────────
    private const string AppRegistryKey = "StandUpBro";
    private const string RunRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const int StartupDeferralSeconds = 10;

    // ── Fields ────────────────────────────────────────────────────────
    private readonly DispatcherTimer _countdownTimer;
    private readonly Forms.NotifyIcon _trayIcon;
    private TimeSpan _remainingTime;
    private bool _isExiting;        // true when user picks "Exit" from tray
    private bool _isHiddenToTray;   // true while the window is hidden (skip UI updates)

    // ═══════════════════════════════════════════════════════════════════
    //  Lifecycle
    // ═══════════════════════════════════════════════════════════════════

    public MainWindow()
    {
        InitializeComponent();

        // --- Countdown timer (ticks every second, purely event-driven) ---
        _countdownTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += CountdownTimer_Tick;

        // --- System tray icon ---
        _trayIcon = CreateTrayIcon();

        // --- Reflect current autostart state in the checkbox ---
        AutostartCheckBox.IsChecked = IsAutostartEnabled();

        // --- Handle silent startup (e.g. launched by Windows autostart) ---
        if (WasLaunchedSilently())
        {
            Loaded += (_, _) => HandleSilentStartup();
        }
    }

    /// <summary>
    /// Checks if the app was launched with the "--silent" flag,
    /// which the autostart registry entry includes.
    /// </summary>
    private static bool WasLaunchedSilently()
    {
        string[] args = Environment.GetCommandLineArgs();
        return args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// When launched silently at Windows startup:
    ///   1. Immediately hide to system tray (no window flash).
    ///   2. Use a one-shot DispatcherTimer to wait 10 s, then auto-start
    ///      the countdown — giving Windows time to finish booting.
    /// </summary>
    private void HandleSilentStartup()
    {
        HideToTray();

        // Deferred start: wait 10 seconds, then kick off the countdown.
        // This is a one-shot timer — no loops, no Thread.Sleep.
        var deferTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(StartupDeferralSeconds)
        };
        deferTimer.Tick += (_, _) =>
        {
            deferTimer.Stop(); // one-shot: stop immediately

            if (int.TryParse(IntervalInput.Text.Trim(), out int minutes) && minutes > 0)
            {
                _remainingTime = TimeSpan.FromMinutes(minutes);
                // Don't update display yet — we're hidden, save the work
                _countdownTimer.Start();

                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                IntervalInput.IsEnabled = false;
                StatusLabel.Text = "Running — go get stuff done!";
            }
        };
        deferTimer.Start();
    }

    /// <summary>
    /// Intercept the window "close" to hide to tray instead, unless the
    /// user explicitly chose "Exit" from the tray context menu.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        // Clean up tray icon before true exit
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        base.OnClosing(e);
    }

    /// <summary>
    /// Also hide to tray when the user minimizes.
    /// </summary>
    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            HideToTray();

        base.OnStateChanged(e);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Timer logic  (strictly event-driven — no loops, no sleeps)
    // ═══════════════════════════════════════════════════════════════════

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(IntervalInput.Text.Trim(), out int minutes) || minutes <= 0)
        {
            MessageBox.Show("Please enter a valid number of minutes (> 0).",
                            "Invalid interval", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _remainingTime = TimeSpan.FromMinutes(minutes);
        UpdateTimerDisplay();

        _countdownTimer.Start();

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        IntervalInput.IsEnabled = false;
        StatusLabel.Text = "Running — go get stuff done!";
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopTimer();
    }

    private void StopTimer()
    {
        _countdownTimer.Stop();

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        IntervalInput.IsEnabled = true;
        StatusLabel.Text = "Stopped";
    }

    private void CountdownTimer_Tick(object? sender, EventArgs e)
    {
        _remainingTime -= TimeSpan.FromSeconds(1);

        if (_remainingTime <= TimeSpan.Zero)
        {
            _countdownTimer.Stop();
            OnTimerExpired();
            return;
        }

        // Skip UI updates while hidden — saves CPU (0 % when in tray)
        if (!_isHiddenToTray)
            UpdateTimerDisplay();
    }

    private void UpdateTimerDisplay()
    {
        TimerDisplay.Text = _remainingTime.ToString(@"mm\:ss");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Timer expiry — notification + math challenge
    // ═══════════════════════════════════════════════════════════════════

    private void OnTimerExpired()
    {
        // 1. Show a Windows toast notification
        _trayIcon.BalloonTipTitle = "StandUpBro";
        _trayIcon.BalloonTipText = "Stand up, Bro! 🧍";
        _trayIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(5000);

        // 2. Show the modal math challenge (blocks until solved)
        var challenge = new MathChallengeWindow();
        challenge.ShowDialog();

        // 3. Restart the timer for the next cycle
        if (int.TryParse(IntervalInput.Text.Trim(), out int minutes) && minutes > 0)
        {
            _remainingTime = TimeSpan.FromMinutes(minutes);

            // Only touch the UI if visible
            if (!_isHiddenToTray)
                UpdateTimerDisplay();

            _countdownTimer.Start();
            StatusLabel.Text = "Running — go get stuff done!";
        }
        else
        {
            StopTimer();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  System tray
    // ═══════════════════════════════════════════════════════════════════

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var icon = new Forms.NotifyIcon
        {
            Text = "StandUpBro",
            Icon = LoadEmbeddedIcon(),
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        // Double-click tray icon → show window
        icon.DoubleClick += (_, _) => ShowFromTray();

        return icon;
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        var openItem = new Forms.ToolStripMenuItem("Open StandUpBro");
        openItem.Click += (_, _) => ShowFromTray();

        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();

        menu.Items.Add(openItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    private void HideToTray()
    {
        _isHiddenToTray = true;
        Hide();
        WindowState = WindowState.Normal; // reset so it restores properly
    }

    private void ShowFromTray()
    {
        _isHiddenToTray = false;

        // Refresh the display with the current remaining time
        // (it wasn't updated while hidden)
        if (_countdownTimer.IsEnabled)
            UpdateTimerDisplay();

        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _countdownTimer.Stop();
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Loads the application icon for the system tray.
    /// Tries the output directory first, then extracts from the exe, then falls back to a system icon.
    /// Wrapped in try-catch so a malformed .ico never crashes the app.
    /// </summary>
    private static Icon LoadEmbeddedIcon()
    {
        // Try loading from the Resources folder next to the executable
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(exeDir, "Resources", "icon.ico"),
            Path.Combine(exeDir, "icon.ico"),
        ];

        foreach (string path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    return new Icon(path);
                }
                catch
                {
                    // Malformed icon file — skip to next candidate
                }
            }
        }

        // Fallback: extract the icon embedded in the exe by ApplicationIcon
        try
        {
            string? exePath = Environment.ProcessPath;
            if (exePath != null)
            {
                System.Drawing.Icon? extracted = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (extracted != null)
                    return extracted;
            }
        }
        catch
        {
            // Extraction failed — fall through
        }

        // Last resort: use a built-in system icon
        return SystemIcons.Application;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Autostart (Windows Registry)
    // ═══════════════════════════════════════════════════════════════════

    private void AutostartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        bool enable = AutostartCheckBox.IsChecked == true;
        SetAutostart(enable);
    }

    /// <summary>
    /// Adds or removes the app from the HKCU Run key so it launches at login.
    /// The "--silent" flag tells the app to hide to tray and defer startup.
    /// </summary>
    private static void SetAutostart(bool enable)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true);
            if (key == null) return;

            if (enable)
            {
                string exePath = Environment.ProcessPath
                    ?? Path.Combine(AppContext.BaseDirectory, "StandUpBro.exe");
                key.SetValue(AppRegistryKey, $"\"{exePath}\" --silent");
            }
            else
            {
                key.DeleteValue(AppRegistryKey, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not update autostart setting:\n{ex.Message}",
                            "Autostart Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Checks whether the app is currently registered to start with Windows.
    /// </summary>
    private static bool IsAutostartEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryPath);
            return key?.GetValue(AppRegistryKey) != null;
        }
        catch
        {
            return false;
        }
    }
}
