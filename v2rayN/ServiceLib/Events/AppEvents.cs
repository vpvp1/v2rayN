namespace ServiceLib.Events;

public static class AppEvents
{
    public static readonly EventChannel<Unit> ReloadRequested = new();
    public static readonly EventChannel<bool?> ShowHideWindowRequested = new();
    public static readonly EventChannel<Unit> AddServerViaScanRequested = new();
    public static readonly EventChannel<Unit> AddServerViaClipboardRequested = new();
    public static readonly EventChannel<bool> SubscriptionsUpdateRequested = new();

    public static readonly EventChannel<Unit> ProfilesRefreshRequested = new();
    public static readonly EventChannel<Unit> SubscriptionsRefreshRequested = new();
    public static readonly EventChannel<Unit> ProxiesReloadRequested = new();
    public static readonly EventChannel<ServerSpeedItem> DispatcherStatisticsRequested = new();

    public static readonly EventChannel<string> SendSnackMsgRequested = new();
    public static readonly EventChannel<string> SendMsgViewRequested = new();

    public static readonly EventChannel<Unit> AppExitRequested = new();
    public static readonly EventChannel<bool> ShutdownRequested = new();

    public static readonly EventChannel<Unit> AdjustMainLvColWidthRequested = new();

    public static readonly EventChannel<string> SetDefaultServerRequested = new();

    public static readonly EventChannel<Unit> RoutingsMenuRefreshRequested = new();
    public static readonly EventChannel<Unit> TestServerRequested = new();
    public static readonly EventChannel<Unit> InboundDisplayRequested = new();
    public static readonly EventChannel<ESysProxyType> SysProxyChangeRequested = new();

    /// <summary>
    /// Published whenever the user toggles Auto Switch on/off (e.g. from the
    /// status bar or tray menu), or changes the rotation interval.
    /// </summary>
    public static readonly EventChannel<bool> AutoSwitchToggleRequested = new();

    /// <summary>
    /// Published whenever the set of profiles enabled for Auto Switch, or their
    /// rotation order, changes (e.g. checkbox/order edited in the Profiles grid).
    /// Lets AutoSwitchManager refresh its rotation list.
    /// </summary>
    public static readonly EventChannel<Unit> AutoSwitchListChanged = new();

    /// <summary>
    /// Published by AutoSwitchManager whenever the active rotation state changes
    /// (enabled/disabled, or interval updated), so UI elements (status bar, tray)
    /// can refresh their display.
    /// </summary>
    public static readonly EventChannel<Unit> AutoSwitchStateChanged = new();
}
