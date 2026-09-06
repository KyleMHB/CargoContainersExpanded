# Cargo Containers Expanded

Cargo Containers Expanded is a maintained RimWorld 1.6 fork of AOBA's Cargo Container mod. It stores large resource payloads in buildable containers and tanks, then lets colonists extract the contents through bills.

This fork replaces the original mod. Do not enable it alongside `Aoba.CargoContainer`.

## Features

- Full and half containers for metallic, woody, stony, fabric, and leathery resources.
- Fixed-payload storage for survival meals, medicine, drugs, beer, neutroamine, wort, and chemfuel.
- Extraction bills for batches of 1, 25, or 100, including exact final partial batches.
- Clean deconstruction refunds for the remaining payload and frame materials.
- Full and half refrigerated containers for rottable resources.
- Powered refrigeration pauses payload rot.
- Unpowered refrigerated payloads take twice as long to rot as the equivalent loose item.
- Stored payload contributes 10% of its loose-item market value to colony wealth and falls as items are extracted.

## Requirements

- RimWorld 1.6
- Harmony

Keep Harmony loaded before Cargo Containers Expanded. The mod metadata declares the original Cargo Container package incompatible.

## How to use

Build a resource container from the material you want to store, or choose a fixed-payload container or tank. Select the completed container and add an extraction bill for 1, 25, or 100 items. When fewer items remain than the chosen batch, the final extraction returns the exact remainder.

Refrigerated containers use a rottable resource as both their construction material and payload. Power pauses payload rot. Without power, the payload continues aging at twice the loose item's normal rot duration.

## Save compatibility

Existing RimWorld 1.6 saves retain payload count, rot progress, bills, pending final extraction state, and serialized field keys. Package IDs, container Def names, generated recipe Def names, payload counts, extraction batches, and work amounts remain unchanged. See [`TESTING.md`](TESTING.md) for the historical evidence and current save/load smoke checklist.

## Localization

English is complete. Simplified and Traditional Chinese retain inherited translations for legacy container definitions. New extraction and refrigeration text falls back to English, so Chinese support is partial.

## Building from source

The build uses pinned development-only reference packages and does not require a RimWorld or Steam Workshop installation. Install the .NET SDK selected by `global.json`, then run:

```powershell
dotnet restore --locked-mode
dotnet test ./CargoContainersExpanded.sln -c Release --no-restore
pwsh -File ./Scripts/Validate.ps1
dotnet build ./Source/cargo-containers-expanded.csproj -c Release --no-restore
pwsh -File ./Scripts/Validate.ps1
```

The Release output is `Assemblies/CargoContainersExpanded.dll`. It does not package RimWorld, Unity, Harmony, .NET Framework DLLs, or PDB files.

See [`TESTING.md`](TESTING.md) for the automated checks and in-game acceptance suite.

## Contributing

> Contributions, issues, and feature requests are welcome.

## Credits

AOBA created the original Cargo Container mod. KyleMHB maintains this fork. Inherited definitions, text, textures, masks, and other artwork remain credited to the original creator.

## Links

Support me on Ko-fi. This does not imply endorsement by the original authors.

[![Support me on Ko-fi](https://img.shields.io/badge/Support_me_on_Ko--fi-72a4f2?style=for-the-badge&logo=kofi&logoColor=white)](https://ko-fi.com/I7L525WMJ6)
[![GitHub Repository](https://img.shields.io/badge/GitHub-Repository-181717?style=for-the-badge&logo=github&logoColor=white)](https://github.com/KyleMHB/CargoContainersExpanded)

- [Issue tracker](https://github.com/KyleMHB/CargoContainersExpanded/issues)
- [Original Cargo Container Workshop item](https://steamcommunity.com/sharedfiles/filedetails/?id=2725808118)

## License

Fork-authored code and contributions are released under the scoped [MIT License](LICENSE). Inherited upstream material retains its original terms, which are currently unknown, and is not relicensed by the MIT notice. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for provenance details.
