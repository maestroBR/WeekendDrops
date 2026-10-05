using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;

namespace WeekendDrops.Models;

public class ChallengeDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("current")]
    public int Current { get; set; }

    [JsonPropertyName("target")]
    public int Target { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("difficulty")]
    public int Difficulty { get; set; }

    [JsonPropertyName("minDistanceMeters")]
    public int MinDistanceMeters { get; set; }

    [JsonPropertyName("targetWeaponClass")]
    public string TargetWeaponClass { get; set; } = "";

    [JsonPropertyName("targetBoss")]
    public string TargetBoss { get; set; } = "";
}

public class WeekendStateDto
{
    [JsonPropertyName("isWeekendActive")]
    public bool IsWeekendActive { get; set; }

    [JsonPropertyName("weekendId")]
    public string WeekendId { get; set; } = "";

    [JsonPropertyName("timeRemainingSeconds")]
    public double TimeRemainingSeconds { get; set; }

    [JsonPropertyName("challenges")]
    public List<ChallengeDto> Challenges { get; set; } = [];

    [JsonPropertyName("claimedTiers")]
    public List<int> ClaimedTiers { get; set; } = [];

    [JsonPropertyName("tierThresholds")]
    public List<int> TierThresholds { get; set; } = [];

    [JsonPropertyName("tierGpRewards")]
    public List<int> TierGpRewards { get; set; } = [];

    [JsonPropertyName("gpCoins")]
    public int GpCoins { get; set; }

    [JsonPropertyName("tarcoins")]
    public int Tarcoins { get; set; }

    // 0 means TarCoin is unavailable (backport missing or disabled), which is what the
    // client uses to decide whether to show the currency at all.
    [JsonPropertyName("tarcoinRate")]
    public int TarcoinRate { get; set; }

    [JsonPropertyName("scheduleText")]
    public string ScheduleText { get; set; } = "";

    [JsonPropertyName("debugMode")]
    public bool DebugMode { get; set; }

    [JsonPropertyName("pendingGifts")]
    public List<ReceivedGiftDto> PendingGifts { get; set; } = [];

    [JsonPropertyName("modifier")]
    public WeekendModifierDto? Modifier { get; set; }

    [JsonPropertyName("rerollEnabled")]
    public bool RerollEnabled { get; set; }

    [JsonPropertyName("rerollAvailable")]
    public bool RerollAvailable { get; set; }

    [JsonPropertyName("rerollCost")]
    public int RerollCost { get; set; }

    [JsonPropertyName("rerollsUsed")]
    public int RerollsUsed { get; set; }

    [JsonPropertyName("rerollsMax")]
    public int RerollsMax { get; set; }
}

public class WeekendModifierDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("weapClass")]
    public string WeapClass { get; set; } = "";

    [JsonPropertyName("multiplier")]
    public double Multiplier { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("gpPerKill")]
    public int GpPerKill { get; set; }

    [JsonPropertyName("maxKillsPerRaid")]
    public int MaxKillsPerRaid { get; set; }

    [JsonPropertyName("minDistanceMeters")]
    public int MinDistanceMeters { get; set; }
}

public class ReceivedGiftDto
{
    [JsonPropertyName("fromNickname")]
    public string FromNickname { get; set; } = "";

    [JsonPropertyName("amount")]
    public int Amount { get; set; }
}

public class GiftFriendDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";
}

public class FriendsStateDto
{
    [JsonPropertyName("friends")]
    public List<GiftFriendDto> Friends { get; set; } = [];
}

public class SquadRowDto
{
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("gpBalance")]
    public int GpBalance { get; set; }

    [JsonPropertyName("gpEarnedWeekend")]
    public int GpEarnedWeekend { get; set; }

    [JsonPropertyName("weeklyDone")]
    public int WeeklyDone { get; set; }

    [JsonPropertyName("weeklyTotal")]
    public int WeeklyTotal { get; set; }

    [JsonPropertyName("isYou")]
    public bool IsYou { get; set; }
}

public class SquadStateDto
{
    [JsonPropertyName("rows")]
    public List<SquadRowDto> Rows { get; set; } = [];
}

public class GiftRequest : IRequestData
{
    [JsonPropertyName("toId")]
    public string ToId { get; set; } = "";

    [JsonPropertyName("amount")]
    public int Amount { get; set; }
}

public class DailyChallengeDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("current")]
    public int Current { get; set; }

    [JsonPropertyName("target")]
    public int Target { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("gpReward")]
    public int GpReward { get; set; }

    [JsonPropertyName("rewardClaimed")]
    public bool RewardClaimed { get; set; }

    [JsonPropertyName("minDistanceMeters")]
    public int MinDistanceMeters { get; set; }

    [JsonPropertyName("targetWeaponClass")]
    public string TargetWeaponClass { get; set; } = "";

    [JsonPropertyName("targetBoss")]
    public string TargetBoss { get; set; } = "";
}

public class ShopItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("gpCost")]
    public int GpCost { get; set; }

    [JsonPropertyName("stock")]
    public int Stock { get; set; }

    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("contents")]
    public List<ShopContentDto> Contents { get; set; } = [];

    [JsonPropertyName("barterCost")]
    public List<ShopContentDto> BarterCost { get; set; } = [];

    [JsonPropertyName("restockSeconds")]
    public double RestockSeconds { get; set; }

    [JsonPropertyName("trophy")]
    public bool Trophy { get; set; }
}

public class ShopContentDto
{
    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class DailyStateDto
{
    [JsonPropertyName("challenges")]
    public List<DailyChallengeDto> Challenges { get; set; } = [];

    [JsonPropertyName("shopItems")]
    public List<ShopItemDto> ShopItems { get; set; } = [];

    [JsonPropertyName("nextResetSeconds")]
    public double NextResetSeconds { get; set; }

    [JsonPropertyName("globalRestockSeconds")]
    public double GlobalRestockSeconds { get; set; }

    [JsonPropertyName("dailyBonusGp")]
    public int DailyBonusGp { get; set; }

    [JsonPropertyName("dailyBonusClaimed")]
    public bool DailyBonusClaimed { get; set; }

    [JsonPropertyName("rerollEnabled")]
    public bool RerollEnabled { get; set; }

    [JsonPropertyName("rerollAvailable")]
    public bool RerollAvailable { get; set; }

    [JsonPropertyName("rerollCost")]
    public int RerollCost { get; set; }

    [JsonPropertyName("rerollsUsed")]
    public int RerollsUsed { get; set; }

    [JsonPropertyName("rerollsMax")]
    public int RerollsMax { get; set; }
}

public class StringIdRequest : IRequestData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}

public class SeedRecordRequest : IRequestData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("value")]
    public double Value { get; set; }
}

public class ClientFlagsRequest : IRequestData
{
    [JsonPropertyName("noScav")]
    public bool NoScav { get; set; }

    [JsonPropertyName("lootNet")]
    public bool LootNet { get; set; }
}

public class ContractDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("objectiveText")]
    public string ObjectiveText { get; set; } = "";

    [JsonPropertyName("flavor")]
    public string Flavor { get; set; } = "";

    [JsonPropertyName("zone")]
    public string Zone { get; set; } = "";

    [JsonPropertyName("objectiveRoles")]
    public List<string> ObjectiveRoles { get; set; } = [];

    [JsonPropertyName("objectiveCount")]
    public int ObjectiveCount { get; set; } = 1;

    [JsonPropertyName("gpReward")]
    public int GpReward { get; set; }

    [JsonPropertyName("dialog")]
    public string Dialog { get; set; } = "";

    [JsonPropertyName("dialogSpeaker")]
    public string DialogSpeaker { get; set; } = "";

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("sealed")]
    public bool Sealed { get; set; }

    [JsonPropertyName("cooldownSeconds")]
    public double CooldownSeconds { get; set; }

    [JsonPropertyName("triggerAirdrop")]
    public bool TriggerAirdrop { get; set; }

    [JsonPropertyName("airdropX")]
    public float AirdropX { get; set; }

    [JsonPropertyName("airdropY")]
    public float AirdropY { get; set; }

    [JsonPropertyName("airdropZ")]
    public float AirdropZ { get; set; }

    [JsonPropertyName("hideoutPosts")]
    public List<Vec3> HideoutPosts { get; set; } = [];
}

public class ContractsStateDto
{
    [JsonPropertyName("contracts")]
    public List<ContractDto> Contracts { get; set; } = [];

    [JsonPropertyName("activeContractId")]
    public string ActiveContractId { get; set; } = "";

    [JsonPropertyName("pickAvailable")]
    public bool PickAvailable { get; set; }

    [JsonPropertyName("nextRefreshSeconds")]
    public double NextRefreshSeconds { get; set; }

    [JsonPropertyName("debugMode")]
    public bool DebugMode { get; set; }
}

public class ContractResultRequest : IRequestData
{
    [JsonPropertyName("contractId")]
    public string ContractId { get; set; } = "";

    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    [JsonPropertyName("survived")]
    public bool Survived { get; set; }
}

public class RaidResultRequest : IRequestData
{
    [JsonPropertyName("raidId")]
    public string RaidId { get; set; } = "";

    [JsonPropertyName("scavKills")]
    public int ScavKills { get; set; }

    [JsonPropertyName("pmcKills")]
    public int PmcKills { get; set; }

    [JsonPropertyName("bossKills")]
    public int BossKills { get; set; }

    [JsonPropertyName("cultistKills")]
    public int CultistKills { get; set; }

    [JsonPropertyName("priestKills")]
    public int PriestKills { get; set; }

    [JsonPropertyName("raiderKills")]
    public int RaiderKills { get; set; }

    [JsonPropertyName("rogueKills")]
    public int RogueKills { get; set; }

    [JsonPropertyName("meleeKills")]
    public int MeleeKills { get; set; }

    [JsonPropertyName("headshots")]
    public int Headshots { get; set; }

    [JsonPropertyName("grenadeKills")]
    public int GrenadeKills { get; set; }

    [JsonPropertyName("legKills")]
    public int LegKills { get; set; }

    [JsonPropertyName("armKills")]
    public int ArmKills { get; set; }

    [JsonPropertyName("stomachKills")]
    public int StomachKills { get; set; }

    [JsonPropertyName("modifierKills")]
    public int ModifierKills { get; set; }

    [JsonPropertyName("suppressedKills")]
    public int SuppressedKills { get; set; }

    [JsonPropertyName("opticKills")]
    public int OpticKills { get; set; }

    [JsonPropertyName("ironSightKills")]
    public int IronSightKills { get; set; }

    [JsonPropertyName("killDistances")]
    public List<int> KillDistances { get; set; } = [];

    [JsonPropertyName("weaponClassKills")]
    public Dictionary<string, int> WeaponClassKills { get; set; } = [];

    [JsonPropertyName("bossRoles")]
    public List<string> BossRoles { get; set; } = [];

    [JsonPropertyName("longestKill")]
    public int LongestKill { get; set; }

    [JsonPropertyName("lootValue")]
    public int LootValue { get; set; }

    [JsonPropertyName("survived")]
    public bool Survived { get; set; }

    [JsonPropertyName("survivedSeconds")]
    public float SurvivedSeconds { get; set; }

    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    [JsonPropertyName("isScavRaid")]
    public bool IsScavRaid { get; set; }
}

public class CollectionItemDto
{
    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("donated")]
    public bool Donated { get; set; }

    [JsonPropertyName("questItem")]
    public bool QuestItem { get; set; }
}

public class CollectionMilestoneDto
{
    [JsonPropertyName("required")]
    public int Required { get; set; }

    [JsonPropertyName("gpReward")]
    public int GpReward { get; set; }

    [JsonPropertyName("perk")]
    public string Perk { get; set; } = "";

    [JsonPropertyName("perkValue")]
    public double PerkValue { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("reached")]
    public bool Reached { get; set; }
}

public class CollectionSetDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("gpPerItem")]
    public int GpPerItem { get; set; }

    [JsonPropertyName("donated")]
    public int Donated { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("items")]
    public List<CollectionItemDto> Items { get; set; } = [];

    [JsonPropertyName("milestones")]
    public List<CollectionMilestoneDto> Milestones { get; set; } = [];
}

public class CollectionStateDto
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("requireFoundInRaid")]
    public bool RequireFoundInRaid { get; set; }

    [JsonPropertyName("sets")]
    public List<CollectionSetDto> Sets { get; set; } = [];

    [JsonPropertyName("prestigeGp")]
    public int PrestigeGp { get; set; }

    [JsonPropertyName("prestigePieces")]
    public int PrestigePieces { get; set; }

    [JsonPropertyName("prestigeSets")]
    public int PrestigeSets { get; set; }
}
