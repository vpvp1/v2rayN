namespace ServiceLib.Manager;

/// <summary>
/// Drives automatic, timer-based rotation between a user-selected set of
/// configs (profiles). The user enables a profile for rotation and assigns it
/// a position via the "Auto Switch" checkbox + "Order" columns in the Profiles
/// grid, then enables the overall feature (and sets the interval, in seconds)
/// from the status bar toggle or tray menu.
///
/// Rotation order is always sequential (1, 2, 3, ... 1, 2, 3, ...), following
/// the AutoSwitchOrder values assigned to each enabled profile.
/// </summary>
public class AutoSwitchManager
{
    private static readonly Lazy<AutoSwitchManager> _instance = new(() => new());
    public static AutoSwitchManager Instance => _instance.Value;

    private static readonly string _tag = "AutoSwitchManager";

    private Config? _config;
    private System.Timers.Timer? _timer;
    private readonly object _syncRoot = new();
    private int _currentRotationIndex = -1;
    private bool _initialized;

    private AutoSwitchManager()
    {
    }

    /// <summary>
    /// Initializes the manager. Safe to call multiple times; only the first
    /// call has effect. If Auto Switch was left enabled from a previous
    /// session, the rotation timer is started immediately.
    /// </summary>
    public Task Init(Config config)
    {
        _config = config;

        if (!_initialized)
        {
            _initialized = true;

            AppEvents.AutoSwitchToggleRequested
                .AsObservable()
                .Subscribe(enabled => _ = SetEnabled(enabled));

            AppEvents.AutoSwitchListChanged
                .AsObservable()
                .Subscribe(_ => OnRotationListChanged());

            if (_config.AutoSwitchItem?.Enabled == true)
            {
                _ = SetEnabled(true);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether Auto Switch is currently active.
    /// </summary>
    public bool IsEnabled => _config?.AutoSwitchItem?.Enabled == true;

    /// <summary>
    /// Current rotation interval, in seconds.
    /// </summary>
    public int IntervalSeconds => Math.Max(1, _config?.AutoSwitchItem?.IntervalSeconds ?? 60);

    /// <summary>
    /// Enables or disables the Auto Switch rotation, persists the setting, and
    /// starts/stops the underlying timer accordingly.
    /// </summary>
    public async Task SetEnabled(bool enabled)
    {
        if (_config?.AutoSwitchItem is null)
        {
            return;
        }

        if (enabled)
        {
            var rotation = ProfileExManager.Instance.GetAutoSwitchItemsOrdered();
            if (rotation.Count == 0)
            {
                Logging.SaveLog(_tag, new InvalidOperationException("No profiles selected for Auto Switch."));
                enabled = false;
            }
        }

        _config.AutoSwitchItem.Enabled = enabled;
        await ConfigHandler.SaveConfig(_config);

        if (enabled)
        {
            StartTimer();
        }
        else
        {
            StopTimer();
        }

        AppEvents.AutoSwitchStateChanged.Publish();
    }

    /// <summary>
    /// Updates the rotation interval (in seconds), persists it, and restarts
    /// the timer (if running) so the new interval takes effect immediately.
    /// </summary>
    public async Task SetIntervalSeconds(int seconds)
    {
        if (_config?.AutoSwitchItem is null)
        {
            return;
        }

        seconds = Math.Max(1, seconds);
        if (_config.AutoSwitchItem.IntervalSeconds == seconds)
        {
            return;
        }

        _config.AutoSwitchItem.IntervalSeconds = seconds;
        await ConfigHandler.SaveConfig(_config);

        if (IsEnabled)
        {
            StartTimer();
        }

        AppEvents.AutoSwitchStateChanged.Publish();
    }

    /// <summary>
    /// Called when the set of profiles enabled for rotation (or their order)
    /// changes. If rotation is currently running and the list becomes empty,
    /// rotation is stopped automatically.
    /// </summary>
    private void OnRotationListChanged()
    {
        if (!IsEnabled)
        {
            return;
        }

        var rotation = ProfileExManager.Instance.GetAutoSwitchItemsOrdered();
        if (rotation.Count == 0)
        {
            _ = SetEnabled(false);
            return;
        }

        // Keep the timer running; the next tick will simply re-evaluate the
        // (possibly changed) rotation list.
        lock (_syncRoot)
        {
            if (_currentRotationIndex >= rotation.Count)
            {
                _currentRotationIndex = -1;
            }
        }
    }

    private void StartTimer()
    {
        StopTimer();

        lock (_syncRoot)
        {
            _currentRotationIndex = -1;
            _timer = new System.Timers.Timer(IntervalSeconds * 1000d)
            {
                AutoReset = true,
            };
            _timer.Elapsed += (_, _) => _ = OnTimerElapsed();
            _timer.Start();
        }

        Logging.SaveLog($"{_tag} - started, interval = {IntervalSeconds}s");
    }

    private void StopTimer()
    {
        lock (_syncRoot)
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }
            _currentRotationIndex = -1;
        }

        Logging.SaveLog($"{_tag} - stopped");
    }

    private async Task OnTimerElapsed()
    {
        try
        {
            await SwitchToNextProfile();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    /// <summary>
    /// Advances the rotation by one step (sequentially, looping back to the
    /// start) and switches the active server to that profile.
    /// </summary>
    private async Task SwitchToNextProfile()
    {
        if (_config is null || !IsEnabled)
        {
            return;
        }

        var rotation = ProfileExManager.Instance.GetAutoSwitchItemsOrdered();
        if (rotation.Count == 0)
        {
            await SetEnabled(false);
            return;
        }

        int nextIndex;
        lock (_syncRoot)
        {
            nextIndex = (_currentRotationIndex + 1) % rotation.Count;
            _currentRotationIndex = nextIndex;
        }

        var nextIndexId = rotation[nextIndex].IndexId;
        if (nextIndexId.IsNullOrEmpty())
        {
            return;
        }

        if (nextIndexId == _config.IndexId)
        {
            // Already the active server; still counts as a rotation step, but
            // there's nothing to switch.
            return;
        }

        var item = await AppManager.Instance.GetProfileItem(nextIndexId);
        if (item is null)
        {
            // The profile may have been deleted; drop it from the rotation and
            // try again on the next tick rather than blocking the cycle.
            ProfileExManager.Instance.RemoveFromAutoSwitch(nextIndexId);
            await ProfileExManager.Instance.SaveTo();
            return;
        }

        if (await ConfigHandler.SetDefaultServerIndex(_config, nextIndexId) == 0)
        {
            Logging.SaveLog($"{_tag} - switched to: {item.GetSummary()}");
            AppEvents.ProfilesRefreshRequested.Publish();
            AppEvents.ReloadRequested.Publish();
        }
    }
}
