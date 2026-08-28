# Cargo Containers Expanded

Cargo Containers Expanded is a maintained RimWorld 1.6 fork of AOBA's Cargo Container mod. It replaces the original mod and must not be enabled alongside `Aoba.CargoContainer`.

The mod turns large amounts of resources into buildable containers and tanks, then lets colonists extract the remaining payload through bills.

## Requirements

- RimWorld 1.6
- Harmony

Keep Harmony loaded before Cargo Containers Expanded. Do not enable the original Cargo Container mod at the same time; the repository metadata declares it incompatible.

## Features and balance rules

- Stuffable full and half resource containers for metallic, woody, stony, fabric, and leathery resources.
- Fixed-payload containers for survival meals, medicine, drugs, beer, neutroamine, wort, and chemfuel.
- Extraction bills for batches of 1, 25, or 100, including exact final partial batches.
- Clean deconstruction refunds for remaining payload and frame materials.
- Refrigerated full and half containers for rottable resources.
- Powered refrigeration pauses payload rot.
- Unpowered refrigerated payloads take twice as long to rot as the equivalent loose item. This 2x duration is intentional.
- Stored payload contributes 10% of its loose-item market value to colony wealth. This is intentional and scales with the remaining count.

## Save compatibility

Existing RimWorld 1.6 saves retain their payload count, rot progress, bills, pending final extraction state, and serialized field keys. Package IDs, container Def names, generated recipe Def names, payload counts, extraction batches, and work amounts are unchanged.

## Localization

English is complete. Simplified and Traditional Chinese retain the inherited translations for legacy container definitions. New extraction and refrigeration UI currently falls back to English; Chinese support is therefore partial.

## Building and validation

The build uses pinned development-only reference packages and does not require a RimWorld or Workshop installation. Install the .NET SDK selected by `global.json`, then run:

```powershell
dotnet restore --locked-mode
pwsh -File ./Scripts/Validate.ps1
dotnet build ./Source/cargo-containers-expanded.csproj -c Release --no-restore
pwsh -File ./Scripts/Validate.ps1
```

The Release output is `Assemblies/CargoContainersExpanded.dll`. RimWorld, Unity, Harmony, framework DLLs, and PDBs are not packaged.

See [TESTING.md](TESTING.md) for the automated and in-game acceptance suites.

## Contributing

Contributions, issues, and feature requests are welcome. Fork-authored code and contributions are available under the scoped MIT notice in [LICENSE](LICENSE). Inherited upstream definitions and artwork have separate, unknown provenance described in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Links and credits


Support KyleMHB's ongoing maintenance of this fork. This does not imply endorsement by the original authors.

[![Support me on Ko-fi](https://img.shields.io/badge/Support_me_on_Ko--fi-72a4f2?style=for-the-badge&logo=kofi&logoColor=white)](https://ko-fi.com/I7L525WMJ6)
[![GitHub Repository](https://img.shields.io/badge/GitHub-Repository-181717?style=for-the-badge&logo=github&logoColor=white)](https://github.com/KyleMHB/CargoContainersExpanded)
- Source repository: <https://github.com/KyleMHB/CargoContainersExpanded>
- Issue tracker: <https://github.com/KyleMHB/CargoContainersExpanded/issues>
- Original Workshop item: <https://steamcommunity.com/sharedfiles/filedetails/?id=2725808118>
- Original mod by AOBA; maintained fork by KyleMHB.
