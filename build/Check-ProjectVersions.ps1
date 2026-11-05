<# Reject ambiguous, unconditional project versions before any build.
   Conditional properties are deliberately excluded: they may vary by target.
   -ProjectFiles allows checking an explicit set without traversing a checkout. #>
param([string[]]$ProjectFiles)
$ErrorActionPreference = 'Stop'
if (!$ProjectFiles) {
    $repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    $ProjectFiles = @(Get-ChildItem -LiteralPath $repository -Filter '*.csproj' -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|packages|TestResults|\.git)[\\/]' } |
        ForEach-Object { $_.FullName })
}
$issues = @()
foreach ($file in $ProjectFiles) {
    [xml]$project = Get-Content -LiteralPath $file -Raw
    foreach ($name in @('Version', 'AssemblyVersion', 'FileVersion')) {
        $nodes = @($project.SelectNodes("/*[local-name()='Project']/*[local-name()='PropertyGroup' and not(@Condition)]/*[local-name()='$name' and not(@Condition)]"))
        if ($nodes.Count -gt 1) {
            $issues += "${file}: $name definita $($nodes.Count) volte ($($nodes.InnerText -join ', '))."
        }
    }
}
if ($issues.Count) { throw "Versioni di progetto ambigue:`n  " + ($issues -join "`n  ") }
Write-Output "Versioni di progetto: $($ProjectFiles.Count) progetti controllati, nessuna duplicazione non condizionata."
