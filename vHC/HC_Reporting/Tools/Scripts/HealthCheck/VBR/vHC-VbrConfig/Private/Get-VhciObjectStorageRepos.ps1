#Requires -Version 5.1

function Get-VhciObjectStorageRepos {
    <#
    .Synopsis
        Collects object storage repository configuration (name, type, bucket/container,
        region, endpoint, account display name — never passwords).
        Exports _ObjectStorageRepos.csv.

        Valid Type enum values (VBR 13, Get-VBRObjectStorageRepository -Type):
          AmazonS3           — AWS S3
          AmazonS3Compatible — S3-compatible (MinIO, Ceph, IBM, on-prem, etc.)
          AzureBlob          — Azure Blob Storage
          AzureDataBox       — Azure Data Box
          GoogleCloudStorage — Google Cloud Storage
          Wasabi             — Wasabi Cloud Storage
          DataCloudVault     — Veeam Data Cloud Vault
          ElevenEleven       — 11:11 Cloud Object Storage

        Note: AzureArchive is returned by Get-VBRArchiveObjectStorageRepository (separate cmdlet).
        IBM Cloud is handled through AmazonS3Compatible in VBR 13.

        Property paths verified against VBR 13 docs and community examples:
          AmazonS3 / AmazonS3Compatible / Wasabi:
            Bucket name  → .AmazonS3Folder.Bucket.Name   [docs + forum confirmed]
            Region       → .AmazonS3Folder.Bucket.Region  [forum confirmed]
            Folder name  → [string]$repo.AmazonS3Folder   [forum confirmed]
            Endpoint     → .ServicePoint (S3Compatible only, top-level property)
          AzureBlob / AzureDataBox:
            Container    → .AzureBlobFolder.Container.Name [docs confirmed: VBRAzureBlobFolder]
            Folder name  → [string]$repo.AzureBlobFolder
            Endpoint     → .AzureBlobFolder.Container.StorageAccount (service point)
          GoogleCloudStorage:
            Bucket name  → .Folder.Bucket.Name            [docs confirmed: VBRGoogleCloudFolder]
            Region       → .Folder.Bucket.Region
            Folder name  → [string]$repo.Folder
          DataCloudVault / ElevenEleven:
            Reflection probe (proprietary types, limited PS exposure)

        Gateway: $repo.GatewayServer (top-level, confirmed from live VBR output).
        ConnectionType: $repo.ConnectionType (Direct | Gateway).
        Account: accessed via PSObject.Properties check on Account/Credentials.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting object storage repositories..."
    Write-LogFile $message

    try {
        $repos = Get-VBRObjectStorageRepository | ForEach-Object {
            $repo     = $_
            $typeName = "$($repo.Type)"

            $bucket   = ''
            $folder   = ''
            $region   = ''
            $endpoint = ''
            $account  = ''
            $connType = "$($repo.ConnectionType)"
            $gateway  = ($repo.GatewayServer.Name -join '; ')

            # ── Provider-specific property routing ──────────────────────────────
            # Property paths verified against VBR 13 helpcenter docs and Veeam
            # community forum examples (forums.veeam.com/powershell-f26/t98760).
            switch ($typeName) {

                # ── AWS S3 ────────────────────────────────────────────────────
                # Add/Set cmdlet uses -AmazonS3Folder <VBRAmazonS3Folder>
                # Bucket and region live under .AmazonS3Folder.Bucket (VBRAmazonS3Bucket)
                'AmazonS3' {
                    $bucket = $repo.AmazonS3Folder.Bucket.Name
                    $region = $repo.AmazonS3Folder.Bucket.Region
                    $folder = [string]$repo.AmazonS3Folder
                }

                # ── S3-Compatible (MinIO, Ceph, IBM, on-prem, etc.) ───────────
                # Same VBRAmazonS3Folder pattern; endpoint is the service point URL.
                # ServicePoint may be top-level or on the connection — check both.
                'AmazonS3Compatible' {
                    $bucket = $repo.AmazonS3Folder.Bucket.Name
                    $region = $repo.AmazonS3Folder.Bucket.Region
                    $folder = [string]$repo.AmazonS3Folder
                    if ($repo.PSObject.Properties['ServicePoint'] -and $repo.ServicePoint) {
                        $endpoint = "$($repo.ServicePoint)"
                    }
                }

                # ── Wasabi Cloud Storage ───────────────────────────────────────
                # Type enum is 'Wasabi' (not 'WasabiCloud'). Uses same AmazonS3Folder
                # pattern as S3Compatible since Wasabi is S3-protocol compatible.
                'Wasabi' {
                    $bucket = $repo.AmazonS3Folder.Bucket.Name
                    $region = $repo.AmazonS3Folder.Bucket.Region
                    $folder = [string]$repo.AmazonS3Folder
                }

                # ── Azure Blob Storage ────────────────────────────────────────
                # Add cmdlet uses -AzureBlobFolder <VBRAzureBlobFolder>
                # Container lives under .AzureBlobFolder.Container
                'AzureBlob' {
                    $bucket = $repo.AzureBlobFolder.Container.Name
                    $folder = [string]$repo.AzureBlobFolder
                    if ($repo.AzureBlobFolder.PSObject.Properties['Container']) {
                        $endpoint = "$($repo.AzureBlobFolder.Container.StorageAccount)"
                    }
                }

                # ── Azure Data Box ─────────────────────────────────────────────
                # Same VBRAzureBlobFolder pattern as AzureBlob.
                'AzureDataBox' {
                    $bucket = $repo.AzureBlobFolder.Container.Name
                    $folder = [string]$repo.AzureBlobFolder
                    if ($repo.AzureBlobFolder.PSObject.Properties['Container']) {
                        $endpoint = "$($repo.AzureBlobFolder.Container.StorageAccount)"
                    }
                }

                # ── Google Cloud Storage ───────────────────────────────────────
                # Add cmdlet uses -Folder <VBRGoogleCloudFolder> (property is 'Folder')
                'GoogleCloudStorage' {
                    $bucket = $repo.Folder.Bucket.Name
                    $region = $repo.Folder.Bucket.Region
                    $folder = [string]$repo.Folder
                }

                # ── Veeam Data Cloud Vault ────────────────────────────────────
                # Proprietary type; limited PS property exposure. Probe via reflection.
                'DataCloudVault' {
                    Write-LogFile "ObjectStorageRepos: DataCloudVault repo '$($repo.Name)' — probing via Get-Member" -LogLevel "INFO"
                    $bucket = Get-VhciObjStoreProp $repo @('BucketName','Bucket','Container','ContainerName')
                    $folder = Get-VhciObjStoreProp $repo @('FolderName','Folder')
                    $region = Get-VhciObjStoreProp $repo @('Region','RegionId','RegionType')
                }

                # ── 11:11 Cloud Object Storage ────────────────────────────────
                'ElevenEleven' {
                    Write-LogFile "ObjectStorageRepos: ElevenEleven repo '$($repo.Name)' — probing via Get-Member" -LogLevel "INFO"
                    $bucket = Get-VhciObjStoreProp $repo @('BucketName','Bucket','Container','ContainerName')
                    $folder = Get-VhciObjStoreProp $repo @('FolderName','Folder')
                    $region = Get-VhciObjStoreProp $repo @('Region','RegionId','RegionType')
                }

                Default {
                    # Unrecognized type — full reflection probe.
                    # WARN so field SEs know to report new provider types for explicit support.
                    Write-LogFile "ObjectStorageRepos: unrecognized Type '$typeName' on repo '$($repo.Name)' — probing via Get-Member" -LogLevel "WARN"
                    foreach ($m in ($repo | Get-Member -MemberType Property | Select-Object -ExpandProperty Name)) {
                        $val = $repo.$m
                        if ($null -eq $val -or $val -isnot [string] -or $val -eq '') { continue }
                        $ml  = $m.ToLower()
                        if     ($ml -match 'bucket|container')           { $bucket   = $val }
                        elseif ($ml -match 'foldername|folder')          { $folder   = $val }
                        elseif ($ml -match 'region')                     { $region   = $val }
                        elseif ($ml -match 'servicepoint|endpoint|host') { $endpoint = $val }
                    }
                }
            }

            # ── Account / credential display name (never expose password) ───────
            foreach ($prop in @('Account','Credentials','CloudCredentials')) {
                if ($repo.PSObject.Properties[$prop] -and $null -ne $repo.$prop) {
                    $account = "$($repo.$prop.Name)"
                    break
                }
            }

            [PSCustomObject]@{
                Name           = $repo.Name
                Type           = $typeName
                Bucket         = $bucket
                Folder         = $folder
                Region         = $region
                Endpoint       = $endpoint
                Account        = $account
                ConnectionType = $connType
                Gateway        = $gateway
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

# Helper: try named properties in order, return first non-empty string value found.
function Get-VhciObjStoreProp {
    param($obj, [string[]]$names)
    foreach ($n in $names) {
        if ($obj.PSObject.Properties[$n] -and $null -ne $obj.$n -and "$($obj.$n)" -ne '') {
            return "$($obj.$n)"
        }
    }
    return ''
}
