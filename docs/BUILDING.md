# Build and workspace

Use .NET SDK 10, Python 3 and a local SPT 4.1.6 installation with compatible game/BepInEx assemblies. Restore requires access to NuGet (including Mono.Cecil 0.11.5). No game or SPT binaries are downloaded by these scripts.

Set `WEEKEND_GAME_ROOT` to your SPT game root and ensure `dotnet` is on PATH (or set `DOTNET` for Python scripts). You may alternatively create an ignored `Directory.Build.local.props` setting `SptRoot` and `GameRoot`. Never commit machine-specific paths.

```sh
export WEEKEND_GAME_ROOT=/path/to/SPT
python3 scripts/build-release.py --original-archive /path/to/WeekendDrops-1.0.7.zip
```

The first client build extracts the original mod UI from the author's 1.0.7 archive, applies the included text-layout patch, and saves a private compatible base in `client-audio/.local`. Subsequent builds reuse it. The original complete UI source is absent from the upstream snapshot; the integration tools deliberately validate expected method shapes and reject unfamiliar bases. Do not select an already integrated client as the base.

The release script compiles the backend, main UI integration, text-layout library and localization companion, then stages an allowlisted ZIP under `artifacts`. It includes clean configuration and the original distributed bot template. It never copies installed profiles, progression or live configuration. The runtime UI contains no dependency on the intermediate audio build module.

Regression runners are console programs, executed with `dotnet run --project <path>/Replay.csproj`: `tests`, `tests-maintenance`, `tests-localization`, `tests-audio`, `client-preview/tests` and `client-preview/sampling-tests`. They require local assemblies where applicable. Server-state tests refuse live data paths and intercept reward mail.

Open `WeekendDrops.code-workspace` for VS Code tasks. Build commands do not install or restart anything. Use a backup and close both game and server before manually testing a release.
