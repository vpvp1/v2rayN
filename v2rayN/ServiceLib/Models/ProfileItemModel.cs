namespace ServiceLib.Models;

[Serializable]
public class ProfileItemModel : ReactiveObject
{
    public bool IsActive { get; set; }
    public string IndexId { get; set; }
    public EConfigType ConfigType { get; set; }
    public string Remarks { get; set; }
    public string Address { get; set; }
    public int Port { get; set; }
    public string Network { get; set; }
    public string StreamSecurity { get; set; }
    public string Subid { get; set; }
    public string SubRemarks { get; set; }
    public int Sort { get; set; }

    [Reactive]
    public int Delay { get; set; }

    public decimal Speed { get; set; }

    [Reactive]
    public string DelayVal { get; set; }

    [Reactive]
    public string SpeedVal { get; set; }

    [Reactive]
    public string TodayUp { get; set; }

    [Reactive]
    public string TodayDown { get; set; }

    [Reactive]
    public string TotalUp { get; set; }

    [Reactive]
    public string TotalDown { get; set; }

    /// <summary>
    /// Whether this profile is included in the Auto Switch rotation (checkbox column).
    /// </summary>
    [Reactive]
    public bool AutoSwitchEnabled { get; set; }

    /// <summary>
    /// Position of this profile within the Auto Switch rotation order (editable column).
    /// </summary>
    [Reactive]
    public int AutoSwitchOrder { get; set; }

    /// <summary>
    /// How many seconds to stay on this profile during Auto Switch rotation.
    /// Default is 30 seconds. 0 means use global interval.
    /// </summary>
    [Reactive]
    public int AutoSwitchDuration { get; set; } = 30;

    public string GetSummary()
    {
        var summary = $"[{ConfigType}] {Remarks}";
        if (!ConfigType.IsComplexType())
        {
            summary += $"({Address}:{Port})";
        }

        return summary;
    }
}
