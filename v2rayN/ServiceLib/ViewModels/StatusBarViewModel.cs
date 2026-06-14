namespace ServiceLib.ViewModels;

public class StatusBarViewModel : MyReactiveObject
{
    private static readonly Lazy<StatusBarViewModel> _instance = new(() => new(null));
    public static StatusBarViewModel Instance => _instance.Value;

    #region ObservableCollection

    public IObservableCollection<RoutingItem> RoutingItems { get; } = new ObservableCollectionExtended<RoutingItem>();

    public IObservableCollection<ComboItem> Servers { get; } = new ObservableCollectionExtended<ComboItem>();

    [Reactive]
    public RoutingItem SelectedRouting { get; set; }

    [Reactive]
    public ComboItem SelectedServer { get; set; }

    [Reactive]
    public bool BlServers { get; set; }

    #endregion ObservableCollection

    public ReactiveCommand<Unit, Unit> AddServerViaClipboardCmd { get; }
    public ReactiveCommand<Unit, Unit> AddServerViaScanCmd { get; }
    public ReactiveCommand<Unit, Unit> SubUpdateCmd { get; }
    public ReactiveCommand<Unit, Unit> SubUpdateViaProxyCmd { get; }
    public ReactiveCommand<Unit, Unit> CopyProxyCmdToClipboardCmd { get; }
    public ReactiveCommand<Unit, Unit> NotifyLeftClickCmd { get; }
    public ReactiveCommand<Unit, Unit> ShowWindowCmd { get; }
    public ReactiveCommand<Unit, Unit> HideWindowCmd { get; }

    #region System Proxy

    [Reactive]
    public bool BlSystemProxyClear { get; set; }

    [Reactive]
    public bool BlSystemProxySet { get; set; }

    [Reactive]
    public bool BlSystemProxyNothing { get; set; }

    [Reactive]
    public bool BlSystemProxyPac { get; set; }

    public ReactiveCommand<Unit, Unit> SystemProxyClearCmd { get; }
    public ReactiveCommand<Unit, Unit> SystemProxySetCmd { get; }
    public ReactiveCommand<Unit, Unit> SystemProxyNothingCmd { get; }
    public ReactiveCommand<Unit, Unit> SystemProxyPacCmd { get; }

    [Reactive]
    public bool BlRouting { get; set; }

    [Reactive]
    public int SystemProxySelected { get; set; }

    [Reactive]
    public bool BlSystemProxyPacVisible { get; set; }

    #endregion System Proxy

    #region UI

    [Reactive]
    public string InboundDisplay { get; set; }

    [Reactive]
    public string InboundLanDisplay { get; set; }

    [Reactive]
    public string RunningServerDisplay { get; set; }

    [Reactive]
    public string RunningServerToolTipText { get; set; }

    [Reactive]
    public string RunningInfoDisplay { get; set; }

    [Reactive]
    public string SpeedProxyDisplay { get; set; }

    [Reactive]
    public string SpeedDirectDisplay { get; set; }

    [Reactive]
    public bool EnableTun { get; set; }

    [Reactive]
    public bool BlIsNonWindows { get; set; }

    [Reactive]
    public List<ComboItem> OutboundInterfaces { get; set; } = [];

    [Reactive]
    public ComboItem? SelectedOutboundInterface { get; set; }

    /// <summary>
    /// Whether automatic config rotation (Auto Switch) is currently enabled.
    /// Bound to the toggle in the status bar and the tray menu.
    /// </summary>
    [Reactive]
    public bool AutoSwitchEnabled { get; set; }

    /// <summary>
    /// Rotation interval, in seconds, between automatic config switches.
    /// Bound to a text box in the status bar and tray menu.
    /// </summary>
    [Reactive]
    public string AutoSwitchIntervalSecondsText { get; set; } = "60";

    #endregion UI

    public StatusBarViewModel(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _config = AppManager.Instance.Config;
        SelectedRouting = new();
        SelectedServer = new();
        RunningServerToolTipText = "-";
        BlSystemProxyPacVisible = Utils.IsWindows();
        BlIsNonWindows = Utils.IsNonWindows();

        if (_config.TunModeItem.EnableTun && AllowEnableTun())
        {
            EnableTun = true;
        }
        else
        {
            _config.TunModeItem.EnableTun = EnableTun = false;
        }

        #region WhenAnyValue && ReactiveCommand

        this.WhenAnyValue(
                x => x.SelectedRouting,
                y => y != null && !y.Remarks.IsNullOrEmpty())
            .Subscribe(async c => await RoutingSelectedChangedAsync(c));

        this.WhenAnyValue(
                x => x.SelectedServer,
                y => y != null && !y.Text.IsNullOrEmpty())
            .Subscribe(ServerSelectedChanged);

        SystemProxySelected = (int)_config.SystemProxyItem.SysProxyType;
        this.WhenAnyValue(
                x => x.SystemProxySelected,
                y => y >= 0)
            .Subscribe(async c => await DoSystemProxySelected(c));

        this.WhenAnyValue(
                x => x.EnableTun,
                y => y == true)
            .Subscribe(async c => await DoEnableTun(c));

        CopyProxyCmdToClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await CopyProxyCmdToClipboard();
        });

        NotifyLeftClickCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            AppEvents.ShowHideWindowRequested.Publish(null);
            await Task.CompletedTask;
        });
        ShowWindowCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            AppEvents.ShowHideWindowRequested.Publish(true);
            await Task.CompletedTask;
        });
        HideWindowCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            AppEvents.ShowHideWindowRequested.Publish(false);
            await Task.CompletedTask;
        });

        AddServerViaClipboardCmd = ReactiveCommand.CreateFromTask(async () =>
            {
                await AddServerViaClipboard();
            });
        AddServerViaScanCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await AddServerViaScan();
        });
        SubUpdateCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess(false);
        });
        SubUpdateViaProxyCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSubscriptionProcess(true);
        });

        //System proxy
        SystemProxyClearCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetListenerType(ESysProxyType.ForcedClear);
        });
        SystemProxySetCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetListenerType(ESysProxyType.ForcedChange);
        });
        SystemProxyNothingCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetListenerType(ESysProxyType.Unchanged);
        });
        SystemProxyPacCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SetListenerType(ESysProxyType.Pac);
        });

        // Outbound Interface: watch selection changes
        this.WhenAnyValue(x => x.SelectedOutboundInterface)
            .Subscribe(item => OutboundInterfaceSelectedChanged(item));

        InitOutboundInterfaces();

        // Auto Switch: initial state from config
        AutoSwitchEnabled = _config.AutoSwitchItem?.Enabled == true;
        AutoSwitchIntervalSecondsText = (_config.AutoSwitchItem?.IntervalSeconds ?? 60).ToString();

        this.WhenAnyValue(x => x.AutoSwitchEnabled)
            .Skip(1)
            .Subscribe(enabled => AutoSwitchEnabledChanged(enabled));

        this.WhenAnyValue(x => x.AutoSwitchIntervalSecondsText)
            .Skip(1)
            .Subscribe(text => AutoSwitchIntervalChanged(text));

        AppEvents.AutoSwitchStateChanged
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => RefreshAutoSwitchUi());

        #endregion WhenAnyValue && ReactiveCommand

        #region AppEvents

        if (updateView != null)
        {
            InitUpdateView(updateView);
        }

        AppEvents.DispatcherStatisticsRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async result => await UpdateStatistics(result));

        AppEvents.RoutingsMenuRefreshRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await RefreshRoutingsMenu());

        AppEvents.TestServerRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await TestServerAvailability());

        AppEvents.InboundDisplayRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async _ => await InboundDisplayStatus());

        AppEvents.SysProxyChangeRequested
            .AsObservable()
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(async result => await SetListenerType(result));

        #endregion AppEvents

        _ = Init();
    }

    private async Task Init()
    {
        await ConfigHandler.InitBuiltinRouting(_config);
        await RefreshRoutingsMenu();
        await InboundDisplayStatus();
        await ChangeSystemProxyAsync(_config.SystemProxyItem.SysProxyType, true);
    }

    public void InitUpdateView(Func<EViewAction, object?, Task<bool>>? updateView)
    {
        _updateView = updateView;
        if (_updateView != null)
        {
            AppEvents.ProfilesRefreshRequested
              .AsObservable()
              .ObserveOn(RxSchedulers.MainThreadScheduler)
              .Subscribe(async _ => await RefreshServersBiz()); //.DisposeWith(_disposables);
        }
    }

    private async Task CopyProxyCmdToClipboard()
    {
        var cmd = Utils.IsWindows() ? "set" : "export";
        var address = $"{Global.Loopback}:{AppManager.Instance.GetLocalPort(EInboundProtocol.socks)}";

        var sb = new StringBuilder();
        sb.AppendLine($"{cmd} http_proxy={Global.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} https_proxy={Global.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} all_proxy={Global.Socks5Protocol}{address}");
        sb.AppendLine("");
        sb.AppendLine($"{cmd} HTTP_PROXY={Global.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} HTTPS_PROXY={Global.HttpProtocol}{address}");
        sb.AppendLine($"{cmd} ALL_PROXY={Global.Socks5Protocol}{address}");

        await _updateView?.Invoke(EViewAction.SetClipboardData, sb.ToString());
    }

    private async Task AddServerViaClipboard()
    {
        AppEvents.AddServerViaClipboardRequested.Publish();
        await Task.Delay(1000);
    }

    private async Task AddServerViaScan()
    {
        AppEvents.AddServerViaScanRequested.Publish();
        await Task.Delay(1000);
    }

    private async Task UpdateSubscriptionProcess(bool blProxy)
    {
        AppEvents.SubscriptionsUpdateRequested.Publish(blProxy);
        await Task.Delay(1000);
    }

    private async Task RefreshServersBiz()
    {
        await RefreshServersMenu();

        //display running server
        var running = await ConfigHandler.GetDefaultServer(_config);
        if (running != null)
        {
            RunningServerDisplay =
                RunningServerToolTipText = running.GetSummary();
        }
        else
        {
            RunningServerDisplay =
                RunningServerToolTipText = ResUI.CheckServerSettings;
        }
    }

    private async Task RefreshServersMenu()
    {
        var lstModel = await AppManager.Instance.ProfileModels(_config.SubIndexId, "");

        Servers.Clear();
        if (lstModel.Count > _config.GuiItem.TrayMenuServersLimit)
        {
            BlServers = false;
            return;
        }

        BlServers = true;
        for (var k = 0; k < lstModel.Count; k++)
        {
            var it = lstModel[k];
            var name = it.GetSummary();

            var item = new ComboItem() { ID = it.IndexId, Text = name };
            Servers.Add(item);
            if (_config.IndexId == it.IndexId)
            {
                SelectedServer = item;
            }
        }
    }

    private void ServerSelectedChanged(bool c)
    {
        if (!c)
        {
            return;
        }
        if (SelectedServer == null)
        {
            return;
        }
        if (SelectedServer.ID.IsNullOrEmpty())
        {
            return;
        }
        AppEvents.SetDefaultServerRequested.Publish(SelectedServer.ID);
    }

    public async Task TestServerAvailability()
    {
        var item = await ConfigHandler.GetDefaultServer(_config);
        if (item == null)
        {
            return;
        }

        await TestServerAvailabilitySub(ResUI.Speedtesting);

        var msg = await Task.Run(ConnectionHandler.RunAvailabilityCheck);

        NoticeManager.Instance.SendMessageEx(msg);
        await TestServerAvailabilitySub(msg);
    }

    private async Task TestServerAvailabilitySub(string msg)
    {
        RxSchedulers.MainThreadScheduler.Schedule(msg, (scheduler, msg) =>
        {
            _ = TestServerAvailabilityResult(msg);
            return Disposable.Empty;
        });
        await Task.CompletedTask;
    }

    public async Task TestServerAvailabilityResult(string msg)
    {
        RunningInfoDisplay = msg;
        await Task.CompletedTask;
    }

    #region System proxy and Routings

    private async Task SetListenerType(ESysProxyType type)
    {
        if (_config.SystemProxyItem.SysProxyType == type)
        {
            return;
        }
        _config.SystemProxyItem.SysProxyType = type;
        await ChangeSystemProxyAsync(type, true);
        NoticeManager.Instance.SendMessageEx($"{ResUI.TipChangeSystemProxy} - {_config.SystemProxyItem.SysProxyType}");

        SystemProxySelected = (int)_config.SystemProxyItem.SysProxyType;
        await ConfigHandler.SaveConfig(_config);
    }

    public async Task ChangeSystemProxyAsync(ESysProxyType type, bool blChange)
    {
        await SysProxyHandler.UpdateSysProxy(_config, false);

        BlSystemProxyClear = type == ESysProxyType.ForcedClear;
        BlSystemProxySet = type == ESysProxyType.ForcedChange;
        BlSystemProxyNothing = type == ESysProxyType.Unchanged;
        BlSystemProxyPac = type == ESysProxyType.Pac;

        if (blChange)
        {
            _updateView?.Invoke(EViewAction.DispatcherRefreshIcon, null);
        }
    }

    private async Task RefreshRoutingsMenu()
    {
        RoutingItems.Clear();

        BlRouting = true;
        var routings = await AppManager.Instance.RoutingItems();
        foreach (var item in routings)
        {
            RoutingItems.Add(item);
            if (item.IsActive)
            {
                SelectedRouting = item;
            }
        }
    }

    private async Task RoutingSelectedChangedAsync(bool c)
    {
        if (!c)
        {
            return;
        }

        if (SelectedRouting == null)
        {
            return;
        }

        var item = await AppManager.Instance.GetRoutingItem(SelectedRouting?.Id);
        if (item is null)
        {
            return;
        }

        if (await ConfigHandler.SetDefaultRouting(_config, item) == 0)
        {
            NoticeManager.Instance.SendMessageEx(ResUI.TipChangeRouting);
            AppEvents.ReloadRequested.Publish();
            _updateView?.Invoke(EViewAction.DispatcherRefreshIcon, null);
        }
    }

    private async Task DoSystemProxySelected(bool c)
    {
        if (!c)
        {
            return;
        }
        if (_config.SystemProxyItem.SysProxyType == (ESysProxyType)SystemProxySelected)
        {
            return;
        }
        await SetListenerType((ESysProxyType)SystemProxySelected);
    }

    private async Task DoEnableTun(bool c)
    {
        if (_config.TunModeItem.EnableTun == EnableTun)
        {
            return;
        }

        _config.TunModeItem.EnableTun = EnableTun;

        if (EnableTun && AllowEnableTun() == false)
        {
            // When running as a non-administrator, reboot to administrator mode
            if (Utils.IsWindows())
            {
                _config.TunModeItem.EnableTun = false;
                await AppManager.Instance.RebootAsAdmin();
                return;
            }
            else
            {
                bool? passwordResult = await _updateView?.Invoke(EViewAction.PasswordInput, null);
                if (passwordResult == false)
                {
                    _config.TunModeItem.EnableTun = false;
                    return;
                }
            }
        }

        await ConfigHandler.SaveConfig(_config);
        AppEvents.ReloadRequested.Publish();
    }

    private bool AllowEnableTun()
    {
        if (Utils.IsWindows())
        {
            return Utils.IsAdministrator();
        }
        else if (Utils.IsLinux())
        {
            return AppManager.Instance.LinuxSudoPwd.IsNotEmpty();
        }
        else if (Utils.IsMacOS())
        {
            return AppManager.Instance.LinuxSudoPwd.IsNotEmpty();
        }
        return false;
    }

    #endregion System proxy and Routings

    #region UI

    private async Task InboundDisplayStatus()
    {
        StringBuilder sb = new();
        sb.Append($"[{EInboundProtocol.mixed}:{AppManager.Instance.GetLocalPort(EInboundProtocol.socks)}");
        if (_config.Inbound.First().SecondLocalPortEnabled)
        {
            sb.Append($",{AppManager.Instance.GetLocalPort(EInboundProtocol.socks2)}");
        }
        sb.Append(']');
        InboundDisplay = $"{ResUI.LabLocal}:{sb}";

        if (_config.Inbound.First().AllowLANConn)
        {
            var lan = _config.Inbound.First().NewPort4LAN
                ? $"[{EInboundProtocol.mixed}:{AppManager.Instance.GetLocalPort(EInboundProtocol.socks3)}]"
                : $"[{EInboundProtocol.mixed}:{AppManager.Instance.GetLocalPort(EInboundProtocol.socks)}]";
            InboundLanDisplay = $"{ResUI.LabLAN}:{lan}";
        }
        else
        {
            InboundLanDisplay = $"{ResUI.LabLAN}:{Global.None}";
        }
        await Task.CompletedTask;
    }

    public async Task UpdateStatistics(ServerSpeedItem update)
    {
        if (!_config.GuiItem.DisplayRealTimeSpeed)
        {
            return;
        }

        try
        {
            if (AppManager.Instance.IsRunningCore(ECoreType.sing_box))
            {
                SpeedProxyDisplay = string.Format(ResUI.SpeedDisplayText, EInboundProtocol.mixed, Utils.HumanFy(update.ProxyUp), Utils.HumanFy(update.ProxyDown));
                SpeedDirectDisplay = string.Empty;
            }
            else
            {
                SpeedProxyDisplay = string.Format(ResUI.SpeedDisplayText, Global.ProxyTag, Utils.HumanFy(update.ProxyUp), Utils.HumanFy(update.ProxyDown));
                SpeedDirectDisplay = string.Format(ResUI.SpeedDisplayText, Global.DirectTag, Utils.HumanFy(update.DirectUp), Utils.HumanFy(update.DirectDown));
            }
        }
        catch
        {
        }
        await Task.CompletedTask;
    }

    #region Outbound Interface

    private void InitOutboundInterfaces()
    {
        var interfaces = new List<ComboItem>
        {
            new() { ID = string.Empty, Text = "Auto (Default)" }
        };

        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                    continue;
                if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    continue;
                if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                    continue;

                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                        continue;

                    var ip = addr.Address.ToString();
                    interfaces.Add(new ComboItem
                    {
                        ID   = ip,
                        Text = $"{nic.Name} \u2014 {ip}"
                    });
                }
            }
        }
        catch { }

        OutboundInterfaces = interfaces;

        // Restore saved selection
        var saved = _config.CoreBasicItem.OutboundInterface ?? string.Empty;
        SelectedOutboundInterface = interfaces.FirstOrDefault(i => i.ID == saved) ?? interfaces[0];
    }

    private void OutboundInterfaceSelectedChanged(ComboItem? item)
    {
        if (item == null) return;

        var newVal = item.ID ?? string.Empty;
        if (_config.CoreBasicItem.OutboundInterface == newVal)
            return;

        _config.CoreBasicItem.OutboundInterface = newVal;
        _ = ConfigHandler.SaveConfig(_config);
        AppEvents.ReloadRequested.Publish();
    }

    #endregion Outbound Interface

    #region Auto Switch

    /// <summary>
    /// Called when the user toggles Auto Switch on/off from the status bar
    /// toggle button or the tray menu checkbox.
    /// </summary>
    private void AutoSwitchEnabledChanged(bool enabled)
    {
        AppEvents.AutoSwitchToggleRequested.Publish(enabled);
    }

    /// <summary>
    /// Called when the user edits the rotation interval text box (value in
    /// seconds). Non-numeric or non-positive input is ignored.
    /// </summary>
    private void AutoSwitchIntervalChanged(string text)
    {
        if (!int.TryParse(text?.Trim(), out var seconds) || seconds <= 0)
        {
            return;
        }

        _ = AutoSwitchManager.Instance.SetIntervalSeconds(seconds);
    }

    /// <summary>
    /// Refreshes the status bar / tray toggle and interval display from the
    /// current AutoSwitchManager state, e.g. after the manager auto-disables
    /// itself because the rotation list became empty.
    /// </summary>
    private void RefreshAutoSwitchUi()
    {
        var enabled = AutoSwitchManager.Instance.IsEnabled;
        if (AutoSwitchEnabled != enabled)
        {
            AutoSwitchEnabled = enabled;
        }

        var interval = AutoSwitchManager.Instance.IntervalSeconds.ToString();
        if (AutoSwitchIntervalSecondsText != interval)
        {
            AutoSwitchIntervalSecondsText = interval;
        }
    }

    #endregion Auto Switch

    #endregion UI
}
