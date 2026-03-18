#Requires -Version 5.1

function Get-VhciSessionLogWithTimeout {
    <#
    .Synopsis
        Retrieves log records from a VBR session object using a timeout-guarded runspace.
        Prevents the main process from hanging on sessions with corrupt or unresponsive loggers.
    .Parameter Session
        The VBR backup session object.
    .Parameter TimeoutSeconds
        Maximum seconds to wait before aborting. Default 30.
    #>
    param(
        $Session,
        [int]$TimeoutSeconds = 30
    )

    $runspace = [runspacefactory]::CreateRunspace()
    $runspace.Open()
    $runspace.SessionStateProxy.SetVariable('session', $Session)

    $ps = [powershell]::Create()
    $ps.Runspace = $runspace
    $ps.AddScript({
        try { $session.Logger.GetLog().UpdatedRecords } catch { $null }
    }) | Out-Null

    $handle    = $ps.BeginInvoke()
    $completed = $handle.AsyncWaitHandle.WaitOne($TimeoutSeconds * 1000)

    if ($completed) {
        try {
            return $ps.EndInvoke($handle)
        } catch {
            return $null
        } finally {
            $handle.AsyncWaitHandle.Dispose()
            $ps.Dispose()
            $runspace.Close()
            $runspace.Dispose()
        }
    } else {
        $handle.AsyncWaitHandle.Dispose()
        $ps.Stop()
        $ps.Dispose()
        $runspace.Close()
        $runspace.Dispose()
        return $null
    }
}
