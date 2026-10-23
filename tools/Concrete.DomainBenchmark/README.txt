CONCRETE DOMAIN SEARCH BENCHMARK

Purpose
Compare the complete CalculateConvergedDomainPoint path: main iterations, existing fallbacks and returned-point validation.
The production code contains no performance counters. Prepare.ps1 copies and instruments the selected solver sources, then builds
a Release .NET 8 executable with the same section catalogue and referenced Model/Geometry/Utilities DLLs for both versions.

Requirements
PowerShell 7+, .NET 8 SDK/runtime (a later SDK targeting net8.0 is also suitable), the usual sibling dependency DLLs,
and Python 3.9+ only for report generation. Packages: MathNet.Numerics 5.0.0 and MSTest.TestFramework 3.1.1.
The existing DomainPointContractTests file is compiled solely to reuse its section factories/catalogue; its tests are not run by this tool.

From this directory (replace the paths with existing local locations):
  ./Prepare.ps1 -Source <baseline GPCChecker.Concrete directory> -Output <new before directory> -RepositoryRoot <dependency parent>
  ./Prepare.ps1 -Source ../../GPCChecker.Concrete -Output <new after directory> -RepositoryRoot <same dependency parent>
  ./Run.ps1 -Before <before directory>/bin/GPCChecker.Test.Concrete.dll -After <after directory>/bin/GPCChecker.Test.Concrete.dll -Output <new measurements directory> -IncludeFocus
  python Report.py <measurements directory> <TRX directory> <report directory>

Never reuse an already instrumented source copy as the input of Prepare.ps1. Preserve the source commit and dependency hashes.
The tools do not change fixtures, standards, geometry or integration settings. Run.ps1 refuses to overwrite existing measurements.

Measurements
- One cold search and five warm searches per case, sequential execution, tiered compilation disabled, initial JIT warm-up.
- An additional reverse-order control (after then before), three warm searches, excludes the unchanged StressBlock branch.
- Time includes the entire solve/validation path, including any vectorized construction of a fallback domain.
- Preparation, JSON output, external diagnostic validation and reintegration of the returned plane are outside the timed region.
- Iterations count main Newton update attempts (including restarts and unsuccessful attempts); bracketIterations count Illinois refinements.
- evaluations counts scalar CalculateForceResultantForDomain evaluations, including finite differences and trial steps. It does not count
  vectorized evaluation of the whole fallback domain; that cost is nevertheless included in the recorded elapsed time.
- Numerical acceptance uses DomainPointConvergence and the unchanged public tolerances. It is separate from structural utilization.
- planeAccepted independently checks the result of reintegrating the returned strain plane against those same constraints/tolerances.
- For fixed N/moments, residuals are differences in N/Nmm; SearchAngle is in radians. Plane differences are reintegration minus returned force.
- The JSONL records every run. The report uses each case's median warm time, then P50/P95/max across those per-case medians.

Report and remaining contracts
Report.py is the reproducible report for the 0.18.0.1 investigation (baseline commit 5f97766a).
It also reads the five named TRX files and the historical/actual domain-point snapshots. The numerical fixtures are intentionally not updated.
The S1 case exercises a jump of the existing resultant surface, despite the continuous concrete diagram; the solver is allowed to use a fallback.
Time/precision comparisons must distinguish this path, the direct Newton path, and the unchanged stress-block path.

Parabola-rectangle focus
IncludeFocus adds a fixed 1,896-case catalogue (576 ConstantN and 1,320 ConstantEccentricity cases), followed by the reverse-order control.
The catalogue uses six geometries, both failure domains, twelve moment azimuths and four fixed axial levels per geometry for ConstantN.
ConstantEccentricity includes N=+/-100 kN with e/D = 0.05, 0.20, 0.75, 2.00 (D = maximum dimension in metres), pure bending,
and the 24 original eccentricity cases. No actions are generated from either version's result. Call the executable directly as:
  dotnet <binary> <new-output.jsonl> 5 "" focus
FocusReport.py, called by Report.py, compares identical IDs accepted in every repetition by both versions. Newly accepted and lost cases
are listed separately. A second, stricter paired cohort also requires acceptance of the reintegrated plane in both versions.
The report includes tails by domain, reverse-order times, first-search P95, result/plane residuals, resistance changes and the original C2 tail cases.
It also requires a fresh paired 72-case control (low-before-check and low-after-check), produced by Run.ps1 -IncludeFocus.
When iterating on the candidate, preserve the unchanged baseline measurements and the previous candidate separately; never relabel intermediate
measurements as final. The final report explicitly distinguishes reused baseline timings, new candidate timings and the fresh paired control.
Per-case raw records and metadata remain in the audit directory; the committed CSV contains paired medians and diagnostics for every case.
The catalogue is structured and correlated by geometry/symmetry, not an independent random sample of production models.
