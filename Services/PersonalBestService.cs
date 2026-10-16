using System.Text.Json;
using SysPath = System.IO.Path;
using SPTarkov.DI.Annotations;
using SPTarkov.Common.Models.Logging;
using WeekendDrops.Models;

namespace WeekendDrops.Services;

[Injectable(InjectionType.Singleton)]
public class PersonalBestService(GpBalanceService gpBalance, ISptLogger<PersonalBestService> logger)
{
    public const string LongestKill      = "longestKill";
    public const string MostKillsRaid    = "mostKillsRaid";
    public const string MostHeadshotsRaid = "mostHeadshotsRaid";
    public const string MostPmcKillsRaid = "mostPmcKillsRaid";
    public const string BestExtractValue = "bestExtractValue";
    public const string LongestExtractStreak = "longestExtractStreak";

    /// Ceiling on what one raid can pay across every record, so a first monster raid
    /// cannot out-earn a week of dailies.
    private const int PerRaidGpCap = 250;

    public static readonly IReadOnlyList<RecordDefinition> Definitions =
    [
        new() { Id = LongestKill,       Unit = RecordUnit.Meters,  GpPerUnit = 0.75,     Floor = 100,     MinStep = 5,     GpCap = 150 },
        new() { Id = MostKillsRaid,     Unit = RecordUnit.Count,   GpPerUnit = 20,       Floor = 5,       MinStep = 1,     GpCap = 150 },
        new() { Id = MostHeadshotsRaid, Unit = RecordUnit.Count,   GpPerUnit = 30,       Floor = 3,       MinStep = 1,     GpCap = 150 },
        new() { Id = MostPmcKillsRaid,  Unit = RecordUnit.Count,   GpPerUnit = 40,       Floor = 2,       MinStep = 1,     GpCap = 150 },
        new() { Id = BestExtractValue,  Unit = RecordUnit.Roubles, GpPerUnit = 1d / 4000, Floor = 500000, MinStep = 50000, GpCap = 150, RequiresSurvival = true },
        new() { Id = LongestExtractStreak, Unit = RecordUnit.Streak, GpPerUnit = 30,     Floor = 3,       MinStep = 1,     GpCap = 150, RequiresSurvival = true },
    ];

    private readonly string _file = WdPaths.Data("records.json");
    private readonly object _lock = new();

    private RecordStore _store = new();
    private bool _loaded;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public RecordsStateDto GetState(string sessionId)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var mine = _store.Records.TryGetValue(sessionId, out var m) ? m : new();

            var dto = new RecordsStateDto();
            foreach (var def in Definitions)
            {
                mine.TryGetValue(def.Id, out var e);
                dto.Records.Add(new RecordDto
                {
                    Id       = def.Id,
                    Unit     = def.Unit.ToString(),
                    Value    = e?.Value ?? 0,
                    Location = e?.Location ?? "",
                    DateUtc  = e?.DateUtc ?? "",
                    Beats    = e?.Beats ?? 0,
                    GpEarned = e?.GpEarned ?? 0,
                });
                dto.TotalBeats    += e?.Beats ?? 0;
                dto.TotalGpEarned += e?.GpEarned ?? 0;
            }
            return dto;
        }
    }

  
    public void Seed(string sessionId, string recordId, double value)
    {
        var def = Definitions.FirstOrDefault(d => d.Id == recordId);
        if (def is null || value < def.Floor) return;

        lock (_lock)
        {
            EnsureLoaded();
            var mine = Mine(sessionId);
            if (mine.TryGetValue(recordId, out var cur) && cur.Value >= value) return;

            mine[recordId] = new RecordEntry
            {
                Value    = value,
                Location = cur?.Location ?? "",
                DateUtc  = cur?.DateUtc ?? DateTime.UtcNow.ToString("o"),
                Beats    = cur?.Beats ?? 0,
                GpEarned = cur?.GpEarned ?? 0,
            };
            Save();
        }
    }

    public List<RecordDto> ApplyRaidResult(string sessionId, RaidResultRequest r)
    {
        var beaten = new List<RecordDto>();
        int budget = PerRaidGpCap;

        lock (_lock)
        {
            EnsureLoaded();
            var mine = Mine(sessionId);

            int streak = r.Survived
                ? (_store.ExtractStreaks.TryGetValue(sessionId, out var cur0) ? cur0 : 0) + 1
                : 0;
            _store.ExtractStreaks[sessionId] = streak;

            foreach (var def in Definitions)
            {
                if (def.RequiresSurvival && !r.Survived) continue;

                double value = ValueFor(def.Id, r, streak);
                if (value < def.Floor) continue;

                mine.TryGetValue(def.Id, out var cur);
                double previous = cur?.Value ?? 0;
                if (value <= previous) continue;

                // Nudges too small to be worth a card: keep the number, skip the fanfare.
                if (value - previous < def.MinStep)
                {
                    mine[def.Id] = new RecordEntry
                    {
                        Value    = value,
                        Location = cur?.Location ?? r.Location ?? "",
                        DateUtc  = cur?.DateUtc ?? DateTime.UtcNow.ToString("o"),
                        Beats    = cur?.Beats ?? 0,
                        GpEarned = cur?.GpEarned ?? 0,
                    };
                    continue;
                }

                // Baseline the very first record at the floor, otherwise a debut entry
                // would pay out for every unit below it that was never actually earned.
                double paidFrom = Math.Max(previous, def.Floor);
                int gp = (int)Math.Floor((value - paidFrom) * def.GpPerUnit);
                gp = Math.Max(0, Math.Min(Math.Min(gp, def.GpCap), budget));
                budget -= gp;

                mine[def.Id] = new RecordEntry
                {
                    Value    = value,
                    Location = r.Location ?? "",
                    DateUtc  = DateTime.UtcNow.ToString("o"),
                    Beats    = (cur?.Beats ?? 0) + 1,
                    GpEarned = (cur?.GpEarned ?? 0) + gp,
                };
                if (gp > 0) gpBalance.Add(sessionId, gp);

                beaten.Add(new RecordDto
                {
                    Id       = def.Id,
                    Unit     = def.Unit.ToString(),
                    Value    = value,
                    Previous = previous,
                    Location = r.Location ?? "",
                    DateUtc  = mine[def.Id].DateUtc,
                    Beats    = mine[def.Id].Beats,
                    GpEarned = gp,
                });
            }

            Save();
        }

        return beaten;
    }

    private static double ValueFor(string id, RaidResultRequest r, int extractStreak) => id switch
    {
        // KillDistances is a capped sample, so trust the client's running max first.
        LongestKill       => r.LongestKill > 0 ? r.LongestKill
                           : r.KillDistances.Count == 0 ? 0 : r.KillDistances.Max(),
        MostKillsRaid     => r.ScavKills + r.PmcKills + r.BossKills + r.RaiderKills + r.RogueKills,
        MostHeadshotsRaid => r.Headshots,
        MostPmcKillsRaid  => r.PmcKills,
        BestExtractValue  => r.LootValue,
        LongestExtractStreak => extractStreak,
        _                 => 0,
    };

    private Dictionary<string, RecordEntry> Mine(string sessionId)
    {
        if (!_store.Records.TryGetValue(sessionId, out var m))
            _store.Records[sessionId] = m = new();
        return m;
    }

    private class RecordStore
    {
        public Dictionary<string, Dictionary<string, RecordEntry>> Records { get; set; } = new();

        /// Live extract streak per profile. A death writes 0 here, which is why the
        /// streak is updated outside the per-record survival gate.
        public Dictionary<string, int> ExtractStreaks { get; set; } = new();
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (File.Exists(_file))
                _store = JsonSerializer.Deserialize<RecordStore>(File.ReadAllText(_file)) ?? new();
        }
        catch (Exception ex)
        {
            _store = new();
            logger.Error($"[WeekendDrops] records.json could not be read, starting from empty - " +
                         $"every personal best is gone unless you restore the file: {ex.Message}", null);
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(SysPath.GetDirectoryName(_file)!);
            AtomicFile.WriteAllText(_file, JsonSerializer.Serialize(_store, JsonOptions));
        }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] records.json could not be written - this raid's records are lost: {ex.Message}", null);
        }
    }
}
