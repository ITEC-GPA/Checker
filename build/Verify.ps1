<#
Every Checker suite, as build/RILASCIO.txt requires before a release:
- the sibling Model libraries from their pinned bundle, then the Checker libraries and GPCModelChecker, all in Release;
- net472 Test.Concrete and Test.Steel: packages.config restore, MSBuild without project references, vstest x64;
- net8 CompositeBridge, Geotechnics and Calculation and net6 ModelChecker.Tests: dotnet test;
- public API of GPCModelChecker and GPCChecker.Concrete against build/api (tool: Model/tools/ApiSurface).
A failure is admitted only by suite and exact test name from build/known-failures.json. Writes <Results>\summary.json.

  powershell -NoProfile -ExecutionPolicy Bypass -File build\Verify.ps1 [-ModelRoot <Model checkout>] [-Suites all|modelchecker] [-Results <folder>]
#>
param([string]$ModelRoot, [ValidateSet('all', 'modelchecker')][string]$Suites = 'all', [string]$Results)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$ModelRoot) { $ModelRoot = Join-Path $repository '..\Model' }
$ModelRoot = [IO.Path]::GetFullPath($ModelRoot)
if (!$Results) { $Results = Join-Path $repository 'TestResults\Verify' }
$Results = [IO.Path]::GetFullPath($Results)
if (Test-Path -LiteralPath $Results) { Remove-Item -LiteralPath $Results -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Results | Out-Null

$known = @{}
foreach ($p in (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'known-failures.json') -Raw | ConvertFrom-Json).suites.PSObject.Properties) {
    $known[$p.Name] = @($p.Value.tests)
}
function Invoke-Checked([string]$what, [scriptblock]$action) {
    & $action
    if ($LASTEXITCODE) { throw "$what (uscita $LASTEXITCODE)" }
}
function VisualStudio([string[]]$relative) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (!(Test-Path -LiteralPath $vswhere)) { throw 'vswhere non trovato: serve Visual Studio 2022 con MSBuild e la piattaforma di test.' }
    $root = (& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath | Select-Object -First 1)
    foreach ($r in $relative) { $path = Join-Path $root $r; if (Test-Path -LiteralPath $path) { return $path } }
    throw "Visual Studio in $root : nessuno di $($relative -join ', ')."
}
function Read-Trx([string]$path) {
    [xml]$x = Get-Content -LiteralPath $path -Raw
    $ns = New-Object Xml.XmlNamespaceManager $x.NameTable
    $ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $rows = @($x.SelectNodes('//t:UnitTestResult', $ns))
    [pscustomobject]@{
        Passed = @($rows | Where-Object { $_.outcome -eq 'Passed' }).Count
        Failed = @($rows | Where-Object { $_.outcome -eq 'Failed' } | ForEach-Object { $_.testName })
        Other = @($rows | Where-Object { $_.outcome -ne 'Passed' -and $_.outcome -ne 'Failed' }).Count
    }
}

Push-Location $repository
try {
    & (Join-Path $ModelRoot 'build\Prepare-Dependencies.ps1') | Out-Null
    foreach ($project in @('GPC.Model.Persistence\GPC.Model.Persistence.csproj', 'ModelData\GPCModelData.csproj')) {
        Invoke-Checked "Build Model $project" { dotnet build (Join-Path $ModelRoot $project) -c Release -nologo -v:q }
    }
    $net = @('ModelChecker.Tests\ModelChecker.Tests.csproj')
    $classic = @()
    if ($Suites -eq 'all') {
        foreach ($p in @('GPCChecker.Concrete', 'GPCChecker.Steel', 'GPCChecker.CompositeBridge', 'GPCChecker.Geotechnics', 'ModelChecker\GPCModelChecker')) {
            $file = if ($p.Contains('\')) { "$p.csproj" } else { "$p\$p.csproj" }
            Invoke-Checked "Build $p" { dotnet build $file -c Release -nologo -v:q }
        }
        $msbuild = VisualStudio @('MSBuild\Current\Bin\MSBuild.exe')
        $vstest = VisualStudio @('Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe', 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe')
        Invoke-Checked 'Restore packages.config' { & $msbuild GPCChecker.sln /t:Restore /p:RestorePackagesConfig=true /nologo /v:q }
        $classic = @('GPCChecker.Test.Concrete', 'GPCChecker.Test.Steel')
        foreach ($p in $classic) {
            Invoke-Checked "Build $p" { & $msbuild "$p\$p.csproj" /p:Configuration=Release /p:BuildProjectReferences=false /nologo /v:m }
        }
        $net = @('GPCChecker.Test.CompositeBridge\GPCChecker.Test.CompositeBridge.csproj', 'GPCChecker.Test.Geotechnics\GPCChecker.Test.Geotechnics.csproj',
            'GPCChecker.Test.Calculation\GPCChecker.Test.Calculation.csproj') + $net
    }
    foreach ($p in $net) { Invoke-Checked "Build $p" { dotnet build $p -c Release -nologo -v:q } }

    # Run the suites in parallel: each one writes <name>.trx and <name>.log into $Results.
    $jobs = @()
    foreach ($p in $classic) {
        $jobs += Start-Job -ArgumentList $vstest, (Join-Path $repository "$p\bin\Release\$p.dll"), $Results, $p -ScriptBlock {
            param($vstest, $dll, $results, $name)
            & $vstest $dll /Platform:x64 "/logger:trx;LogFileName=$name.trx" "/ResultsDirectory:$results" *> (Join-Path $results "$name.log")
        }
    }
    foreach ($p in $net) {
        $jobs += Start-Job -ArgumentList (Join-Path $repository $p), $Results, ([IO.Path]::GetFileNameWithoutExtension($p)) -ScriptBlock {
            param($project, $results, $name)
            dotnet test $project -c Release -nologo --no-build "--logger:trx;LogFileName=$name.trx" --results-directory $results *> (Join-Path $results "$name.log")
        }
    }
    $jobs | Wait-Job | Out-Null
    $jobs | Remove-Job

    $summary = @(); $unexpected = @()
    foreach ($name in @($classic + ($net | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) }))) {
        $trx = Join-Path $Results "$name.trx"
        if (!(Test-Path -LiteralPath $trx)) { $unexpected += "${name}: nessun risultato (vedi $name.log)"; continue }
        $r = Read-Trx $trx
        $allowed = @($known[$name] | Where-Object { $_ })
        $bad = @($r.Failed | Where-Object { $allowed -notcontains $_ })
        $summary += [ordered]@{ name = $name; passed = $r.Passed; failed = $r.Failed.Count; other = $r.Other
            knownFailures = @($r.Failed | Where-Object { $allowed -contains $_ }); unexpectedFailures = $bad
            knownNowPassing = @($allowed | Where-Object { $r.Failed -notcontains $_ }) }
        $unexpected += @($bad | ForEach-Object { "${name}: $_" })
        if ($r.Passed + $r.Failed.Count -eq 0) { $unexpected += "${name}: nessun test eseguito" }
        Write-Output ("{0,-32} superati {1,4}  falliti {2,3} (ammessi {3})" -f $name, $r.Passed, $r.Failed.Count, ($r.Failed.Count - $bad.Count))
    }
    dotnet run --project (Join-Path $ModelRoot 'tools\ApiSurface\ApiSurface.csproj') -c Release -- check `
        (Join-Path $repository 'ModelChecker.Tests\bin\Release\net6.0') (Join-Path $repository 'build\api') GPCModelChecker GPCChecker.Concrete
    $api = ($LASTEXITCODE -eq 0)
    if (!$api) { $unexpected += 'API pubbliche di GPCModelChecker/GPCChecker.Concrete: rimozioni non approvate' }
    $json = [ordered]@{ suites = $Suites; created = (Get-Date).ToString('s'); results = $summary; apiCompatible = $api; unexpected = $unexpected } | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText((Join-Path $Results 'summary.json'), $json, (New-Object Text.UTF8Encoding($false)))
    if ($unexpected.Count) { throw "Verifica di Checker non superata:`n  " + ($unexpected -join "`n  ") }
    Write-Output "Checker: tutte le suite superate (fallimenti ammessi solo da build/known-failures.json)."
} finally { Pop-Location }
