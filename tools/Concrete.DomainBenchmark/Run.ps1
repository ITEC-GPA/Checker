param([Parameter(Mandatory=$true)][string]$Before, [Parameter(Mandatory=$true)][string]$After,
      [Parameter(Mandatory=$true)][string]$Output, [int]$Repeats=5, [switch]$IncludeFocus)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$env:DOTNET_TieredCompilation='0'
$runs=@(
    @{Name='before'; Binary=$Before; Filter=$null; Repeats=$Repeats},
    @{Name='after'; Binary=$After; Filter=$null; Repeats=$Repeats},
    @{Name='after-reverse'; Binary=$After; Filter='!StressBlock'; Repeats=3},
    @{Name='before-reverse'; Binary=$Before; Filter='!StressBlock'; Repeats=3}
)
if ($IncludeFocus) {
    $runs=@(
        @{Name='focus-before'; Binary=$Before; Filter=''; Repeats=$Repeats; Catalogue='focus'},
        @{Name='focus-after'; Binary=$After; Filter=''; Repeats=$Repeats; Catalogue='focus'},
        @{Name='focus-after-reverse'; Binary=$After; Filter=''; Repeats=3; Catalogue='focus'},
        @{Name='focus-before-reverse'; Binary=$Before; Filter=''; Repeats=3; Catalogue='focus'}
    )+$runs
    $runs+=@(
        @{Name='low-before-check'; Binary=$Before; Filter='/Plastic/ConstantEccentricity/-1/0.05/'; Repeats=$Repeats; Catalogue='focus'},
        @{Name='low-after-check'; Binary=$After; Filter='/Plastic/ConstantEccentricity/-1/0.05/'; Repeats=$Repeats; Catalogue='focus'}
    )
}
foreach($run in $runs) {
    $result=Join-Path $Output ($run.Name+'.jsonl')
    if (Test-Path -LiteralPath $result) { throw "Result already exists: $result" }
    $arguments=@($run.Binary,$result,$run.Repeats)
    if($run.Filter) { $arguments+=$run.Filter }
    elseif($run.Catalogue) { $arguments+=''; }
    if($run.Catalogue) { $arguments+=$run.Catalogue }
    Write-Output ('Starting '+$run.Name)
    & dotnet @arguments *> (Join-Path $Output ($run.Name+'.log'))
    if($LASTEXITCODE) { throw ('Benchmark failed: '+$run.Name) }
    Write-Output ('Completed '+$run.Name)
}
