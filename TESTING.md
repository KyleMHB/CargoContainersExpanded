# Testing Cargo Containers Expanded

This file records the acceptance procedure and observed evidence for the repository remediation. A scenario is complete only when its result is recorded from an actual run.

## Automated acceptance

Run from the repository root without depending on RimWorld or Steam Workshop installation paths:

```powershell
dotnet restore --locked-mode
pwsh -File ./Scripts/Validate.ps1
dotnet build ./Source/cargo-containers-expanded.csproj -c Release --no-restore
pwsh -File ./Scripts/Validate.ps1
```

Expected result: zero build warnings or errors; valid XML, metadata, definitions, textures, localization, and package contents; only `CargoContainersExpanded.dll` in `Assemblies`; no dependency DLLs or PDBs; and a deterministic rebuild that does not change the tracked DLL.

Observed on 2026-08-28:

- `dotnet restore Source/cargo-containers-expanded.csproj --force-evaluate`: passed and generated the lock file with the pinned packages.
- Temporary Release build using SDK 9.0.316: passed with 0 warnings and 0 errors without writing the tracked assembly.
- `pwsh -NoProfile -File ./Scripts/Validate.ps1`: passed before the final assembly rebuild.
- `dotnet restore --locked-mode`: passed from the repository root.
- Final packaged Release build: passed with 0 warnings and 0 errors.
- Post-build `pwsh -NoProfile -File ./Scripts/Validate.ps1`: passed.
- A second Release build produced the same SHA-256 hash, `A599ACC2B97A911EA19C13A13AAFC9D6BEAB3C833C56D8989D5FF6CA01E65699`.
- `Assemblies` contains only `CargoContainersExpanded.dll`; no PDB or dependency DLL was emitted.

## Focused RimWorld 1.6.4871 smoke suite

Use only Core, Harmony, and Cargo Containers Expanded. Do not deploy or change live Workshop metadata as part of this test.

- [ ] Startup has no red errors, missing Defs, failed Harmony patches, duplicate recipes, or unresolved translations; the Harmony owner is `KyleMHB.CargoContainersExpanded`.
- [ ] Full and half resource containers, one fixed-payload container, both refrigerated sizes, and beer, neutroamine, wort, and chemfuel tanks construct successfully; RawRice remains the refrigerated default.
- [ ] Beer, neutroamine, and wort tick `Never`; chemfuel ticks `Normal`; refrigerated containers tick `Rare`.
- [ ] Only recipes matching the payload appear; batches 1, 25, and 100 work; a smaller final batch is exact; final extraction refunds only the frame; invalid bills are rejected without repeated log spam.
- [ ] Powered refrigeration pauses rot; unpowered refrigeration uses the 2x duration; extraction works without power; unrelated unusable worktables and empty containers remain unusable.
- [ ] Direct `MarketValue` and the market-value stat display both equal payload base value times remaining count times 0.1, decreasing proportionally after extraction.
- [ ] A pre-change save loads; payload, partial rot, bills, pending final extraction, and power behavior survive save and reload.
- [ ] Full, partial, and empty deconstruction return exact frame and payload refunds without duplication; blocked nearby cells use the vacated container cell without loss.
- [ ] English, Simplified Chinese, and Traditional Chinese launch cleanly; legacy Chinese Def text remains and new keyed UI falls back to English without TODO or missing-key errors.
- [ ] Enabling the original package alongside the fork produces RimWorld's declared incompatibility warning.

Observed on 2026-08-28: not executed. The Windows automation helper failed to initialize its sandbox before RimWorld could be controlled. No live game or Workshop metadata was changed. These checks remain release-blocking until an actual game run records each result.

## External metadata

The live Steam `Translation` tag is outside repository scope and remains an external maintainer action.
