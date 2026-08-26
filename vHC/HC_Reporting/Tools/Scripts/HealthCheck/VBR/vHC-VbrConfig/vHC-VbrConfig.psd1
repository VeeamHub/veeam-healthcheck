@{
    ModuleVersion     = '1.0.0'
    RootModule        = 'vHC-VbrConfig.psm1'
    PowerShellVersion = '5.1'
    Description       = 'VBR configuration collector module for Veeam Health Check'
    Author            = 'Veeam Health Check'
    FunctionsToExport = @(
        'Get-VhcArchiveTier',
        'Get-VhcBackupSessions',
        'Get-VhcCapacityTier',
        'Get-VhcCloudConnect',
        'Get-VhcConcurrencyData',
        'Get-VhcEntraId',
        'Get-VhcJob',
        'Get-VhcLicense',
        'Get-VhcModuleErrors',
        'Get-VhcMajorVersion',
        'Get-VhcMalwareDetection',
        'Get-VhcOrphanedSupersededBackups',
        'Get-VhcProtectedWorkloads',
        'Get-VhcRegistrySettings',
        'Get-VhcRepository',
        'Get-VhcSecurityCompliance',
        'Get-VhcServer',
        'Get-VhcSessionReport',
        'Get-VhcTrafficRules',
        'Get-VhcUserRoles',
        'Get-VhcVbrInfo',
        'Get-VhcWanAccelerator',
        'Get-VhciObjectStorageRepos',
        'Get-VhciPlatformMap',
        'Initialize-VhcModule',
        'Invoke-VhcCollector',
        'Invoke-VhcConcurrencyAnalysis',
        'Write-LogFile'
    )
}
