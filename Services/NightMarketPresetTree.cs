using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
namespace WeekendDrops.Services;

internal static class NightMarketPresetTree
{
    // PresetHelper returns a deep clone, so remapping does not change shared presets.
    internal static List<Item> Prepare(List<Item> items, MongoId template)
    {
        if (items.Count == 0 || items.Select(x => x.Id).Distinct().Count() != items.Count)
            throw new InvalidOperationException("Empty preset or duplicate item IDs");
        var ids = items.Select(x => x.Id.ToString()).ToHashSet();
        var roots = items.Where(x => x.ParentId is null || !ids.Contains(x.ParentId)).ToList();
        if (roots.Count != 1 || roots[0].Template != template)
            throw new InvalidOperationException("Preset root does not match the weapon");
        var root = roots[0];
        var reachable = new HashSet<string> { root.Id.ToString() };
        bool changed;
        do
        {
            changed = false;
            foreach (var item in items)
                if (item.ParentId is string parent && reachable.Contains(parent))
                    changed |= reachable.Add(item.Id.ToString());
        } while (changed);
        if (reachable.Count != items.Count)
            throw new InvalidOperationException("Disconnected or cyclic preset");
        var remap = items.ToDictionary(x => x.Id.ToString(), _ => new MongoId());
        foreach (var item in items)
        {
            var old = item.Id.ToString();
            if (item != root) item.ParentId = remap[item.ParentId!];
            item.Id = remap[old];
        }
        root.ParentId = null;
        root.SlotId = "main";
        root.Upd ??= new();
        root.Upd.StackObjectsCount = 1;
        return new[] { root }.Concat(items.Where(x => !ReferenceEquals(x, root))).ToList();
    }
}
