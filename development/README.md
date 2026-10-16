# Development

This folder contains client integration sources, localization sources, build tools, regression runners and technical validation notes. These are for maintainers; players should download the ready-to-install archive from [Releases](https://github.com/maestroBR/WeekendDrops/releases/latest).

- `WeekendDrops.slnx`: IDE solution for the server and client tooling.
- `client-audio`, `client-issues`, `client-layout`, `client-preview`: integrated client features and supporting libraries.
- `localization`: full translations and editorial tools.
- `tests*` and nested client tests: isolated regressions.
- `scripts/build-release.py`: clean distribution packaging.
- `docs/BUILDING.md` and `docs/VALIDATION.md`: build instructions and validation scope.

The original server source remains in the repository's existing Controllers, Models, Patches, Services and Web folders. No game assemblies or private UI base are committed. Local editor settings and generated output are ignored.
