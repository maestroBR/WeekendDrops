using System.Text.Json;
using System.Text.Json.Serialization;
using SysPath = System.IO.Path;
using SPTarkov.DI.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Commerce;
using WeekendDrops.Models;

namespace WeekendDrops.Services;

[Injectable(InjectionType.Singleton)]
public class CollectionService(
    TemplateTable templates,
    HideoutTable hideout,
    GpBalanceService gpBalance,
    ProfileHelper profileHelper,
    MailSendService mailSendService,
    ISptLogger<CollectionService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private CollectionConfig _config = new();
    private readonly object _fileLock = new();
    private readonly object _operationLock = new();

    private readonly string _dataDir = SysPath.Combine(WdPaths.DataDir, "collection");

    public bool Enabled => _config.Enabled && _config.Sets.Count > 0;
    public bool RequireFoundInRaid => _config.RequireFoundInRaid;

    public void LoadConfig(string configDir)
    {
        var path = SysPath.Combine(configDir, "collection.json");
        try
        {
            _config = File.Exists(path)
                ? JsonSerializer.Deserialize<CollectionConfig>(File.ReadAllText(path), JsonOptions) ?? new CollectionConfig()
                : new CollectionConfig();
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] collection.json failed to load, the collection is off: {ex.Message}", null);
            _config = new CollectionConfig { Enabled = false };
        }

        if (!_config.Enabled) return;

        StripUnobtainableItems();
        StripReservedItems();
        Directory.CreateDirectory(_dataDir);
    }

    private void StripUnobtainableItems()
    {
        var dropped = new List<string>();

        foreach (var set in _config.Sets)
        {
            var keep = new List<string>(set.ItemIds.Count);
            foreach (var id in set.ItemIds)
            {
                if (!templates.Items.TryGetValue(id, out var tpl) || tpl is null)
                {
                    dropped.Add($"{id} (not in item DB)");
                    continue;
                }
                if (tpl.Properties?.QuestItem == true)
                {
                    dropped.Add($"{DescribeItem(id)} (raid quest item)");
                    continue;
                }
                keep.Add(id);
            }
            set.ItemIds = keep;
        }

        if (dropped.Count > 0)
            logger.Warning($"[WeekendDrops] Collection: dropped unobtainable {string.Join(", ", dropped)}");
    }

    private void DropUnreachableMilestones()
    {
        foreach (var set in _config.Sets)
        {
            if (set.Milestones is null) continue;
            int before = set.Milestones.Count;
            set.Milestones = set.Milestones.Where(m => m.Required <= set.ItemIds.Count).ToList();
            if (set.Milestones.Count != before)
                logger.Warning($"[WeekendDrops] Collection: set '{set.Id}' has {set.ItemIds.Count} item(s), " +
                               $"{before - set.Milestones.Count} milestone(s) dropped as unreachable");
        }
    }

    private readonly HashSet<string> _questItems = new(StringComparer.OrdinalIgnoreCase);

    public bool IsQuestItem(string templateId) => _questItems.Contains(templateId);

    private void StripReservedItems()
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _questItems.Clear();

        try
        {
            foreach (var (questId, quest) in templates.Quests)
            {
                var conds = quest?.Conditions;
                if (conds is null) continue;

                bool locked = _config.ProtectedQuestIds.Contains(questId.ToString(), StringComparer.OrdinalIgnoreCase);

                foreach (var list in new[] { conds.AvailableForFinish, conds.Started, conds.Success, conds.Fail })
                {
                    if (list is null) continue;
                    foreach (var c in list)
                    {
                        var t = c?.Target;
                        if (t is null) continue;

                        var targets = t.IsList && t.List is not null ? t.List
                                    : t.IsItem && !string.IsNullOrEmpty(t.Item) ? [t.Item]
                                    : new List<string>();

                        foreach (var s in targets)
                        {
                            if (string.IsNullOrEmpty(s)) continue;
                            _questItems.Add(s);
                            if (locked) reserved.Add(s);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] Could not read quest requirements; the collection is off rather than risk eating a quest item: {ex.Message}", null);
            _config.Enabled = false;
            return;
        }

        try
        {
            foreach (var area in hideout.Areas)
            {
                if (area?.Stages is null) continue;
                foreach (var stage in area.Stages.Values)
                {
                    if (stage?.Requirements is null) continue;
                    foreach (var req in stage.Requirements)
                    {
                        var tpl = req?.TemplateId.ToString();
                        if (!string.IsNullOrEmpty(tpl)) reserved.Add(tpl!);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] Could not read hideout requirements; the collection is off rather than risk eating an upgrade material: {ex.Message}", null);
            _config.Enabled = false;
            return;
        }

        WithheldItems.Clear();
        foreach (var set in _config.Sets)
        {
            var keep = new List<string>(set.ItemIds.Count);
            foreach (var id in set.ItemIds)
            {
                if (reserved.Contains(id)) WithheldItems.Add(DescribeItem(id));
                else keep.Add(id);
            }
            set.ItemIds = keep;
        }
        _config.Sets = _config.Sets.Where(s => s.ItemIds.Count > 0).ToList();
        DropUnreachableMilestones();

        if (WithheldItems.Count > 0)
            logger.Debug($"[WeekendDrops] Collection: withheld {string.Join(", ", WithheldItems)}");
        logger.Debug($"[WeekendDrops] Collection: {SetCount} set(s), {DonatableCount} donatable item(s)");
    }

    public List<string> WithheldItems { get; } = [];
    public int SetCount => _config.Sets.Count;
    public int DonatableCount => _config.Sets.Sum(s => s.ItemIds.Count);

    private string DescribeItem(string tpl)
    {
        try
        {
            if (templates.Items.TryGetValue(tpl, out var t) && !string.IsNullOrEmpty(t?.Name))
                return $"{t.Name} ({tpl})";
        }
        catch { }
        return tpl;
    }

    public string Donate(string sessionId, string templateId)
    {
        lock (_operationLock)
        {
            if (!Enabled) return "disabled";
            if (string.IsNullOrEmpty(templateId)) return "unknown_item";

            var set = _config.Sets.FirstOrDefault(s => s.ItemIds.Contains(templateId));
            if (set is null) return "unknown_item";

            var state = Load(sessionId);
            if (state.Donated.Contains(templateId)) return "already_donated";

            state.Donated.Add(templateId);

            int gp = Math.Max(0, set.GpPerItem);
            int donatedInSet = set.ItemIds.Count(state.Donated.Contains);

            foreach (var ms in set.Milestones.Where(m => m.Required <= donatedInSet))
            {
                string key = set.Id + ":" + ms.Required;
                if (state.ClaimedMilestones.Contains(key)) continue;
                state.ClaimedMilestones.Add(key);
                gp += Math.Max(0, ms.GpReward);
                logger.Info($"[WeekendDrops] Collection: {sessionId} completed '{set.Id}' milestone {ms.Required} ({ms.Perk})");
            }

            Save(sessionId, state);
            if (gp > 0) gpBalance.Add(sessionId, gp);

            logger.Info($"[WeekendDrops] Collection: {sessionId} donated {templateId} (+{gp} GP)");
            return "ok";
        }
    }

    private void ReconcileMilestones(string sessionId)
    {
        var state = Load(sessionId);
        int gp = 0;
        bool changed = false;

        foreach (var set in _config.Sets)
        {
            int donated = set.ItemIds.Count(state.Donated.Contains);
            foreach (var ms in set.Milestones.Where(m => m.Required <= donated))
            {
                string key = set.Id + ":" + ms.Required;
                if (state.ClaimedMilestones.Contains(key)) continue;
                state.ClaimedMilestones.Add(key);
                changed = true;
                gp += Math.Max(0, ms.GpReward);
                logger.Info($"[WeekendDrops] Collection: {sessionId} back-paid '{set.Id}' milestone {ms.Required}");
            }
        }

        if (!changed) return;
        Save(sessionId, state);
        if (gp > 0) gpBalance.Add(sessionId, gp);
    }

    private double PerkTotal(string sessionId, CollectionPerkKind kind)
    {
        lock (_operationLock)
        {
            if (!Enabled) return 0;
            var state = Load(sessionId);
            double total = 0;

            foreach (var set in _config.Sets)
            {
                int donated = set.ItemIds.Count(state.Donated.Contains);
                foreach (var ms in set.Milestones)
                    if (ms.Perk == kind && ms.Required <= donated) total += ms.PerkValue;
            }
            return total;
        }
    }

    public double GpBonusMultiplier(string sessionId) =>
        1.0 + PerkTotal(sessionId, CollectionPerkKind.GpPercent) / 100.0;

    public int ScaleGp(string sessionId, int amount)
    {
        if (amount <= 0 || !Enabled) return amount;
        double mult = GpBonusMultiplier(sessionId);
        return mult <= 1.0 ? amount : (int)Math.Round(amount * mult);
    }

    public CollectionStateDto GetState(string sessionId)
    {
        lock (_operationLock)
        {
            var dto = new CollectionStateDto { Enabled = Enabled, RequireFoundInRaid = _config.RequireFoundInRaid };
            if (!Enabled) return dto;

            ReconcileMilestones(sessionId);
            var state = Load(sessionId);

            dto.Sets = _config.Sets.Select(set =>
            {
                int donated = set.ItemIds.Count(state.Donated.Contains);
                return new CollectionSetDto
                {
                    Id = set.Id,
                    Name = set.Name,
                    Description = set.Description,
                    GpPerItem = set.GpPerItem,
                    Donated = donated,
                    Total = set.ItemIds.Count,
                    Items = set.ItemIds.Select(id => new CollectionItemDto
                    {
                        TemplateId = id,
                        Donated = state.Donated.Contains(id),
                        QuestItem = IsQuestItem(id)
                    }).ToList(),
                    Milestones = set.Milestones.OrderBy(m => m.Required).Select(m => new CollectionMilestoneDto
                    {
                        Required = m.Required,
                        GpReward = m.GpReward,
                        Perk = m.Perk.ToString(),
                        PerkValue = m.PerkValue,
                        Description = m.Description,
                        Reached = m.Required <= donated
                    }).ToList()
                };
            }).ToList();

            if (state.PendingPrestigeGp > 0)
            {
                dto.PrestigeGp     = state.PendingPrestigeGp;
                dto.PrestigePieces = state.PendingPrestigePieces;
                dto.PrestigeSets   = state.PendingPrestigeSets;

                state.PendingPrestigeGp = 0;
                state.PendingPrestigePieces = 0;
                state.PendingPrestigeSets = 0;
                Save(sessionId, state);
            }

            return dto;
        }
    }

    private string StatePath(string sessionId) => SysPath.Combine(_dataDir, $"{sessionId}.json");

    private PlayerCollectionState Load(string sessionId)
    {
        var state = ReadState(sessionId);
        return ApplyProfileWipe(sessionId, state);
    }

    private PlayerCollectionState ReadState(string sessionId)
    {
        try
        {
            var path = StatePath(sessionId);
            lock (_fileLock)
                if (File.Exists(path))
                    return JsonSerializer.Deserialize<PlayerCollectionState>(File.ReadAllText(path)) ?? new PlayerCollectionState();
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] Collection state unreadable for {sessionId}: {ex.Message}", null);
        }
        return new PlayerCollectionState();
    }

    private readonly HashSet<string> _wipeChecked = [];

    private PlayerCollectionState ApplyProfileWipe(string sessionId, PlayerCollectionState state)
    {
        if (!_config.WipeWithProfile) return state;
        lock (_fileLock)
            if (!_wipeChecked.Add(sessionId)) return state;

        string stamp;
        try
        {
            var info = profileHelper.GetPmcProfile(sessionId)?.Info;
            if (info is null) return state;
            stamp = $"{info.RegistrationDate ?? 0}:{info.PrestigeLevel ?? 0}";
        }
        catch (Exception ex)
        {
            logger.Warning($"[WeekendDrops] Collection: could not read profile for wipe check ({ex.Message}), keeping state");
            return state;
        }

        if (state.ProfileStamp == stamp) return state;

        bool prestiged = false;
        if (!string.IsNullOrEmpty(state.ProfileStamp))
        {
            prestiged = PrestigeOf(stamp) > PrestigeOf(state.ProfileStamp);
            if (!prestiged && state.Donated.Count > 0)
                logger.Info($"[WeekendDrops] Collection: profile {sessionId} was wiped, clearing " +
                            $"{state.Donated.Count} donation(s) and their perks");
        }

        var fresh = string.IsNullOrEmpty(state.ProfileStamp)
            ? state
            : new PlayerCollectionState();
        fresh.ProfileStamp = stamp;
        if (prestiged) PayPrestigeBonus(sessionId, state, fresh);
        Save(sessionId, fresh);
        return fresh;
    }

    private static int PrestigeOf(string stamp)
    {
        int i = stamp.LastIndexOf(':');
        return i >= 0 && int.TryParse(stamp[(i + 1)..], out int p) ? p : 0;
    }

    private void PayPrestigeBonus(string sessionId, PlayerCollectionState old, PlayerCollectionState fresh)
    {
        int pieces = old.Donated.Count;
        if (pieces == 0) return;

        int sets = _config.Sets.Count(s => s.ItemIds.Count > 0 && s.ItemIds.All(old.Donated.Contains));
        int gp = Math.Max(0, _config.PrestigeGpPerPiece) * pieces
               + Math.Max(0, _config.PrestigeGpPerSet) * sets;
        if (gp <= 0) return;

        gpBalance.Add(sessionId, gp);
        fresh.PendingPrestigeGp = gp;
        fresh.PendingPrestigePieces = pieces;
        fresh.PendingPrestigeSets = sets;
        logger.Info($"[WeekendDrops] Collection: {sessionId} prestiged with {pieces} piece(s) and " +
                    $"{sets} complete set(s), paid {gp} GP");

        try
        {
            mailSendService.SendSystemMessageToPlayer(
                sessionId,
                $"The collection was catalogued before you left: {pieces} piece(s), {sets} complete set(s). " +
                $"{gp:N0} GP has been credited to start the new run.",
                [],
                (long)TimeSpan.FromDays(7).TotalSeconds);
        }
        catch (Exception ex)
        {
            logger.Warning($"[WeekendDrops] Prestige payout mail failed for {sessionId}: {ex.Message}");
        }
    }

    private void Save(string sessionId, PlayerCollectionState state)
    {
        try
        {
            Directory.CreateDirectory(_dataDir);
            var json = JsonSerializer.Serialize(state, JsonOptions);
            lock (_fileLock) AtomicFile.WriteAllText(StatePath(sessionId), json);
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] Collection state could not be saved for {sessionId}: {ex.Message}", null);
        }
    }
}
