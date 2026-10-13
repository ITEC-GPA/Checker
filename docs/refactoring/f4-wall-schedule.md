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
