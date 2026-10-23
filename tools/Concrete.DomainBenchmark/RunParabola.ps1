param([Parameter(Mandatory)][string]$Before, [Parameter(Mandatory)][string]$After,
    [Parameter(Mandatory)][string]$AnalyticGauss, [Parameter(Mandatory)][string]$Output)
$ErrorActionPreference='Stop'
if (Test-Path -LiteralPath $Output) { throw "Output already exists: $Output" }
New-Item -ItemType Directory -Path $Output | Out-Null
$binaries=@{before=$Before; after=$After; gauss=$AnalyticGauss}
$manifest=@{createdUtc=[DateTime]::UtcNow.ToString('o'); binaries=@{}}
foreach ($name in $binaries.Keys) {
    $manifest.binaries[$name]=@{path=[IO.Path]::GetFullPath($binaries[$name]); sha256=(Get-FileHash -LiteralPath $binaries[$name] -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Output 'campaign.json') -Encoding utf8
$env:DOTNET_TieredCompilation='0'
$runs=@(
    @{Name='before'; Variant='before'; Filter='ParabolaRectangle'; Repeats=5; Catalogue='all'},
    @{Name='after'; Variant='after'; Filter='ParabolaRectangle'; Repeats=5; Catalogue='all'},
    @{Name='gauss'; Variant='gauss'; Filter='ParabolaRectangle'; Repeats=5; Catalogue='all'},
    @{Name='focus-before'; Variant='before'; Filter=''; Repeats=5; Catalogue='focus'},
    @{Name='focus-after'; Variant='after'; Filter=''; Repeats=5; Catalogue='focus'},
    @{Name='focus-gauss'; Variant='gauss'; Filter=''; Repeats=3; Catalogue='focus'},
    @{Name='reverse-after'; Variant='after'; Filter=''; Repeats=3; Catalogue='focus'},
    @{Name='reverse-before'; Variant='before'; Filter=''; Repeats=3; Catalogue='focus'},
    @{Name='mesh-before'; Variant='before'; Filter=''; Repeats=5; Catalogue='mesh'},
    @{Name='mesh-after'; Variant='after'; Filter=''; Repeats=5; Catalogue='mesh'},
    @{Name='mesh-gauss'; Variant='gauss'; Filter=''; Repeats=3; Catalogue='mesh'},
    @{Name='unchanged-before'; Variant='before'; Filter='!ParabolaRectangle'; Repeats=1; Catalogue='all'},
    @{Name='unchanged-after'; Variant='after'; Filter='!ParabolaRectangle'; Repeats=1; Catalogue='all'}
)
foreach ($run in $runs) {
    Write-Output ('Starting '+$run.Name)
    & dotnet $binaries[$run.Variant] (Join-Path $Output ($run.Name+'.jsonl')) $run.Repeats $run.Filter $run.Catalogue *> (Join-Path $Output ($run.Name+'.log'))
    if ($LASTEXITCODE) { throw ('Benchmark failed: '+$run.Name) }
    Write-Output ('Completed '+$run.Name)
}
foreach ($name in @('before','gauss','focus-before','focus-gauss','mesh-before','mesh-gauss')) {
    & dotnet $After replay (Join-Path $Output ($name+'.jsonl')) (Join-Path $Output ($name+'-reintegrated.jsonl'))
    if ($LASTEXITCODE) { throw ('Reintegration failed: '+$name) }
}
& dotnet $AnalyticGauss replay (Join-Path $Output 'before.jsonl') (Join-Path $Output 'before-analytic-gauss.jsonl')
if ($LASTEXITCODE) { throw 'Same-plane analytic Gauss comparison failed' }
