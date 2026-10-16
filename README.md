# Weekend Drops Continued

A maintained continuation of **Weekend Drops** by **20fpsguy**, with fixes for weapon rewards, schedules, currency handling, localization and client overhead. This repository remains a fork of the original project.

## Requirements

- SPT 4.1.x; builds and isolated tests in this release used SPT 4.1.6.
- The client requires BigBrain, as does the original Weekend Drops UI. Keep a version compatible with your SPT installation.
- Fika is optional. Multiplayer gameplay still needs validation with your complete mod setup.
- Optional content referenced by your shop/market configuration must be installed separately.

## Install or update

1. Close the game and stop the SPT server.
2. Back up `SPT_Runtime/user/mods/WeekendDrops`, the existing Weekend client folder and its BepInEx configuration.
3. For updates, preserve your `data` directory, customized `config` files and custom bot definitions. The ZIP includes default configuration for clean installs; compare and merge it rather than overwriting your own settings.
4. Extract the release ZIP into the SPT game root, where `BepInEx` and `SPT_Runtime` are located. The package includes a text-layout library and a localization companion required by this distribution.
5. Remove the obsolete separate Weekend improvements/sound plugin if left over from an older installation. Its sound features have been replaced by integrated native sounds.
6. Start the server and game. Open Weekend from its inventory tab; the default in-raid widget key is F7. Use F12 for the mod configuration.

Both core components report **1.0.8** and retain the original loader GUID for compatibility. Collection addons and their GP bonuses are separate downloads, not included here.

## Included changes

- Complete weapon preset deliveries and static weapon images in the Night Market.
- Kit extras on hover, with item icons, localized names and quantities.
- Native game sounds integrated into the main UI DLL.
- Seventeen language files and automatic game-language handling.
- Calendar, concurrent update, currency overflow and saved-state protections.
- Raid loot sampling reduced to twice per second, with a fresh extraction sample.
- Experimental interactive 3D preview removed after a black rendering issue.

See [CHANGELOG.md](CHANGELOG.md) for details and [validation notes](docs/VALIDATION.md) for test scope and limitations. No measured FPS gain is claimed.

## Troubleshooting and removal

Check `BepInEx/LogOutput.log` for client errors and the SPT server logs for backend errors. Restart the client after replacing a DLL. Incorrect or unreadable balance/market files now produce explicit failures; recover them from a valid backup rather than deleting progression.

To uninstall, close both processes and remove the Weekend server/client folders and the bundled localization companion. Save your `data` and configuration first if you intend to reinstall. Existing collection addons may depend on Weekend and should be disabled separately.

## Development

Open `WeekendDrops.code-workspace` in VS Code or `WeekendDrops.slnx` in a compatible IDE. See [BUILDING.md](docs/BUILDING.md). Build scripts never deploy into a live game automatically. Game assemblies and the original mod UI are local build inputs, not committed dependencies.

## Credits and license

Original Weekend Drops: **20fpsguy**. Continued maintenance: **Project Brainiac**. SPT, BepInEx, Harmony, BigBrain and the original project's contributors provide the underlying platforms. Original copyright and MIT license are preserved in [LICENSE](LICENSE).
