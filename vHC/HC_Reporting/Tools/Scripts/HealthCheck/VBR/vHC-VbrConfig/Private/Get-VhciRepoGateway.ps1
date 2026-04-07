#Requires -Version 5.1

function Get-VhciRepoGateway {
    <#
    .Synopsis
        Collects repository and gateway server data, exports _RepositoryServers.csv and _Gateways.csv.
        Returns an array of role-entry descriptors (Gateway and Repository roles) for merging by the caller.
        Source: Get-VBRConfig.ps1 lines 604-689.
    .Parameter Repositories
        Array of backup repository objects returned by Get-VBRBackupRepository.
    .Parameter VServers
        Array of VBR server objects (from Get-VBRServer) for hardware info lookup.
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)] [object[]] $Repositories = @(),
        [Parameter(Mandatory)] [object[]] $VServers
    )

    $message = "Calculating Repository and GW Data..."
    Write-LogFile $message

    $roleEntries = [System.Collections.Generic.List[PSCustomObject]]::new()
    $RepoData = [System.Collections.Generic.List[PSCustomObject]]::new()
    $GWData   = [System.Collections.Generic.List[PSCustomObject]]::new()

    foreach ($Repository in $Repositories) {
            $NrofRepositoryTasks = $Repository.Options.MaxTaskCount
            $gatewayServers      = $Repository.GetActualGateways()
            $NrofgatewayServers  = $gatewayServers.Count

            # -1 signals unlimited; cap at 128 to match original behaviour
            if ($NrofRepositoryTasks -eq -1) {
                $NrofRepositoryTasks = 128
            }

            if ($NrofgatewayServers -gt 0) {
                # Gateway servers are always local infrastructure even when
                # the backing repo is object storage / VeeamVault / cloud.
                foreach ($gatewayServer in $gatewayServers) {
                    $Server  = $VServers | Where-Object { $_.Name -eq $gatewayServer.Name }
                    $hw      = Get-VhciHostHardware $Server
                    $GWCores = $hw.Cores
                    $GWRAM   = $hw.RAM

                    $gwTaskCount = [int][Math]::Ceiling($NrofRepositoryTasks / $NrofgatewayServers)
                    $GWDetails = [pscustomobject][ordered]@{
                        'Repository Name'  = $Repository.Name
                        'Gateway Server'   = $gatewayServer.Name
                        'Gateway Cores'    = $GWCores
                        'Gateway RAM (GB)' = $GWRAM
                        'Concurrent Tasks' = $gwTaskCount
                    }
                    $GWData.Add($GWDetails)

                    $roleEntries.Add([PSCustomObject]@{
                        HostName     = $gatewayServer.Name
                        RoleName     = 'Gateway'
                        EntryName    = $Repository.Name
                        TaskCount    = $gwTaskCount
                        TaskCountKey = 'TotalGWTasks'
                        Cores        = $GWCores
                        RAM          = $GWRAM
                    })
                }
            } else {
                # No gateway - the repo host IS the storage target.
                # Skip non-local hosts: VCC cloud repos (Host.Type = Cloud) and
                # object storage / VeeamVault repos that have no usable Host.Name.
                if ($null -eq $Repository.Host -or
                    [string]::IsNullOrEmpty($Repository.Host.Name) -or
                    $Repository.Host.Type -eq 'Cloud') {
                    continue
                }

                $Server    = $VServers | Where-Object { $_.Name -eq $Repository.Host.Name }
                $hw        = Get-VhciHostHardware $Server
                $RepoCores = $hw.Cores
                $RepoRAM   = $hw.RAM

                $RepoDetails = [pscustomobject][ordered]@{
                    'Repository Name'     = $Repository.Name
                    'Repository Server'   = $Repository.Host.Name
                    'Repository Cores'    = $RepoCores
                    'Repository RAM (GB)' = $RepoRAM
                    'Concurrent Tasks'    = $NrofRepositoryTasks
                }
                $RepoData.Add($RepoDetails)

                $roleEntries.Add([PSCustomObject]@{
                    HostName     = $Repository.Host.Name
                    RoleName     = 'Repository'
                    EntryName    = $Repository.Name
                    TaskCount    = $NrofRepositoryTasks
                    TaskCountKey = 'TotalRepoTasks'
                    Cores        = $RepoCores
                    RAM          = $RepoRAM
                })
            }
        }

    Write-LogFile ($message + "DONE")
    $RepoData | Export-VhciCsv -FileName '_RepositoryServers.csv'
    $GWData   | Export-VhciCsv -FileName '_Gateways.csv'
    return $roleEntries.ToArray()
}
