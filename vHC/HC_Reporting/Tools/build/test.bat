PowerShell.exe remove-item -force -recurse -path "\\VBR-12-beta3\C$\temp\vHC" 
Powershell.exe Robocopy.exe 'C:\code\VeeamHub\veeam-healthcheck\vHC\HC_Reporting\bin\Debug\net7.0-windows\win-x64\' '\\VBR-12-beta3\c$\code\VeeamHub\veeam-healthcheck\vHC\HC_Reporting\bin\Debug\net7.0-windows\win-x64\' /COPY:DAT /e /MT
EXIT 0