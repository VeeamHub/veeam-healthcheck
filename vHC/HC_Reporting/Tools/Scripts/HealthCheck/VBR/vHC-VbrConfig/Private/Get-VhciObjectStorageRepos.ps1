#Requires -Version 5.1

function Get-VhciObjectStorageRepos {
    <#
    .Synopsis
        Collects object storage repository configuration (name, type, bucket/container,
        region, account display name — never passwords).
        Exports _ObjectStorageRepos.csv.
    #>
    [CmdletBinding()]
    param()

    $message = "Collecting object storage repositories..."
    Write-LogFile $message

    try {
        $repos = Get-VBRObjectStorageRepository | ForEach-Object {
            $repo = $_

            $bucket     = $null
            $folder     = $null
            $region     = $null
            $account    = $null
            $useGateway = $null
            $gateway    = $null

            try { $bucket     = $repo.AmazonS3Folder.BucketName        } catch {}
            try { if (-not $bucket) { $bucket = $repo.AzureBlobFolder.ContainerName } } catch {}
            try { if (-not $bucket) { $bucket = $repo.Container.Name   } } catch {}

            try { $folder     = $repo.AmazonS3Folder.FolderName        } catch {}
            try { if (-not $folder) { $folder = $repo.AzureBlobFolder.FolderName    } } catch {}
            try { if (-not $folder) { $folder = $repo.Folder           } } catch {}

            try { $region     = $repo.AmazonS3Folder.Connection.AmazonS3Region } catch {}
            try { if (-not $region) { $region = $repo.AzureBlobFolder.Connection.AzureRegion } } catch {}

            try { $account    = $repo.Account.Name                     } catch {}
            try { if (-not $account) { $account = $repo.Credentials.Name } } catch {}

            try { $useGateway = $repo.UseGatewayServer                 } catch {}
            try { $gateway    = $repo.GatewayServer.Name               } catch {}

            [PSCustomObject]@{
                Name        = $repo.Name
                Type        = $repo.Type
                Bucket      = $bucket
                Folder      = $folder
                Region      = $region
                Account     = $account
                UseGateway  = $useGateway
                Gateway     = $gateway
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
