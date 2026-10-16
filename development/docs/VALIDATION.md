# Validation scope for 1.0.8

Local builds use the installed SPT 4.1.6/.NET 10 server and game/BepInEx assemblies. Server and helper Release builds compile without warnings.

- Maintenance replay: 1,714 assertions for calendars, currency arithmetic, concurrent purchases/claims, invalid input, corrupt state and atomic writes. Reward mail is intercepted in an isolated process.
- Weapon reward replay: 25 configured presets, independent copies, parent/ID validation and invalid trees.
- Localization: 17 files with 1,121 keys each; placeholder/markup checks and 1,479 runtime formatting checks in the prior full locale audit. Layout length warnings still require visual review. Language bridge/schedule regressions are included in tests-localization.
- Client integration: 28 structural checks for static icons, kit tooltips, removal of the 3D window, the raid sampling gate and the unthrottled extraction read. Integration additionally checks stack/branch consistency and unchanged unrelated methods.
- Native audio: 28 controlled-stub checks. They do not measure audibility in the running game.
- Loot cadence: 13 checks, including first sample, exact boundaries, clock rollback, raid changes and invalid clocks. Simulated 60-second runs at 30/60/144 FPS each perform 120 reads instead of 1,800/3,600/8,640 Update calls. These are call counts, not FPS benchmarks.

Previous local maintenance builds reached server Ready and were exercised by the user. The removed 3D feature produced a black window in actual gameplay. The final 1.0.8 package requires its own startup/gameplay check, especially actual mail retrieval, loot progress on extraction, Fika/headless, AI equipment and translated layout/fonts.

Known limits: Fika contract discovery is not authoritative to current match membership; some progress operations rely on client reports. Atomic replacement is per file and cannot guarantee exactly-once delivery if a process crashes between balance, mail and progression updates. The original UI source is not fully available. No measured FPS gain is claimed.
