using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Services.InRaid;
using WeekendDrops.Services;

namespace WeekendDrops.Patches;

public static class WeekendXpPatch
{
    private static WeekendModifierService? _modifiers;
    private static ProfileHelper? _profiles;
    private static ISptLogger<WeekendDropsLoader>? _logger;
    private static bool _applied;

    public static void Apply(WeekendModifierService modifiers, ProfileHelper profiles,
                             ISptLogger<WeekendDropsLoader> logger)
    {
        _modifiers = modifiers;
        _profiles = profiles;
        _logger = logger;
        if (_applied) return;

        var target = AccessTools.Method(typeof(LocationLifecycleService), "HandlePostRaidPmc");
        if (target is null)
        {
            logger.Error("[WeekendDrops] HandlePostRaidPmc not found - the XP modifier will not pay", null);
            return;
        }

        new Harmony("com.weekenddrops.xp").Patch(
            target,
            prefix:  new HarmonyMethod(typeof(WeekendXpPatch), nameof(Prefix)),
            postfix: new HarmonyMethod(typeof(WeekendXpPatch), nameof(Postfix)));
        _applied = true;
    }

    private static void Prefix(MongoId sessionId, SptProfile fullServerProfile, out long __state)
    {
        __state = NoState;
        try
        {
            if (_modifiers is null || _modifiers.XpMultiplier <= 1.0) return;

            var info = fullServerProfile?.CharacterData?.PmcData?.Info;
            if (info is null)
            {
                _logger?.Warning("[WeekendDrops] XP modifier: no PMC info on the profile at raid end, skipping");
                return;
            }

            __state = info.Experience ?? 0;
        }
        catch { __state = NoState; }
    }

    private static void Postfix(MongoId sessionId, SptProfile fullServerProfile,
                                EndLocalRaidRequestData request, long __state)
    {
        try
        {
            if (__state == NoState || _modifiers is null) return;

            double mult = _modifiers.XpMultiplier;
            if (mult <= 1.0) return;

            long after = fullServerProfile?.CharacterData?.PmcData?.Info?.Experience ?? 0;
            long gained = after - __state;

            // another mod may rewrite Info.Experience itself, so fall back to what the client reported
            if (gained <= 0)
                gained = (long)(request?.Results?.Profile?.Stats?.Eft?.TotalSessionExperience ?? 0);

            if (gained <= 0)
            {
                _logger?.Info($"[WeekendDrops] XP modifier active (x{mult:0.##}) but the raid paid no XP, nothing to top up");
                return;
            }

            int bonus = (int)Math.Round(gained * (mult - 1.0));
            if (bonus <= 0) return;

            _profiles?.AddExperienceToPmc(sessionId, bonus);
            _logger?.Info($"[WeekendDrops] XP modifier paid +{bonus} XP on top of {gained} (x{mult:0.##})");
        }
        catch (Exception ex)
        {
            _logger?.Error($"[WeekendDrops] XP modifier failed: {ex.Message}", null);
        }
    }

    private const long NoState = long.MinValue;
}
