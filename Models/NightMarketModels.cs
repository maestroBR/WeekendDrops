using System.Text.Json.Serialization;

namespace WeekendDrops.Models;

/// One hand-picked entry the market can roll. Kept separate from shop.json on purpose:
/// the market is meant to offer things the GP shop never does.
public class NightMarketEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;

    /// Price in TarCoin units.
    [JsonPropertyName("cost")]
    public int Cost { get; set; }

    [JsonPropertyName("weight")]
    public int Weight { get; set; } = 10;

    [JsonPropertyName("tier")]
    public string Tier { get; set; } = "rare";

    /// Runs the weapon kitter over the item before it is mailed, so the card hands over a
    /// built gun rather than a bare receiver.
    [JsonPropertyName("kit")]
    public bool Kit { get; set; }

    /// Extra items mailed alongside, for kit-style cards (ammo, meds, armour).
    [JsonPropertyName("extras")]
    public List<NightMarketExtra> Extras { get; set; } = [];

    /// A card that stays unknown after flipping and only resolves when bought. Its own
    /// templateId is unused; the contents come from one of the entries below.
    [JsonPropertyName("mystery")]
    public bool Mystery { get; set; }

    /// Pool entry ids this card can resolve to. Empty means any non-mystery entry whose
    /// tier matches OutcomeTier.
    [JsonPropertyName("outcomes")]
    public List<string> Outcomes { get; set; } = [];

    /// Tier filter used when Outcomes is empty. Defaults to this card's own tier.
    [JsonPropertyName("outcomeTier")]
    public string? OutcomeTier { get; set; }
}

public class NightMarketExtra
{
    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;
}

public class NightMarketConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("cardCount")]
    public int CardCount { get; set; } = 6;

    [JsonPropertyName("schedule")]
    public NightMarketSchedule Schedule { get; set; } = new();

    [JsonPropertyName("pool")]
    public List<NightMarketEntry> Pool { get; set; } = [];
}

/// The market keeps its own window, separate from the weekend. Defaults to a nightly
/// 22:00 to 04:00 opening with the card roll changing once a week.
public class NightMarketSchedule
{
    [JsonPropertyName("startHour")]
    public int StartHour { get; set; } = 22;

    [JsonPropertyName("durationHours")]
    public int DurationHours { get; set; } = 6;

    /// Days of week the window may open on (0 = Sunday). Empty means every night.
    [JsonPropertyName("days")]
    public List<int> Days { get; set; } = [];

    /// How often the cards are rerolled, in days. 7 keeps one roll for a week of nights.
    [JsonPropertyName("rerollDays")]
    public int RerollDays { get; set; } = 7;
}

/// Persisted per player: the roll is kept rather than reseeded, so editing the pool
/// mid-weekend cannot reshuffle a market someone has already revealed.
public class NightMarketCard
{
    [JsonPropertyName("entryId")]
    public string EntryId { get; set; } = "";

    [JsonPropertyName("cost")]
    public int Cost { get; set; }

    [JsonPropertyName("revealed")]
    public bool Revealed { get; set; }

    [JsonPropertyName("bought")]
    public bool Bought { get; set; }

    /// For a mystery card, what it resolved to. Set at purchase and kept so the card can
    /// show the result afterwards.
    [JsonPropertyName("resultEntryId")]
    public string? ResultEntryId { get; set; }
}

public class NightMarketState
{
    [JsonPropertyName("marketId")]
    public string MarketId { get; set; } = "";

    [JsonPropertyName("cards")]
    public List<NightMarketCard> Cards { get; set; } = [];
}

/// What the client is told about one card. Everything except the flip state stays null
/// until the card is revealed, so the contents cannot be read out of the response.
public class NightMarketCardDto
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("revealed")]
    public bool Revealed { get; set; }

    [JsonPropertyName("bought")]
    public bool Bought { get; set; }

    [JsonPropertyName("cost")]
    public int? Cost { get; set; }

    /// Pool entry id, used by the client to find a translated name. Sent only once the
    /// card's contents are public, for the same reason the template id is withheld.
    [JsonPropertyName("entryId")]
    public string? EntryId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("templateId")]
    public string? TemplateId { get; set; }

    [JsonPropertyName("tier")]
    public string? Tier { get; set; }

    [JsonPropertyName("kit")]
    public bool Kit { get; set; }

    [JsonPropertyName("mystery")]
    public bool Mystery { get; set; }

    [JsonPropertyName("extras")]
    public List<NightMarketExtra>? Extras { get; set; }
}

public class NightMarketDto
{
    [JsonPropertyName("open")]
    public bool Open { get; set; }

    [JsonPropertyName("marketId")]
    public string MarketId { get; set; } = "";

    [JsonPropertyName("tarcoins")]
    public int Tarcoins { get; set; }

    [JsonPropertyName("secondsRemaining")]
    public double SecondsRemaining { get; set; }

    [JsonPropertyName("secondsUntilOpen")]
    public double SecondsUntilOpen { get; set; }

    [JsonPropertyName("scheduleText")]
    public string ScheduleText { get; set; } = "";

    [JsonPropertyName("forceOpen")]
    public bool ForceOpen { get; set; }

    [JsonPropertyName("cards")]
    public List<NightMarketCardDto> Cards { get; set; } = [];
}
