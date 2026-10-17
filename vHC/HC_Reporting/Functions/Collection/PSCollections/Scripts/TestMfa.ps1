param(
    [Parameter(Mandatory = $false)]
    [string]$Server = '',
    [Parameter(Mandatory = $false)]
    [string]$Username = '',
    [Parameter(Mandatory = $false)]
    [string]$PasswordBase64 = '',
    [Parameter(Mandatory = $false)]
    [int]$VBRVersion = 0
)

# Suppress ANSI color codes in PS7+ so stderr is always plain text
if (Get-Variable -Name PSStyle -ErrorAction SilentlyContinue) {
    $PSStyle.OutputRendering = 'PlainText'
}

function Resolve-VeeamConsolePath {
    $attempted = [System.Collections.Generic.List[string]]::new()

    # Helper: get parent directory via string ops (works on any platform with Windows paths)
    $getParent = {
        param([string]$p)
        $t = $p.TrimEnd('\','/')
        $i = [Math]::Max($t.LastIndexOf('\'), $t.LastIndexOf('/'))
        if ($i -gt 0) { $t.Substring(0, $i) } else { $t }
    }

    # Registry is authoritative — try it first
    # Reject UNC paths from the registry to prevent SMB coercion via tampered key
    try {
        $regKey = 'HKLM:\SOFTWARE\Veeam\Veeam Backup and Replication'
        $corePath = (Get-ItemProperty -Path $regKey -Name 'CorePath' -ErrorAction Stop).CorePath
        if ($corePath -match '^[A-Za-z]:\\') {
            # CorePath points to the Backup\ subfolder; Console is its sibling, not its child
            $installRoot = & $getParent $corePath
            $candidate   = "$installRoot\Console"
            $attempted.Add($candidate)
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }
    catch {
        Write-Verbose "Registry Value probe missed: $($_.Exception.Message)"
        # Registry key or value absent — continue to next probe
    }

    # Registry above is not authoritave when using remote console machines to run the health check.
    # The reason for this is that the CorePath key is not installed with the console even if it
    # is installed to a different drive. This probe checks the machine for if the console is
    # installed and infers the registry path from this.
    try {
        $VeeamConsolePackage = Get-Package -Name "Veeam Backup & Replication Console"
        $packagePath = $VeeamConsolePackage.FullPath
        if ($packagePath -match '^[A-Za-z]:\\') {
            # need to refactor this into a common function.
            $candidate   = $packagePath + "Console"
            $attempted.Add($candidate)
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }
    catch {
        Write-Verbose "Get-Package list probe missed: $($_.Exception.Message)"
        # if probe fails.
    }

    # Mount Service registry probe — authoritative for non-default installs
    # InstallationPath points to the Backup\ directory; Console is its sibling
    try {
        $mountKey  = 'HKLM:\SOFTWARE\Veeam\Veeam Mount Service'
        $mountPath = (Get-ItemProperty -Path $mountKey -Name 'InstallationPath' -ErrorAction Stop).InstallationPath
        if ($mountPath -match '^[A-Za-z]:\\') {
            $installRoot = & $getParent $mountPath
            $candidate   = "$installRoot\Console"
            $attempted.Add($candidate)
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }
    catch {
        Write-Verbose "Mount Service registry probe missed: $($_.Exception.Message)"
    }

    # Fall back to standard environment-variable paths
    $envCandidates = @($env:ProgramFiles, ${env:ProgramFiles(x86)}) | Where-Object { $_ }
    foreach ($base in $envCandidates) {
        $candidate = "$base\Veeam\Backup and Replication\Console"
        $attempted.Add($candidate)
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $pathList = $attempted -join "`n  "
    Write-Error "Veeam Console path not found. Paths attempted:`n  $pathList"
    return $null
}

if ($MyInvocation.InvocationName -ne '.') {
    try {
        Write-Host "[VERBOSE] PowerShell Version: $($PSVersionTable.PSVersion.ToString())"

        $veeamConsolePath = Resolve-VeeamConsolePath
        if ($null -eq $veeamConsolePath) {
            exit 1
        }

        Write-Verbose "Adding Veeam Console path to PSModulePath: $veeamConsolePath"
        $env:PSModulePath = "$veeamConsolePath;$env:PSModulePath"

        Write-Verbose "Attempting to import Veeam.Backup.PowerShell module..."
        Write-Host "[VERBOSE] Attempting to import Veeam.Backup.PowerShell module..."
        Import-Module Veeam.Backup.PowerShell -Force -WarningAction Ignore
        Write-Host "[VERBOSE] Module imported. Attempting to connect to VBR Server: $Server with user $Username."

        # Decode Base64 password
        $passwordBytes = [System.Convert]::FromBase64String($PasswordBase64)
        $password = [System.Text.Encoding]::UTF8.GetString($passwordBytes)

        Write-Host "[VERBOSE] Password decoded successfully (length: $($password.Length))"
        Write-Host "[VERBOSE] Server: $Server"
        Write-Host "[VERBOSE] Username: $Username"

        # Use -User and -Password parameters directly (same as manual CLI usage)
        # This approach works better for local accounts vs -Credential.
        # -ForceAcceptTlsCertificate only exists on VBR v13+; gate by version so v12 (where the
        # parameter does not exist and would throw) still connects (issue #149 v12-regression guard).
        $certParam = @{}
        if ($VBRVersion -ge 13) { $certParam['ForceAcceptTlsCertificate'] = $true }
        Connect-VBRServer -Server $Server -User $Username -Password $password @certParam -ErrorAction Stop
        Write-Host "[VERBOSE] Successfully connected to VBR Server."
        exit 0
    }
    catch {
        $errorMsg = $_.Exception.Message
        Write-Host "[VERBOSE] Exception occurred: $errorMsg"

        # Output the full error to STDERR so C# can parse it
        Write-Error $errorMsg

        exit 1
    }
}
