# Sign-Remotely.ps1 — signe les agents Windows FR avec le certificat de signature de code de l'utilisateur courant.
# A lancer dans un dossier contenant :
#   Remotely-Win-x64.zip          (agent installe)
#   Win-x64\Remotely_Desktop.exe  (client portable)
# Resultat dans .\out\ : Remotely-Win-x64.zip et Win-x64\Remotely_Desktop.exe signes.

$ErrorActionPreference = 'Stop'
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
Remove-Item .\agent, .\out -Recurse -Force -ErrorAction SilentlyContinue
New-Item .\out\Win-x64 -ItemType Directory | Out-Null

# Agent installe : dezipper, signer, rezipper
[IO.Compression.ZipFile]::ExtractToDirectory("$PWD\Remotely-Win-x64.zip", "$PWD\agent")
Sign (Get-ChildItem .\agent -Recurse -Include *.exe, *.dll).FullName
[IO.Compression.ZipFile]::CreateFromDirectory("$PWD\agent", "$PWD\out\Remotely-Win-x64.zip")
Remove-Item .\agent -Recurse -Force

# Portable
Copy-Item .\Win-x64\Remotely_Desktop.exe .\out\Win-x64\
Sign (Resolve-Path .\out\Win-x64\Remotely_Desktop.exe).Path

Get-ChildItem .\out -Recurse -File | Select-Object FullName, Length