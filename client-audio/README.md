# Weekend Drops native UI audio

`NativeAudio.cs` is integrated into **WeekendDropsUI.dll**. It is not a BepInEx plugin and requires no separate audio DLL or downloaded sound assets.

| Action | Tarkov sound |
| --- | --- |
| Open the Weekend panel or Night Market | `MenuInspectorWindowOpen` |
| Close the panel or a modal | `MenuInspectorWindowClose` |
| Switch Weekend tabs | `ButtonBottomBarClick` |
| Buy from the GP shop, barter, or buy a Night Market card | `TradeOperationComplete` |
| Successful daily reward, daily bonus, tier reward, collection donation | `QuestFinished` |
| Reveal a Night Market card | `MenuDropdownSelect` |
| Rejected claim/card action | `ErrorMessage` |

Audio uses `Comfort.Common.Singleton<GUISounds>` and the game's own UI sound settings. Invalid legacy aliases are resolved to enum values present in the installed game. Missing native audio during scene transitions does not break the UI.

The original client UI source is absent from this fork. The local build therefore compiles these editable audio routines against the installed game, integrates their type into a saved base UI assembly with Mono.Cecil, and updates the relevant UI/coroutine call sites. Existing gameplay, localization and layout methods are preserved. No intermediate audio module is deployed.

## Build locally

From the repository workspace, the first build saves the current base UI under ignored `.local/`:

```sh
python3 client-audio/build-local.py --game-root /path/to/SPT --original-archive /path/to/WeekendDrops-1.0.7.zip
```

Subsequent builds:

```sh
python3 client-audio/build-local.py
```

Output: `client-audio/bin/Release/WeekendDropsUI.dll`. The build performs method-shape checks, branch/stack verification and rejects a dependency on the intermediate audio assembly. `obj/integration-report.json` records the base/output hashes and changed methods. It never installs automatically. Selecting a newer base requires explicitly reviewing/replacing the ignored base first; do not use an already integrated DLL as a new base.

Mono.Cecil is restored from NuGet. Game paths come from `WEEKEND_GAME_ROOT` or `--game-root`; the original 1.0.7 mod archive supplies the first private UI base. See `docs/BUILDING.md` for the full release workflow.
