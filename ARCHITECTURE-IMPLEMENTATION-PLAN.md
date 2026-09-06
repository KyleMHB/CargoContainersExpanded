# Architecture Implementation Plan

## Goal

Deepen the extraction recipe catalog and payload accounting modules without changing player behavior, save compatibility, package identity, balance, or build portability. Assess refrigeration policy next. Change the host compatibility scopes only if the first two phases leave measurable friction.

## Fixed constraints

- Preserve RimWorld 1.6 support and the `KyleMHB.CargoContainerExpanded` package ID.
- Preserve every serialized field key, container Def name, generated recipe Def name, batch size, work amount, payload count, and Harmony owner ID.
- Preserve the 10% stored-payload market-value rule, 2x unpowered rot duration, RawRice default, exact final partial batches, clean refunds, and extraction from unpowered refrigerated containers.
- Keep the build independent of a local RimWorld or Steam Workshop installation.
- Do not package RimWorld, Unity, Harmony, .NET Framework, test, or PDB files.
- Do not alter inherited assets or upstream-derived definitions unless a failing acceptance scenario proves that a change is required.

## Agent contract

All implementation and review work uses Codex agents configured with model `gpt-5.6-luna` and reasoning effort `xhigh`.

- Use one writing agent at a time. Agents share the worktree, so parallel writes would make the red-green history unreliable.
- Give each agent one bounded vertical slice with its seam, behavior, failing test, allowed files, and validation command.
- After a phase is green, use a separate Luna xhigh review agent to inspect the diff against this plan and repository instructions.
- Address review findings through new red-green slices. Do not mix speculative refactoring into a green step.
- Keep commits coherent by phase and exclude unrelated user changes.

## TDD protocol

Before the first test, agree with the user on the public interface and the seams under test. No agent may add a test at an unconfirmed seam.

For every slice:

1. Red: add one behavior test through the confirmed interface and run only the smallest relevant test command. Record the expected failure.
2. Green: add only enough implementation to pass that test. Run the same test again.
3. Continue vertically: let the last result determine the next behavior test.
4. Review: after the phase is green, inspect architecture, standards, and spec compliance. Refactoring starts only from a new failing characterization or behavior test.

Tests must avoid private methods, internal collaboration assertions, and expected values calculated with the production algorithm. Use worked examples and stable literals.

## Phase 0: confirm seams and establish the test harness

### Decisions to confirm

- The extraction recipe catalog interface is the test surface for payload matching, batch metadata, legacy handling, work amounts, and filtered recipe decisions.
- The payload accounting interface is the test surface for initialization, clamping, consumption, finalization state, valuation, and refund planning.
- RimWorld definition mutation, cache access, spawning, placement, destruction, save hooks, translation, power, temperature, and graphics remain host adapters unless a later decision explicitly moves them.

### Harness

- Add a separate test project under `Tests/` targeting a framework compatible with the production `net472` assembly.
- Pin the chosen test runner and adapter as development-only packages and lock restore inputs.
- Reference the production project instead of compiling duplicate production sources.
- Add the test project to `CargoContainersExpanded.sln` and the repository validation path.
- Prove the harness with one intentionally failing test, then the smallest passing implementation. Do not keep a tautological smoke test.

Exit evidence:

- Locked restore succeeds from the repository root.
- The selected test command fails for the intended red assertion, then passes after green.
- The Release package still contains only `CargoContainersExpanded.dll`.

## Phase 1: deepen the extraction recipe catalog

### Slice order

1. Characterize payload-to-recipe matching for one fixed-payload container.
2. Add stuff-based matching for one material-backed container.
3. Cover batch sizes `1`, `25`, and `100` with their current work amounts.
4. Cover legacy recipe recognition without exposing legacy recipes to new bill choices.
5. Cover deterministic duplicate removal and stable recipe ordering.
6. Cover filtering of valid and invalid payload recipes.
7. Move mutable catalog collections behind the confirmed interface.
8. Add a definition adapter for `DefDatabase`, `ThingDef.recipes`, and work-giver configuration.
9. Add a cache adapter for reflected `allRecipesCached` access and its compatibility fallback.
10. Redirect bootstrap, container, worker, and Harmony callers to the deep catalog module.

Integration checks:

- Startup generates the same recipe Def names and batch metadata.
- Each container exposes only recipes for its payload.
- Invalid bills are rejected and logged once per rejection key.
- Missing reflected cache access keeps the documented fallback behavior.

Exit evidence:

- No caller receives a mutable catalog registry.
- Catalog rules are testable without a live `DefDatabase`.
- Existing startup, extraction, and bill-menu behavior passes automated and in-game checks.

## Phase 2: deepen payload accounting and refunds

### Slice order

1. Characterize initial payload count for fixed and material-backed containers.
2. Cover zero and negative extraction requests.
3. Cover requests smaller than, equal to, and larger than the remaining payload.
4. Cover exact final partial batches and finalization scheduling.
5. Cover count clamping after load without changing serialized keys.
6. Cover the 10% market-value rule with known literals and invalid payload values.
7. Cover frame refund plans that exclude fixed payload ingredients.
8. Cover stack splitting at the payload definition's stack limit.
9. Move count, finalization, valuation, and refund planning behind the confirmed payload interface.
10. Keep `ThingComp`, `ThingMaker`, placement, spawning, destruction, and rot transfer in host adapters.
11. Redirect the building, recipe worker, product patch, and market-value patch through the deep payload module.

Integration checks:

- Save and reload preserve payload, rot, bills, and pending final extraction state.
- Full, partial, and empty deconstruction return exact refunds once.
- Blocked nearby cells use the existing placement fallback without loss.
- Extracted rottable products inherit the current rot percentage.

Exit evidence:

- Deterministic accounting tests run without a live map.
- Host side effects remain concentrated in adapters.
- The focused RimWorld smoke scenarios remain unchanged.

## Phase 3: assess and deepen refrigeration policy

Proceed only after phases 1 and 2 are green and reviewed.

### Decision gate

Use the completed catalog and payload interfaces to determine whether a separate refrigeration policy creates leverage. Stop if it would duplicate rules or introduce a seam with only one adapter and no testability gain.

### Candidate slices

1. Characterize eligibility for a normal rottable item.
2. Cover Steel, corpse, minified, egg, hatcher, category, and generated-name exclusions.
3. Cover the 2x unpowered rot-duration decision.
4. Cover powered, frozen, refrigerated, and normal-temperature decisions.
5. Move eligible rules behind the confirmed refrigeration interface.
6. Keep definition mutation in the startup adapter and live power and temperature reads in the runtime adapter.
7. Keep translation and `Graphic_RefrigeratedContainer` outside the policy seam.

Exit evidence:

- Eligibility and rot decisions run against synthetic facts.
- Actual definition mutation and `CompRottable` setup retain focused integration coverage.
- Player-facing inspect text and graphics remain unchanged.

## Phase 4: reassess host compatibility scopes

This phase is conditional. Do not implement it merely because the architecture review found it.

Proceed only if phases 1 through 3 still leave policy duplicated across Harmony modules or make exception-safe restoration hard to verify.

If needed, add vertical slices for:

- nested extraction power scopes;
- unmatched scope exits;
- unrelated work-giver calls;
- recipe-list and cache restoration after success;
- recipe-list and cache restoration after exceptions.

Keep reflection, thread-static state, temporary mutation, and restoration in the smallest host adapter implementation. Do not introduce another seam unless at least two adapters require it.

## Validation and delivery

Run after each phase:

```powershell
dotnet restore --locked-mode
dotnet test --no-restore
pwsh -File ./Scripts/Validate.ps1
dotnet build ./Source/cargo-containers-expanded.csproj -c Release --no-restore
pwsh -File ./Scripts/Validate.ps1
```

At the end:

- Rebuild twice and compare the packaged DLL hash.
- Run the focused RimWorld 1.6 smoke suite in `TESTING.md`.
- Record new repeatable procedures and observed evidence in `TESTING.md`.
- Add completed internal changes to `CHANGELOG.MD`; do not describe plans as shipped behavior.
- Update `README.md`, `steam-description.md`, or `About/About.xml` only if player-visible facts changed, using their shared templates and Unslop validation.
- Do not bump a version for architecture-only work.

## Stop conditions

Stop and ask for direction if a slice would change save keys, Def names, package identity, balance values, dependencies shipped with the mod, or player-visible behavior. Stop a candidate when its new interface is not smaller than the knowledge it replaces or when the deletion test shows no gain in locality or leverage.
