# F4.10b — pile segment design

`GPC.Design.Piles.PileSegmentDesign` coordinates the existing Concrete engines:
`PileReinforcement.Layouts`, construction checks, seismic head checks, nominal
N–M–V checks and exact-N parallel resistance tables. The older `SelectCandidate`
checks only nominal N–M–V on an eagerly constructed list; it does not cover
construction screening, lazy solver factories, seismic head checks, progress or
parallel preparation. Its mechanical services are reused, not copied.

The coordinator receives concomitant actions, section boundaries and a factory
that transfers an independent section/solver per call. It owns copies of actions
and seismic settings. Catalogue order and first feasible selection are unchanged;
geometrical refusals are returned with their layout. A missing solution remains
explicit. Cancellation is never returned as a partial solution. Progress has typed
stages; labels and document updates belong to ANTHEA.

Design 0.5.0.0: second component incremented for the new orchestration API.
Twenty Design tests pass, including a real circular-section search with one and
two workers, the older nominal selector, retained caller inputs, missing/nonfinite
actions, geometric refusal and cancellation before/during search. Existing frozen
wall, schedule and gravity tests are unchanged. Public APIs remain compatible.
Evidence: TestResults/f4-pile-design-a and f4-pile-design-api.

The ANTHEA adapter, frozen pile comparison and the next coherent release set are
still required. This source commit does not close F4.10b or activate a DLL.
