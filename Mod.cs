using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Game;
using SPTarkov.Server.Web;

namespace WeekendDrops;

public record ModMetadata : IModMetadata, IModBlazorMetadata
{
    public string ModGuid { get; init; } = "com.20fpsguy.WeekendDrops";
    public string Name { get; init; } = "Weekend Drops Continued";
    public string Author { get; init; } = "20fpsguy";
    public List<string>? Contributors { get; init; } = ["Project Brainiac"];
    public SemanticVersioning.Version Version { get; init; } =
        new(typeof(ModMetadata).Assembly.GetName().Version!.ToString(3));
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/maestroBR/WeekendDrops";
    public bool HasPrepatcher { get; init; }
    public string License { get; init; } = "MIT";

    public string? HomePage { get; init; } = "/weekenddrops";
    public string? HomePageDescription { get; init; } = "Weekend schedule, challenges and rewards";
    public string? WWWRootUrl { get; init; } = "";
}
