using System.Text.Json.Serialization;

namespace WeekendDrops.Models;

/// One container the coin can spawn in, matched on the item template's internal _name
/// (exact, case-insensitive). Weight is a share of that container's existing pool total,
/// so it stays sane across maps whose pools differ by an order of magnitude.
public class TarcoinContainer
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("weightPercent")]
    public double WeightPercent { get; set; }
}

public class TarcoinConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "69dd0de23dfe95d9e70b5ebb";

    // The coin is cloned from roubles, so the static generator rolls a money stack count
    // from StackMinRandom/StackMaxRandom. The backport ships both as 0, which yields a
    // zero-count item. Pin them to 1.
    [JsonPropertyName("lockStackSize")]
    public bool LockStackSize { get; set; } = true;

    // TarCoin credited per coin deposited. Purely denomination: prices are set in these
    // units, so a coin is never worth a fraction of one.
    [JsonPropertyName("unitsPerCoin")]
    public int UnitsPerCoin { get; set; } = 100;

    /// TarCoin paid out by the mod itself. Loot spawns alone cannot fund the night market
    /// (roughly one coin every 22 raids), so these are the predictable income.
    [JsonPropertyName("rewards")]
    public TarcoinRewards Rewards { get; set; } = new();

    [JsonPropertyName("containers")]
    public List<TarcoinContainer> Containers { get; set; } = [];

    [JsonPropertyName("debug")]
    public bool Debug { get; set; }
}

public class TarcoinRewards
{
    /// Paid per weekend reward tier claimed.
    [JsonPropertyName("perTier")]
    public int PerTier { get; set; } = 200;

    /// Paid per contract completed.
    [JsonPropertyName("perContract")]
    public int PerContract { get; set; } = 100;
}
