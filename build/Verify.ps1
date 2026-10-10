param([string]$ModelRoot)
# GPCModelChecker sits downstream of Model: build the sibling Model libraries from their pinned bundle, run ModelChecker.Tests
# and compare the public API of GPCModelChecker and GPCChecker.Concrete with build/api (baselines moved from Model on 10/10/2026).
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$ModelRoot) { $ModelRoot = Join-Path $repository '..\Model' }
$ModelRoot = [IO.Path]::GetFullPath($ModelRoot)
Push-Location $repository
try {
    & (Join-Path $ModelRoot 'build\Prepare-Dependencies.ps1')
    foreach ($project in @('GPC.Model.Persistence\GPC.Model.Persistence.csproj', 'ModelData\GPCModelData.csproj')) {
        dotnet build (Join-Path $ModelRoot $project) -c Release -nologo -v:q
        if ($LASTEXITCODE) { throw "Model build failed: $project" }
    }
    dotnet test ModelChecker.Tests/ModelChecker.Tests.csproj -c Release -p:WarningLevel=0 --logger trx --results-directory TestResults/ModelChecker
    if ($LASTEXITCODE) { throw 'Tests failed: ModelChecker.Tests' }
    dotnet run --project (Join-Path $ModelRoot 'tools\ApiSurface\ApiSurface.csproj') -c Release -- check ModelChecker.Tests/bin/Release/net6.0 build/api GPCModelChecker GPCChecker.Concrete
    if ($LASTEXITCODE) { throw 'Public API compatibility check failed.' }
} finally { Pop-Location }
