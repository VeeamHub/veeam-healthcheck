#Requires -Version 5.1

function Get-VhciObjectStorageRepos {
    <#
    .Synopsis
        Collects object storage repository configuration (name, type, bucket/container,
        region, endpoint, account display name — never passwords).
        Exports _ObjectStorageRepos.csv.

        Provider routing uses $repo.Type to select the correct SDK property paths:
          AmazonS3            → .AmazonS3.{BucketName, FolderName, RegionType}
          AmazonS3Compatible  → .AmazonS3Compatible.{BucketName, FolderName, ServicePoint, RegionType}
          WasabiCloud         → .WasabiCloud.{BucketName, FolderName, RegionId}
          AzureBlob           → .AzureBlob.{ContainerName, FolderName, ServicePoint}
          AzureArchive        → .AzureArchive.{ContainerName, FolderName, ServicePoint}
          GoogleCloudStorage  → .GoogleCloud.{BucketName, FolderName, RegionId}
          IBMCloud            → .IBMCloud.{BucketName, FolderName, Region}
          Unknown             → reflection probe (logs warning, exports whatever it finds)

        Account name comes from $repo.Account.Name (VBR 12+) or $repo.Credentials.Name (fallback).
        Gateway: $repo.UseGatewayServer / $repo.GatewayServer.Name.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting object storage repositories..."
    Write-LogFile $message

    try {
        $repos = Get-VBRObjectStorageRepository | ForEach-Object {
            $repo = $_
            $typeName = "$($repo.Type)"

            $bucket    = ''
            $folder    = ''
            $region    = ''
            $endpoint  = ''   # S3-compatible / Azure service point
            $account   = ''
            $useGw     = $false
            $gateway   = ''

            # ── Provider-specific property routing ──────────────────────────────
            switch ($typeName) {

                'AmazonS3' {
                    $sub    = $repo.AmazonS3
                    $bucket = $sub.BucketName
                    $folder = $sub.FolderName
                    $region = $sub.RegionType   # e.g. 'us-east-1'
                }

                'AmazonS3Compatible' {
                    $sub      = $repo.AmazonS3Compatible
                    $bucket   = $sub.BucketName
                    $folder   = $sub.FolderName
                    $endpoint = $sub.ServicePoint   # custom S3 endpoint URL
                    $region   = $sub.RegionType     # may be blank for on-prem S3
                }

                'WasabiCloud' {
                    $sub    = $repo.WasabiCloud
                    $bucket = $sub.BucketName
                    $folder = $sub.FolderName
                    $region = $sub.RegionId     # e.g. 'us-east-1'
                }

                'AzureBlob' {
                    $sub      = $repo.AzureBlob
                    $bucket   = $sub.ContainerName
                    $folder   = $sub.FolderName
                    $endpoint = $sub.ServicePoint   # storage account endpoint
                }

                'AzureArchive' {
                    $sub      = $repo.AzureArchive
                    $bucket   = $sub.ContainerName
                    $folder   = $sub.FolderName
                    $endpoint = $sub.ServicePoint
                }

                'GoogleCloudStorage' {
                    $sub    = $repo.GoogleCloud
                    $bucket = $sub.BucketName
                    $folder = $sub.FolderName
                    $region = $sub.RegionId     # e.g. 'us-central1'
                }

                'IBMCloud' {
                    $sub    = $repo.IBMCloud
                    $bucket = $sub.BucketName
                    $folder = $sub.FolderName
                    $region = $sub.Region
                }

                Default {
                    # Unknown provider — probe via reflection so we get something useful.
                    # Logs a warning so field SEs know to report the new type.
                    Write-LogFile "ObjectStorageRepos: unrecognized Type '$typeName' on repo '$($repo.Name)' — probing via Get-Member" -LogLevel "WARN"
                    $members = $repo | Get-Member -MemberType Property | Select-Object -ExpandProperty Name

                    foreach ($m in $members) {
                        $val = $repo.$m
                        if ($null -eq $val -or $val -isnot [string]) { continue }
                        $ml = $m.ToLower()
                        if ($ml -match 'bucket|container')  { $bucket   = $val }
                        elseif ($ml -match 'folder')        { $folder   = $val }
                        elseif ($ml -match 'region')        { $region   = $val }
                        elseif ($ml -match 'endpoint|servicepoint') { $endpoint = $val }
                    }
                }
            }

            # ── Account / credential name (never expose password) ───────────────
            if ($repo.PSObject.Properties['Account'] -and $null -ne $repo.Account) {
                $account = $repo.Account.Name
            } elseif ($repo.PSObject.Properties['Credentials'] -and $null -ne $repo.Credentials) {
                $account = $repo.Credentials.Name
            }

            # ── Gateway ─────────────────────────────────────────────────────────
            if ($repo.PSObject.Properties['UseGatewayServer']) {
                $useGw = $repo.UseGatewayServer
            }
            if ($repo.PSObject.Properties['GatewayServer'] -and $null -ne $repo.GatewayServer) {
                $gateway = ($repo.GatewayServer.Name -join '; ')
            }

            [PSCustomObject]@{
                Name       = $repo.Name
                Type       = $typeName
                Bucket     = $bucket
                Folder     = $folder
                Region     = $region
                Endpoint   = $endpoint
                Account    = $account
                UseGateway = $useGw
                Gateway    = $gateway
            }
        }

        Write-LogFile "Found $(@($repos).Count) object storage repositories"
        $repos | Export-VhciCsv -FileName '_ObjectStorageRepos.csv'
        Write-LogFile ($message + "DONE")
    } catch {
        Write-LogFile ($message + "FAILED!")
        Write-LogFile $_.Exception.Message -LogLevel "ERROR"
        Add-VhciModuleError -CollectorName 'ObjectStorageRepos' -ErrorMessage $_.Exception.Message
    }
}
