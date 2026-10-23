param([Parameter(Mandatory=$true)][string]$Output, [string]$RepositoryRoot, [string]$Source)
$ErrorActionPreference='Stop'
if (!$Source) { $Source=Join-Path $PSScriptRoot '../../GPCChecker.Concrete' }
if (!$RepositoryRoot) { $RepositoryRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')) }
$source=[IO.Path]::GetFullPath($Source)
$copy=Join-Path $Output 'source'
if (Test-Path -LiteralPath $Output) { throw "Output already exists: $Output" }
New-Item -ItemType Directory -Path $copy -Force | Out-Null
Get-ChildItem -LiteralPath $source -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object {
    $relative=[IO.Path]::GetRelativePath([IO.Path]::GetFullPath($source),$_.FullName)
    $dest=Join-Path $copy $relative
    New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($dest)) | Out-Null
    Copy-Item -LiteralPath $_.FullName -Destination $dest
}
# Instrument only the throwaway benchmark copy; production DLLs contain no counters.
function Insert([string]$file,[string]$needle,[string]$code) {
    $path=Join-Path $copy $file; $text=[IO.File]::ReadAllText($path).Replace("`r`n","`n")
    $needle=$needle.Replace("`r`n","`n")
    if (!$text.Contains($needle)) { throw "Instrumentation marker missing in $file : $needle" }
    [IO.File]::WriteAllText($path,$text.Replace($needle,$needle + $code))
}
Insert 'SectionSolvers/SectionSolver.cs' "internal virtual ForceTuple CalculateForceResultantForDomain(StrainPlane strainPlane, Dictionary<int, bool> rebarIsInsideAssociation)`r`n        {" "`r`n            System.Threading.Interlocked.Increment(ref global::DomainBenchmarkCounters.Integrations);"
Insert 'SectionSolvers/DomainPointStrategyIterative.cs' 'increment = CalculateIncrement(' ''
$legacy=Join-Path $copy 'SectionSolvers/DomainPointStrategyIterative.cs'
$text=[IO.File]::ReadAllText($legacy).Replace('increment = CalculateIncrement(', 'System.Threading.Interlocked.Increment(ref global::DomainBenchmarkCounters.Iterations); increment = CalculateIncrement(')
[IO.File]::WriteAllText($legacy,$text)
$newton=Join-Path $copy 'SectionSolvers/DomainPointStrategyNewton.cs'
if (Test-Path $newton) {
    $text=[IO.File]::ReadAllText($newton).Replace('// Domain search iteration.', 'System.Threading.Interlocked.Increment(ref global::DomainBenchmarkCounters.Iterations);')
    [IO.File]::WriteAllText($newton,$text)
}
$bisect=Join-Path $copy 'SectionSolvers/DomainPointStrategyBisection.cs'
$text=[IO.File]::ReadAllText($bisect).Replace("`r`n","`n").Replace("for (int iteration = 0; iteration < MaxIterations; iteration++)`n            {", "for (int iteration = 0; iteration < MaxIterations; iteration++)`n            {`n                System.Threading.Interlocked.Increment(ref global::DomainBenchmarkCounters.BracketIterations);")
[IO.File]::WriteAllText($bisect,$text)
dotnet build "$PSScriptRoot/Concrete.DomainBenchmark.csproj" -c Release -nologo -v:q "-p:SolverSource=$copy" "-p:RepositoryRoot=$RepositoryRoot" "-p:OutputPath=$Output/bin/"
if ($LASTEXITCODE) { throw 'Benchmark build failed' }
