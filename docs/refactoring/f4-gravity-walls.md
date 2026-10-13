# F4.8a — gravity-wall kernels and coordination

Equivalent port from ANTHEA 1.7.1, source `RetainingWall.Gravity.cs` and the
assigned-strength branch of `RetainingWall.Structures.cs`. The original
application paths remain unchanged until the full adapter and coherent set.

- Plain-concrete material and shear policy: `Concrete/Walls/PlainConcreteWallMaterial`.
- Masonry material and joint shear: new `src/GPCChecker.Masonry`, without a
  dependency on Concrete or Geotechnics. This preserves the limited existing
  masonry policy; it is not a general masonry verifier.
- Stability, compressed section, load-case coordination and curvature:
  `src/GPC.Design/Walls/GravityWallAnalysis`. The coordinator reuses the two
  material checkers. It accepts typed data and cancellation, never ANTHEA JSON
  or WPF. Its .NET 8 target matches the application/runtime used for searches;
  constituent checkers remain netstandard2.0.

Explicit units: mm, N/mm for axial/shear force per wall width, Nmm/mm for
moment and MPa for stresses. Axial compression is positive here, consistent
with the geotechnical wall contract. This differs deliberately from the
reinforced-concrete section solver convention; adapters own that inversion.

The assigned-strength path has no implicit elastic stability correction.
Plain concrete and masonry retain the 2H cantilever condition, the existing
0.8 Ncr applicability limit, initial eccentricity and the uncracked-field
restriction. Missing capacity never yields a satisfied engineering outcome.
Input data can be ready while the physical verification is unavailable.

## Before/after evidence

Six frozen F4 walls (three materials, two eccentricities), with SHA verified
against ANTHEA `tests/fixtures/f4/reference.json`. A diagnostic exporter adds
only material projections from the unchanged legacy path. Original evidence:
`20261013-f4-gravity-capture/baseline_f4-wall-port/gravity-f4.json`.

The reduced test fixture contains only inputs and original observed values,
without recalculating expectations. SHA256:
`2DA105542D1914E469192404D436DCCE10816AEC2A54F241D413076A63E11633`.
The test asserts this fingerprint. No independent expected value is replaced.

**8672 checks and 240 curvatures equivalent**, including numerical demands,
capacities, ratios, availability and judgments; the existing 1e-9 absolute +
1e-9 relative tolerance is unchanged. Four tests also cover independent stress
and masonry vectors, cracked/unstable states, loss of contact, duplicate IDs,
invalid data and cancellation. The first instability test used a load below
0.8 Ncr; its input was corrected to a load above that analytically defined
threshold, without changing any frozen expectation or tolerance.

## Build and versions

`build/Verify.ps1` includes Design.Checks and compatibility checks for the new
APIs. First API snapshots contain 128 Design and 9 Masonry signatures.
`Release-Set.ps1` packages both new DLLs in the same coherent train. No isolated
DLL has been installed in ANTHEA.

Concrete candidate **0.5.0.0**: second component incremented for the additional
plain-concrete API. First versions of Design and Masonry: **0.1.0.0**.
F4.8 still needs reinforcement-detail coordination and the complete application
adapter; this point alone does not close F4.8 or the release.

Final complete verification `gravity-verify`: **1655 PASS**, only the 17 named
historical Steel failures, zero new failures, compatible APIs. ANTHEA runner
`20261013-f4-gravity-source-final`: **5 PASS**, including repeat capture and
architecture. Production ANTHEA remains on set V.

### Assigned axial tension edge case (F4.8a.1)

The frozen six-wall corpus does not contain assigned tensile axial force.
An additional independent regression exposes signed demands in the port;
legacy CheckValue always stores their absolute magnitude. The coordinator
now applies that same normalization, without changing capacities or signs
of input actions. Toe t=500 mm, N=-100 N/mm, M=V=0 gives both stress demands
0.2 MPa, with ratios 0.1 and 2 for assigned strengths 2 and 0.1 MPa.
Targeted verification: five tests pass, including the unchanged 8672/240
frozen comparison (`gravity-tension-fix`). Design patch version 0.1.0.1.

### Assigned strengths without stiffness (F4.8a.2)

The historical assigned-strength path does not request E. The first native
contract incorrectly required it, although no assigned check uses stiffness.
The three-argument Assigned factory declares unavailable stiffness explicitly
(HasStiffness=false, EffectiveModulus=0); the four-argument factory keeps its
positive-modulus contract. The coordinator skips the unused Euler calculation.
No stability check or SLS curvature is produced for this material.
Design patch 0.4.0.1 fixes this integration defect. All 16 Design checks pass,
including identical assigned ULS results with/without E and absent SLS results.
Evidence: TestResults/f4-assigned-without-e. No fixture or tolerance changed.
