param(
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$ModelRoot = '',
    [string]$SourceRevision = '',
    [ValidateSet('Baseline', 'HollowColdFormed', 'HollowWelded', 'HollowAlpha049Only', 'InteractionGamma105')]
    [string]$Scenario = 'Baseline'
)
$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot '../../GPCChecker.Test.Steel/EN1993p11Test.cs'
$generatedDirectory = Join-Path $PSScriptRoot 'obj'
New-Item -ItemType Directory -Path $generatedDirectory -Force | Out-Null
$snapshotPath = Join-Path $generatedDirectory 'LegacySource.txt'
if ($SourceRevision) {
    $repositoryPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $sourceCommit = & git -C $repositoryPath rev-parse --verify --end-of-options "${SourceRevision}^{commit}"
    if ($LASTEXITCODE -ne 0) { throw "Cannot resolve source revision: $SourceRevision" }
    $sourceLines = & git -C $repositoryPath show "${sourceCommit}:GPCChecker.Test.Steel/EN1993p11Test.cs"
    if ($LASTEXITCODE -ne 0) { throw "Cannot read legacy source at $sourceCommit" }
    # Recreate the repository's CRLF checkout representation in the disposable snapshot.
    [IO.File]::WriteAllText($snapshotPath, ($sourceLines -join "`r`n") + "`r`n", [Text.UTF8Encoding]::new($false))
    Write-Output "Legacy source revision: $sourceCommit"
} else {
    [IO.File]::WriteAllBytes($snapshotPath, [IO.File]::ReadAllBytes($sourcePath))
}
$source = [IO.File]::ReadAllText($snapshotPath)
# Instrument only a disposable copy: legacy inputs, expressions and expected values stay unchanged.
$source = [regex]::Replace($source, '(\b\w+)\.PerformCheck\(\);',
    'LegacyDiagnostics.Configure($1); $0 LegacyDiagnostics.Capture($1);')
$source = $source.Replace('Assert.', 'DiagnosticAssert.')
[IO.File]::WriteAllText((Join-Path $generatedDirectory 'LegacyTests.g.cs'), $source)
$buildArguments = @('build', $PSScriptRoot, '-c', 'Release', '-v:q', '-nologo')
if ($ModelRoot) { $buildArguments += "-p:SteelModelRoot=$ModelRoot" }
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Diagnostic build failed: $LASTEXITCODE" }
& dotnet (Join-Path $PSScriptRoot 'bin/Release/net8.0/Steel.LegacyDiagnostics.dll') $OutputPath $snapshotPath $Scenario
if ($LASTEXITCODE -ne 0) { throw "Diagnostic execution failed: $LASTEXITCODE" }
