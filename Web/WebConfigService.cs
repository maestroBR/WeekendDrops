using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using WeekendDrops.Models;
using WeekendDrops.Services;

namespace WeekendDrops.Web;

[Injectable(InjectionType.Singleton)]
public class WebConfigService(ISptLogger<WebConfigService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private string ConfigPath => WdPaths.Config("config.json");
    private string ContractsPath => WdPaths.Config("contracts.json");

    public ModConfig Config { get; private set; } = new();

    private System.Text.Json.Nodes.JsonNode? _contracts;

    public int MaxContractsPerRaid
    {
        get => _contracts?["maxContractsPerRaid"]?.GetValue<int>() ?? 3;
        set { if (_contracts != null) _contracts["maxContractsPerRaid"] = value; }
    }

    public bool ContractsReadable => _contracts != null;

    public void Load()
    {
        try
        {
            Config = File.Exists(ConfigPath)
                ? JsonSerializer.Deserialize<ModConfig>(File.ReadAllText(ConfigPath), JsonOptions) ?? new ModConfig()
                : new ModConfig();
        }
        catch (Exception ex)
        {
            logger.Warning($"[WeekendDrops] config.json unreadable, page is showing defaults: {ex.Message}");
            Config = new ModConfig();
        }

        try
        {
            _contracts = File.Exists(ContractsPath)
                ? System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(ContractsPath))
                : null;
        }
        catch (Exception ex)
        {
            logger.Warning($"[WeekendDrops] contracts.json unreadable, its settings are hidden: {ex.Message}");
            _contracts = null;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(WdPaths.ConfigDir);
        WeekendDrops.Services.AtomicFile.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, JsonOptions));

        if (_contracts != null)
            WeekendDrops.Services.AtomicFile.WriteAllText(ContractsPath, _contracts.ToJsonString(JsonOptions));

        logger.Info("[WeekendDrops] Config saved from the mod page. Restart the server to apply it.");
    }

    public string ScheduleSummary()
    {
        static string Day(int d) => ((DayOfWeek)Math.Clamp(d, 0, 6)).ToString();
        return $"{Day(Config.WeekendStartDay)} {Config.WeekendStartHour:00}:00 " +
               $"to {Day(Config.WeekendEndDay)} {Config.WeekendEndHour:00}:00";
    }

    public string BudgetSummary()
    {
        int n = Math.Max(1, Config.ChallengesPerWeekend);
        int budget = Config.WeekendDifficultyBudget;

        if (budget < n) return $"Budget {budget} is below {n} challenges, so it will be raised to {n}.";
        if (budget > n * 3) return $"Budget {budget} is above the {n * 3} maximum for {n} challenges, so it will be lowered.";

        int hard = Math.Max(0, budget - n * 2);
        int easy = Math.Max(0, n * 2 - budget);
        int med  = n - hard - easy;
        return $"About {easy} easy, {med} medium and {hard} hard per weekend.";
    }
}
