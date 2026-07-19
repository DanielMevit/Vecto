<#
    Windows build: dotnet publish -> single-file exe + portable ZIP (+ MSIX).

    KEEP THIS FILE PURE ASCII. Windows PowerShell 5.1 reads a BOM-less .ps1 as ANSI
    (cp1252), so a UTF-8 em-dash arrives as three bytes whose last one is a curly
    quote -- which PowerShell honours as a string terminator and everything after
    silently unbalances. (Learned the hard way in Laydown.)

    Outputs (in publish\):
      Vecto-<version>.exe              single self-contained app, no install
      Vecto-<version>-win-x64.zip      portable: app + vecto CLI + portable.txt
                                       (settings live beside the exe)
      Vecto-<version>-windows-x64.msix sideload package, self-signed
      Vecto-<version>-store.msix       with -Store: UNSIGNED, Partner Center upload

    Usage:
        powershell -ExecutionPolicy Bypass -File packaging\windows\build.ps1
        ... -SkipPublish   reuse existing publish\app-* outputs
        ... -SkipMsix      exe + portable ZIP only
        ... -Store         Store package; identity comes from the environment:
                             VECTO_STORE_IDENTITY_NAME      e.g. 12345DanielMevit.Vecto
                             VECTO_STORE_PUBLISHER          e.g. CN=A1B2C3D4-....
                             VECTO_STORE_PUBLISHER_DISPLAY  publisher display name
                           (Partner Center: your app -> Product management ->
                            Product identity.)
#>
param(
    [switch]$SkipPublish,
    [switch]$SkipMsix,
    [switch]$Store
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path "$PSScriptRoot\..\..").Path
Push-Location $root

# -- version: read from Directory.Build.props, never restated --
$props = Get-Content "$root\Directory.Build.props" -Raw
if ($props -notmatch '<Version>(\d+\.\d+\.\d+)</Version>') {
    Write-Error "Directory.Build.props must carry a three-part <Version>; MSIX appends the fourth."
}
$version = $Matches[1]
$appVersion = "$version.0"

Write-Host "=== Vecto $version -- Windows x64 build ===" -ForegroundColor Cyan

$outDir = "$root\publish"
$stageDir = "$outDir\msix_stage"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

# -- 1. dotnet publish ---------------------------------
if (-not $SkipPublish) {
    Write-Host "`n--- dotnet publish ---" -ForegroundColor Cyan
    # folder layout for MSIX (packaged apps want real files, not a self-extractor)
    dotnet publish Vecto.App -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=false -o "$outDir\app-folder" --nologo
    if ($LASTEXITCODE -ne 0) { Write-Error "publish (app folder) failed" }
    # single file for the no-install download
    dotnet publish Vecto.App -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
        -o "$outDir\app-single" --nologo
    if ($LASTEXITCODE -ne 0) { Write-Error "publish (app single) failed" }
    dotnet publish Vecto.Cli -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o "$outDir\cli-single" --nologo
    if ($LASTEXITCODE -ne 0) { Write-Error "publish (cli) failed" }
}
if (-not (Test-Path "$outDir\app-folder\Vecto.exe")) {
    Write-Error "No publish output -- run without -SkipPublish"
}

Copy-Item "$outDir\app-single\Vecto.exe" "$outDir\Vecto-$version.exe" -Force
Write-Host "[ok] $outDir\Vecto-$version.exe" -ForegroundColor Green

# -- 2. Portable ZIP (app + CLI, settings beside the exe) --
Write-Host "`n--- Portable ZIP ---" -ForegroundColor Cyan
$portableDir = "$outDir\Vecto-$version-win-x64"
if (Test-Path $portableDir) { Remove-Item $portableDir -Recurse -Force }
New-Item -ItemType Directory -Path $portableDir | Out-Null
Copy-Item "$outDir\app-single\Vecto.exe" $portableDir
# Windows filenames are case-insensitive: the CLI's vecto.exe beside the app's
# Vecto.exe silently overwrites the app, so the CLI ships in cli\.
New-Item -ItemType Directory -Path "$portableDir\cli" | Out-Null
Copy-Item "$outDir\cli-single\vecto.exe" "$portableDir\cli\"
Copy-Item "$root\LICENSE" $portableDir
Copy-Item "$root\README.md" $portableDir
# the marker file: Vecto.App stores settings.json beside the exe when it exists
Set-Content "$portableDir\portable.txt" "This file keeps Vecto portable: settings are written beside the exe instead of %APPDATA%." -Encoding UTF8
@"
Vecto $version -- portable (x64)

Vecto.exe is the app; cli\vecto.exe is the command-line tracer (add the cli
folder to PATH to call it from any terminal). Nothing is installed; settings
live in settings.json in this folder because portable.txt is present -- delete
portable.txt if you prefer them in %APPDATA%\Vecto.

Windows may warn that the publisher is unknown: the binaries are unsigned.
More info -> Run anyway.
"@ | Set-Content "$portableDir\README-portable.txt" -Encoding UTF8

$zipPath = "$outDir\Vecto-$version-win-x64.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$portableDir\*" -DestinationPath $zipPath
Write-Host "[ok] $zipPath" -ForegroundColor Green

# -- 3. MSIX ------------------------------------------
if ($SkipMsix) {
    Write-Host "`n[skip] MSIX" -ForegroundColor Yellow
    Pop-Location
    exit 0
}

$sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path "$($_.FullName)\x64\makeappx.exe" } |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $sdk) {
    Write-Host "[skip] MSIX -- no Windows 10 SDK found (exe + ZIP are built)" -ForegroundColor Yellow
    Pop-Location
    exit 0
}
$makeappx = "$($sdk.FullName)\x64\makeappx.exe"
$signtool = "$($sdk.FullName)\x64\signtool.exe"
Write-Host "`n--- MSIX (SDK $($sdk.Name)) ---" -ForegroundColor Cyan

if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
New-Item -ItemType Directory -Path $stageDir | Out-Null
Copy-Item "$outDir\app-folder\*" $stageDir -Recurse

# stamp the version into the staged manifest (the checked-in file is a template)
$manifest = Get-Content "$root\AppxManifest.xml" -Raw
$manifest = [regex]::Replace($manifest, '(<Identity[^>]*?\sVersion=")[^"]+(")', "`${1}$appVersion`${2}")
if ($manifest -notmatch [regex]::Escape($appVersion)) {
    Write-Error "Could not stamp version $appVersion into AppxManifest.xml"
}
if ($Store) {
    foreach ($required in @("VECTO_STORE_IDENTITY_NAME", "VECTO_STORE_PUBLISHER", "VECTO_STORE_PUBLISHER_DISPLAY")) {
        if (-not (Get-Item "env:$required" -ErrorAction SilentlyContinue).Value) {
            Write-Error "-Store needs $required (see Partner Center -> Product identity)"
        }
    }
    $manifest = [regex]::Replace($manifest, '(<Identity[^>]*?\sName=")[^"]+(")', "`${1}$($env:VECTO_STORE_IDENTITY_NAME)`${2}")
    $manifest = [regex]::Replace($manifest, '(<Identity[^>]*?\sPublisher=")[^"]+(")', "`${1}$($env:VECTO_STORE_PUBLISHER)`${2}")
    $manifest = [regex]::Replace($manifest, '(<PublisherDisplayName>)[^<]+(</PublisherDisplayName>)', "`${1}$($env:VECTO_STORE_PUBLISHER_DISPLAY)`${2}")
    Write-Host "[ok] Store identity stamped: $($env:VECTO_STORE_IDENTITY_NAME)" -ForegroundColor Green
}
Set-Content "$stageDir\AppxManifest.xml" $manifest -Encoding UTF8

New-Item -ItemType Directory -Path "$stageDir\assets\msix" -Force | Out-Null
Copy-Item "$root\assets\msix\*" "$stageDir\assets\msix\"

# Sideload builds sign with a stable self-signed cert (certs\ is gitignored -- the
# PFX is a secret). Store builds skip signing: the Store signs the upload itself.
if (-not $Store) {
    $certDir = "$root\certs"
    $pfxPath = "$certDir\Vecto.pfx"
    $cerPath = "$certDir\Vecto.cer"
    $pfxPassword = if ($env:VECTO_CERT_PASSWORD) { $env:VECTO_CERT_PASSWORD } else { "Vecto2026" }
    if (-not (Test-Path $pfxPath)) {
        Write-Host "Creating a self-signed certificate (first run, local builds only)" -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $certDir -Force | Out-Null
        $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=VectoTeam" `
            -KeyUsage DigitalSignature -FriendlyName "Vecto" -CertStoreLocation "Cert:\CurrentUser\My" `
            -NotAfter (Get-Date).AddYears(5) `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
        $pw = ConvertTo-SecureString -String $pfxPassword -Force -AsPlainText
        Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $pfxPath -Password $pw | Out-Null
        Export-Certificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $cerPath | Out-Null
    }
}

$suffix = if ($Store) { "store" } else { "windows-x64" }
$msixPath = "$outDir\Vecto-$version-$suffix.msix"
if (Test-Path $msixPath) { Remove-Item $msixPath -Force }
& $makeappx pack /d $stageDir /p $msixPath /o
if ($LASTEXITCODE -ne 0) { Write-Error "makeappx failed" }
if ($Store) {
    Write-Host "[ok] $msixPath (unsigned, for Partner Center upload)" -ForegroundColor Green
    Pop-Location
    exit 0
}
& $signtool sign /fd SHA256 /a /f $pfxPath /p $pfxPassword $msixPath
if ($LASTEXITCODE -ne 0) { Write-Error "signtool failed" }
Copy-Item $cerPath "$outDir\Vecto-msix-signing.cer"

Write-Host "[ok] $msixPath" -ForegroundColor Green
Write-Host "[ok] $outDir\Vecto-msix-signing.cer (users trust this once)" -ForegroundColor Green
Write-Host "`nTrust the certificate once (elevated):" -ForegroundColor Yellow
Write-Host "  Import-Certificate -FilePath `"$cerPath`" -CertStoreLocation Cert:\LocalMachine\TrustedPeople"
Pop-Location
