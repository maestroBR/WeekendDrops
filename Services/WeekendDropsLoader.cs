using System.Reflection;
using SysPath = System.IO.Path;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Common.Models.Logging;
using Color = Spectre.Console.Color;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using WeekendDrops.Patches;
using WeekendDrops.Services;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Match;

namespace WeekendDrops;

[Injectable(TypePriority = OnLoadOrder.GameCallbacks + 1000)]
public class WeekendDropsLoader(
    WeekendChallengeService weekendChallengeService,
    DailyChallengeService dailyChallengeService,
    ContractService contractService,
    CollectionService collectionService,
    TarcoinLoot tarcoinLoot,
    NightMarketService nightMarket,
    WeekendModifierService modifierService,
    ProfileHelper profileHelper,
    BotTable botTable,
    JsonUtil jsonUtil,
    ItemHelper itemHelper,
    ISptLogger<WeekendDropsLoader> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        VerifyInstall();

        LoadContractBotTypes();

        weekendChallengeService.LoadConfig();
        dailyChallengeService.LoadConfig();
        contractService.LoadConfig();
        collectionService.LoadConfig(WdPaths.ConfigDir);
        nightMarket.LoadConfig();

        weekendChallengeService.RegisterLootContainerPools();

        weekendChallengeService.RegisterArenaShopPools();

        tarcoinLoot.Inject();

        LootContainerAmmoStackPatch.Apply(itemHelper, logger);

        CrateCompositionPatch.Apply(itemHelper, logger);

        ContractSpawnPatch.Apply(contractService, logger);
        WeekendXpPatch.Apply(modifierService, profileHelper, logger);
        ContractBotLootPatch.Apply(contractService, logger);

        if (weekendChallengeService.Config.KitWeaponDrops)
            WeaponKitPatch.Apply(itemHelper, logger);

        PrintBanner();
        return Task.CompletedTask;
    }

    private static readonly string[] RequiredConfigs =
    [
        "config.json", "challenges.json", "daily_challenges.json", "contracts.json",
        "drops.json", "crate_pools.json", "shop.json",
    ];

    private void VerifyInstall()
    {
        logger.Debug($"[WeekendDrops] Mod dir: {WdPaths.ModDir}");

        if (!Directory.Exists(WdPaths.ConfigDir))
        {
            logger.Error(
                $"[WeekendDrops] Config dir NOT FOUND at {WdPaths.ConfigDir}. The mod will run on " +
                "built-in defaults: no challenges, no contracts, no shop. Check that the 'config' " +
                "folder shipped alongside the dll and that its name is lowercase 'config'.");
            return;
        }

        var missing = RequiredConfigs.Where(f => !File.Exists(WdPaths.Config(f))).ToList();
        if (missing.Count > 0)
            logger.Error(
                $"[WeekendDrops] Missing config file(s) in {WdPaths.ConfigDir}: {string.Join(", ", missing)}. " +
                "On a case-sensitive filesystem the names must match exactly.");

        try { Directory.CreateDirectory(WdPaths.DataDir); }
        catch (Exception ex)
        {
            logger.Error($"[WeekendDrops] Cannot create data dir {WdPaths.DataDir}: {ex.Message}. Progress will not save.");
        }
    }

    private void LoadContractBotTypes()
    {
        var typesDir = System.IO.Path.Combine(WdPaths.ModDir, "db", "bots", "types");
        if (!Directory.Exists(typesDir))
        {
            logger.Warning($"[WeekendDrops] No bot types dir at {typesDir} - contract crew will use vanilla gear");
            return;
        }

        var types = botTable.Types;
        foreach (var file in Directory.GetFiles(typesDir, "*.json"))
        {
            var role = System.IO.Path.GetFileNameWithoutExtension(file);
            var botType = jsonUtil.DeserializeFromFile<BotType>(file);
            if (botType is null)
            {
                logger.Error($"[WeekendDrops] Failed to read bot type {file}");
                continue;
            }

            var key = types.Keys.FirstOrDefault(k => string.Equals(k, role, StringComparison.OrdinalIgnoreCase)) ?? role;
            types[key] = botType;
            logger.Debug($"[WeekendDrops] Contract bot type '{key}' overridden from {System.IO.Path.GetFileName(file)}");
        }
    }

    private const int BannerWidth = 46;

    private void PrintBanner()
    {
        var border = new string('═', BannerWidth);
        logger.LogWithColor("  ╔" + border + "╗", Color.Cyan);
        logger.LogWithColor(Centre("W E E K E N D   D R O P S"), Color.Cyan);
        logger.LogWithColor(Centre("loot crates and daily challenges"), Color.Cyan);
        logger.LogWithColor("  ╚" + border + "╝", Color.Cyan);

        bool debug  = weekendChallengeService.Config.DebugMode;
        bool active = weekendChallengeService.IsWeekendActive();

        Field("weekend",
              debug  ? "OPEN (forced by debug mode)"
            : active ? "OPEN, go get loot"
                     : $"closed, opens {weekendChallengeService.GetWeekendScheduleText()}",
              debug ? Color.Yellow : active ? Color.Green : Color.Grey);

        var mod = modifierService.Active;
        Field("modifier", mod is null ? "none this weekend" : ModifierText(mod),
              mod is null ? Color.Grey : Color.Yellow);

        Field("challenges", $"{weekendChallengeService.Config.ChallengesPerWeekend} per weekend, " +
                            $"{weekendChallengeService.Config.WeekendDifficultyBudget} difficulty points", Color.Grey);

        Field("content", $"{contractService.ContractCount} bounties · " +
                         $"{collectionService.SetCount} collection sets ({collectionService.DonatableCount} items) · " +
                         $"{weekendChallengeService.ArenaCrateCount} arena crates", Color.Grey);

        int total = weekendChallengeService.OptionalItemsTotal;
        if (total > 0)
        {
            string backport = ModPresence.ContentBackportInstalled ? "WTT-ContentBackport installed" : "vanilla only";
            Field("crate pool", $"{weekendChallengeService.OptionalItemsAvailable} of {total} optional items available ({backport})", Color.Grey);
        }

        var held = collectionService.WithheldItems;
        if (held.Count > 0)
        {
            Field("withheld", $"{held.Count} collectible(s) kept out of the collection:", Color.Yellow);
            foreach (var item in held)
                Field("", $"  {item}", Color.Yellow);
            Field("", "  (needed by the Collector quest or a hideout upgrade, which are one-shot)", Color.Grey);
        }

        if (debug)
            Field("debug", "ON: every bounty offered, unlimited picks, in-game debug controls", Color.Yellow);

        logger.Success("[WeekendDrops] Ready");
    }

    private static string ModifierText(Models.WeekendModifier m)
    {
        var name = string.IsNullOrWhiteSpace(m.Name) ? m.Id : m.Name;
        var desc = m.Description?.TrimEnd('.');
        return string.IsNullOrWhiteSpace(desc) ? name : $"{name} - {desc}";
    }

    private static string Centre(string text)
    {
        var pad = Math.Max(0, BannerWidth - text.Length);
        var left = pad / 2;
        return "  ║" + new string(' ', left) + text + new string(' ', pad - left) + "║";
    }

    private void Field(string label, string value, Color color) =>
        logger.LogWithColor($"   {label.PadRight(11)} {value}", color);
}
