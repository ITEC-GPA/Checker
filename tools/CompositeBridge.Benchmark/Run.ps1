param(
    [Parameter(Mandatory=$true)][string]$Out,
    [string]$Reference,
    [ValidateSet('serial','stations','variables')][string]$Execution = 'serial',
    [ValidateSet('Workstation','Server')][string]$GarbageCollector = 'Workstation',
    [int]$Degree = 8,
    [int]$Repetitions = 3
)
$ErrorActionPreference = 'Stop'
$bench = Join-Path $PSScriptRoot 'bin/Release/net8.0/CompositeBridge.Benchmark.dll'
if (!(Test-Path -LiteralPath $bench)) { throw 'Build the benchmark in Release before timing.' }
$Out = [IO.Path]::GetFullPath($Out)
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$oldTiered = $env:DOTNET_TieredCompilation
$oldGc = $env:DOTNET_gcServer
try {
    # Fixed runtime policy: exclude tier promotion during short runs. Every scenario starts in a fresh process.
    $env:DOTNET_TieredCompilation = '0'
    $env:DOTNET_gcServer = if ($GarbageCollector -eq 'Server') { '1' } else { '0' }
    foreach ($method in @('cumulative','history')) {
        foreach ($class4 in @('false','true')) {
            foreach ($stations in @(1,100)) {
                $key = "$method-class4-$class4-$stations"
                $arguments = @($bench,'--method',$method,'--class4',$class4,'--stations',"$stations",'--repetitions',"$Repetitions",
                    '--execution',$Execution,'--degree',"$Degree",'--out',(Join-Path $Out "$key.json"))
                if ($Reference) { $arguments += @('--reference',(Join-Path $Reference "$key.values.bin")) }
                & dotnet @arguments
                if ($LASTEXITCODE -ne 0) { throw "Benchmark failed: $key" }
            }
        }
    }
} finally { $env:DOTNET_TieredCompilation = $oldTiered; $env:DOTNET_gcServer = $oldGc }
