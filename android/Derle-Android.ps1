param(
    [switch]$Offline,
    [string]$BuildDirectory,
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Muzikcalar.apk')
)

$ErrorActionPreference = 'Stop'
$destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
if (!$BuildDirectory) { $BuildDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/android' }
$buildRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($BuildDirectory)
$signingConfig = $env:MUZIKCALAR_SIGNING_PROPERTIES
if (!$signingConfig) {
    $signingConfig = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Muzikcalar/signing/release.properties'
}
if (!(Test-Path -LiteralPath $signingConfig -PathType Leaf)) {
    throw 'Proje dışındaki Android imzalama ayarları bulunamadı.'
}
Push-Location $PSScriptRoot
try {
    $gradleArgs = @(':app:assembleRelease', '--console=plain', "-PmuzikcalarBuildDir=$buildRoot")
    if ($Offline) { $gradleArgs += '--offline' }
    & '.\gradlew.bat' @gradleArgs
    if ($LASTEXITCODE -ne 0) { throw 'Android derlemesi başarısız.' }
    $apk = Join-Path $buildRoot 'app/outputs/apk/release/app-release.apk'
    $staged = $destination + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        Copy-Item -LiteralPath $apk -Destination $staged
        $hash = (Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash.ToLowerInvariant()
        if (Test-Path -LiteralPath $destination) { [IO.File]::Replace($staged, $destination, [NullString]::Value) }
        else { [IO.File]::Move($staged, $destination) }
        [IO.File]::WriteAllText($destination + '.sha256', $hash + '  ' + [IO.Path]::GetFileName($destination) + [Environment]::NewLine)
    } finally {
        if (Test-Path -LiteralPath $staged) { Remove-Item -LiteralPath $staged -Force }
    }
    Write-Host ('APK hazır: ' + $destination)
} finally { Pop-Location }
