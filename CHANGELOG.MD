# Changelog

## 2026-05-02

### Added

- Added updated README, Steam Workshop description, and normalized changelog naming for the documentation refresh.
- Added a PowerShell deploy script for syncing runtime asset folders to the Steam mod target.

### Changed

- Moved source development to `E:\Coding\Rimworld\Cargo Containers Expanded` and kept the Steam mod folder as a runtime deploy target.
- Updated the C# project to resolve RimWorld, Harmony, and deploy paths from explicit MSBuild properties.

## Current Project Changes

- Updated the mod for RimWorld 1.6 only and refreshed the package metadata, author, repository URL, Harmony dependency, and mod description.
- Added Harmony-backed cargo extraction support for containers, including extract bills, low-priority extraction work, filtered bill menus, and payload-safe product generation.
- Added clean payload refunds on deconstruction so containers return their stored payload and frame resources without double-refunding fixed payload ingredients.
- Added batch extraction recipes for 1, 25, and 100 item pulls, with work scaling and per-payload recipe generation.
- Added refrigerated full and half containers that use rottable payloads as their selected material, require power for active refrigeration, and show payload icons on the container graphic.
- Added dynamic refrigerated payload eligibility for rottable item defs while excluding corpses, minified things, eggs, hatchers, and other unsafe targets.
- Added rot behavior for refrigerated containers when unpowered, including temperature-aware inspect text, frozen/refrigerated/not refrigerated status, and rot progress transfer to extracted products.
- Allowed extraction bills to continue on unpowered refrigerated containers when they still have payload, without globally bypassing normal power behavior.
- Added dynamic cargo market value and stat patches so container wealth reflects remaining payload value instead of stale base building value.
- Fixed extraction bill filtering so containers only show and accept recipes matching their current payload.
- Updated English, Chinese Simplified, and Chinese Traditional localization for new cargo payload, extraction, refrigeration, and inspect strings.
- Added README documentation covering the fork, features, extraction behavior, refrigerated storage behavior, build notes, and compatibility expectations.
- Updated refrigerated container art and preview assets.
- Removed temporary `.krz` source/export artifacts.

## Repository Cleanup

- Removed unsupported legacy `1.3`, `1.4`, and `1.5` def folders now that the mod metadata supports only RimWorld 1.6.
- Removed tracked editor backup images ending in `*.png~`.
- Removed the tracked generated `Assemblies/CargoContainersExpanded.pdb`.
- Updated `.gitignore` to ignore generated PDB files and editor backup files.
- Updated the C# project to disable debug symbol output so PDB files are not regenerated into `Assemblies`.
- Cleaned generated local build/runtime folders such as `.dotnet` and `Source/obj`.

## Verification

- Built `Source/cargo-containers-expanded.csproj` in Release successfully with 0 warnings and 0 errors.
