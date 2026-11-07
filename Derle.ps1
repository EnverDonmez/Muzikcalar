param([string]$OutputPath = (Join-Path $PSScriptRoot 'Muzikcalar.exe'))

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler -PathType Leaf)) { throw '.NET Framework 4.8 Windows derleyicisi bulunamadı.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Recurse -File -Filter '*.cs' | Sort-Object FullName | ForEach-Object FullName)
$output = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
$icon = Join-Path $PSScriptRoot 'assets\branding\muzikcalar-icon.ico'
$stagingFolder = Join-Path ([IO.Path]::GetDirectoryName($output)) ('.windows-build-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($stagingFolder)
$staged = Join-Path $stagingFolder ([IO.Path]::GetFileName($output))
try {
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$staged" "/win32icon:$icon" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Web.dll /reference:System.Net.Http.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'Windows derlemesi başarısız.' }
    for ($attempt = 0; ; $attempt++) {
        try {
            if (Test-Path -LiteralPath $output) { [IO.File]::Replace($staged, $output, [NullString]::Value) }
            else { [IO.File]::Move($staged, $output) }
            break
        } catch {
            $code = $_.Exception.GetBaseException().HResult -band 0xffff
            if ($code -notin 32,33) { throw }
            if ($attempt -ge 9) { throw "EXE kullanımda. Müzikçalar’ı kapatıp derlemeyi yeniden çalıştır. $output" }
            Start-Sleep -Milliseconds 250
        }
    }
    Write-Host "Windows EXE hazır. $output"
} finally {
    if (Test-Path -LiteralPath $staged) { Remove-Item -LiteralPath $staged -Force }
    [IO.Directory]::Delete($stagingFolder)
}
