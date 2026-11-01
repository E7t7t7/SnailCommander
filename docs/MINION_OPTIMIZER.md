# Minion optimizer

The initial optimizer compares legal troop compositions from supplied account data and supplied bonuses. It does not yet recognize an account's full minion inventory automatically, deploy troops, or simulate Super Snail combat. No canonical minion roster or unverified game statistics are bundled.

## Use in the application

Open **Minion Command**, then use **Import account JSON** or **Edit / save account**. Choose a goal and mode and select **Calculate compositions**. Imported and edited accounts persist at `%LOCALAPPDATA%/SnailCommander/minions.json`.

Start from [the empty account](../samples/minions.empty-account.json). [The synthetic account](../samples/minions.synthetic.json) and **Load synthetic example** demonstrate the interface with invented names, values, costs, and bonuses. They provide no gameplay advice for the user's real account. Loading the synthetic example alone does not save it as the real account.

Each `MinionType` supplies a unique `Id`, a `Name`, `Form`, `Tier`, effective per-copy `Stats`, quantities, and `Evidence`. Optional data includes `Partner`, `BaseStats`, `AbilityNotes`, `CloneCost`, and `CloneMinutes`. The supported stat fields are `Hp`, `Attack`, `Defense`, `Rush`, `ElementalDamage`, and `ElementalResistance`. Field names are case insensitive; use the provided schema names, including `Attack` rather than `ATK`.

`Evidence` contains `Kind` (`Unverified`, `Observation`, `Documented`, or `Synthetic`), a nonempty `Source`, `Confidence` from 0 to 1, and optional `VerifiedAt`. Supply an observation identifier or a document URL as the source. Confidence records the input's provenance; it is not a measured win probability. Low confidence and unverified inputs produce warnings. Synthetic evidence in any minion or bonus also produces a synthetic warning. Parsing and saving restore the account's `Synthetic` flag when nested evidence remains synthetic, even if someone clears the top-level flag.

## Leadership, MAX, and readiness

These quantities have distinct meanings in this model:

| Field | Meaning |
| --- | --- |
| `Leadership` | Maximum total copies in the proposed composition, across all types. |
| `MinionMax` | Maximum owned copies of this individual type. |
| `Available` | Ready, undeployed copies that may enter this composition. |
| `Deployed` | Copies committed elsewhere; unavailable for this proposal. |
| `Healing` | Copies recovering; unavailable for this proposal. |

Validation requires `Available + Deployed + Healing <= MinionMax`. Every candidate uses at most `Available` copies of each type and at most `Leadership` copies in total. A high Leadership value does not create extra minions or bypass individual caps. Cloning also counts deployed and healing copies against MAX.

## Supplied bonuses and double counting

`Stats` must contain effective **per-copy** values for the account. `BaseStats` is optional reference data and is not added to `Stats`. Enter only bonuses that have not already been incorporated into those effective values.

Each `TroopBonus` supplies:

- `Mode`: omitted/null for every context, otherwise only the matching mode.
- `TriggerForm`: omitted/null to count all deployed copies, otherwise only that form.
- `RequiredCount`: minimum matching copies required for activation.
- `PerGroup`: false for a single threshold, true for `floor(matching count / RequiredCount)` stacks, capped by `MaxStacks`.
- `TargetForm`: omitted/null to modify all deployed forms, otherwise only the specified form. A targeted bonus does not activate without a deployed target.
- `Percent`: fractional stat increases, so `0.15` means +15%.
- `Flat`: a flat troop-wide contribution per active stack, added after percentage changes.
- `Name` and `Evidence`: identity and provenance displayed in the result.

Global and form-specific percentages add together before multiplying each form's effective stat totals. For example, supplied +10% global ATK and +20% Demon ATK produce a 1.30 multiplier for Demon ATK. Flat contributions are not multiplied by those percentages. This aggregation is the implemented data model; it is not a claim that every game's effect follows these rules. Effects requiring a different verified formula need an additional implementation.

Per-10 and per-60 thresholds are supported as explicit input values. They are not assumed to apply universally. Partner, relic, gear, and mode effects only influence a result when supplied as effective stats or an applicable bonus, and must be entered once.

## Search and scoring

`TroopOptimizer.Optimize(account, context, goal, options, cancellationToken)` returns ranked candidates, effective totals, active bonus explanations, search counters, and warnings. `Evaluate` scores a specific supplied composition and rejects illegal counts.

Search first establishes complete greedy and single-type baselines within the evaluation budget. These protect against an early type consuming the search budget before stronger later types are considered. Beam expansion then evaluates combinations one type at a time, preserving a bounded number of high-scoring partial candidates. Large count ranges are sampled; explicit threshold crossings and capacity reserved for a later form's threshold are added to the sampled choices. Extra threshold choices can exceed the nominal quantity sample count, but the hard evaluation budget still bounds candidate search. Final descriptions are materialized only for ranked results.

Defaults are a beam width of 128, at most 100,000 search evaluations, a nominal quantity sample count of 256, and up to five results. Cancellation is supported. The returned `SearchLimited` and `QuantitySampled` fields disclose truncation. Beam pruning can discard a weak partial army that would become stronger after a later synergy. Baselines and threshold preservation improve the search without proving global optimality. Results are the best candidates found under the supplied bounds, not a guaranteed global optimum.

Goals are explicit linear stat proxies:

| Goal | Emphasis |
| --- | --- |
| `Balanced` | ATK with smaller HP, DEF, RUSH, and elemental weights. |
| `Damage` | ATK and elemental damage. |
| `Survivability` | HP, DEF, and elemental resistance. |
| `MinimumLossExposure` | A stronger defensive weighting. |
| `Speed` | ATK and RUSH. |
| `ElementalDamage` | Elemental damage, with a small ATK weight. |
| `ElementalDefense` | Elemental resistance, with smaller HP/DEF weights. |

Exact weights are in `TroopOptimizer.WeightsFor`. Callers can supply calibrated `BattleContext.ScoreWeights`. The mode enum (`General`, `MinionSimulation`, `SpeciesWar`, `Domain`, `Rocket`, `Gathering`) filters supplied bonuses; it does not assert actual formation availability or implement each mode's game rules.

Scores do not predict damage dealt, enemy interactions, healing, battle duration, casualties, or victory. In particular, `MinimumLossExposure` does not estimate actual losses, and `Speed` does not estimate completion time. Validating battle formulas and calibrating predictions against observed outcomes remain future work.

## Advisory cloning API

`ClonePlanner.Plan(account, budget, desiredReady, goal, maxCopies)` produces an advisory queue. It is a core API with tests; there is no cloning queue GUI or game execution yet.

`ResourceBudget.Balances` holds current quantities and `Reserves` specifies protected quantities by resource key. `desiredReady` maps minion IDs to requested ready quantities. The planner fills shortages without exceeding MAX, spending reserves, or assuming unknown costs are free. An empty `CloneCost` blocks cloning; explicit zero costs must be supplied if independently verified. Negative/nonfinite balances, reserves, costs, and invalid demand are rejected.

Eligible copies rank by weighted per-copy stats divided by `1 + normalized resource pressure + cloning hours`. The queue preserves selection order and groups consecutive copies. It reports planned quantities, costs, time workload, remaining balances, and blockers. The copy limit bounds work. This heuristic does not model upgrade value, future event rewards, simultaneous cloning slots, or when deployed/healing copies return. Producing a plan never spends resources.

## Verification and benchmark limits

Run `dotnet run --project tests/SnailCommander.Tests/SnailCommander.Tests.csproj -c Release`. Minion regressions cover legal counts, independent caps, deployed/healing availability, supplied per-10/per-60 stacking, mode filters, differing objectives, low-budget baselines, reserved threshold capacity, cancellation, JSON persistence, synthetic provenance, and clone reserves.

The tiny two-type, Leadership-4 fixture compares the beam result with all 15 legal complete compositions. A local development run measured about 13 ms for the seeded beam search's 23 evaluations versus 1.4 ms for exhaustive enumeration of those 15 compositions. The test prints current timings on every run. These are illustrative cold-path measurements with validation, initialization, different evaluation counts, and JIT overhead; they do not establish which algorithm is faster on a real account. Exhaustive enumeration is useful for small correctness fixtures, while bounded search prevents combinatorial growth on larger inventories. No real-account optimizer benchmark or live battle validation has occurred.
