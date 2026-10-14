# F4.8b — wall reinforcement details

The source service `GPC.Design.Walls.WallReinforcementDetailing` accepts typed,
owned wall geometry and reinforcement data, without JSON, WPF or ANTHEA types.
It coordinates the existing Concrete anchorage and durability services.
Spacing and secondary-area rules now have one implementation in
`Concrete.Detailing.PlateDetailingRules`, reused by the existing member checker.

Units: geometry, paths and developed bar lengths in metres; diameters,
cover, spacing, anchorage and lap lengths in millimetres; strengths in MPa.
The existing per-face wall policy is preserved: half the minimum horizontal
concrete ratio on each face. Slab details retain their critical-region policy.
NTC detailing is explicit; the cover specification independently carries its
selected supported durability profile and all its modifiers.

`CoverInput.WithBarDiameter` changes only the actual bar diameter. The caller
must preserve `PertinentCmin`: ANTHEA uses
`ExposureClasses.Uni11104MinimumStrength`, not the simplified environment-group
default. An initial test-adapter omission gave 30 rather than 35 mm for XC3/C25;
the adapter was fixed. No library formula, expected value or tolerance changed.

## Frozen evidence

ANTHEA capture commit `ebd781d0`, runner `20261013-f4-detail-capture` (2 PASS).
56 unchanged legacy inputs and material projections, including the 30 original
schedules. SHA256 of `tests/Design.Checks/Fixtures/details-f4.json`:
`E31A18D8D65CB9BA45566B9EA20AA6C643122C20BB5CE2A591A387917007A899`.
The file is immutable and the tests assert its hash.

Four targeted tests pass: **374 bars, 2316 shape points, 3260 checks and 56
weights equivalent** within the existing 1e-9 absolute + relative tolerance.
Every demand, capacity, ratio, outcome, position and bar property is compared.
Other tests cover input ownership, cancellation, invalid inputs and preservation
of cover settings. The first test run referred to the legacy `Outcome` field
instead of `Status`; only the test reader was corrected.

A negative available capacity retains the historical unavailable ratio and
NotEvaluated result; it never passes. Zero demand and zero capacity pass as
before. Neither estimates nor schedules certify a fabrication drawing.

Versions: Concrete **0.6.0.0**, Design **0.2.0.0**, increments of the second
component for new public services. The application remains on coherent set V;
runtime adapter and full set integration remain separate pending steps.

Complete verification `details-verify`: **1660 PASS**, 17 named historical
Steel failures, zero unexpected failures; public APIs compatible.

