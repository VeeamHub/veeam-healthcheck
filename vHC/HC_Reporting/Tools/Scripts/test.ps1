            Connect-VBRServer -Server localhost
            $jobs = Get-VBRJob
            $piJob = Get-VBRPluginJob
            $jobInfo = @()
            foreach ($job in $jobs)
            {
                $jobInfo += $job
            }

            $jobInfo