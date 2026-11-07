param(
    [string]$Executable,
    [string]$ReportDirectory,
    [switch]$Canli
)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
if (!$Executable) { $Executable = Join-Path $project 'Muzikcalar.exe' }
if (!$ReportDirectory) {
    $ReportDirectory = Join-Path $project ('artifacts\windows-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
$exe = (Resolve-Path -LiteralPath $Executable).Path
$reports = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ReportDirectory)
[void][IO.Directory]::CreateDirectory($reports)
$checks = @('search-check', 'library-check', 'local-playback-check', 'visual-check')
if ($Canli) { $checks += 'self-test' }
foreach ($check in $checks) {
    $report = Join-Path $reports ($check + '.txt')
    $process = Start-Process -FilePath $exe -ArgumentList @("--$check", ('"' + $report + '"')) -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(240000)) {
        Stop-Process -Id $process.Id
        throw "$check zaman aşımına uğradı."
    }
    if ($process.ExitCode -ne 0) { throw "$check başarısız. Rapor: $report.failed.txt" }
    Get-Content -LiteralPath $report
}
Write-Host "Windows doğrulaması tamamlandı. Raporlar $reports"
