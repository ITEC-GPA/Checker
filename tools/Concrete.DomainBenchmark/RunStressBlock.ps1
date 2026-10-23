param([Parameter(Mandatory)][string]$Before, [Parameter(Mandatory)][string]$After,
    [Parameter(Mandatory)][string]$Output, [int]$Repeats=5, [string]$IntegrationOnly)
$ErrorActionPreference='Stop'
if (Test-Path -LiteralPath $Output) { throw "Output already exists: $Output" }
New-Item -ItemType Directory -Path $Output | Out-Null
$binaries=@{before=$Before; after=$After}
if ($IntegrationOnly) { $binaries['integrationOnly']=$IntegrationOnly }
$manifest=@{createdUtc=[DateTime]::UtcNow.ToString('o'); binaries=@{}}
foreach ($name in $binaries.Keys) {
    $manifest.binaries[$name]=@{path=[IO.Path]::GetFullPath($binaries[$name]); sha256=(Get-FileHash -LiteralPath $binaries[$name] -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Output 'campaign.json') -Encoding utf8
$env:DOTNET_TieredCompilation='0'
$runs=@(
    @{Name='before'; Binary=$Before; Filter='StressBlock'; Repeats=$Repeats; Catalogue='all'},
    @{Name='after'; Binary=$After; Filter='StressBlock'; Repeats=$Repeats; Catalogue='all'},
    @{Name='after-reverse'; Binary=$After; Filter='StressBlock/Plastic/'; Repeats=3; Catalogue='all'},
    @{Name='before-reverse'; Binary=$Before; Filter='StressBlock/Plastic/'; Repeats=3; Catalogue='all'},
    @{Name='low-before'; Binary=$Before; Filter=''; Repeats=$Repeats; Catalogue='stress-small'},
    @{Name='low-after'; Binary=$After; Filter=''; Repeats=$Repeats; Catalogue='stress-small'},
    @{Name='continuous-before'; Binary=$Before; Filter='!StressBlock'; Repeats=1; Catalogue='all'},
    @{Name='continuous-after'; Binary=$After; Filter='!StressBlock'; Repeats=1; Catalogue='all'}
)
if ($IntegrationOnly) { $runs += @{Name='integration-only'; Binary=$IntegrationOnly; Filter='StressBlock'; Repeats=3; Catalogue='all'} }
foreach ($run in $runs) {
    Write-Output ('Starting '+$run.Name)
    & dotnet $run.Binary (Join-Path $Output ($run.Name+'.jsonl')) $run.Repeats $run.Filter $run.Catalogue *> (Join-Path $Output ($run.Name+'.log'))
    if ($LASTEXITCODE) { throw ('Benchmark failed: '+$run.Name) }
    Write-Output ('Completed '+$run.Name)
}
& dotnet $After replay (Join-Path $Output 'before.jsonl') (Join-Path $Output 'before-planes-reintegrated.jsonl')
if ($LASTEXITCODE) { throw 'Reintegration of the baseline planes failed' }
