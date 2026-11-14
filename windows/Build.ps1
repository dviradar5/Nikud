$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
# The legacy compiler can lose non-ASCII characters in the working directory.
$stage = New-Item -ItemType Directory -Path (Join-Path $env:TEMP ('nikud-build-' + [guid]::NewGuid()))
Push-Location -LiteralPath $stage.FullName
try {
    $sources = 'Nikud.cs', 'Desktop.cs', 'Windows.cs', 'Engine.cs'
    foreach ($file in $sources) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination . }
    & $compiler /nologo /target:winexe /platform:anycpu /codepage:65001 /out:Nikud.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll $sources
    if ($LASTEXITCODE -ne 0) { throw "The Windows build failed with exit code $LASTEXITCODE." }
    Copy-Item -LiteralPath Nikud.exe -Destination (Join-Path $project 'Nikud.exe')
} finally {
    foreach ($file in ($sources + 'Nikud.exe')) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Pop-Location
    Remove-Item -LiteralPath $stage.FullName
}
