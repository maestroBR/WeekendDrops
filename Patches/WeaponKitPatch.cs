using System.Reflection;
using HarmonyLib;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using WeekendDrops;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Enums;

namespace WeekendDrops.Patches;

public static class WeaponKitPatch
{
    private static ItemHelper? _itemHelper;
    private static bool _applied;
    private static readonly Random Rng = new();

    internal static void EnsureHelper(ItemHelper itemHelper) => _itemHelper ??= itemHelper;

    private static readonly (string Prefix, double Chance)[] TargetSlots =
    {
        ("mod_scope",    0.55),
        ("mod_muzzle",   0.45),
        ("mod_foregrip", 0.50),
        ("mod_tactical", 0.40),
    };

    public static void Apply(ItemHelper itemHelper, ISptLogger<WeekendDropsLoader> logger)
    {
        _itemHelper = itemHelper;
        if (_applied) return;

        var target = AccessTools.Method(
            typeof(LootGenerator),
            nameof(LootGenerator.GetRandomLootContainerLoot));
        if (target is null)
        {
            logger.Error("[WeekendDrops] Could not find LootGenerator.GetRandomLootContainerLoot - weapon kitting disabled");
            return;
        }

        var postfix = typeof(WeaponKitPatch)
            .GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic);

        new Harmony("com.20fpsguy.WeekendDrops.WeaponKit")
            .Patch(target, postfix: new HarmonyMethod(postfix));

        _applied = true;
    }

    private static void Postfix(RewardDetails __0, List<List<Item>> __result)
    {
        if (_itemHelper is null || __result is null) return;

        if (!WdCrateRegistry.IsOurs(__0)) return;

        var modTier = WdCrateRegistry.RecipeFor(__0)?.ModTier ?? "";

        foreach (var group in __result)
        {
            if (group.Count == 0) continue;
            var root = group[0];

            if (!_itemHelper.IsOfBaseclass(root.Template, BaseClasses.WEAPON)) continue;

            KitWeapon(group, modTier);
        }
    }

    /// Also called by the night market so a card can hand over a built gun.
    internal static void KitWeapon(List<Item> group, string modTier)
    {
        var hosts = group.ToList();

        foreach (var (prefix, chance) in TargetSlots)
        {
            if (Rng.NextDouble() > chance) continue;
            TryFillSlotType(group, hosts, prefix, modTier);
        }
    }

    private static void TryFillSlotType(List<Item> group, List<Item> hosts, string prefix, string modTier)
    {
        foreach (var host in hosts)
        {
            var hostDb = _itemHelper!.GetItem(host.Template).Value;
            var slots = hostDb?.Properties?.Slots;
            if (slots is null) continue;

            foreach (var slot in slots)
            {
                var slotName = slot.Name;
                if (string.IsNullOrEmpty(slotName) || !slotName.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                var hostId = host.Id.ToString();
                if (group.Any(i => i.ParentId == hostId && i.SlotId == slotName))
                    continue;

                var modTpl = PickModForSlot(slot, prefix, modTier);
                if (modTpl is null) continue;

                group.Add(new Item
                {
                    Id = new MongoId(),
                    Template = modTpl.Value,
                    ParentId = host.Id,
                    SlotId = slotName,
                });
                return;
            }
        }
    }

    /// Slice of the price-sorted candidate list a tier is allowed to fit, so an Epic
    /// rifle stops turning up wearing the cheapest grip in the game.
    private static (double Lo, double Hi) Band(string modTier) => modTier switch
    {
        "common" => (0.00, 0.45),
        "rare"   => (0.35, 0.85),
        "epic"   => (0.55, 1.00),
        _        => (0.00, 1.00),
    };

    private static MongoId? PickModForSlot(Slot slot, string prefix, string modTier)
    {
        var filter = slot.Properties?.Filters?.FirstOrDefault()?.Filter;
        if (filter is null || filter.Count == 0) return null;

        var candidates = new List<(MongoId Tpl, double Price)>();
        var suppressors = new List<(MongoId Tpl, double Price)>();

        foreach (var tpl in filter)
        {
            var db = _itemHelper!.GetItem(tpl).Value;
            if (db?.Properties is null) continue;

            var needsChildren = db.Properties.Slots?.Any(s => s.Required == true) ?? false;
            if (needsChildren) continue;

            var entry = (tpl, db.Properties.CreditsPrice ?? 0);
            candidates.Add(entry);
            if (db.Parent == BaseClasses.SILENCER) suppressors.Add(entry);
        }

        if (candidates.Count == 0) return null;

        var pickFrom = prefix == "mod_muzzle" && suppressors.Count > 0 ? suppressors : candidates;
        return PickInBand(pickFrom, modTier);
    }

    private static MongoId PickInBand(List<(MongoId Tpl, double Price)> candidates, string modTier)
    {
        if (candidates.Count == 1) return candidates[0].Tpl;

        var (lo, hi) = Band(modTier);
        var sorted = candidates.OrderBy(c => c.Price).ToList();

        var from = (int)Math.Floor(lo * sorted.Count);
        var to   = (int)Math.Ceiling(hi * sorted.Count);
        from = Math.Clamp(from, 0, sorted.Count - 1);
        to   = Math.Clamp(to, from + 1, sorted.Count);

        return sorted[Rng.Next(from, to)].Tpl;
    }
}
