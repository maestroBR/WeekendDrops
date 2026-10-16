using System.Text.Json;
using SysPath = System.IO.Path;
using SPTarkov.DI.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using WeekendDrops.Models;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Profile;

namespace WeekendDrops.Services;

[Injectable(InjectionType.Singleton)]
public class GpGiftService
{
    private readonly ProfileHelper _profileHelper;
    private readonly GpBalanceService _gpBalance;

    private readonly string _file = WdPaths.Data("gp_gifts.json");

    private readonly object _lock = new();
    private Dictionary<string, List<PendingGift>> _pending = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ISptLogger<GpGiftService> _logger;

    public GpGiftService(ProfileHelper profileHelper, GpBalanceService gpBalance,
                         ISptLogger<GpGiftService> logger)
    {
        _profileHelper = profileHelper;
        _gpBalance = gpBalance;
        _logger = logger;
        Load();
    }

    public List<GiftFriendDto> ListFriends(string sessionId)
    {
        var result = new List<GiftFriendDto>();
        Dictionary<MongoId, SPTarkov.Server.Core.Models.Eft.Profile.SptProfile> profiles;
        try { profiles = _profileHelper.GetProfiles(); }
        catch { return result; }

        foreach (var id in profiles.Keys)
        {
            var idStr = id.ToString();
            if (idStr == sessionId) continue;

            string? nick;
            try { nick = _profileHelper.GetPmcProfile(id)?.Info?.Nickname; }
            catch { nick = null; }
            if (string.IsNullOrWhiteSpace(nick)) continue;
            if (FikaProfiles.IsHeadlessNickname(nick)) continue;

            result.Add(new GiftFriendDto { Id = idStr, Nickname = nick });
        }

        result.Sort((a, b) => string.Compare(a.Nickname, b.Nickname, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    public string SendGift(string fromId, string toId, int amount)
    {
        if (amount <= 0) return "bad_amount";
        if (string.IsNullOrEmpty(toId) || toId == fromId) return "bad_target";

        bool targetExists;
        try { targetExists = _profileHelper.GetPmcProfile(new MongoId(toId)) is not null; }
        catch { targetExists = false; }
        if (!targetExists) return "bad_target";

        if (!_gpBalance.TryTransfer(fromId, toId, amount)) return "insufficient_gp";

        string? fromNick;
        try { fromNick = _profileHelper.GetPmcProfile(new MongoId(fromId))?.Info?.Nickname; }
        catch { fromNick = null; }

        Enqueue(toId, new PendingGift
        {
            FromNickname = string.IsNullOrWhiteSpace(fromNick) ? "A friend" : fromNick,
            Amount = amount
        });
        return "ok";
    }

    public List<ReceivedGiftDto> TakePending(string sessionId)
    {
        lock (_lock)
        {
            if (!_pending.TryGetValue(sessionId, out var list) || list.Count == 0)
                return [];

            var taken = list
                .Select(g => new ReceivedGiftDto { FromNickname = g.FromNickname, Amount = g.Amount })
                .ToList();
            _pending.Remove(sessionId);
            Save();
            return taken;
        }
    }

    private void Enqueue(string toId, PendingGift gift)
    {
        lock (_lock)
        {
            if (!_pending.TryGetValue(toId, out var list))
                _pending[toId] = list = new List<PendingGift>();
            list.Add(gift);
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_file))
                _pending = JsonSerializer.Deserialize<Dictionary<string, List<PendingGift>>>(
                    File.ReadAllText(_file)) ?? new();
        }
        catch { _pending = new(); }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(SysPath.GetDirectoryName(_file)!);
            AtomicFile.WriteAllText(_file, JsonSerializer.Serialize(_pending, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.Error($"[WeekendDrops] gp_gifts.json could not be written - pending gifts are lost: {ex.Message}", null);
        }
    }

    private class PendingGift
    {
        public string FromNickname { get; set; } = "";
        public int Amount { get; set; }
    }
}
