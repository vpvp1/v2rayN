//using System.Reactive.Linq;

namespace ServiceLib.Manager;

public class ProfileExManager
{
    private static readonly Lazy<ProfileExManager> _instance = new(() => new());
    private ConcurrentBag<ProfileExItem> _lstProfileEx = [];
    private readonly Queue<string> _queIndexIds = new();
    public static ProfileExManager Instance => _instance.Value;
    private static readonly string _tag = "ProfileExHandler";

    public ProfileExManager()
    {
        //Init();
    }

    public async Task Init()
    {
        await InitData();
    }

    public async Task<ConcurrentBag<ProfileExItem>> GetProfileExs()
    {
        return await Task.FromResult(_lstProfileEx);
    }

    private async Task InitData()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileExItem where indexId not in ( select indexId from ProfileItem )");

        _lstProfileEx = new(await SQLiteHelper.Instance.TableAsync<ProfileExItem>().ToListAsync());
    }

    private void IndexIdEnqueue(string indexId)
    {
        if (indexId.IsNotEmpty() && !_queIndexIds.Contains(indexId))
        {
            _queIndexIds.Enqueue(indexId);
        }
    }

    private async Task SaveQueueIndexIds()
    {
        var cnt = _queIndexIds.Count;
        if (cnt > 0)
        {
            var lstExists = await SQLiteHelper.Instance.TableAsync<ProfileExItem>().ToListAsync();
            List<ProfileExItem> lstInserts = [];
            List<ProfileExItem> lstUpdates = [];

            for (var i = 0; i < cnt; i++)
            {
                var id = _queIndexIds.Dequeue();
                var item = lstExists.FirstOrDefault(t => t.IndexId == id);
                var itemNew = _lstProfileEx?.FirstOrDefault(t => t.IndexId == id);
                if (itemNew is null)
                {
                    continue;
                }

                if (item is not null)
                {
                    lstUpdates.Add(itemNew);
                }
                else
                {
                    lstInserts.Add(itemNew);
                }
            }

            try
            {
                if (lstInserts.Count > 0)
                {
                    await SQLiteHelper.Instance.InsertAllAsync(lstInserts);
                }

                if (lstUpdates.Count > 0)
                {
                    await SQLiteHelper.Instance.UpdateAllAsync(lstUpdates);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
        }
    }

    private ProfileExItem AddProfileEx(string indexId)
    {
        var profileEx = new ProfileExItem()
        {
            IndexId = indexId,
            Delay = 0,
            Speed = 0,
            Sort = 0,
            Message = string.Empty,
            AutoSwitchEnabled = false,
            AutoSwitchOrder = 0
        };
        _lstProfileEx.Add(profileEx);
        IndexIdEnqueue(indexId);
        return profileEx;
    }

    private ProfileExItem GetProfileExItem(string? indexId)
    {
        return _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId) ?? AddProfileEx(indexId);
    }

    public async Task ClearAll()
    {
        await SQLiteHelper.Instance.ExecuteAsync($"delete from ProfileExItem ");
        _lstProfileEx = new();
    }

    public async Task SaveTo()
    {
        try
        {
            await SaveQueueIndexIds();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
    }

    public void SetTestDelay(string indexId, int delay)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.Delay = delay;
        IndexIdEnqueue(indexId);
    }

    public void SetTestSpeed(string indexId, decimal speed)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.Speed = speed;
        IndexIdEnqueue(indexId);
    }

    public void SetTestMessage(string indexId, string message)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.Message = message;
        IndexIdEnqueue(indexId);
    }

    public void SetSort(string indexId, int sort)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.Sort = sort;
        IndexIdEnqueue(indexId);
    }

    public int GetSort(string indexId)
    {
        var profileEx = _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId);
        if (profileEx == null)
        {
            return 0;
        }
        return profileEx.Sort;
    }

    public int GetMaxSort()
    {
        if (_lstProfileEx.Count <= 0)
        {
            return 0;
        }
        return _lstProfileEx.Max(t => t == null ? 0 : t.Sort);
    }

    #region Auto Switch

    /// <summary>
    /// Returns whether the given profile is included in the Auto Switch rotation.
    /// </summary>
    public bool GetAutoSwitchEnabled(string indexId)
    {
        var profileEx = _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId);
        return profileEx?.AutoSwitchEnabled ?? false;
    }

    /// <summary>
    /// Returns the Auto Switch rotation order (1-based) for the given profile.
    /// 0 means the profile has not been assigned an order yet.
    /// </summary>
    public int GetAutoSwitchOrder(string indexId)
    {
        var profileEx = _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId);
        return profileEx?.AutoSwitchOrder ?? 0;
    }

    /// <summary>
    /// Returns the highest Auto Switch order currently assigned among enabled profiles.
    /// </summary>
    public int GetMaxAutoSwitchOrder()
    {
        if (_lstProfileEx.Count <= 0)
        {
            return 0;
        }
        return _lstProfileEx.Where(t => t != null && t.AutoSwitchEnabled).Select(t => t.AutoSwitchOrder).DefaultIfEmpty(0).Max();
    }

    /// <summary>
    /// Returns all profiles currently enabled for Auto Switch, ordered by their
    /// rotation order (ascending).
    /// </summary>
    public List<ProfileExItem> GetAutoSwitchItemsOrdered()
    {
        return _lstProfileEx
            .Where(t => t != null && t.AutoSwitchEnabled)
            .OrderBy(t => t.AutoSwitchOrder)
            .ThenBy(t => t.IndexId)
            .ToList();
    }

    /// <summary>
    /// Enables or disables a profile for the Auto Switch rotation.
    /// When enabling without an explicit order, the profile is appended to the
    /// end of the rotation. When disabling, the profile's order is cleared and
    /// the remaining enabled profiles are re-indexed to stay contiguous (1..N).
    /// </summary>
    public void SetAutoSwitchEnabled(string indexId, bool enabled)
    {
        var profileEx = GetProfileExItem(indexId);

        profileEx.AutoSwitchEnabled = enabled;
        if (enabled)
        {
            if (profileEx.AutoSwitchOrder <= 0)
            {
                profileEx.AutoSwitchOrder = GetMaxAutoSwitchOrder() + 1;
            }
        }
        else
        {
            profileEx.AutoSwitchOrder = 0;
        }

        IndexIdEnqueue(indexId);
        ReindexAutoSwitchOrders();
    }

    /// <summary>
    /// Sets (or changes) the Auto Switch rotation order for a profile.
    /// If the requested order collides with another enabled profile, the
    /// profile being edited wins (last value entered wins) and the rest of
    /// the rotation is re-indexed sequentially (1..N) to remain contiguous.
    /// Setting an order automatically enables the profile for Auto Switch.
    /// </summary>
    public void SetAutoSwitchOrder(string indexId, int order)
    {
        var profileEx = GetProfileExItem(indexId);

        if (order <= 0)
        {
            // Treat 0/negative as "remove from rotation".
            SetAutoSwitchEnabled(indexId, false);
            return;
        }

        profileEx.AutoSwitchEnabled = true;
        profileEx.AutoSwitchOrder = order;

        IndexIdEnqueue(indexId);
        ReindexAutoSwitchOrders(indexId);
    }

    /// <summary>
    /// Re-indexes the AutoSwitchOrder of all enabled profiles so that they form
    /// a contiguous sequence starting at 1, preserving relative order.
    /// If <paramref name="preferredIndexId"/> is provided and multiple profiles
    /// share the same order value as it, that profile keeps its requested order
    /// (i.e. "last value entered wins") and the conflicting profiles are shifted.
    /// </summary>
    private void ReindexAutoSwitchOrders(string? preferredIndexId = null)
    {
        var enabled = _lstProfileEx.Where(t => t != null && t.AutoSwitchEnabled).ToList();
        if (enabled.Count == 0)
        {
            return;
        }

        IEnumerable<ProfileExItem> ordered;
        if (preferredIndexId.IsNotEmpty())
        {
            var preferred = enabled.FirstOrDefault(t => t.IndexId == preferredIndexId);
            var others = enabled.Where(t => t.IndexId != preferredIndexId)
                                 .OrderBy(t => t.AutoSwitchOrder)
                                 .ThenBy(t => t.IndexId);

            if (preferred != null)
            {
                // Place the preferred item at its requested position, shifting others.
                var result = new List<ProfileExItem>();
                var inserted = false;
                var position = 1;
                foreach (var item in others)
                {
                    if (!inserted && position == preferred.AutoSwitchOrder)
                    {
                        result.Add(preferred);
                        inserted = true;
                    }
                    result.Add(item);
                    position++;
                }
                if (!inserted)
                {
                    result.Add(preferred);
                }
                ordered = result;
            }
            else
            {
                ordered = enabled.OrderBy(t => t.AutoSwitchOrder).ThenBy(t => t.IndexId);
            }
        }
        else
        {
            ordered = enabled.OrderBy(t => t.AutoSwitchOrder).ThenBy(t => t.IndexId);
        }

        var seq = 1;
        foreach (var item in ordered)
        {
            if (item.AutoSwitchOrder != seq)
            {
                item.AutoSwitchOrder = seq;
                IndexIdEnqueue(item.IndexId);
            }
            seq++;
        }
    }

    /// <summary>
    /// Removes a profile entirely from the Auto Switch rotation (used e.g. when
    /// a server is deleted) and re-indexes the remaining profiles.
    /// </summary>
    public void RemoveFromAutoSwitch(string indexId)
    {
        var profileEx = _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId);
        if (profileEx == null || !profileEx.AutoSwitchEnabled)
        {
            return;
        }

        profileEx.AutoSwitchEnabled = false;
        profileEx.AutoSwitchOrder = 0;
        IndexIdEnqueue(indexId);
        ReindexAutoSwitchOrders();
    }

    #endregion Auto Switch
}
