# Changelog

## 1.0.8 — 2026-10-16

### Night Market and shop
- Build complete weapon reward trees with required parts, independent item IDs and validated parent links.
- Prepare deliveries before deducting TarCoins or GP; reject invalid presets and malformed card indices.
- Use native complete presets for static weapon images. Random optional delivery attachments can differ from the image.
- Show kit extras on hover with icons, localized names and quantities; preserve mystery-card concealment.
- Correct medical, ammunition, provisions and grenade package text and the Junkbox capacity (196 slots).
- Remove the experimental interactive 3D preview after black-window rendering reports.

### Performance and native sounds
- Sample LootNet's inventory-backed value at most twice per second during raid instead of every frame.
- Take a fresh, unthrottled loot sample before constructing the extraction report.
- Reset sample timing for a new raid or clock rollback; avoid catch-up bursts.
- Integrate native game sounds for navigation, opening/closing, purchases, reveals, donations and claims. Confirmed results control the handled success cues.
- Replace the obsolete separate sound/improvements companion.

### Languages
- Include 1,121 strings per locale across 17 language codes: en, po, ru, ge, fr, ch, jp, kr, es, es-mx, pl, cz, it, hu, ro, sk and tu.
- Preserve placeholders/markup and localize schedule weekdays and times.
- Follow the selected game language in Auto mode and invalidate language caches when it changes. These fixes include a bundled localization companion.

### Calendar, rewards and persistence
- Fix custom weekday ranges, same-day and midnight/week-boundary windows, and market windows longer than 24 hours.
- Treat equal weekly opening/closing boundaries as a full week; anchor period identifiers to the opening time.
- Serialize read/modify/write operations within market, stock, contract, collection and daily/weekly services.
- Reject negative costs, protect arithmetic from overflow and preserve sender balances when a transfer cannot be credited.
- Fail explicitly on unreadable balance/market state instead of silently resetting or rerolling.
- Write state and web configuration through flushed temporary files and atomic replacement.

### Development and limits
- Organize the solution, VS Code tasks, portable build inputs and clean release packaging; disable automatic deployment.
- Isolated regression checks cover maintenance, weapon trees, language formatting, sounds and loot sample timing. See docs/VALIDATION.md.
- This release does not claim measured FPS gains, full multiplayer certification or native-speaker approval of every translation. Atomic writes do not provide one transaction across mail, balances and progression.

## Earlier versions

The original 1.0.7 and earlier releases were developed by 20fpsguy. Version 1.0.8 consolidates the continued maintenance changes in this fork.
