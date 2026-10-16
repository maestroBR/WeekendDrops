using System.Text.Json;
using System.Text.Json.Serialization;
using SysPath = System.IO.Path;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using WeekendDrops.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Repeatable;
using SPTarkov.Server.Core.Services.Commerce;

namespace WeekendDrops.Services;

[Injectable(InjectionType.Singleton)]
public class DailyChallengeService(
    ProfileHelper profileHelper,
    MailSendService mailSendService,
    GpBalanceService gpBalance,
    WeekendModifierService modifiers,
    CollectionService collection,
    ISptLogger<DailyChallengeService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private ModConfig _config = new();
    private List<DailyChallengeDefinition> _pool = [];        
    private List<DailyChallengeDefinition> _allDaily = [];    
    private bool _lootNetActive;
    private bool _scavDisabled;
    private List<ShopItemDefinition> _shopItems = [];

    private readonly Dictionary<string, DateTime> _restockUntil = [];

    private readonly Dictionary<string, int> _maxStock = [];

    private DateTime _nextGlobalRestock = DateTime.MinValue;

    private readonly string _dataDir = WdPaths.DataDir;

    private readonly string _configDir = WdPaths.ConfigDir;

    public void LoadConfig()
    {
        _config = LoadJson<ModConfig>(SysPath.Combine(_configDir, "config.json")) ?? new ModConfig();

        _allDaily = LoadJson<List<DailyChallengeDefinition>>(
            SysPath.Combine(_configDir, "daily_challenges.json")) ?? [];

        _lootNetActive = _config.IncludeLootNet || ModPresence.LootNetInstalled;
        ApplyDailyPool();

        _shopItems = LoadJson<List<ShopItemDefinition>>(
            SysPath.Combine(_configDir, "shop.json")) ?? [];

        var barters = LoadJson<List<ShopItemDefinition>>(
            SysPath.Combine(_configDir, "handover.json")) ?? [];
        _shopItems.AddRange(barters);

        Directory.CreateDirectory(_dataDir);

        _maxStock.Clear();
        foreach (var s in _shopItems)
            _maxStock[s.Id] = s.Stock;

        LoadRestockState();
        LoadGlobalRestockState();
        LoadShopStock();

        if (_config.DebugMode)
            logger.Debug("[WeekendDrops] Daily DEBUG MODE active");
    }

    private void ApplyDailyPool() =>
        _pool = _allDaily
            .Where(c => _lootNetActive || !c.RequiresLootNet)
            .Where(c => ScavEnabled || !ChallengeMetrics.IsScavOnly(c.Type))
            .ToList();

    private bool ScavEnabled => _config.EnableScavChallenges && !_scavDisabled;

    public void SetLootNetActive()
    {
        if (_lootNetActive) return;
        _lootNetActive = true;
        ApplyDailyPool();
    }

    public void SetScavChallengesDisabled()
    {
        if (_scavDisabled) return;
        _scavDisabled = true;
        ApplyDailyPool();
    }

    public int ApplyRaidResult(MongoId sessionId, RaidResultRequest r)
    {
        lock (_operationLock)
        {
            if (_pool.Count == 0) return 0;

            var profile = profileHelper.GetPmcProfile(sessionId);
            if (profile is null) return 0;

            var state = GetOrCreateDailyState(sessionId, profile);
            WireDefinitions(state);

            if (!string.IsNullOrEmpty(r.RaidId) && state.LastRaidId == r.RaidId) return 0;
            state.LastRaidId = r.RaidId;

            if (r.Survived) state.SurvivalTimeBank += r.SurvivedSeconds;
            else            state.SurvivalTimeBank = 0;

            int totalKills = r.ScavKills + r.PmcKills + r.BossKills + r.RaiderKills + r.RogueKills;
            int gpEarned = 0;

            foreach (var cp in state.Challenges.Where(c => !c.Completed))
            {
                if (cp.Definition is null) continue;

                cp.Current = ChallengeProgression.Advance(
                    cp.Definition, cp.Current, cp.Target,
                    r, state.SurvivalTimeBank, totalKills);

                if (cp.Completed) gpEarned += cp.Definition.GpReward;
            }

            SaveDailyState(sessionId, state);
            logger.Info($"[WeekendDrops] Daily raid result applied (survived={r.Survived}, gpEarned={gpEarned}) - " +
                string.Join(", ", state.Challenges.Select(c => $"{c.Definition?.Type}:{c.Current}/{c.Definition?.Target ?? c.Target}")));
            return gpEarned;
        }
    }

    public DailyStateDto GetDailyState(MongoId sessionId)
    {
        lock (_operationLock)
        {
            CheckGlobalRestock();

            var profile = profileHelper.GetPmcProfile(sessionId);

            List<DailyChallengeProgress> challenges = [];
            bool bonusClaimed = false;
            var rerollState = new PlayerDailyState();
            if (profile != null)
            {
                var state = GetOrCreateDailyState(sessionId, profile);
                WireDefinitions(state);
                challenges = state.Challenges;
                bonusClaimed = state.BonusClaimed;
                rerollState = state;
            }

            return new DailyStateDto
            {
                Challenges = challenges.Select(cp => new DailyChallengeDto
                {
                    Id            = cp.DefinitionId,
                    Type          = cp.Definition?.Type.ToString() ?? "",
                    Description   = cp.Definition?.Description ?? cp.DefinitionId,
                    Current       = cp.Current,
                    Target        = cp.Definition?.Target ?? cp.Target,
                    Completed     = cp.Completed,
                    GpReward      = cp.Definition?.GpReward ?? 0,
                    RewardClaimed = cp.RewardClaimed,
                    MinDistanceMeters = cp.Definition?.MinDistanceMeters ?? 0,
                    TargetWeaponClass = cp.Definition?.TargetWeaponClass ?? "",
                    TargetBoss        = cp.Definition?.TargetBoss ?? ""
                }).ToList(),
                ShopItems = _shopItems.Select(s => new ShopItemDto
                {
                    Id          = s.Id,
                    Name        = s.Name,
                    Description = s.Description,
                    GpCost      = EffectiveCost(s),
                    Stock       = s.Stock,
                    TemplateId  = s.TemplateId,
                    Contents    = s.Contents?.Select(c => new ShopContentDto
                    {
                        TemplateId = c.TemplateId,
                        Count      = c.Count
                    }).ToList() ?? [],
                    BarterCost  = s.BarterCost?.Select(c => new ShopContentDto
                    {
                        TemplateId = c.TemplateId,
                        Count      = c.Count
                    }).ToList() ?? [],
                    RestockSeconds = _restockUntil.TryGetValue(s.Id, out var u) && u > DateTime.UtcNow
                        ? (u - DateTime.UtcNow).TotalSeconds
                        : 0,
                    Trophy = s.Trophy
                }).ToList(),
                NextResetSeconds = SecondsUntilMidnight(),
                GlobalRestockSeconds = _nextGlobalRestock > DateTime.UtcNow
                    ? (_nextGlobalRestock - DateTime.UtcNow).TotalSeconds
                    : 0,
                DailyBonusGp = BonusGp(challenges),
                DailyBonusClaimed = bonusClaimed,
                RerollEnabled = _config.EnableDailyReroll,
                RerollAvailable = _config.EnableDailyReroll && !DailyRerollsExhausted(rerollState),
                RerollCost = GetDailyRerollCost(rerollState),
                RerollsUsed = rerollState.RerollsUsed,
                RerollsMax = _config.DailyRerollMaxPerDay
            };
        }
    }

    private static int BonusGp(IEnumerable<DailyChallengeProgress> challenges) =>
        (int)Math.Round(challenges.Sum(c => c.Definition?.GpReward ?? 0) * 0.5,
            MidpointRounding.AwayFromZero);

    public string ClaimDailyBonus(MongoId sessionId)
    {
        lock (_operationLock)
        {
            var profile = profileHelper.GetPmcProfile(sessionId);
            if (profile is null) return "profile_not_found";

            var state = GetOrCreateDailyState(sessionId, profile);
            WireDefinitions(state);

            if (state.Challenges.Count == 0)            return "no_challenges";
            if (!state.Challenges.All(c => c.Completed)) return "not_completed";
            if (state.BonusClaimed)                      return "already_claimed";

            int reward = BonusGp(state.Challenges);
            if (reward <= 0) return "no_reward";

            gpBalance.Add(sessionId.ToString(), collection.ScaleGp(sessionId.ToString(), modifiers.ScaleGp(reward)));
            state.BonusClaimed = true;
            SaveDailyState(sessionId, state);

            logger.Info($"[WeekendDrops] Daily complete-all bonus: +{reward} GP credited for {sessionId} (balance {gpBalance.Get(sessionId.ToString())})");
            return "ok";
        }
    }

    public string ClaimDailyReward(MongoId sessionId, string challengeId)
    {
        lock (_operationLock)
        {
            var profile = profileHelper.GetPmcProfile(sessionId);
            if (profile is null) return "profile_not_found";

            var state = GetOrCreateDailyState(sessionId, profile);
            WireDefinitions(state);

            var cp = state.Challenges.FirstOrDefault(c => c.DefinitionId == challengeId);
            if (cp is null)      return "challenge_not_found";
            if (!cp.Completed)   return "not_completed";
            if (cp.RewardClaimed) return "already_claimed";

            int reward = cp.Definition?.GpReward ?? 0;
            if (reward <= 0) return "no_reward";

            gpBalance.Add(sessionId.ToString(), collection.ScaleGp(sessionId.ToString(), modifiers.ScaleGp(reward)));

            cp.RewardClaimed = true;
            SaveDailyState(sessionId, state);

            logger.Info($"[WeekendDrops] Daily reward: +{reward} GP credited for '{challengeId}' by {sessionId} (balance {gpBalance.Get(sessionId.ToString())})");
            return "ok";
        }
    }

    public string BuyShopItem(MongoId sessionId, string itemId)
    {
        lock (_operationLock)
        {
            var shopItem = _shopItems.FirstOrDefault(s => s.Id == itemId);
            if (shopItem is null)    return "item_not_found";
            if (shopItem.BarterCost is { Count: > 0 }) return "not_for_sale";
            if (shopItem.Stock == 0) return "out_of_stock";

            if (_restockUntil.TryGetValue(itemId, out var until) && DateTime.UtcNow < until)
                return "restocking";

            var delivery = BuildShopRewardItems(shopItem);
            int cost = EffectiveCost(shopItem);
            if (!gpBalance.TrySpend(sessionId.ToString(), cost))
                return "insufficient_gp";

            mailSendService.SendSystemMessageToPlayer(
                sessionId,
                $"GP Shop Purchase: {shopItem.Name}",
                delivery,
                (long)TimeSpan.FromHours(_config.DropExpiryHours).TotalSeconds
            );

            if (shopItem.Stock > 0) { shopItem.Stock--; SaveShopStock(); }

            double restockHours = shopItem.RestockHours ?? _config.ShopRestockHours;
            if (restockHours > 0)
            {
                _restockUntil[itemId] = DateTime.UtcNow.AddHours(restockHours);
                SaveRestockState();
            }

            logger.Info($"[WeekendDrops] Shop purchase: {shopItem.Name} (-{cost} GP, " +
                        $"balance {gpBalance.Get(sessionId.ToString())}). Restock in {restockHours}h");
            return "ok";
        }
    }

    public string RedeemBarter(MongoId sessionId, string itemId)
    {
        lock (_operationLock)
        {
            var entry = _shopItems.FirstOrDefault(s => s.Id == itemId);
            if (entry is null)                          return "item_not_found";
            if (entry.BarterCost is not { Count: > 0 }) return "not_a_barter";

            mailSendService.SendSystemMessageToPlayer(
                sessionId,
                $"Trade-in: {entry.Name}",
                BuildShopRewardItems(entry),
                (long)TimeSpan.FromHours(_config.DropExpiryHours).TotalSeconds
            );

            logger.Info($"[WeekendDrops] Trade-in redeemed: {entry.Name}");
            return "ok";
        }
    }

    private int EffectiveCost(ShopItemDefinition item)
    {
        double mult = _config.ShopPriceMultiplier <= 0 ? 1.0 : _config.ShopPriceMultiplier;
        return Math.Max(1, (int)Math.Round(item.GpCost * mult, MidpointRounding.AwayFromZero));
    }

    private readonly object _fileLock = new();
    private readonly object _operationLock = new();

    private PlayerDailyState GetOrCreateDailyState(MongoId sessionId, PmcData profile)
    {
        var path = DailyStatePath(sessionId);
        PlayerDailyState? state;

        lock (_fileLock)
            state = File.Exists(path) ? LoadJson<PlayerDailyState>(path) : null;

        var todayId = GetCurrentDailyId();

        if (state is not null && state.DailyId == todayId && ReplaceScavChallenges(state))
        {
            SaveDailyState(sessionId, state);
            logger.Info($"[WeekendDrops] Daily Scav challenges replaced for {sessionId} (Scav challenges disabled)");
        }

        bool stale = state is not null
            && state.Challenges.Any(c => _pool.All(d => d.Id != c.DefinitionId));

        if (state is null || state.DailyId != todayId || stale)
        {
            state = new PlayerDailyState { DailyId = todayId };
            AssignDailyChallenges(state);
            SaveDailyState(sessionId, state);
            logger.Info($"[WeekendDrops] Daily reset for {sessionId} - assigned {state.Challenges.Count} challenges");
        }

        return state;
    }

    private bool ReplaceScavChallenges(PlayerDailyState state)
    {
        if (!_scavDisabled) return false;

        var rng     = new Random();
        var usedIds = state.Challenges.Select(c => c.DefinitionId).ToHashSet();
        var usedGroups = state.Challenges
            .Select(c => _allDaily.FirstOrDefault(d => d.Id == c.DefinitionId))
            .Where(d => d is not null && !ChallengeMetrics.IsScavOnly(d.Type))
            .Select(d => ChallengeMetrics.Group(d!.Type))
            .ToHashSet();

        bool changed = false;

        foreach (var cp in state.Challenges)
        {
            var def = _allDaily.FirstOrDefault(d => d.Id == cp.DefinitionId);
            if (def is null || !ChallengeMetrics.IsScavOnly(def.Type)) continue;

            var fresh = _pool.Where(d => !usedIds.Contains(d.Id)
                                      && !usedGroups.Contains(ChallengeMetrics.Group(d.Type))).ToList();
            var pmc   = fresh.Where(d => ChallengeMetrics.Group(d.Type) == "pmc").ToList();
            var pickPool = pmc.Count > 0 ? pmc : fresh;
            if (pickPool.Count == 0)
                pickPool = _pool.Where(d => !usedIds.Contains(d.Id)).ToList();
            if (pickPool.Count == 0) continue;

            var pick = pickPool[rng.Next(pickPool.Count)];

            usedIds.Remove(cp.DefinitionId);
            cp.DefinitionId  = pick.Id;
            cp.Target        = pick.Target;
            cp.Current       = 0;
            cp.RewardClaimed = false;
            cp.Definition    = pick;
            usedIds.Add(pick.Id);
            usedGroups.Add(ChallengeMetrics.Group(pick.Type));
            changed = true;
        }

        return changed;
    }

    private void SaveDailyState(MongoId sessionId, PlayerDailyState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        lock (_fileLock)
            AtomicFile.WriteAllText(DailyStatePath(sessionId), json);
    }

    private string DailyStatePath(MongoId sessionId) =>
        SysPath.Combine(_dataDir, $"{sessionId}_daily.json");

    private void WireDefinitions(PlayerDailyState state)
    {
        foreach (var cp in state.Challenges)
            cp.Definition = _pool.FirstOrDefault(d => d.Id == cp.DefinitionId);
    }

    private void AssignDailyChallenges(PlayerDailyState state)
    {
        var rng = new Random();

        var byGroup = _pool
            .OrderBy(_ => rng.Next())
            .GroupBy(d => ChallengeMetrics.Group(d.Type))
            .Select(g => g.First());
        var selected = byGroup
            .Concat(_pool.OrderBy(_ => rng.Next()))
            .DistinctBy(d => d.Id)
            .Take(Math.Min(5, _pool.Count))
            .ToList();

        state.Challenges = selected.Select(d => new DailyChallengeProgress
        {
            DefinitionId = d.Id,
            Target       = d.Target,
            Definition   = d
        }).ToList();

        state.RerollsUsed = 0;
    }

    public int GetDailyRerollCost(PlayerDailyState state) =>
        Math.Max(0, _config.DailyRerollCost + _config.DailyRerollCostStep * state.RerollsUsed);

    public bool DailyRerollsExhausted(PlayerDailyState state) =>
        _config.DailyRerollMaxPerDay > 0 && state.RerollsUsed >= _config.DailyRerollMaxPerDay;

    public string RerollDailyChallenge(MongoId sessionId, string challengeId)
    {
        lock (_operationLock)
        {
            if (!_config.EnableDailyReroll) return "disabled";

            var profile = profileHelper.GetPmcProfile(sessionId);
            if (profile is null) return "not_found";

            var state = GetOrCreateDailyState(sessionId, profile);
            WireDefinitions(state);

            var cp = state.Challenges.FirstOrDefault(c => c.DefinitionId == challengeId);
            if (cp is null) return "not_found";
            if (cp.Completed) return "already_done";
            if (DailyRerollsExhausted(state)) return "no_rerolls_left";

            var pick = PickDailyReplacement(state, cp.DefinitionId);
            if (pick is null) return "no_replacement";

            int cost = GetDailyRerollCost(state);
            if (!gpBalance.TrySpend(sessionId.ToString(), cost)) return "insufficient_gp";

            cp.DefinitionId  = pick.Id;
            cp.Target        = pick.Target;
            cp.Current       = 0;
            cp.RewardClaimed = false;
            cp.Definition    = pick;
            state.RerollsUsed++;

            state.BonusClaimed = false;

            SaveDailyState(sessionId, state);
            logger.Info($"[WeekendDrops] Player {sessionId} rerolled daily '{challengeId}' -> '{pick.Id}' " +
                        $"for {cost} GP, {state.RerollsUsed} used today");
            return "ok";
        }
    }

    private DailyChallengeDefinition? PickDailyReplacement(PlayerDailyState state, string oldId)
    {
        var rng = new Random();
        var usedIds = state.Challenges.Select(c => c.DefinitionId).ToHashSet();
        var usedGroups = state.Challenges
            .Where(c => c.DefinitionId != oldId)
            .Select(c => _allDaily.FirstOrDefault(d => d.Id == c.DefinitionId))
            .Where(d => d is not null)
            .Select(d => ChallengeMetrics.Group(d!.Type))
            .ToHashSet();

        var candidates = _pool.Where(d => !usedIds.Contains(d.Id)).ToList();
        if (candidates.Count == 0) return null;

        var fresh = candidates.Where(d => !usedGroups.Contains(ChallengeMetrics.Group(d.Type))).ToList();
        var pool = fresh.Count > 0 ? fresh : candidates;
        return pool[rng.Next(pool.Count)];
    }

    public bool DebugAction(MongoId sessionId, string action)
    {
        lock (_operationLock)
        {
            if (!_config.DebugMode)
            {
                logger.Warning("[WeekendDrops] Daily debug action ignored - debugMode is off");
                return false;
            }

            var profile = profileHelper.GetPmcProfile(sessionId);
            if (profile is null) return false;

            var state = GetOrCreateDailyState(sessionId, profile);
            WireDefinitions(state);

            switch (action?.ToLowerInvariant())
            {
                case "completeone":
                    var next = state.Challenges.FirstOrDefault(c => !c.Completed);
                    if (next is not null) next.Current = next.Target;
                    break;
                case "completeall":
                    foreach (var c in state.Challenges) c.Current = c.Target;
                    break;
                case "resetprogress":
                    foreach (var c in state.Challenges) { c.Current = 0; c.RewardClaimed = false; }
                    state.SurvivalTimeBank = 0;
                    state.BonusClaimed = false;
                    break;
                case "reroll":
                    AssignDailyChallenges(state);
                    state.SurvivalTimeBank = 0;
                    state.BonusClaimed = false;
                    break;
                default:
                    logger.Warning($"[WeekendDrops] Unknown daily debug action '{action}'");
                    return false;
            }

            SaveDailyState(sessionId, state);
            logger.Info($"[WeekendDrops] Daily debug action '{action}' applied for {sessionId}");
            return true;
        }
    }

    public void ResetDailyProgress(MongoId sessionId)
    {
        lock (_operationLock)
        {
            var profile = profileHelper.GetPmcProfile(sessionId);
            if (profile is null) return;

            var state = GetOrCreateDailyState(sessionId, profile);
            WireDefinitions(state);

            foreach (var c in state.Challenges)
            {
                c.Current = 0;
                c.RewardClaimed = false;
            }
            state.SurvivalTimeBank = 0;
            state.BonusClaimed = false;
            SaveDailyState(sessionId, state);
            logger.Info($"[WeekendDrops] Debug: daily progress reset for {sessionId}");
        }
    }

    public void RerollDaily(MongoId sessionId)
    {
        lock (_operationLock)
        {
            var profile = profileHelper.GetPmcProfile(sessionId);
            if (profile is null) return;

            var state = GetOrCreateDailyState(sessionId, profile);
            AssignDailyChallenges(state);
            state.SurvivalTimeBank = 0;
            state.BonusClaimed = false;
            SaveDailyState(sessionId, state);
            logger.Info($"[WeekendDrops] Debug: daily set rerolled for {sessionId}");
        }
    }

    private static List<Item> BuildShopRewardItems(ShopItemDefinition shop)
    {
        if (shop.Contents is { Count: > 0 })
            return shop.Contents
                .Where(c => !string.IsNullOrEmpty(c.TemplateId))
                .SelectMany(c => BuildRewardItems(c.TemplateId, c.Count))
                .ToList();

        return BuildRewardItems(shop.TemplateId, shop.Count);
    }

    private static List<Item> BuildRewardItems(string templateId, int count)
    {
        var item = new Item
        {
            Id       = new MongoId(),
            Template = new MongoId(templateId),
            ParentId = null,
            SlotId   = "main",
        };
        if (count > 1) { item.Upd ??= new(); item.Upd.StackObjectsCount = count; }
        return [item];
    }

    private static string GetCurrentDailyId() => DateTime.UtcNow.ToString("yyyy-MM-dd");

    private static double SecondsUntilMidnight()
    {
        var now = DateTime.UtcNow;
        return (now.Date.AddDays(1) - now).TotalSeconds;
    }

    private T? LoadJson<T>(string path)
    {
        if (!File.Exists(path)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] Could not read {SysPath.GetFileName(path)}: {ex.Message}. Ignoring this file (using defaults).");
            return default;
        }
    }

    private string RestockStatePath => SysPath.Combine(_dataDir, "shop_restock.json");

    private void LoadRestockState()
    {
        _restockUntil.Clear();
        var saved = LoadJson<Dictionary<string, DateTime>>(RestockStatePath);
        if (saved is null) return;

        foreach (var kv in saved)
            if (kv.Value > DateTime.UtcNow)
                _restockUntil[kv.Key] = kv.Value;
    }

    private void SaveRestockState()
    {
        try
        {
            var json = JsonSerializer.Serialize(_restockUntil, JsonOptions);
            AtomicFile.WriteAllText(RestockStatePath, json);
        }
        catch (Exception ex)
        {
            logger.Warning($"[WeekendDrops] Failed to save restock state: {ex.Message}");
        }
    }

    private string GlobalRestockPath => SysPath.Combine(_dataDir, "shop_global_restock.json");

    private void LoadGlobalRestockState()
    {
        var saved = LoadJson<GlobalRestockState>(GlobalRestockPath);
        _nextGlobalRestock = saved?.NextRestock ?? DateTime.MinValue;
        CheckGlobalRestock();
    }

    private void CheckGlobalRestock()
    {
        if (_config.ShopGlobalRestockHours <= 0) return;

        if (_nextGlobalRestock == DateTime.MinValue)
        {
            _nextGlobalRestock = DateTime.UtcNow.AddHours(_config.ShopGlobalRestockHours);
            SaveGlobalRestock();
            return;
        }

        if (DateTime.UtcNow < _nextGlobalRestock) return;

        foreach (var s in _shopItems)
            if (_maxStock.TryGetValue(s.Id, out var max))
                s.Stock = max;
        SaveShopStock();

        _nextGlobalRestock = DateTime.UtcNow.AddHours(_config.ShopGlobalRestockHours);
        SaveGlobalRestock();
        logger.Info("[WeekendDrops] Global shop restock - all stock refilled");
    }

    private void SaveGlobalRestock()
    {
        try
        {
            var json = JsonSerializer.Serialize(new GlobalRestockState { NextRestock = _nextGlobalRestock }, JsonOptions);
            AtomicFile.WriteAllText(GlobalRestockPath, json);
        }
        catch (Exception ex)
        {
            logger.Warning($"[WeekendDrops] Failed to save global restock state: {ex.Message}");
        }
    }

    private sealed class GlobalRestockState
    {
        public DateTime NextRestock { get; set; }
    }

    private string ShopStockPath => SysPath.Combine(_dataDir, "shop_stock.json");

    private void LoadShopStock()
    {
        var saved = LoadJson<Dictionary<string, int>>(ShopStockPath);
        if (saved is null) return;

        foreach (var s in _shopItems)
        {
            if (!saved.TryGetValue(s.Id, out var stock)) continue;
            if (s.Stock < 0) continue;
            var max = _maxStock.TryGetValue(s.Id, out var m) ? m : s.Stock;
            s.Stock = stock < 0 ? 0 : Math.Min(stock, max);
        }
    }

    private void SaveShopStock()
    {
        try
        {
            var map = _shopItems.ToDictionary(s => s.Id, s => s.Stock);
            AtomicFile.WriteAllText(ShopStockPath, JsonSerializer.Serialize(map, JsonOptions));
        }
        catch (Exception ex)
        {
            logger.Warning($"[WeekendDrops] Failed to save shop stock: {ex.Message}");
        }
    }
}
