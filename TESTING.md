# Cargo Containers Expanded Testing

Updated through: architecture refactor on 2026-09-05

## Agent read route

Read the current procedures with `Select-String -Path TESTING.md -Pattern '^## '`. Run the automated suite first, then the maintainer-only RimWorld 1.6 smoke suite. The catalog maps payload facts to recipe choices; the payload account covers counts, finalization, value, and refund planning; refrigeration policy covers eligibility and rot decisions; compatibility scopes cover temporary Harmony host state. None of these deterministic modules requires a live RimWorld map.

## Test prerequisites

- Repository root: `E:\Coding\Rimworld\Cargo Containers Expanded`.
- .NET SDK selected by `global.json` and PowerShell 7 (`pwsh`).
- No RimWorld or Steam Workshop installation is required for the automated suite.
- The maintainer smoke suite requires RimWorld 1.6.4871 with only Core, Harmony, and Cargo Containers Expanded enabled.
- Do not deploy or change live Workshop metadata as part of validation.

## Fast smoke test

From the repository root, run:

```powershell
dotnet restore --locked-mode
dotnet test ./CargoContainersExpanded.sln -c Release --no-restore
pwsh -File ./Scripts/Validate.ps1
dotnet build ./Source/cargo-containers-expanded.csproj -c Release --no-restore
pwsh -File ./Scripts/Validate.ps1
```

Expected: locked restore succeeds; all tests pass; the Release build has zero warnings and errors; XML, metadata, definitions, textures, localization, and package checks pass; and `Assemblies` contains only `CargoContainersExpanded.dll`.

## Automated architecture suite

The test project is `Tests/CargoContainersExpanded.Tests.csproj`, targets `net472`, and references the production project. It uses NUnit 4.6.1, NUnit3TestAdapter 6.3.0, and Microsoft.NET.Test.Sdk 18.9.0 from locked package inputs.

The tests use four accepted seams:

- `ExtractionRecipeCatalog` for immutable recipe facts, matching, legacy recognition, ordering, diagnostics, and filtered choices.
- `PayloadAccount` for serialized-state normalization, withdrawal, finalization, market value, refund planning, stack splitting, and product clamping.
- `RefrigerationPolicy` for deterministic eligibility, unpowered duration, tick decisions, and temperature classification.
- `IRecipeListHost` with its RimWorld and in-memory adapters for temporary recipe-list and reflected-cache compatibility state.

The suite does not mock these internal modules or assert private collaboration. It uses literal expected values and public behavior on the accepted seams.

## Catalog, payload, refrigeration, and compatibility tests

Run the full solution command shown in the fast smoke test. The expected and observed test distribution is:

- Catalog: 11 tests covering exact recipe names, batches, work values, fixed and stuff payload matching, legacy and unknown recipes, wrong payloads, ordering, duplicate facts, diagnostics, immutable results, and host application behavior.
- Payload: 14 tests covering fresh/load normalization, negative and excessive requests, all withdrawal sizes, finalization retention and consumption, valid and invalid valuation, fixed-payload exclusion, refund order, stack splitting, transactional cleanup, product clamping, and rot transfer.
- Refrigeration: 24 tests covering every eligibility exclusion, normal eligibility, case-insensitive generated names, 2x duration and fallback, powered and unpowered ticks, temperature thresholds, and immutable category facts.
- Compatibility: 12 tests covering unrelated and nested scopes, double disposal, thread isolation, bypass eligibility, same-host recursion, different-host nesting, exact restoration after success and exceptions, missing-cache fallback, and Harmony exception identity.

## Lifecycle test

The following host-side behaviors remain part of the current acceptance gate and cannot be established by the net472 unit suite alone:

- Startup, Def discovery, recipe generation, duplicate prevention, Harmony patching, and translation loading.
- Construction of full and half stuffable containers, fixed-payload containers and tanks, and both refrigerated sizes.
- Extraction and bill filtering for batches 1, 25, and 100, exact final partial batches, invalid-bill rejection, unpowered extraction, placement fallback, and transactional cleanup on refund materialization failure.
- Refrigeration ticking, category assignment, inspect text, graphics, Steel/item/corpse/minified/generated-name/egg/hatcher/rottable exclusions, RawRice default, and 2x unpowered duration.
- Market value before and after extraction.
- Save/load of payload count, rot progress, bills, pending finalization, power behavior, and unchanged serialized keys.
- English, Simplified Chinese, and Traditional Chinese localization behavior.
- Original-mod incompatibility and missing-cache compatibility fallback.

## Performance and soak test

No separate performance or soak run is part of this refactor. The acceptance requirement is deterministic Release output and the package-content validation in `Scripts/Validate.ps1`.

## Result recording

## 2026-09-05 - Automated architecture suite and package validation

- Version/environment: .NET SDK selected by `global.json`; production and test projects target `net472`; Release configuration.
- Configuration differences: no RimWorld or Steam Workshop installation paths; test output is outside `Assemblies`.
- Actions: ran `dotnet restore --locked-mode`; `dotnet test ./CargoContainersExpanded.sln -c Release --no-restore`; `pwsh -File ./Scripts/Validate.ps1`; `dotnet build ./Source/cargo-containers-expanded.csproj -c Release --no-restore`; and `pwsh -File ./Scripts/Validate.ps1`.
- Expected: locked inputs restore; 61 architecture tests pass; the production build has 0 warnings and 0 errors; validation passes; and only `CargoContainersExpanded.dll` is packaged.
- Observed: locked restore passed; 61 tests passed, comprising catalog 11, payload 14, refrigeration 24, and compatibility 12; the Release production build passed with 0 warnings and 0 errors; both validation runs passed; and `Assemblies` contains only `CargoContainersExpanded.dll`.
- Evidence: `Tests/CargoContainersExpanded.Tests.csproj`, `Tests/packages.lock.json`, `Scripts/Validate.ps1`, and the command output from the 2026-09-05 run.
- Status: Passed
- Follow-up: none for the automated gate. Two identical Release builds produced SHA-256 `7B91FE2C916D26E9E1A4A712460301D83860E739A77CFC9F10CF2FB7E608B006` on both runs.

## 2026-09-05 - Maintainer RimWorld 1.6 smoke suite

- Version/environment: RimWorld 1.6.4871; Core, Harmony, and Cargo Containers Expanded only.
- Configuration differences: this is a maintainer-run game check, separate from the automated net472 suite.
- Actions: run every current smoke check listed under Lifecycle test, including startup, construction, extraction and bill filtering, refunds and placement fallback, transactional cleanup, refrigeration and graphics, market value, save/load, localization, original-mod incompatibility, and missing-cache compatibility.
- Expected: no red startup or Harmony errors; unchanged player-visible behavior and save compatibility; exact recipes, refunds, rot, value, translations, and compatibility behavior.
- Observed: not run in the current post-refactor acceptance cycle.
- Evidence: none yet for the current architecture refactor.
- Status: Pending
- Follow-up: the maintainer must complete and record the game run before the architecture refactor can be considered fully accepted.

## 2026-08-28 - Historical pre-refactor RimWorld smoke suite

- Version/environment: RimWorld 1.6.4871; Core, Harmony, and the then-current Cargo Containers Expanded build.
- Configuration differences: this run predates the 2026-09-05 architecture refactor and is retained as historical evidence only.
- Actions: the maintainer manually ran the earlier focused suite covering startup, construction, ticking, extraction, power, market value, save compatibility, refunds, localization, and incompatibility metadata.
- Expected: each earlier scenario passes without red errors, missing Defs, invalid recipes, refund loss, rot/value regressions, localization errors, or incompatibility failures.
- Observed: all earlier scenarios were reported passed on 2026-08-28.
- Evidence: the prior repository acceptance record.
- Status: Passed
- Follow-up: rerun the current Lifecycle test checklist after this refactor; this historical result does not satisfy the current smoke gate.

## External metadata

The live Steam `Translation` tag is outside repository scope and remains an external maintainer action.
