<#
.SYNOPSIS
   Installs the Remotely Client.
.DESCRIPTION
   Do not modify this script.  It was generated specifically for your account.
.EXAMPLE
   powershell.exe -f Install-Remotely.ps1
   powershell.exe -f Install-Remotely.ps1 -DeviceAlias "My Super Computer" -DeviceGroup "My Stuff"
#>

param (
	[string]$DeviceAlias,
	[string]$DeviceGroup,
	[string]$Path,
	[string]$OrganizationId,
	[string]$ServerUrl,
	[switch]$Uninstall,
	[switch]$Quiet
)

#region Set SecurityProtocol
# This will include all security protocols greater than or equal to TLS 1.2.
[System.Net.SecurityProtocolType]$SecurityProtocols = 0;
[System.Enum]::GetValues([System.Net.SecurityProtocolType]) | Where-Object {
	$_ -ge [System.Net.SecurityProtocolType]::Tls12
} | ForEach-Object {
	$SecurityProtocols = $SecurityProtocols -bor $_
}
[System.Net.ServicePointManager]::SecurityProtocol = $SecurityProtocols
#endregion

#region Set Variables
$LogPath = "$env:TEMP\Remotely_Install.txt"

[string]$HostName = $null
if ($ServerUrl) {
	$HostName = $ServerUrl
}

[string]$Organization = $null
if ($OrganizationId) {
	$Organization = $OrganizationId
}

$ConnectionInfo = $null

if ([System.Environment]::Is64BitOperatingSystem) {
	$Platform = "x64"
}
else {
	$Platform = "x86"
}

$InstallPath = "$env:ProgramFiles\Remotely"
#endregion

#region Functions
function Write-Log($Message) {
	if (!$Quiet) {
		Write-Host $Message
	}
	"$((Get-Date).ToString()) - $Message" | Out-File -FilePath $LogPath -Append
}
function Do-Exit() {
	Write-Log "Exiting..."
	Start-Sleep -Seconds 3
	exit
}
function Is-Administrator() {
	$Identity = [Security.Principal.WindowsIdentity]::GetCurrent()
	$Principal = New-Object System.Security.Principal.WindowsPrincipal -ArgumentList $Identity
	return $Principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Run-StartupChecks {

	if (!$HostName -or !$Organization) {
		Write-Log "Required parameters are missing.  Please try downloading the installer again."
		Do-Exit
	}

	if (!(Is-Administrator)) {
		Write-Log -Message "Install script requires elevation.  Attempting to self-elevate..."
		Start-Sleep -Seconds 3

		$Params = "-File `"$($MyInvocation.ScriptName)`"";
		if ($OrganizationId) {
			$Params += " -OrganizationId $OrganizationId"
		}
		if ($ServerUrl) {
			$Params += " -ServerUrl $ServerUrl"
		}
		if ($DeviceAlias) {
			$Params += " -DeviceAlias $DeviceAlias"
		}
		if ($DeviceGroup) {
			$Params += " -DeviceGroup $DeviceGroup"
		}
		if ($Path) {
			$Params += " -Path `"$Path`""
		}
		if ($Uninstall) {
			$Params += " -Uninstall"
		}
		if ($Quiet) {
			$Params += " -Quiet"
		}
		Start-Process -FilePath powershell.exe -ArgumentList $Params -Verb RunAs
		exit
	}
}

function Stop-Remotely {
	Start-Process -FilePath "cmd.exe" -ArgumentList "/c sc delete Remotely_Service" -Wait -WindowStyle Hidden
	Stop-Process -Name Remotely_Agent -Force -ErrorAction SilentlyContinue
	Stop-Process -Name Remotely_Desktop -Force -ErrorAction SilentlyContinue
}

function Uninstall-Remotely {
	Stop-Remotely
	Remove-Item -Path $InstallPath -Force -Recurse -ErrorAction SilentlyContinue
	Remove-NetFirewallRule -Name "Remotely Desktop Unattended" -ErrorAction SilentlyContinue
}

function Install-Remotely {
	$HeadResponse = Invoke-WebRequest -Uri "$HostName/Content/Remotely-Win-$Platform.zip" -Method Head -UseBasicParsing
	$ETag = $HeadResponse.Headers["ETag"]
	if (!$Etag) {
		Write-Log "Failed to get ETag from server.  Aborting install."
	}

	if ((Test-Path -Path "$InstallPath") -and (Test-Path -Path "$InstallPath\ConnectionInfo.json")) {
		$ConnectionInfo = Get-Content -Path "$InstallPath\ConnectionInfo.json" | ConvertFrom-Json
		if ($ConnectionInfo) {
			# Mise à jour vers le même serveur : garder le jeton, sinon le poste ne se fait plus reconnaître.
			if ("$($ConnectionInfo.Host)".TrimEnd("/") -ne $HostName.TrimEnd("/")) {
				$ConnectionInfo.ServerVerificationToken = ""
			}
			$ConnectionInfo.Host = $HostName
			$ConnectionInfo.OrganizationID = $Organization
		}
	}
	else {
		New-Item -ItemType Directory -Path "$InstallPath" -Force
	}

	if (!$ConnectionInfo) {
		$NewDeviceId = [System.Guid]::NewGuid().ToString();
		$ConnectionInfo = @{
			DeviceID                = $NewDeviceId;
			Host                    = $HostName;
			OrganizationID          = $Organization;
			ServerVerificationToken = "";
		}
	}

	if ($HostName.EndsWith("/")) {
		$HostName = $HostName.Substring(0, $HostName.LastIndexOf("/"))
	}

	# Dossier de travail propre à cette installation : nom aléatoire, réservé à SYSTEM et aux Administrateurs
	$WorkDir = Join-Path $env:TEMP ("amarre-" + [System.Guid]::NewGuid().ToString("N"))
	New-Item -ItemType Directory -Path $WorkDir | Out-Null
	& icacls.exe "$WorkDir" /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" | Out-Null
	$ZipPath = Join-Path $WorkDir "Remotely-Win-$Platform.zip"

	if ($Path) {
		Write-Log "Copying install files..."
		Copy-Item -Path $Path -Destination $ZipPath
		# Dossier de mise à jour créé par l'agent : plus utile
		$UpdateDir = Split-Path -Path $Path -Parent
		if ((Split-Path -Path $UpdateDir -Leaf) -like "amarre-maj-*") {
			Remove-Item -Path $UpdateDir -Recurse -Force -ErrorAction SilentlyContinue
		}

	}
	else {
		$ProgressPreference = 'SilentlyContinue'
		Write-Log "Downloading client..."
		Invoke-WebRequest -Uri "$HostName/Content/Remotely-Win-$Platform.zip" -OutFile $ZipPath -UseBasicParsing
		$ProgressPreference = 'Continue'
	}

	if (!(Test-Path -Path $ZipPath)) {
		Write-Log "Client files failed to download."
		Do-Exit
	}

	Stop-Remotely
	Get-ChildItem -Path $InstallPath | Where-Object { $_.Name -notlike "ConnectionInfo.json" } | Remove-Item -Recurse -Force

	Expand-Archive -Path $ZipPath -DestinationPath "$InstallPath" -Force
	Remove-Item -Path $WorkDir -Recurse -Force -ErrorAction SilentlyContinue

	New-Item -ItemType File -Path "$InstallPath\ConnectionInfo.json" -Value (ConvertTo-Json -InputObject $ConnectionInfo) -Force
	# Contient le jeton du poste : SYSTEM et Administrateurs seulement
	& icacls.exe "$InstallPath\ConnectionInfo.json" /inheritance:r /grant:r "*S-1-5-18:F" "*S-1-5-32-544:F" | Out-Null

	New-Item -ItemType File -Path "$InstallPath\etag.txt" -Value $ETag -Force

	if ($DeviceAlias -or $DeviceGroup) {
		$DeviceSetupOptions = @{
			DeviceAlias     = $DeviceAlias;
			DeviceGroupName = $DeviceGroup;
			OrganizationID  = $Organization;
			DeviceID        = $ConnectionInfo.DeviceID;
		}

		$Body = $DeviceSetupOptions | ConvertTo-Json
		# Le serveur exige désormais une clé d'API pour cet appel : sans elle, l'installation continue
		# et l'alias ou le groupe se règlent depuis la console.
		try {
			Invoke-RestMethod -Method Post -ContentType "application/json" -Uri "$HostName/api/devices" -Body $Body
		}
		catch {
			Write-Log "Alias et groupe non appliqués (refusés par le serveur) : à régler depuis la console."
		}
	}

	New-Service -Name "Remotely_Service" -BinaryPathName "`"$InstallPath\Remotely_Agent.exe`"" -DisplayName "Remotely Service" -StartupType Automatic -Description "Background service that maintains a connection to the Remotely server.  The service is used for remote support and maintenance by this computer's administrators."
	Start-Process -FilePath "cmd.exe" -ArgumentList "/c sc.exe failure `"Remotely_Service`" reset=5 actions=restart/5000" -Wait -WindowStyle Hidden
	Start-Service -Name Remotely_Service
}

#endregion

#region Main

try {
	Run-StartupChecks

	Write-Log "Install/uninstall logs are being written to `"$LogPath`""

	if ($Uninstall) {
		Write-Log "Uninstall started."
		Uninstall-Remotely
		Write-Log "Uninstall completed."
		exit
	}
	else {
		Write-Log "Install started."
		Install-Remotely
		Write-Log "Install completed."
		exit
	}
}
catch {
	Write-Log -Message "Error occurred: $($Error[0].InvocationInfo.PositionMessage)"
	throw $Error[0]
	Do-Exit
}
#endregion