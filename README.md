# Cargo Containers Expanded

A maintained RimWorld 1.6 fork of AOBA's Cargo container.

Cargo Containers Expanded adds buildable cargo containers and tanks that compact large amounts of resources into buildings, then let you extract the stored payload later through bills. This fork keeps the original mod's core idea while adding cleaner payload handling, deconstruction refunds, refrigerated containers, payload-aware wealth values, cargo rot handling, and RimWorld 1.6 support.

## Features

- Stuffable resource containers for metallic, woody, stony, fabric, and leathery resources.
- Half-size resource containers for smaller stockpiles.
- Fixed-payload containers for survival meals, medicine, drugs, beer, neutroamine, wort, and chemfuel.
- Extraction bills that return container payloads in batches of 1, 25, or 100.
- Clean deconstruction refunds for remaining payloads and frame materials.
- Refrigerated containers for rottable resources.
- Refrigerated containers keep powered payloads preserved and rot when unpowered according to temperature.
- Extracted rottable products inherit the container's current rot progress.
- Container market value scales with remaining payload at 10% of the payload's base market value.
- Powered refrigeration inspect text, rot state, rot rate, and payload count display.
- Localized inspect and recipe text.

## Containers

The mod includes containers for:

- Stuffable resources
- Half-size stuffable resources
- Survival meals
- Herbal medicine
- Medicine
- Glitterworld medicine
- Yayo
- Flake
- Beer
- Neutroamine
- Wort
- Chemfuel
- Refrigerated rottable resources

## Important Notes

Cargo containers are buildings, not normal stockpiles. Their contents are represented as an internal payload. Use the container's bills to extract the payload back into item stacks.

Refrigerated containers require power. If power is lost, the payload can rot based on the contained item and the surrounding temperature. When you extract rottable payloads, the produced item stacks keep the container's current rot progress.

Container market value is based on the remaining payload. As payload is extracted, the container's market value drops.

Chemfuel containers are dangerous if destroyed.

## Requirements

- RimWorld 1.6
- Harmony

## Building

This repository contains the C# source project under `Source/`.

```powershell
dotnet build .\Source\cargo-containers-expanded.csproj -c Release
```

The mod expects the compiled assembly to be available in the mod's `Assemblies/` folder for RimWorld to load it.

## Credits

Original mod by AOBA:

- [Cargo container on Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2725808118)

Current maintained fork by kylohb:

- [Cargo Containers Expanded](https://github.com/KyleMHB/CargoContainersExpanded)

## Issues

Please report bugs, compatibility problems, or balance issues through GitHub issues:

- [Issue tracker](https://github.com/KyleMHB/CargoContainersExpanded/issues)
