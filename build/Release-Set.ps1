<#
Coherent release set of the GPC libraries (build/RILASCIO.txt): from clean sibling checkouts of Utilities, Geometry, Model
and Checker it checks that Model pins the committed Geometry and Utilities binaries of the set, runs every suite of Model
(build/Verify.ps1: Core, UnitTest, API) and of Checker (build/Verify.ps1: Concrete and Steel net472, CompositeBridge,
Geotechnics, Calculation, ModelChecker, API), then copies the twelve DLLs and writes release-set.json (versions, SHA-256,
commits, suite results). The builds are deterministic (Directory.Build.props), so the same commits give the same SHA-256
on any machine and in any folder.

  powershell -NoProfile -ExecutionPolicy Bypass -File build\Release-Set.ps1 -Out <new folder> [-Root <folder>] [-Previous <release-set.json>] [-Candidate]

  -Root       folder with the sibling checkouts; default: the parent folder of this repository.
  -Out        new folder for the DLLs, their PDB and XML documentation, release-set.json and the test results.
  -Previous   release-set.json of the last release: every DLL whose SHA-256 changed needs a higher assembly version.
              Versions follow the delegation in build/RILASCIO.txt (last component for fixes, second otherwise); this script never changes them.
  -Candidate  set for tests: branches and unpushed commits allowed, no version check, release-set.json marked candidate.
              ANTHEA installs a candidate only with Update-Snapshot.ps1 -AllowCandidate (tests, never a release).
#>
param([string]$Root, [Parameter(Mandatory)][string]$Out, [string]$Previous, [switch]$Candidate)
$ErrorActionPreference = 'Stop'
$checker = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$Root) { $Root = Split-Path $checker -Parent }
$Root = [IO.Path]::GetFullPath($Root)
$Out = [IO.Path]::GetFullPath($Out)
if (Test-Path -LiteralPath $Out) { throw "$Out esiste gia': scegliere una cartella nuova." }
$git = (Get-Command git -ErrorAction SilentlyContinue).Source
if (!$git) { $git = 'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe' }
function GitValue([string]$dir) {
    $eap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try { $value = @(& $git -C $dir @args 2>$null); $code = $LASTEXITCODE } finally { $ErrorActionPreference = $eap }
    [pscustomobject]@{ Code = $code; Text = (($value | ForEach-Object { "$_" }) -join "`n").Trim() }
}

# 1. Sources: clean trees; a release comes from commits on origin/master.
$repos = [ordered]@{}
foreach ($r in 'Utilities', 'Geometry', 'Model', 'Checker') {
    $dir = Join-Path $Root $r
    if (!(Test-Path -LiteralPath (Join-Path $dir '.git'))) { throw "Repository $r non trovato in $Root." }
    $status = (GitValue $dir status --porcelain=v1 --untracked-files=normal).Text
    if ($status) { throw "${r}: albero non pulito, il set nasce da commit:`n$status" }
    # Line endings enter the PDB document hashes, hence the DLL: a file written with LF where .gitattributes asks for CRLF
    # leaves the tree clean for git but changes the SHA-256 (release R of 12/10). Only a fresh checkout is accepted.
    $eol = @((GitValue $dir ls-files --eol).Text -split "`n" | Where-Object { $_ -match 'eol=crlf' -and $_ -match '\bw/(lf|mixed)\b' })
    if ($eol) { throw "${r}: a capo diversi da un checkout pulito, rifare il checkout:`n$($eol -join "`n")" }
    $commit = (GitValue $dir rev-parse HEAD).Text
    $onMaster = (GitValue $dir merge-base --is-ancestor $commit origin/master).Code -eq 0
    if (!$Candidate -and !$onMaster) { throw "${r}: $commit non e' su origin/master. Un rilascio parte da master pushato (-Candidate per le prove)." }
    $repos[$r] = [ordered]@{ commit = $commit; branch = (GitValue $dir rev-parse --abbrev-ref HEAD).Text; onOriginMaster = $onMaster }
}

# 2. Model pins exactly the committed Geometry and Utilities binaries of this set.
[xml]$pins = Get-Content -LiteralPath (Join-Path $Root 'Model\build\dependencies.props') -Raw
foreach ($pin in $pins.Project.ItemGroup.GpcDependency) {
    $source = [IO.Path]::GetFullPath((Join-Path (Join-Path $Root 'Model') $pin.Source))
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if ($hash -ne $pin.Sha256) { throw "Model fissa $($pin.Include) $($pin.AssemblyVersion) ($($pin.Sha256.Substring(0, 12))), il checkout fornisce $($hash.Substring(0, 12)): set incoerente." }
}

# 3. Every suite of both repositories (they also build all the libraries in Release).
$tests = Join-Path $Out 'TestResults'
New-Item -ItemType Directory -Force -Path $tests | Out-Null
& (Join-Path $Root 'Model\build\Verify.ps1') -Results (Join-Path $tests 'Model')
& (Join-Path $checker 'build\Verify.ps1') -ModelRoot (Join-Path $Root 'Model') -Results (Join-Path $tests 'Checker')
$suites = @()
foreach ($trx in Get-ChildItem -LiteralPath (Join-Path $tests 'Model') -Filter *.trx) {
    [xml]$x = Get-Content -LiteralPath $trx.FullName -Raw
    $counters = $x.TestRun.ResultSummary.Counters
    $suites += [ordered]@{ name = $trx.BaseName; repository = 'Model'; passed = [int]$counters.passed; failed = [int]$counters.failed; other = [int]$counters.notExecuted }
}
foreach ($s in (Get-Content -LiteralPath (Join-Path $tests 'Checker\summary.json') -Raw | ConvertFrom-Json).results) {
    $suites += [ordered]@{ name = $s.name; repository = 'Checker'; passed = $s.passed; failed = $s.failed; other = $s.other; knownFailures = @($s.knownFailures) }
}

# 4. DLLs and manifest.
$libraries = @(
    @('Utilities', 'GPCUtilities\bin\Release\netstandard2.0\GPCUtilities.dll'),
    @('Geometry', 'GPCGeometry\bin\Release\netstandard2.0\GPCGeometry.dll'),
    @('Geometry', 'DelaunayMesh\bin\Release\netstandard2.0\DelaunayMesh.dll'),
    @('Model', 'Model\bin\Release\netstandard2.0\GPCModel.dll'),
    @('Model', 'ModelData\bin\Release\netstandard2.0\GPCModelData.dll'),
    @('Model', 'GPC.Model.Persistence\bin\Release\netstandard2.0\GPC.Model.Persistence.dll'),
    @('Model', 'GPC Converter\bin\Release\netstandard2.0\GPC.Converter.dll'),
    @('Checker', 'GPCChecker.Concrete\bin\Release\netstandard2.0\GPCChecker.Concrete.dll'),
    @('Checker', 'GPCChecker.Steel\bin\Release\netstandard2.0\GPCChecker.Steel.dll'),
    @('Checker', 'GPCChecker.CompositeBridge\bin\Release\netstandard2.0\GPCChecker.CompositeBridge.dll'),
    @('Checker', 'GPCChecker.Geotechnics\bin\Release\netstandard2.0\GPCChecker.Geotechnics.dll'),
    @('Checker', 'ModelChecker\bin\Release\netstandard2.0\GPCModelChecker.dll')
)
$files = @()
foreach ($l in $libraries) {
    $path = Join-Path (Join-Path $Root $l[0]) $l[1]
    foreach ($companion in @($path, [IO.Path]::ChangeExtension($path, '.pdb'), [IO.Path]::ChangeExtension($path, '.xml'))) {
        if (Test-Path -LiteralPath $companion) { Copy-Item -LiteralPath $companion -Destination $Out }
    }
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($path)
    $files += [ordered]@{ file = Split-Path $path -Leaf; repository = $l[0]; source = $l[1].Replace('\', '/')
        assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($path).Version.ToString(); fileVersion = $info.FileVersion
        informationalVersion = $info.ProductVersion; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; commit = $repos[$l[0]].commit }
}

# 5. Versions: a changed DLL needs a higher version following build/RILASCIO.txt.
$problems = @()
if ($Previous -and !$Candidate) {
    $old = @{}
    foreach ($f in (Get-Content -LiteralPath $Previous -Raw | ConvertFrom-Json).files) { $old[$f.file] = $f }
    foreach ($f in $files) {
        $p = $old[$f.file]
        if ($p -and $p.sha256 -ne $f.sha256 -and [version]$f.assemblyVersion -le [version]$p.assemblyVersion) {
            $problems += "$($f.file): contenuto cambiato rispetto al set precedente con la versione $($f.assemblyVersion) (prima $($p.assemblyVersion)); assegnare una versione nuova secondo build/RILASCIO.txt."
        }
    }
} elseif (!$Candidate) {
    Write-Warning 'Nessun -Previous: controllo delle versioni non eseguito (solo per il primo set).'
}
$manifest = [ordered]@{ schema = 1; generator = 'Checker/build/Release-Set.ps1'; created = (Get-Date).ToString('s'); candidate = [bool]$Candidate
    previous = $Previous; repositories = $repos; files = $files; suites = $suites; dotnetSdk = (& dotnet --version).Trim() }
[IO.File]::WriteAllText((Join-Path $Out 'release-set.json'), ($manifest | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding($false)))
if ($problems.Count) { throw "Set non rilasciabile:`n  " + ($problems -join "`n  ") }
$files | ForEach-Object { "{0,-32} {1,-10} {2} {3}@{4}" -f $_.file, $_.assemblyVersion, $_.sha256.Substring(0, 12), $_.repository, $_.commit.Substring(0, 8) }
Write-Output "Set $(if ($Candidate) { 'candidato' } else { 'di rilascio' }) in $Out"
