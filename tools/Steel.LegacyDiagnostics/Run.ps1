param(
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$ModelRoot = ''
)
$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot '../../GPCChecker.Test.Steel/EN1993p11Test.cs'
$source = [IO.File]::ReadAllText($sourcePath)
# Instrument only a disposable copy: legacy inputs, expressions and expected values stay unchanged.
$source = [regex]::Replace($source, '(\b\w+)\.PerformCheck\(\);', '$0 LegacyDiagnostics.Capture($1);')
$source = $source.Replace('Assert.', 'DiagnosticAssert.')
$generatedDirectory = Join-Path $PSScriptRoot 'obj'
New-Item -ItemType Directory -Path $generatedDirectory -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $generatedDirectory 'LegacyTests.g.cs'), $source)
$buildArguments = @('build', $PSScriptRoot, '-c', 'Release', '-v:q', '-nologo')
if ($ModelRoot) { $buildArguments += "-p:SteelModelRoot=$ModelRoot" }
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Diagnostic build failed: $LASTEXITCODE" }
& dotnet (Join-Path $PSScriptRoot 'bin/Release/net8.0/Steel.LegacyDiagnostics.dll') $OutputPath $sourcePath
if ($LASTEXITCODE -ne 0) { throw "Diagnostic execution failed: $LASTEXITCODE" }
