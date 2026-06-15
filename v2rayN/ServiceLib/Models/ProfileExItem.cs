namespace ServiceLib.Models;

[Serializable]
public class ProfileExItem
{
    [PrimaryKey]
    public string IndexId { get; set; }

    public int Delay { get; set; }
    public decimal Speed { get; set; }
    public int Sort { get; set; }
    public string? Message { get; set; }

    /// <summary>
    /// Whether this profile is included in the Auto Switch rotation.
    /// </summary>
    public bool AutoSwitchEnabled { get; set; }

    /// <summary>
    /// Position of this profile within the Auto Switch rotation order (1-based).
    /// 0 means "not assigned yet" - the order will be auto-assigned when the
    /// AutoSwitchEnabled checkbox is checked.
    /// </summary>
    public int AutoSwitchOrder { get; set; }

    /// <summary>
    /// How many seconds to stay on this profile during Auto Switch rotation.
    /// 0 means use the global IntervalSeconds from AutoSwitchItem config.
    /// Default is 30 seconds when first enabled.
    /// </summary>
    public int AutoSwitchDuration { get; set; } = 30;
}
