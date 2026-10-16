# WeekendDrops translations

Translation files for the 17 language codes used by the game: `en`, `ge`, `jp`, `ru`, `fr`, `kr`, `ch`, `es`, `pl`, `cz`, `it`, `po`, `hu`, `ro`, `sk`, `tu`, and `es-mx`.

Each locale follows the installed English baseline of 1,121 text entries. The client correction plugin also supports these languages; its smaller locale files are derived from the same complete translations.

These files are included in the 1.0.8 distribution and can be edited in the project workspace.

Validation checks key coverage, duplicate JSON keys, placeholders, rich-text tags, numbers, currencies, symbols, line breaks, and temporary token leakage. Decimal commas are accepted where they represent the same decimal value. Chinese, Japanese, and Korean include system-font preferences required by the original mod.

Contextual editing covers combat objectives, extraction, collection donations, reward multipliers, shop messages, contracts, and prestige. A second comparison pass and a heuristic language audit supplement the editorial changes. This is not native-speaker certification. In-game font rendering and layout remain to be checked before a release.

Structural and gameplay validation scope is documented in `docs/VALIDATION.md` at the repository root. Editorial audit intermediates remain local and are not required by the game.
