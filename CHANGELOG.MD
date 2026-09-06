# Changelog

Agent read route: read only the newest relevant release; locate older versions by searching `^## ` headings.

## 2026-09-05

### Changed

- Reworked the README and Steam Workshop source around the shared public-documentation templates.
- Clarified the fork's scoped MIT license, upstream provenance, compatibility, usage, build, and forking-policy details.

### Internal

- Added a net472 test project with pinned NUnit, NUnit adapter, and Microsoft.NET.Test.Sdk packages.
- Deepened extraction recipe cataloging, payload accounting and refunds, refrigeration policy, and Harmony compatibility scopes while preserving existing Def names, save keys, and balance values.
- Added 61 deterministic architecture tests covering catalog, payload, refrigeration, and compatibility behavior.

## 2026-08-28

- Improved runtime performance with cached extractable-container checks, inert tank ticking, and guarded recipe filtering.
- Declared the original Cargo Container package incompatible and moved Harmony patches to a fork-specific owner ID.
- Hardened deconstruction refunds with prepare-before-destroy behavior and lossless placement fallback.
- Preserved the intentional 10% stored-payload wealth rule, 2x unpowered rot duration, RawRice refrigerated default, save keys, Def names, and extraction behavior.
- Replaced machine-specific build references with pinned development packages, locked restores, deterministic output, repository validation, and Windows CI.
- Corrected English translation errors while retaining legacy Simplified and Traditional Chinese translations with honest English fallback for new UI.
- Updated README, Steam-description source, testing evidence, scoped MIT licensing, and third-party provenance documentation.
- Completed the focused RimWorld 1.6.4871 smoke suite covering startup, construction, ticking, extraction, power, market value, save compatibility, refunds, localization, and incompatibility metadata.

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
