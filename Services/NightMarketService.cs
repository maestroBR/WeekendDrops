using System.Text.Json;
using SysPath = System.IO.Path;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Commerce;
using WeekendDrops.Models;
using WeekendDrops.Patches;

namespace WeekendDrops.Services;

/// A per-player rotating market paid for in TarCoin. The roll is personal and one-shot:
/// each card can be bought once and is gone when the window closes.
[Injectable(InjectionType.Singleton)]
public class NightMarketService(
    WeekendChallengeService weekend,
    TarcoinBalanceService tarcoinBalance,
    MailSendService mailSendService,
    ItemHelper itemHelper,
    ISptLogger<NightMarketService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _dataDir = WdPaths.DataDir;
    private readonly object _fileLock = new();

    private NightMarketConfig _config = new();

    public NightMarketConfig Config => _config;

    public void LoadConfig()
    {
        var path = WdPaths.Config("night_market.json");
        if (!File.Exists(path))
        {
            logger.Debug("[WeekendDrops] night_market.json not found, market disabled");
            _config = new NightMarketConfig { Enabled = false };
            return;
        }

        try
        {
            _config = JsonSerializer.Deserialize<NightMarketConfig>(File.ReadAllText(path), JsonOptions)
                      ?? new NightMarketConfig();
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] night_market.json is unreadable ({ex.Message}), market disabled");
            _config = new NightMarketConfig { Enabled = false };
            return;
        }

        var bad = _config.Pool
            .Where(e => string.IsNullOrEmpty(e.Id) || (string.IsNullOrEmpty(e.TemplateId) && !e.Mystery))
            .ToList();
        foreach (var e in bad) _config.Pool.Remove(e);
        if (bad.Count > 0)
            logger.Warning($"[WeekendDrops] Night market: dropped {bad.Count} pool entries missing id or templateId");

        var dupes = _config.Pool.GroupBy(e => e.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupes.Count > 0)
            logger.Warning($"[WeekendDrops] Night market: duplicate pool ids: {string.Join(", ", dupes)}");

        foreach (var m in _config.Pool.Where(e => e.Mystery))
        {
            var unknown = m.Outcomes.Where(o => _config.Pool.All(e => e.Id != o)).ToList();
            if (unknown.Count > 0)
                logger.Warning($"[WeekendDrops] Night market: mystery card '{m.Id}' lists unknown " +
                               $"outcome(s): {string.Join(", ", unknown)}");
            if (OutcomesFor(m).Count == 0)
                logger.Error($"[WeekendDrops] Night market: mystery card '{m.Id}' has no valid outcomes " +
                             "and cannot be bought");
        }

        logger.Info($"[WeekendDrops] Night market: {_config.Pool.Count} pool entries, " +
                    $"{_config.CardCount} cards per player, {NightMarketWindow.ScheduleText(_config.Schedule)}");
    }

    private bool HasStock => _config.Enabled && _config.Pool.Count > 0;

    /// Debug override, deliberately not persisted: it dies with the server process.
    private bool _forceOpen;

    public bool ForceOpen => _forceOpen;

    public bool IsOpen => HasStock
                          && (_forceOpen || NightMarketWindow.IsOpen(_config.Schedule, DateTime.Now));

    private string CurrentMarketId => _forceOpen
        ? "debug-" + _debugRoll
        : NightMarketWindow.CurrentPeriodId(_config.Schedule, DateTime.Now);

    private int _debugRoll;

    public NightMarketState GetState(string sessionId)
    {
        if (!IsOpen) return new NightMarketState();

        var state = Load(sessionId);
        if (state is not null && state.MarketId == CurrentMarketId) return state;

        state = Roll(sessionId);
        Save(sessionId, state);
        return state;
    }

    public string Reveal(string sessionId, int index)
    {
        if (!IsOpen) return "closed";

        var state = GetState(sessionId);
        if (index < 0 || index >= state.Cards.Count) return "no_such_card";
        if (state.Cards[index].Revealed) return "ok";

        state.Cards[index].Revealed = true;
        Save(sessionId, state);
        return "ok";
    }

    public string Buy(string sessionId, int index)
    {
        if (!IsOpen) return "closed";

        var state = GetState(sessionId);
        if (index < 0 || index >= state.Cards.Count) return "no_such_card";

        var card = state.Cards[index];
        if (!card.Revealed) return "not_revealed";
        if (card.Bought)    return "already_bought";

        var entry = _config.Pool.FirstOrDefault(e => e.Id == card.EntryId);
        if (entry is null)
        {
            logger.Warning($"[WeekendDrops] Night market: card {card.EntryId} is no longer in the pool");
            return "no_such_card";
        }

        // Resolve a mystery card before charging, so a misconfigured one cannot take TC
        // and hand back nothing.
        var delivered = entry;
        if (entry.Mystery)
        {
            var rolled = RollOutcome(entry);
            if (rolled is null)
            {
                logger.Error($"[WeekendDrops] Night market: mystery card '{entry.Id}' has no valid outcomes");
                return "no_such_card";
            }
            delivered = rolled;
        }

        if (!tarcoinBalance.TrySpend(sessionId, card.Cost)) return "insufficient_tarcoin";

        // Mark bought before mailing: a card charged for but not delivered can be fixed by
        // hand, a card delivered twice cannot.
        card.Bought = true;
        if (entry.Mystery) card.ResultEntryId = delivered.Id;
        Save(sessionId, state);

        mailSendService.SendSystemMessageToPlayer(
            new MongoId(sessionId),
            $"Night Market: {delivered.Name}",
            BuildItems(delivered),
            (long)TimeSpan.FromHours(weekend.Config.DropExpiryHours).TotalSeconds
        );

        logger.Info($"[WeekendDrops] Night market purchase: {entry.Name}" +
                    (entry.Mystery ? $" -> {delivered.Name}" : "") +
                    $" (-{card.Cost} TC, balance {tarcoinBalance.Get(sessionId)})");
        return "ok";
    }

    private List<NightMarketEntry> OutcomesFor(NightMarketEntry mystery)
    {
        if (mystery.Outcomes.Count > 0)
            return _config.Pool
                .Where(e => !e.Mystery && e.Weight > 0 && mystery.Outcomes.Contains(e.Id))
                .ToList();

        var tier = mystery.OutcomeTier ?? mystery.Tier;
        return _config.Pool
            .Where(e => !e.Mystery && e.Weight > 0 && string.Equals(e.Tier, tier, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    // Unseeded on purpose: the market roll is deterministic per player and period, but what
    // a mystery card turns into must not be predictable from it.
    private readonly Random _outcomeRng = new();

    private NightMarketEntry? RollOutcome(NightMarketEntry mystery)
    {
        var options = OutcomesFor(mystery);
        if (options.Count == 0) return null;
        lock (_outcomeRng) return PickWeighted(options, _outcomeRng);
    }

    public NightMarketDto ToDto(string sessionId)
    {
        var now = DateTime.Now;
        var dto = new NightMarketDto
        {
            Open         = IsOpen,
            Tarcoins     = tarcoinBalance.Get(sessionId),
            ScheduleText = NightMarketWindow.ScheduleText(_config.Schedule),
            ForceOpen    = _forceOpen,
        };

        if (!IsOpen)
        {
            if (HasStock) dto.SecondsUntilOpen = NightMarketWindow.SecondsUntilOpen(_config.Schedule, now);
            return dto;
        }

        var state = GetState(sessionId);
        dto.MarketId = state.MarketId;
        dto.SecondsRemaining = NightMarketWindow.SecondsUntilClose(_config.Schedule, now);

        for (int i = 0; i < state.Cards.Count; i++)
        {
            var card = state.Cards[i];
            var slot = new NightMarketCardDto
            {
                Index    = i,
                Revealed = card.Revealed,
                Bought   = card.Bought,
            };

            if (card.Revealed)
            {
                var entry = _config.Pool.FirstOrDefault(e => e.Id == card.EntryId);
                slot.Cost        = card.Cost;
                slot.Tier        = entry?.Tier;
                slot.Mystery     = entry?.Mystery ?? false;

                // A mystery card sends its own blurb and no template until it is bought,
                // then swaps to whatever it turned into.
                var shown = slot.Mystery && card.ResultEntryId is not null
                    ? _config.Pool.FirstOrDefault(e => e.Id == card.ResultEntryId)
                    : entry;

                bool hide = slot.Mystery && card.ResultEntryId is null;

                slot.EntryId     = hide ? null : shown?.Id;
                slot.Name        = shown?.Name ?? card.EntryId;
                slot.Description = shown?.Description ?? "";
                slot.TemplateId  = hide ? null : shown?.TemplateId;
                slot.Kit         = !hide && (shown?.Kit ?? false);
                slot.Extras      = hide ? null : shown?.Extras;
            }

            dto.Cards.Add(slot);
        }

        return dto;
    }

    public bool DebugAction(string sessionId, string? action)
    {
        if (!weekend.Config.DebugMode)
        {
            logger.Warning("[WeekendDrops] Night market debug action ignored - debugMode is off");
            return false;
        }

        switch (action?.ToLowerInvariant())
        {
            case "forceopen":
                _forceOpen = !_forceOpen;
                logger.Info($"[WeekendDrops] Night market force-open: {_forceOpen}");
                return true;

            case "reroll":
                // Bumping the roll id is enough: GetState rerolls whenever it does not match.
                _debugRoll++;
                if (!_forceOpen) DeleteState(sessionId);
                logger.Info("[WeekendDrops] Night market rerolled");
                return true;

            case "revealall":
            {
                if (!IsOpen) return false;
                var state = GetState(sessionId);
                foreach (var c in state.Cards) c.Revealed = true;
                Save(sessionId, state);
                return true;
            }

            case "grant":
                tarcoinBalance.Add(sessionId, 1000);
                logger.Info($"[WeekendDrops] Granted 1000 TC (balance {tarcoinBalance.Get(sessionId)})");
                return true;

            case "clear":
                DeleteState(sessionId);
                logger.Info("[WeekendDrops] Night market state cleared");
                return true;

            default:
                logger.Warning($"[WeekendDrops] Unknown night market debug action '{action}'");
                return false;
        }
    }

    private void DeleteState(string sessionId)
    {
        try
        {
            lock (_fileLock)
                if (File.Exists(StatePath(sessionId))) File.Delete(StatePath(sessionId));
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] Could not clear night market state for {sessionId}: {ex.Message}", null);
        }
    }

    private List<Item> BuildItems(NightMarketEntry entry)
    {
        var items = new List<Item>();

        var root = new Item
        {
            Id       = new MongoId(),
            Template = new MongoId(entry.TemplateId),
            ParentId = null,
            SlotId   = "main",
        };
        if (entry.Count > 1) { root.Upd ??= new(); root.Upd.StackObjectsCount = entry.Count; }
        items.Add(root);

        if (entry.Kit && itemHelper.IsOfBaseclass(root.Template, BaseClasses.WEAPON))
        {
            WeaponKitPatch.EnsureHelper(itemHelper);
            WeaponKitPatch.KitWeapon(items, entry.Tier);
        }

        foreach (var extra in entry.Extras)
        {
            if (string.IsNullOrEmpty(extra.TemplateId)) continue;
            var it = new Item
            {
                Id       = new MongoId(),
                Template = new MongoId(extra.TemplateId),
                ParentId = null,
                SlotId   = "main",
            };
            if (extra.Count > 1) { it.Upd ??= new(); it.Upd.StackObjectsCount = extra.Count; }
            items.Add(it);
        }

        return items;
    }

    /// Seeded from the player and the weekend so rerolling the same window is impossible,
    /// and two players never share a market.
    private NightMarketState Roll(string sessionId)
    {
        var marketId = CurrentMarketId;
        // String.GetHashCode is randomised per process, so a restart would otherwise
        // deal every player a new market mid-window.
        var rng = new Random(WeekendWindow.StableHash(sessionId + ":" + marketId));

        var pool = _config.Pool.Where(e => e.Weight > 0).ToList();
        int want = Math.Max(1, Math.Min(_config.CardCount, pool.Count));

        var cards = new List<NightMarketCard>();
        for (int i = 0; i < want && pool.Count > 0; i++)
        {
            var picked = PickWeighted(pool, rng);
            pool.Remove(picked);
            cards.Add(new NightMarketCard { EntryId = picked.Id, Cost = Math.Max(1, picked.Cost) });
        }

        return new NightMarketState { MarketId = marketId, Cards = cards };
    }

    private static NightMarketEntry PickWeighted(List<NightMarketEntry> pool, Random rng)
    {
        int total = pool.Sum(e => e.Weight);
        int roll = rng.Next(total);
        foreach (var e in pool)
        {
            roll -= e.Weight;
            if (roll < 0) return e;
        }
        return pool[^1];
    }

    private string StatePath(string sessionId) =>
        SysPath.Combine(_dataDir, $"{sessionId}_nightmarket.json");

    private NightMarketState? Load(string sessionId)
    {
        var path = StatePath(sessionId);
        try
        {
            lock (_fileLock)
                return File.Exists(path)
                    ? JsonSerializer.Deserialize<NightMarketState>(File.ReadAllText(path), JsonOptions)
                    : null;
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] Could not read {SysPath.GetFileName(path)}: {ex.Message}. Rerolling.");
            return null;
        }
    }

    private void Save(string sessionId, NightMarketState state)
    {
        try
        {
            Directory.CreateDirectory(_dataDir);
            lock (_fileLock)
                File.WriteAllText(StatePath(sessionId), JsonSerializer.Serialize(state, JsonOptions));
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] Night market state could not be written for {sessionId}: {ex.Message}", null);
        }
    }
}
