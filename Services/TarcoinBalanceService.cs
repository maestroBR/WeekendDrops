using System.Text.Json;
using SysPath = System.IO.Path;
using SPTarkov.DI.Annotations;
using SPTarkov.Common.Models.Logging;

namespace WeekendDrops.Services;

/// TarCoin is a premium currency in its own right, kept apart from GP so the two can never
/// be exchanged in either direction.
[Injectable(InjectionType.Singleton)]
public class TarcoinBalanceService
{
    private readonly ISptLogger<TarcoinBalanceService> logger;
    private readonly string _file = WdPaths.Data("tarcoin_balances.json");

    private readonly object _lock = new();
    private Dictionary<string, int> _balances = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public TarcoinBalanceService(ISptLogger<TarcoinBalanceService> log)
    {
        logger = log;
        Load();
    }

    public int Get(string sessionId)
    {
        lock (_lock) return _balances.TryGetValue(sessionId, out var v) ? v : 0;
    }

    public void Add(string sessionId, int amount)
    {
        if (amount <= 0) return;
        lock (_lock)
        {
            _balances[sessionId] = (_balances.TryGetValue(sessionId, out var v) ? v : 0) + amount;
            Save();
        }
    }

    public bool TrySpend(string sessionId, int amount)
    {
        if (amount <= 0) return true;
        lock (_lock)
        {
            int cur = _balances.TryGetValue(sessionId, out var v) ? v : 0;
            if (cur < amount) return false;
            _balances[sessionId] = cur - amount;
            Save();
            return true;
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_file))
                _balances = JsonSerializer.Deserialize<Dictionary<string, int>>(
                    File.ReadAllText(_file)) ?? new();
        }
        catch (Exception ex)
        {
            _balances = new();
            logger.Error($"[WeekendDrops] tarcoin_balances.json could not be read - every TarCoin balance " +
                         $"reads as 0 until it is restored: {ex.Message}", null);
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(SysPath.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(_balances, JsonOptions));
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] tarcoin_balances.json could not be written - TarCoin deposited " +
                         $"now is lost: {ex.Message}", null);
        }
    }
}
