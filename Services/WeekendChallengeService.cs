using System.Globalization;
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
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using WeekendDrops.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Repeatable;
using SPTarkov.Server.Core.Services.Commerce;

namespace WeekendDrops.Services;

[Injectable(InjectionType.Singleton)]
public class WeekendChallengeService(
    ProfileHelper profileHelper,
    MailSendService mailSendService,
    InventoryConfig inventoryConfig,
    ItemHelper itemHelper,
    GpBalanceService gpBalance,
    GpGiftService giftService,
    WeekendModifierService modifiers,
    CollectionService collection,
    TarcoinLoot tarcoin,
    TarcoinBalanceService tarcoinBalance,
    ISptLogger<WeekendChallengeService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private ModConfig _config = new();

    public ModConfig Config => _config;
    private List<ChallengeDefinition> _challengePool = [];
    private List<ChallengeDefinition> _allChallenges = [];
    private bool _lootNetActive;
    private bool _scavDisabled;
    private DropsConfig _dropsConfig = new();
    private CratePoolsConfig _cratePools = new();
    private CratePoolsConfig _wttPools = new();
    private CratePoolsConfig _arenaPools = new();

    private readonly string _dataDir = WdPaths.DataDir;

    private readonly string _configDir = WdPaths.ConfigDir;

    public void LoadConfig()
    {
        _config = LoadJson<ModConfig>(SysPath.Combine(_configDir, "config.json")) ?? new ModConfig();

        _allChallenges = LoadJson<List<ChallengeDefinition>>(SysPath.Combine(_configDir, "challenges.json")) ?? [];

        _lootNetActive = _config.IncludeLootNet || ModPresence.LootNetInstalled;
        ApplyChallengePool();

        _dropsConfig = LoadJson<DropsConfig>(SysPath.Combine(_configDir, "drops.json")) ?? new DropsConfig();
        _cratePools = LoadJson<CratePoolsConfig>(SysPath.Combine(_configDir, "crate_pools.json")) ?? new CratePoolsConfig();
        _wttPools = LoadJson<CratePoolsConfig>(SysPath.Combine(_configDir, "crate_pools_wtt.json")) ?? new CratePoolsConfig();
        _arenaPools = LoadJson<CratePoolsConfig>(SysPath.Combine(_configDir, "arena_pools.json")) ?? new CratePoolsConfig();
        modifiers.LoadConfig(_configDir, _config);

        if (_config.DebugMode)
            logger.Debug("[WeekendDrops] DEBUG MODE active - weekend forced on");

        Directory.CreateDirectory(_dataDir);
    }

    private void ApplyChallengePool()
    {
        _challengePool = _allChallenges
            .Where(c => _lootNetActive || !c.RequiresLootNet)
            .Where(c => ScavEnabled || !ChallengeMetrics.IsScavOnly(c.Type))
            .ToList();
        RecomputeWeekendPlan();
    }

    private int _planCount;
    private int _planBudget;

    private void RecomputeWeekendPlan()
    {
        int desired = Math.Max(1, _config.ChallengesPerWeekend);
        int groups = _challengePool.Select(c => ChallengeMetrics.Group(c.Type)).Distinct().Count();
        _planCount = Math.Min(desired, Math.Max(1, groups));

        var byDifficulty = _challengePool.GroupBy(c => c.Difficulty).ToDictionary(g => g.Key, g => g.ToList());
        bool Feasible(int b) =>
            DifficultyCompositions(_planCount, b).Any(comp => TryBuildComposition(comp, byDifficulty, _planCount, out _));

        int chosen = 0;
        for (int b = Math.Max(_planCount, _config.WeekendDifficultyBudget); b >= _planCount; b--)
            if (Feasible(b)) { chosen = b; break; }
        if (chosen == 0)
            for (int b = _planCount; b <= _planCount * 3; b++)
                if (Feasible(b)) { chosen = b; break; }
        _planBudget = chosen > 0 ? chosen : _config.WeekendDifficultyBudget;

        if (_planCount != desired || _planBudget != _config.WeekendDifficultyBudget)
            logger.Info(
                $"[WeekendDrops] Weekend plan clamped to {_planCount} challenges / {_planBudget} points " +
                $"(config asks {desired}/{_config.WeekendDifficultyBudget}) - limited by {groups} usable challenge groups.");
    }

    private bool ScavEnabled => _config.EnableScavChallenges && !_scavDisabled;

    public void SetLootNetActive()
    {
        if (_lootNetActive) return;
        _lootNetActive = true;
        ApplyChallengePool();
        logger.Info("[WeekendDrops] LootNET bridge detected - loot-value challenges enabled.");
    }

    public void SetScavChallengesDisabled()
    {
        if (_scavDisabled) return;
        _scavDisabled = true;
        ApplyChallengePool();
        logger.Info("[WeekendDrops] Client toggle: Scav-run challenges disabled for this run.");
    }

    private bool ReplaceScavChallenges(PlayerWeekendState state)
    {
        if (!_scavDisabled) return false;

        var rng     = new Random();
        var usedIds = state.Challenges.Select(c => c.DefinitionId).ToHashSet();
        var usedGroups = state.Challenges
            .Select(c => _allChallenges.FirstOrDefault(d => d.Id == c.DefinitionId))
            .Where(d => d is not null && !ChallengeMetrics.IsScavOnly(d.Type))
            .Select(d => ChallengeMetrics.Group(d!.Type))
            .ToHashSet();

        bool changed = false;

        foreach (var cp in state.Challenges)
        {
            var def = _allChallenges.FirstOrDefault(d => d.Id == cp.DefinitionId);
            if (def is null || !ChallengeMetrics.IsScavOnly(def.Type)) continue;

            var sameDiff = _challengePool.Where(d => d.Difficulty == def.Difficulty
                                                  && !usedIds.Contains(d.Id)
                                                  && !usedGroups.Contains(ChallengeMetrics.Group(d.Type))).ToList();
            var pmc      = sameDiff.Where(d => ChallengeMetrics.Group(d.Type) == "pmc").ToList();
            var pickPool = pmc.Count > 0 ? pmc : sameDiff;
            if (pickPool.Count == 0)
                pickPool = _challengePool.Where(d => d.Difficulty == def.Difficulty
                                                  && !usedIds.Contains(d.Id)).ToList();
            if (pickPool.Count == 0) continue;

            var pick = pickPool[rng.Next(pickPool.Count)];

            usedIds.Remove(cp.DefinitionId);
            cp.DefinitionId = pick.Id;
            cp.Target       = pick.Target;
            cp.Current      = 0;
            cp.Definition   = pick;
            usedIds.Add(pick.Id);
            usedGroups.Add(ChallengeMetrics.Group(pick.Type));
            changed = true;
        }

        return changed;
    }

    public int GetRerollCost(PlayerWeekendState state) =>
        Math.Max(0, _config.WeekendRerollCost + _config.WeekendRerollCostStep * state.RerollsUsed);

    public bool RerollsExhausted(PlayerWeekendState state) =>
        _config.WeekendRerollMaxPerWeekend > 0 && state.RerollsUsed >= _config.WeekendRerollMaxPerWeekend;

    public string RerollChallenge(MongoId sessionId, string challengeId)
    {
        if (!_config.EnableWeekendReroll) return "disabled";

        var profile = profileHelper.GetPmcProfile(sessionId);
        if (profile is null) return "not_found";

        var state = GetOrCreateState(sessionId, profile);

        var cp = state.Challenges.FirstOrDefault(c => c.DefinitionId == challengeId);
        if (cp is null) return "not_found";

        if (cp.Completed) return "already_done";

        if (RerollsExhausted(state)) return "no_rerolls_left";

        var def = _allChallenges.FirstOrDefault(d => d.Id == cp.DefinitionId);
        if (def is null) return "not_found";

        var pick = PickReplacement(state, def);
        if (pick is null) return "no_replacement";

        int cost = GetRerollCost(state);
        if (!gpBalance.TrySpend(sessionId.ToString(), cost)) return "insufficient_gp";

        cp.DefinitionId = pick.Id;
        cp.Target       = pick.Target;
        cp.Current      = 0;
        cp.Definition   = pick;
        state.RerollsUsed++;

        SaveState(sessionId, state);
        logger.Info(
            $"[WeekendDrops] Player {sessionId} rerolled '{def.Id}' -> '{pick.Id}' " +
            $"(difficulty {pick.Difficulty}) for {cost} GP, {state.RerollsUsed} used this weekend");
        return "ok";
    }

    private ChallengeDefinition? PickReplacement(PlayerWeekendState state, ChallengeDefinition old)
    {
        var rng = new Random();
        var usedIds = state.Challenges.Select(c => c.DefinitionId).ToHashSet();
        var usedGroups = state.Challenges
            .Where(c => c.DefinitionId != old.Id)
            .Select(c => _allChallenges.FirstOrDefault(d => d.Id == c.DefinitionId))
            .Where(d => d is not null)
            .Select(d => ChallengeMetrics.Group(d!.Type))
            .ToHashSet();

        var sameDiff = _challengePool
            .Where(d => d.Difficulty == old.Difficulty && !usedIds.Contains(d.Id))
            .ToList();
        if (sameDiff.Count == 0) return null;

        var fresh = sameDiff.Where(d => !usedGroups.Contains(ChallengeMetrics.Group(d.Type))).ToList();
        var pool = fresh.Count > 0 ? fresh : sameDiff;
        return pool[rng.Next(pool.Count)];
    }

    public void RegisterLootContainerPools()
    {
        if (_cratePools.Tiers.Count == 0)
        {
            logger.Warning("[WeekendDrops] crate_pools.json missing/empty - drop crates will be empty when opened");
            return;
        }

        var inventory = inventoryConfig;
        var wttAdded = 0;
        var wttSkipped = 0;

        foreach (var tier in _dropsConfig.Tiers)
        {
            var tierKey = tier.RequiredChallenges.ToString(CultureInfo.InvariantCulture);

            if (!_cratePools.Tiers.TryGetValue(tierKey, out var poolDef) || poolDef.Pool.Count == 0)
            {
                logger.Warning($"[WeekendDrops] No crate pool defined for tier '{tier.TierName}' (req {tier.RequiredChallenges}) - its crates stay empty");
                continue;
            }

            var rewardTplPool = poolDef.Pool.ToDictionary(
                kv => new MongoId(kv.Key),
                kv => kv.Value);

            if (_wttPools.Tiers.TryGetValue(tierKey, out var wttDef))
            {
                foreach (var (tpl, weight) in wttDef.Pool)
                {
                    var id = new MongoId(tpl);
                    if (itemHelper.GetItem(id).Key)
                    {
                        rewardTplPool[id] = weight;
                        wttAdded++;
                    }
                    else
                    {
                        wttSkipped++;
                    }
                }
            }

            var recipe = BuildRecipe(poolDef, rewardTplPool, tier.TierName);

            foreach (var crateTpl in tier.Pools.SelectMany(p => p.ItemIds).Distinct())
            {
                var details = new RewardDetails
                {
                    RewardCount = poolDef.RewardCount,
                    FoundInRaid = _cratePools.FoundInRaid,
                    RewardTplPool = rewardTplPool,
                };
                inventory.RandomLootContainers[new MongoId(crateTpl)] = details;
                Patches.WdCrateRegistry.Register(details, recipe);
            }
        }

        OptionalItemsAvailable = wttAdded;
        OptionalItemsTotal     = wttAdded + wttSkipped;

        if (OptionalItemsTotal > 0)
            logger.Debug(
                $"[WeekendDrops] Optional crate items: {wttAdded} of {OptionalItemsTotal} in the database, " +
                $"{wttSkipped} skipped (WTT-ContentBackport {(ModPresence.ContentBackportInstalled ? "installed" : "not installed")})");
    }

    /// Splits a tier's pool by group and flattens its slot list to one entry per reward
    /// draw. Chance-gated slots stay unrolled here; the patch rolls them per crate.
    private Patches.CrateRecipe BuildRecipe(
        CratePoolTier poolDef, Dictionary<MongoId, double> pool, string tierName)
    {
        var groups = new Dictionary<string, List<KeyValuePair<MongoId, double>>>();
        foreach (var kv in pool)
        {
            var g = Patches.CrateGroups.Of(itemHelper, kv.Key);
            if (!groups.TryGetValue(g, out var list)) groups[g] = list = [];
            list.Add(kv);
        }

        var slots = new List<Patches.CrateSlotPlan>();
        foreach (var slot in poolDef.Slots)
            for (var i = 0; i < slot.Count; i++)
                slots.Add(new Patches.CrateSlotPlan
                {
                    Group    = slot.Group,
                    Chance   = slot.Chance,
                    Fallback = slot.Fallback,
                });

        if (poolDef.Slots.Count > 0 && slots.Count != poolDef.RewardCount)
            logger.Warning(
                $"[WeekendDrops] Crate tier '{tierName}' recipe fills {slots.Count} slot(s) but rewardCount is " +
                $"{poolDef.RewardCount} - any extra draws come from the whole pool");

        foreach (var slot in poolDef.Slots)
            if (!groups.ContainsKey(slot.Group))
                logger.Warning(
                    $"[WeekendDrops] Crate tier '{tierName}' asks for '{slot.Group}' but its pool has no such items");

        return new Patches.CrateRecipe
        {
            Slots   = slots,
            Groups  = groups,
            ModTier = poolDef.ModTier,
        };
    }

    public int OptionalItemsAvailable { get; private set; }
    public int OptionalItemsTotal { get; private set; }
    public int ArenaCrateCount { get; private set; }

    public void RegisterArenaShopPools()
    {
        if (_arenaPools.Tiers.Count == 0)
        {
            logger.Info("[WeekendDrops] arena_pools.json missing/empty - paid Arena crates use vanilla loot");
            return;
        }

        var inventory = inventoryConfig;
        var registered = 0;
        var skipped = 0;

        foreach (var (crateTpl, poolDef) in _arenaPools.Tiers)
        {
            if (poolDef.Pool.Count == 0) continue;

            var rewardTplPool = new Dictionary<MongoId, double>();
            foreach (var (tpl, weight) in poolDef.Pool)
            {
                var id = new MongoId(tpl);
                if (itemHelper.GetItem(id).Key) rewardTplPool[id] = weight;
                else skipped++;
            }
            if (rewardTplPool.Count == 0) continue;

            var details = new RewardDetails
            {
                RewardCount = poolDef.RewardCount,
                FoundInRaid = _arenaPools.FoundInRaid,
                RewardTplPool = rewardTplPool,
            };
            inventory.RandomLootContainers[new MongoId(crateTpl)] = details;
            Patches.WdCrateRegistry.Register(details, BuildRecipe(poolDef, rewardTplPool, crateTpl));
            registered++;
        }

        ArenaCrateCount = registered;
        logger.Debug($"[WeekendDrops] Registered loot for {registered} paid Arena crate(s)" +
            (skipped > 0 ? $" ({skipped} item(s) not in DB skipped)" : ""));
    }

    public int ApplyRaidResult(MongoId sessionId, RaidResultRequest r)
    {
        if (!_config.Enabled || !IsWeekendActive()) return 0;

        var profile = profileHelper.GetPmcProfile(sessionId);
        if (profile is null) return 0;

        var state = GetOrCreateState(sessionId, profile);

        if (!string.IsNullOrEmpty(r.RaidId) && state.LastRaidId == r.RaidId)
        {
            logger.Info($"[WeekendDrops] Raid {r.RaidId} already applied - ignoring duplicate report");
            return 0;
        }
        state.LastRaidId = r.RaidId;

        int pointsBefore = CompletedDifficultyPoints(state);

        if (r.Survived) state.SurvivalTimeBank += r.SurvivedSeconds;
        else            state.SurvivalTimeBank = 0;

        int totalKills = r.ScavKills + r.PmcKills + r.BossKills + r.RaiderKills + r.RogueKills;

        foreach (var cp in state.Challenges.Where(c => !c.Completed))
        {
            if (cp.Definition is null) continue;

            cp.Current = ChallengeProgression.Advance(
                cp.Definition, cp.Current, cp.Target,
                r, state.SurvivalTimeBank, totalKills);
        }

        int pointsAfter = CompletedDifficultyPoints(state);
        int gpEarned = _dropsConfig.Tiers
            .Where(t => t.RequiredChallenges > pointsBefore && t.RequiredChallenges <= pointsAfter)
            .Sum(t => t.GpReward);
        if (gpEarned > 0)
            logger.Info($"[WeekendDrops] Weekly tier GP earned this raid: +{gpEarned} (points {pointsBefore} to {pointsAfter})");

        gpEarned += ApplyModifierPayout(sessionId, r);

        SaveState(sessionId, state);

        logger.Info($"[WeekendDrops] Weekly raid result applied (survived={r.Survived}, scavRaid={r.IsScavRaid}, " +
            $"scav={r.ScavKills} pmc={r.PmcKills} boss={r.BossKills} hs={r.Headshots} nade={r.GrenadeKills}) - " +
            string.Join(", ", state.Challenges.Select(c => $"{c.Definition?.Type}:{c.Current}/{c.Target}")));
        return gpEarned;
    }

    private int ApplyModifierPayout(MongoId sessionId, RaidResultRequest r)
    {
        var mod = modifiers.Active;
        if (mod is null || !WeekendModifierKinds.IsPerKill(mod.Kind)) return 0;

        int reported = mod.Kind switch
        {
            WeekendModifierKind.WeaponClass  => r.ModifierKills,
            WeekendModifierKind.HeadshotKill => r.Headshots,
            WeekendModifierKind.MeleeKill    => r.MeleeKills,
            WeekendModifierKind.GrenadeKill  => r.GrenadeKills,
            WeekendModifierKind.SuppressedKill => r.SuppressedKills,
            WeekendModifierKind.LongRangeKill => r.KillDistances.Count(d => d >= mod.MinDistanceMeters),
            _ => 0,
        };
        if (reported <= 0) return 0;

        int kills = Math.Min(reported, modifiers.CapFor(mod));
        int gp = kills * modifiers.RateFor(mod);
        if (gp <= 0) return 0;

        gpBalance.Add(sessionId.ToString(), gp);
        logger.Info($"[WeekendDrops] Modifier '{mod.Id}' paid +{gp} GP ({kills} of {reported} reported kill(s))");
        return gp;
    }

    public string GetWeekendScheduleText() => WeekendWindow.ScheduleText(_config);

    public bool IsWeekendActive() => WeekendWindow.IsActive(_config);

    public string GetCurrentWeekendId() => WeekendWindow.CurrentId(_config);

    private readonly object _fileLock = new();

    private PlayerWeekendState GetOrCreateState(MongoId sessionId, PmcData profile)
    {
        var path = StatePath(sessionId);
        PlayerWeekendState? state;

        lock (_fileLock)
            state = File.Exists(path) ? LoadJson<PlayerWeekendState>(path) : null;

        var currentWeekendId = GetCurrentWeekendId();

        if (state is not null && state.WeekendId == currentWeekendId && ReplaceScavChallenges(state))
        {
            SaveState(sessionId, state);
            logger.Info($"[WeekendDrops] Weekend Scav challenges replaced for {sessionId} (Scav challenges disabled)");
        }

        bool stale = false;
        if (state is not null && state.WeekendId == currentWeekendId)
        {
            bool poolChanged = state.Challenges.Any(c => _challengePool.All(d => d.Id != c.DefinitionId));
            bool planChanged = state.PlanBudget != _planBudget || state.PlanCount != _planCount;
            stale = poolChanged || planChanged;
        }

        if (state is null || state.WeekendId != currentWeekendId || stale)
        {
            if (stale) logger.Info($"[WeekendDrops] Reassigning weekend for {sessionId} - cached set was stale (pool or weekend plan changed)");
            state = new PlayerWeekendState { WeekendId = currentWeekendId };
            AssignChallenges(state);
            logger.Info($"[WeekendDrops] New weekend started for {sessionId} - assigned {state.Challenges.Count} challenges");

            SaveState(sessionId, state);
        }

        foreach (var cp in state.Challenges)
            cp.Definition = _challengePool.FirstOrDefault(d => d.Id == cp.DefinitionId);

        return state;
    }

    private void SaveState(MongoId sessionId, PlayerWeekendState state)
    {
        var json = JsonSerializer.Serialize(state, JsonOptions);
        lock (_fileLock)
            File.WriteAllText(StatePath(sessionId), json);
    }

    private string StatePath(MongoId sessionId) =>
        SysPath.Combine(_dataDir, $"{sessionId}.json");

    public (int done, int total) GetWeeklyProgress(MongoId sessionId)
    {
        try
        {
            var path = StatePath(sessionId);
            if (!File.Exists(path)) return (0, 0);
            var state = JsonSerializer.Deserialize<PlayerWeekendState>(File.ReadAllText(path));
            if (state is null || state.WeekendId != GetCurrentWeekendId()) return (0, 0);
            return (state.Challenges.Count(c => c.Completed), state.Challenges.Count);
        }
        catch { return (0, 0); }
    }

    private void AssignChallenges(PlayerWeekendState state)
    {
        var rng = new Random();
        int n = _planCount;
        int budget = _planBudget;

        var chosen = PickByDifficultyBudget(rng, n, budget) ?? PickWeighted(rng, n);

        state.Challenges = chosen.Select(d => new ChallengeProgress
        {
            DefinitionId = d.Id,
            Target = d.Target,
            Definition = d
        }).ToList();

        state.PlanCount = _planCount;
        state.PlanBudget = _planBudget;

        int total = chosen.Sum(c => c.Difficulty);
        logger.Info($"[WeekendDrops] Assigned {chosen.Count} challenges totalling {total} difficulty points (budget {budget})");
    }

    private List<ChallengeDefinition>? PickByDifficultyBudget(Random rng, int n, int budget)
    {
        var byDifficulty = _challengePool
            .GroupBy(c => c.Difficulty)
            .ToDictionary(g => g.Key, g => g.OrderBy(_ => rng.Next()).ToList());

        foreach (var comp in DifficultyCompositions(n, budget).OrderBy(_ => rng.Next()))
        {
            if (TryBuildComposition(comp, byDifficulty, n, out var picked))
                return picked.OrderBy(_ => rng.Next()).ToList();
        }
        return null;
    }

    private static bool TryBuildComposition(
        Dictionary<int, int> comp,
        Dictionary<int, List<ChallengeDefinition>> byDifficulty,
        int n,
        out List<ChallengeDefinition> picked)
    {
        picked = new List<ChallengeDefinition>();
        var usedGroups = new HashSet<string>();
        foreach (var (diff, count) in comp)
        {
            if (!byDifficulty.TryGetValue(diff, out var avail)) return false;
            int need = count;
            foreach (var cand in avail)
            {
                if (need == 0) break;
                if (!usedGroups.Add(ChallengeMetrics.Group(cand.Type))) continue;
                picked.Add(cand);
                need--;
            }
            if (need > 0) return false;
        }
        return picked.Count == n;
    }

    private static IEnumerable<Dictionary<int, int>> DifficultyCompositions(int n, int budget)
    {
        for (int hard = 0; hard <= n; hard++)
            for (int med = 0; med <= n - hard; med++)
            {
                int easy = n - hard - med;
                if (easy * 1 + med * 2 + hard * 3 != budget) continue;

                var map = new Dictionary<int, int>();
                if (easy > 0) map[1] = easy;
                if (med  > 0) map[2] = med;
                if (hard > 0) map[3] = hard;
                yield return map;
            }
    }

    private List<ChallengeDefinition> PickWeighted(Random rng, int n) =>
        _challengePool
            .SelectMany(c => Enumerable.Repeat(c, Math.Max(1, 4 - c.Difficulty)))
            .OrderBy(_ => rng.Next())
            .DistinctBy(c => c.Id)
            .DistinctBy(c => ChallengeMetrics.Group(c.Type))
            .Take(n)
            .ToList();

    public bool ClaimTier(MongoId sessionId, int requiredChallenges)
    {
        if (!_config.Enabled || !IsWeekendActive()) return false;

        var profile = profileHelper.GetPmcProfile(sessionId);
        if (profile is null) return false;

        var tier = _dropsConfig.Tiers.FirstOrDefault(t => t.RequiredChallenges == requiredChallenges);
        if (tier is null)
        {
            logger.Warning($"[WeekendDrops] Claim rejected - no tier requires {requiredChallenges} points");
            return false;
        }

        var state = GetOrCreateState(sessionId, profile);
        int completedPoints = CompletedDifficultyPoints(state);

        if (!_config.DebugMode && completedPoints < tier.RequiredChallenges)
        {
            logger.Warning($"[WeekendDrops] Claim rejected - {completedPoints}/{tier.RequiredChallenges} difficulty points done");
            return false;
        }
        if (state.ClaimedTiers.Contains(tier.RequiredChallenges))
        {
            logger.Warning($"[WeekendDrops] Claim rejected - '{tier.TierName}' already claimed");
            return false;
        }

        SendDropTier(sessionId, tier);
        if (tier.GpReward > 0)
            gpBalance.Add(sessionId.ToString(), collection.ScaleGp(sessionId.ToString(), modifiers.ScaleGp(tier.GpReward)));

        // TarCoin is deliberately not run through the GP scalers: it is a flat premium
        // payout, not something collection bonuses or weekend modifiers inflate.
        var tcTier = tarcoin.TierReward;
        if (tcTier > 0) tarcoinBalance.Add(sessionId.ToString(), tcTier);

        state.ClaimedTiers.Add(tier.RequiredChallenges);
        SaveState(sessionId, state);

        int totalPoints = state.Challenges.Sum(c => c.Definition?.Difficulty ?? 0);
        logger.Info($"[WeekendDrops] Player {sessionId} claimed '{tier.TierName}' " +
                    $"({completedPoints}/{totalPoints} difficulty points done)" +
                    (tcTier > 0 ? $", +{tcTier} TC" : ""));
        return true;
    }

    private static int CompletedDifficultyPoints(PlayerWeekendState state) =>
        state.Challenges.Where(c => c.Completed).Sum(c => c.Definition?.Difficulty ?? 0);

    public bool DebugAction(MongoId sessionId, string? action)
    {
        if (!_config.DebugMode)
        {
            logger.Warning("[WeekendDrops] Debug action ignored - debugMode is off");
            return false;
        }

        var profile = profileHelper.GetPmcProfile(sessionId);
        if (profile is null) return false;

        var state = GetOrCreateState(sessionId, profile);

        switch (action?.ToLowerInvariant())
        {
            case "resetclaims":
                state.ClaimedTiers.Clear();
                break;
            case "resetprogress":
                state.ClaimedTiers.Clear();
                foreach (var c in state.Challenges) c.Current = 0;
                state.SurvivalTimeBank = 0;
                break;
            case "reroll":
                state.ClaimedTiers.Clear();
                state.SurvivalTimeBank = 0;
                AssignChallenges(state);
                break;
            case "completeone":
                var next = state.Challenges.FirstOrDefault(c => !c.Completed);
                if (next is not null) next.Current = next.Target;
                break;
            case "completeall":
                foreach (var c in state.Challenges) c.Current = c.Target;
                break;
            default:
                logger.Warning($"[WeekendDrops] Unknown debug action '{action}'");
                return false;
        }

        SaveState(sessionId, state);
        logger.Info($"[WeekendDrops] Debug action '{action}' applied for {sessionId}");
        return true;
    }

    private void SendDropTier(MongoId sessionId, DropTier tier)
    {
        var rng = new Random();
        var pool = tier.Pools[rng.Next(tier.Pools.Count)];
        var itemId = pool.ItemIds[rng.Next(pool.ItemIds.Count)];

        var rewardItems = BuildRewardItems(itemId, pool.Count);
        long expirySeconds = (long)TimeSpan.FromHours(_config.DropExpiryHours).TotalSeconds;

        mailSendService.SendSystemMessageToPlayer(
            sessionId,
            $"Weekend Drop Unlocked: {tier.TierName}",
            rewardItems,
            expirySeconds
        );
    }

    private static List<Item> BuildRewardItems(string templateId, int count)
    {
        var root = new Item
        {
            Id = new MongoId(),
            Template = new MongoId(templateId),
            ParentId = null,
            SlotId = "main",
        };

        if (count > 1)
        {
            root.Upd ??= new();
            root.Upd.StackObjectsCount = count;
        }

        return [root];
    }

    public WeekendStateDto GetClientState(MongoId sessionId)
    {
        bool active = IsWeekendActive();

        var dto = new WeekendStateDto
        {
            IsWeekendActive = active,
            WeekendId = GetCurrentWeekendId(),
            TimeRemainingSeconds = active ? GetSecondsUntilWeekendEnd() : 0,
            TierThresholds = _dropsConfig.Tiers.Select(t => t.RequiredChallenges).ToList(),
            TierGpRewards = _dropsConfig.Tiers.Select(t => t.GpReward).ToList(),
            ScheduleText = GetWeekendScheduleText(),
            DebugMode = _config.DebugMode,
            Modifier = modifiers.ToDto()
        };

        var profile = profileHelper.GetPmcProfile(sessionId);
        if (profile is null) return dto;

        dto.GpCoins = gpBalance.Get(sessionId.ToString());
        dto.TarcoinRate = tarcoin.UnitsPerCoin;
        dto.Tarcoins    = tarcoinBalance.Get(sessionId.ToString());

        dto.PendingGifts = giftService.TakePending(sessionId.ToString());

        if (!active) return dto;

        var state = GetOrCreateState(sessionId, profile);

        dto.WeekendId = state.WeekendId;
        dto.ClaimedTiers = state.ClaimedTiers;
        dto.Challenges = state.Challenges.Select(cp => new ChallengeDto
        {
            Id = cp.DefinitionId,
            Type = cp.Definition?.Type.ToString() ?? "",
            Description = cp.Definition?.Description ?? cp.DefinitionId,
            Current = cp.Current,
            Target = cp.Target,
            Completed = cp.Completed,
            Difficulty = cp.Definition?.Difficulty ?? 1,
            MinDistanceMeters = cp.Definition?.MinDistanceMeters ?? 0,
            TargetWeaponClass = cp.Definition?.TargetWeaponClass ?? "",
            TargetBoss        = cp.Definition?.TargetBoss ?? ""
        }).ToList();

        dto.RerollEnabled   = _config.EnableWeekendReroll;
        dto.RerollCost      = GetRerollCost(state);
        dto.RerollsUsed     = state.RerollsUsed;
        dto.RerollsMax      = _config.WeekendRerollMaxPerWeekend;
        dto.RerollAvailable = _config.EnableWeekendReroll && !RerollsExhausted(state);

        return dto;
    }

    private DateTime _debugWeekendEnd = DateTime.MinValue;
    private const double DebugWeekendHours = 48;

    public double GetSecondsUntilWeekendEnd()
    {
        if (_config.DebugMode)
        {
            if (_debugWeekendEnd <= DateTime.UtcNow)
                _debugWeekendEnd = DateTime.UtcNow.AddHours(DebugWeekendHours);
            return (_debugWeekendEnd - DateTime.UtcNow).TotalSeconds;
        }

        var now = DateTime.Now;
        int daysUntilEnd = (_config.WeekendEndDay - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilEnd == 0 && now.Hour >= _config.WeekendEndHour)
            daysUntilEnd = 7;

        var end = now.Date.AddDays(daysUntilEnd).AddHours(_config.WeekendEndHour);
        return (end - now).TotalSeconds;
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
}
