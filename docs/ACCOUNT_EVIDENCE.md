# Observed account evidence (v0.7.0)

The Account page derives partial, unverified readings from persisted live observations. It exposes source text, capture time, device, mode/context and repeat/conflict/stale status. It does not write the troop optimizer's inventory, establish account identity, authorize actions or turn missing values into zero.

## Supported input

Within a screen classified as Minions, the extractor accepts explicit English labels for Leadership, Minion MAX, Available/Ready, Deployed, Healing, HP, ATK, DEF, RUSH, Tier and Form. A label may have a colon/space and one value, or a standalone value on the next OCR line. Only the six known form names are mapped; unsupported text stays unknown. This is label parsing, not a claim that every game screen uses these labels or that displayed stats are effective battle stats.

An individual card is bound only when exactly one explicit `Minion:`/`Minion Name:`/`Name:` label is established, or one exact complete line matches a name already in the user's nonsynthetic optimizer inventory. Multiple names leave individual readings unassigned. Unassigned cards never corroborate one another across frames and are displayed only for the latest frame.

Leadership is distinct from a type's Minion MAX. Minion Sim/Species War text separates the textual context, but does not prove an active battle mode; otherwise the mode remains unspecified. No general-mode capacity is inferred. Labeled B-Tad/W-Tad numbers are displayed as resource *readings*, never assumed to be current balances. Icon-only resource values are unsupported.

Counts must be whole values at most 100,000. Other numeric readings are bounded to 10^12. English comma grouping and decimal points are supported. K/M/B displays retain a Rounded flag; counts with suffixes are rejected. Percentages, bonuses, progress ratios, costs appended to a value, negative numbers, malformed grouping and upgrade arrows are rejected rather than repaired. Reward-gain quantities such as `Obtain 1 body` do not establish ownership totals. Missing HP/ATK or unreadable names stay missing.

## Provenance, freshness and conflicts

New observations record `LiveAdb`, `ImportedImage`, `Replay` or `Synthetic`; old records default to `Unspecified`. The projection consumes only LiveAdb records for the currently selected device. Imported images and replayed OCR can be inspected in Intelligence, but cannot populate live account state. Replay preserves original capture time and records replay provenance. Device identity is not account identity: switching game accounts on the same emulator is not automatically detected.

The projection examines at most 200 recent matching frames from a bounded 3,000-record input window. The GUI loads 200 recent persisted records and refreshes after new live observations, device changes, page selection, or an explicit refresh. While visible, the page recomputes age every 15 seconds. The default freshness window is ten minutes; export recomputes status at export time. Records older than the window remain visibly Stale if still in the lookback; evicted/missing records are unknown.

- Single: one eligible reading.
- Repeated: matching eligible capture times from distinct observation IDs; this is not calibrated OCR confidence.
- Rounded: a numeric abbreviation limits precision, even when repeated.
- Unassigned: no unique individual-minion identity.
- Conflicting: contradictory readings in the latest frame/time; the displayed value is Unknown.
- Stale: capture time exceeds the freshness window.

Duplicate IDs/timestamps cannot manufacture corroboration. Conflicting simultaneous records have no arbitrary latest ordering. A changed latest value resets matching-frame evidence; a rounded prior value cannot establish exact precision. Future timestamps beyond a five-second clock tolerance are excluded. No reading state authorizes spending or deployment.

## OCR sizing and verification

Native OCR remains the first pass. For an Unknown screen below 1,800 pixels on its longest side, the desktop application may retry at up to 2x, still bounded to 1,800 pixels and the existing 20-second deadline. An enlarged result replaces the whole native result only if it resolves the screen to a supported type. Known screens retain native OCR. Two passes on one image remain one observation, not two confirmations. Scale, pass count and selection/fallback notes persist in the observation and appear in Intelligence.

Local measurements on 2026-11-13 found that enlargement changed one captured game update notice from Unknown to Events, at approximately 374 ms for both passes including persistence/replay. Native OCR read a rendered synthetic minion fixture's HP line correctly, while forced enlargement dropped it and misread ATK. Therefore enlargement is not applied indiscriminately. These examples are not a representative game accuracy benchmark.

The deterministic suite tests partial fields, count/stat limits, bonuses/ratios/arrows, ambiguity, identity binding, changed values, stale/future data, duplicates, conflicts, mode contexts and provenance isolation. The Windows-only integration check renders clearly labeled synthetic text, runs the real Windows OCR, verifies selected values and framebuffer bounds, persists metadata, and proves that synthetic data cannot enter live account state:

```powershell
dotnet run --project tests/SnailCommander.Tests -c Release
dotnet run --project tests/SnailCommander.IntegrationTests -c Release -- --check-account-ocr --output artifacts/account-ocr
dotnet run --project tests/SnailCommander.IntegrationTests -c Release -- --image C:\local\screen.png --ocr-scale 1 --output artifacts/native
dotnet run --project tests/SnailCommander.IntegrationTests -c Release -- --image C:\local\screen.png --adaptive-ocr --output artifacts/adaptive
```

The synthetic fixture is not a real game account. The OCR-provider integration is local and requires an installed Windows OCR language; CI gates deterministic account policies, profile checks and actual GUI/updater process tests. No representative positive live minion-detail corpus or automatic inventory ingestion is claimed. The next step is verified own-account/card identity and real field-layout fixtures before the optimizer consumes these readings.
