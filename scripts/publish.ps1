param([ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root "artifacts/$Runtime"
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("foxtail-publish-" + [guid]::NewGuid())
try {
    New-Item -ItemType Directory -Path $stage | Out-Null
    dotnet publish (Join-Path $root 'src/FoxTail.Desktop') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -o $stage
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    Copy-Item (Join-Path $root 'README.md') $stage
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    Copy-Item "$stage/*" $out -Recurse -Force
    $archive = Join-Path $root "artifacts/FoxTail-$Runtime.zip"
    Compress-Archive -Path "$stage/*" -DestinationPath $archive -Force
    Write-Host "Portable Windows package: $archive"
} finally {
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
}
