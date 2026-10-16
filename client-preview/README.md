# Complete weapon previews

The original item icon coroutine called `ItemFactory.CreateItem(id, templateId, null)`. For a weapon this creates a bare receiver, so the Night Market card showed only that part even when the server prepared a complete delivery tree.

`ApplyPresetPreview.cs` changes that UI call to the installed game's `ItemFactory.GetPresetItem(templateId)`. The native method clones a configured preset and falls back to normal item creation for templates without a preset. Native asynchronous sprite generation remains unchanged. No inventory, profile, price, purchase state or delivery data is modified.

This change is built into **WeekendDropsUI.dll**, together with the integrated native sounds. It is not a runtime plugin. Build through `python3 client-audio/build-local.py`; the original client source is still absent, so the compatible base UI is preserved in the ignored local build directory. The integrator checks the exact original call structure and refuses unfamiliar layouts.

The image represents the default complete preset; optional randomized accessories added by the server can differ. Templates without a native preset retain the game's fallback behavior. Restart the client to clear the old in-memory icon cache.

The replay in `client-preview/tests` verifies the original bare-item call, the new native preset call, stack flow, cloning/fallback support, native sprite generation and rejection of an unexpected base structure. Rendering and actual purchases still require an in-game check.

## 3D preview removed

The interactive 3D window and click handlers were removed after an in-game report of a black preview. Static complete-preset icons, native audio and kit hover details remain. No model camera or render texture is allocated by the integrated helper.

## Night Market kit contents on hover

Hover a revealed card with extras to show each additional item's icon, localized game name and exact quantity from the server DTO. Mystery contents remain hidden until bought. The tooltip renders after cards, clamps to the screen and splits long lists into columns. It does not intercept clicks or change purchases. KitTooltip.cs is compiled into the same UI DLL. Gameplay layout validation remains pending.

## Raid loot sampling

The integration in Plugin.Update reads LootNet inventory-backed DisplayValue at most once every 0.5 seconds instead of every frame. A new GameWorld identity or clock rollback resets the cadence. ReportRaidResult takes an unconditional fresh sample before starting its result coroutine, outside the throttle. The original LootNet reader, positive-value handling and other raid progression events remain unchanged. No change is made to LootNet itself or its own polling. Source: LootSampling.cs and LootSamplingRuntime.cs, compiled into the same UI DLL. Cadence tests are simulations and do not measure game FPS.
