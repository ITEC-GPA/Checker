# ANTHEA 1.16.0 — wall reinforcement and schedule as on a drawing (GPC.Design 0.42.0.0)

The preliminary schedule below is superseded (user request of 29/10/2026). `WallReinforcementDetailing` now:
- spaces the main bars at 1000/n (the spacing of As = n·πØ²/4 per metre in a continuous wall) in the clear spacing,
  maximum spacing, lap pair spacing and mandrel (ab) checks; the panel count is ⌈(L − 2c_e − Ø)/spacing⌉ + 1;
- crosses the stem feet in the foundation (upstream bars towards the toe, downstream towards the heel; opening corner,
  EN 1992-1-1 Annex J.2.3), the other way only when the tail does not fit (`FootReversed`, schedule warning);
- rests the foot on the bottom mesh (cover + transverse Ø + longitudinal Ø + Ø/2);
- makes the toe and heel bars of a layer one bar when they have the same Ø and number and fit in the stock bar
  (`WallDetailOptions.StockLength`), with end bends at the free edges (EN 1992-1-1 9.3.1.4, fig. 9.8);
- closes the head with U-bars (legs 2·head thickness), computes the lap ties (135° hooks + 10Ø, NTC 2018 7.4.6.2.2),
  states the batter bend angle, and places the longitudinal bars inside the main bars with one in each corner.

`WallBarScheduling` numbers the positions in drawing order, rounds the legs at the centre line to the cm (cut length =
their sum), splices the longitudinal bars beyond the stock bar into equal pieces with the Concrete lap, and returns the
totals per diameter, the concrete volume of the panel and the incidence. Shapes are built by `WallBarShapes` (fillets of
the corner polyline, exact arcs). Tests: `WallTavolaTests` (hand-computed); the frozen F4 tests compare every value, the
corrected ones against the old value transformed by the declared correction.

# F4.10a — preliminary wall bar schedule

`WallBarScheduling` consumes the same typed detail input and calls
`WallReinforcementDetailing`; no second anchorage, cover or shape engine.
All lengths, counts, exact 90-degree arcs and shared-zone boundary ownership
are ported from the captured ANTHEA schedule. Main bar spacing fills the net
panel width, secondary bars at a shared boundary belong only to the next zone.

The output carries typed kinds, face/zone identities and warning codes.
Italian notes and CSV formatting belong to the application/report adapter.
An unknown tie cut length remains null, with partial weight and an explicit
warning. Stock excess is reported; no silent splitting or invented splice.
Cancellation is checked before input work and during loops, returning no
partial result. Invalid panel dimensions and impossible free space are refused.

Immutable fixture and its source capture are documented in
[f4-wall-details.md](f4-wall-details.md). The 30 original schedules retain exact
integer counts; **435 bars, 600 legs and 1680 shape points** compare within the
pre-existing 1e-9 absolute + relative numeric tolerance, along with cut lengths,
spacings, weights, completeness and warning counts. Independent checks cover
boundary ownership, net panel width, unknown tie length, stock excess and
cancellation. No expected value was regenerated.

Targeted Design suite: **13 PASS**, zero failures, including all gravity and
detail regressions. Design **0.3.0.0**, second component increased for the new
public schedule API. Concrete remains 0.6.0.0. Full library verification of
the immediately preceding detail port was 1660 PASS / 17 known Steel failures;
this additive step will also run in the final coherent release verification.
ANTHEA's deployed calculation and set V are unchanged until F4.9. Search of
wall/pile reinforcement still remains in the following F4.10 steps.

## F4.7c.0 — undetailed quantity adapter prerequisite

`WallRebarQuantities.PrincipalSteelKgPerMetre` ports the separate historical
strip estimate (before anchors and secondary reinforcement are counted).
It is checked against 22 original frozen `SteelKg` values with details off,
and a hand-calculated asymmetric-face example. These expected masses are
extracted from the original SHA-verified F4 results, never recalculated by
the new service; fixture hash is asserted by the test.

Model.Material.SteelDensity is now the single source of density for this
estimate, detailed reinforcement and the schedule. All 15 Design tests pass,
including the 56 detail and 30 schedule comparisons, with unchanged tolerance.
Design 0.4.0.0 for the additive API; no new formulas in ANTHEA.
