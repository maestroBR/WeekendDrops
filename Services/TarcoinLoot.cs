using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using WeekendDrops.Models;

namespace WeekendDrops.Services;

/// The WTT backport defines TarCoin but never injects it anywhere, so it cannot be found
/// in raid at all. This gives it a fixed set of spawns without editing the backport's files.
[Injectable]
public class TarcoinLoot(
    LocationTable locations,
    TemplateTable templates,
    ISptLogger<TarcoinLoot> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private TarcoinConfig? _config;

    public TarcoinConfig Config => _config ??= LoadConfig();

    /// 0 when the coin cannot be deposited at all, which the deposit route treats as "reject".
    public int UnitsPerCoin => Available ? Math.Max(0, Config.UnitsPerCoin) : 0;

    private bool Available => Config.Enabled && ModPresence.ContentBackportInstalled;

    /// 0 when TarCoin is unavailable, so callers can pay out unconditionally.
    public int TierReward => Available ? Math.Max(0, Config.Rewards.PerTier) : 0;

    public int ContractReward => Available ? Math.Max(0, Config.Rewards.PerContract) : 0;

    public void Inject()
    {
        var config = Config;
        if (!config.Enabled || config.Containers.Count == 0) return;

        if (!ModPresence.ContentBackportInstalled)
        {
            logger.Debug("[WeekendDrops] TarCoin: WTT-ContentBackport not installed, skipping");
            return;
        }

        var coinTpl = new MongoId(config.Tpl);
        if (!templates.Items.TryGetValue(coinTpl, out var coin))
        {
            logger.Warning($"[WeekendDrops] TarCoin: template {config.Tpl} not in the database, no spawns added");
            return;
        }

        if (config.LockStackSize && coin.Properties is not null)
        {
            coin.Properties.StackMaxSize = 1;
            coin.Properties.StackMinRandom = 1;
            coin.Properties.StackMaxRandom = 1;
        }

        var wanted = ResolveContainers(config);
        if (wanted.Count == 0)
        {
            logger.Warning("[WeekendDrops] TarCoin: none of the configured containers matched a template, no spawns added");
            return;
        }

        var queued = 0;
        foreach (var (name, location) in locations.GetDictionary())
        {
            if (location?.StaticLoot is null) continue;

            var mapName = name;
            location.StaticLoot.AddTransformer(staticLoot =>
            {
                // A throwing transformer takes the whole map's loot down with it.
                try
                {
                    var touched = 0;
                    foreach (var (containerTpl, details) in staticLoot ?? [])
                    {
                        if (!wanted.TryGetValue(containerTpl, out var percent)) continue;
                        if (AddToContainer(details, coinTpl, percent)) touched++;
                    }

                    if (config.Debug)
                        logger.Info($"[WeekendDrops]   TarCoin: {mapName}, {touched} container(s)");
                }
                catch (Exception ex)
                {
                    logger.Error($"[WeekendDrops] TarCoin injection failed on {mapName}: {ex.Message}");
                }

                return staticLoot;
            });

            queued++;
        }

        var summary = string.Join(", ", config.Containers.Select(c => $"{c.Name} {c.WeightPercent}%"));
        logger.Info($"[WeekendDrops] TarCoin spawns queued on {queued} locations ({summary})");
    }

    private Dictionary<MongoId, double> ResolveContainers(TarcoinConfig config)
    {
        var byName = config.Containers
            .Where(c => !string.IsNullOrWhiteSpace(c.Name) && c.WeightPercent > 0)
            .ToDictionary(c => c.Name, c => c.WeightPercent, StringComparer.OrdinalIgnoreCase);

        var resolved = new Dictionary<MongoId, double>();
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tpl, item) in templates.Items)
        {
            if (item.Name is null) continue;
            if (!byName.TryGetValue(item.Name, out var percent)) continue;
            resolved[tpl] = percent;
            matched.Add(item.Name);
        }

        var missing = byName.Keys.Where(n => !matched.Contains(n)).ToList();
        if (missing.Count > 0)
            logger.Warning($"[WeekendDrops] TarCoin: unknown container name(s) in tarcoin.json: {string.Join(", ", missing)}");

        return resolved;
    }

    private static bool AddToContainer(StaticLootDetails details, MongoId coinTpl, double weightPercent)
    {
        var pool = details.ItemDistribution?.ToList();
        if (pool is null || pool.Count == 0 || pool.Any(d => d.Tpl == coinTpl)) return false;

        var total = pool.Sum(d => d.RelativeProbability ?? 0);
        if (total <= 0) return false;

        pool.Add(new ItemDistribution
        {
            Tpl = coinTpl,
            RelativeProbability = (float)Math.Max(1, Math.Round(total * weightPercent / 100.0)),
        });

        details.ItemDistribution = pool;
        return true;
    }

    private TarcoinConfig LoadConfig()
    {
        var path = WdPaths.Config("tarcoin.json");
        if (!File.Exists(path)) return new TarcoinConfig();
        try
        {
            return JsonSerializer.Deserialize<TarcoinConfig>(File.ReadAllText(path), JsonOptions)
                   ?? new TarcoinConfig();
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] TarCoin: tarcoin.json is unreadable ({ex.Message}), spawns disabled");
            return new TarcoinConfig { Enabled = false };
        }
    }
}
