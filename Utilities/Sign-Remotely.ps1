# Sign-Remotely.ps1 - signe les agents Windows x64 d'une release, en une commande, pour une instance qui
# re-signe ses agents avec son certificat de signature de code (Cert:\CurrentUser\My, le plus recent).
#
#   powershell -ExecutionPolicy Bypass -File <depot>\Utilities\Sign-Remotely.ps1 -Content .\Content.zip -Cle <cle.pem>
#
# -Content : Content.zip de la release (decompresse a cote dans .\content) ou dossier deja decompresse.
# -Cle     : cle privee de signature des mises a jour (amarre-maj.cs cle). Sa cle publique (<cle>.pub, a cote)
#            est ajoutee au zip de l'agent en cles-maj.pub, pour que les postes acceptent le manifeste.
# Fait, sur place : Authenticode des exe/dll de Remotely-Win-x64.zip et de Win-x64\Remotely_Desktop.exe
# (seulement ce qui ne l'est pas : relancer ne casse rien), puis Manifest.txt signe (SDK .NET 10 requis).
# Le dossier obtenu est pret a deposer tel quel dans le volume content du serveur.
param(
    [Parameter(Mandatory)][string]$Content,
    [Parameter(Mandatory)][string]$Cle
)
$ErrorActionPreference = 'Stop'

$Cle = (Resolve-Path $Cle).Path
$pub = [IO.Path]::ChangeExtension($Cle, '.pub')
if (-not (Test-Path $pub)) { throw "Cle publique introuvable : $pub (creee a cote du .pem par amarre-maj.cs cle)" }
if ($Content -like '*.zip') {
    $zip = (Resolve-Path $Content).Path
    $Content = Join-Path (Split-Path $zip) 'content'
    if (Test-Path $Content) { throw "$Content existe deja : le supprimer ou passer ce dossier a -Content" }
    Expand-Archive $zip -DestinationPath $Content
}
$Content = (Resolve-Path $Content).Path

$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Sort-Object NotAfter | Select-Object -Last 1
if (-not $cert) { throw 'Aucun certificat de signature de code dans CurrentUser\My' }
$ts = 'http://timestamp.digicert.com'

function Sign($paths) {
  # ponytail: ne signe que ce qui ne l'est pas (les dll du runtime .NET sont deja signees par Microsoft)
  $todo = $paths | Where-Object { (Get-AuthenticodeSignature $_).Status -eq 'NotSigned' }
  $r = $todo | ForEach-Object { Set-AuthenticodeSignature $_ $cert -TimestampServer $ts -HashAlgorithm SHA256 }
  $bad = $r | Where-Object Status -ne 'Valid'
  if ($bad) { $bad | Format-Table Path, Status, StatusMessage; throw 'Signature KO' }
  "$(@($r).Count) fichier(s) signe(s)"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$agentZip = Join-Path $Content 'Remotely-Win-x64.zip'
$work = Join-Path $env:TEMP ('amarre-signe-' + [guid]::NewGuid().ToString('N'))

# Agent installe : dezipper, signer, ajouter la cle publique, rezipper a la place de l'original
[IO.Compression.ZipFile]::ExtractToDirectory($agentZip, $work)
try {
  Sign (Get-ChildItem $work -Recurse -Include *.exe, *.dll).FullName
  Copy-Item $pub (Join-Path $work 'cles-maj.pub') -Force
  Remove-Item $agentZip
  [IO.Compression.ZipFile]::CreateFromDirectory($work, $agentZip)
} finally {
  Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

# Client portable
Sign (Join-Path $Content 'Win-x64\Remotely_Desktop.exe')

# Manifeste : en dernier, plus rien ne doit toucher au dossier ensuite
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& dotnet run (Join-Path $PSScriptRoot 'amarre-maj.cs') -- signer $Content $Cle
if ($LASTEXITCODE -ne 0) { throw 'Signature du manifeste KO' }
"Pret a deposer : $Content"
