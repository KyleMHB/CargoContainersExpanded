# Cargo Containers Expanded

Cargo Containers Expanded is a maintained RimWorld 1.6 fork of AOBA's cargo container mod.

It adds buildable cargo containers and tanks that compact large amounts of resources into buildings, then let you extract the stored payload later through bills.

## Features

- **Stuffable resource containers** for metallic, woody, stony, fabric, and leathery resources.
- **Half-size resource containers** for smaller stockpiles.
- **Fixed-payload containers** for survival meals, medicine, drugs, beer, neutroamine, wort, and chemfuel.
- **Extraction bills** that return container payloads in batches of 1, 25, or 100.
- **Clean deconstruction refunds** for remaining payloads and frame materials.
- **Refrigerated containers** for rottable resources.
- **Rot-aware payload handling** so refrigerated contents keep their state when extracted.
- **Payload-based market value** so container wealth reflects remaining stored resources.
- **Localized inspect and recipe text**.

## Installation

### Manual Installation

1. Download or clone the repository.
2. Place the mod folder in your RimWorld `Mods` directory.
3. Enable **Harmony** and **Cargo Containers Expanded** in RimWorld's mod list.

### Steam Workshop

If you are using a Workshop build, subscribe to the published mod and keep the RimWorld load order the same as the manual installation instructions.

## Usage

Cargo containers are buildings, not stockpiles. Their contents are represented as an internal payload.

Use the container bills to extract payload back into item stacks. Refrigerated containers require power to stay actively refrigerated, and unpowered refrigerated containers can rot based on temperature and item type.

## Configuration

The mod uses RimWorld's normal mod and building configuration systems.

Important notes:

- refrigerated containers require power to stay active
- chemfuel containers are dangerous if destroyed
- container value changes as payload is extracted

## Building from Source

Prerequisites:

- .NET SDK compatible with the project
- RimWorld 1.6 reference access through the project package references

Build the project with:

```powershell
dotnet build .\Source\cargo-containers-expanded.csproj -c Release
```

## Testing and Validation

The primary validation command is the Release build:

```powershell
dotnet build .\Source\cargo-containers-expanded.csproj -c Release
```

## Contributing & Forking Policy

> Contributions, issues, and feature requests are welcome.
>
> **Forking Policy:** If your fork primarily consists of bug fixes or feature additions that align with the core vision of this project, I reserve the right to request that your changes be submitted as a Pull Request to this existing codebase rather than being published as a completely separate standalone release, package, listing, or distribution.

## Links

- **Source Repository:** <https://github.com/KyleMHB/CargoContainersExpanded>
- **Issue Tracker:** <https://github.com/KyleMHB/CargoContainersExpanded/issues>
- **Original Steam Workshop mod:** <https://steamcommunity.com/sharedfiles/filedetails/?id=2725808118>

## License

> This project is a fork of **Cargo container** and inherits the original project's license. See the original project for license terms: <https://steamcommunity.com/sharedfiles/filedetails/?id=2725808118>.

## Credits

- Original mod by AOBA.
- Current maintained fork by kylohb.
