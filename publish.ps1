# Produit les exécutables autonomes (aucun runtime .NET requis).
#   dist\StealthMode-autonome : installation « normale » (données dans %LOCALAPPDATA%\StealthMode)
#   dist\StealthMode-portable : à copier sur une clé USB (fichier « portable » présent : les profils et réglages
#                               sont enregistrés dans le dossier StealthModeData, à côté du programme)
# Usage : powershell -ExecutionPolicy Bypass -File .\publish.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out  = Join-Path $root 'dist\StealthMode-autonome'
$usb  = Join-Path $root 'dist\StealthMode-portable'

$common = @(
    '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=none', '-p:DebugSymbols=false',
    '-o', $out
)

dotnet publish (Join-Path $root 'src\StealthMode.App\StealthMode.App.csproj') @common
if ($LASTEXITCODE -ne 0) { throw 'Echec de la publication de StealthMode' }
dotnet publish (Join-Path $root 'tools\StealthMode.Recovery\StealthMode.Recovery.csproj') @common
if ($LASTEXITCODE -ne 0) { throw 'Echec de la publication de StealthMode.Recovery' }

# Dossier portable : les deux exécutables + le fichier marqueur. Le dossier StealthModeData (vos profils) n'est jamais
# écrasé ni supprimé ici : il est créé par l'application, ou copié par vous.
New-Item -ItemType Directory -Force $usb | Out-Null
Copy-Item (Join-Path $out 'StealthMode.exe'), (Join-Path $out 'StealthMode.Recovery.exe') $usb -Force
Set-Content -Path (Join-Path $usb 'portable') -Value 'Mode portable : les donnees sont dans le dossier StealthModeData.' -Encoding ASCII

Get-ChildItem $out | Select-Object Name, @{n = 'Taille (Mo)'; e = { [math]::Round($_.Length / 1MB, 1) } }
Write-Host "Dossier portable : $usb"
