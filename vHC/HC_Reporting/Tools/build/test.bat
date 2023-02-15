PowerShell.exe remove-item -force -recurse -path "\\WIN-PDH5Q0UO9TK\C$\temp\vHC" 
Powershell.exe Robocopy.exe 'C:\code\VeeamHub\veeam-healthcheck\vHC\HC_Reporting\bin\Debug\net7.0-windows\win-x64\' '\\WIN-PDH5Q0UO9TK\c$\code\VeeamHub\veeam-healthcheck\vHC\HC_Reporting\bin\Debug\net7.0-windows\win-x64\' /COPY:DAT /e /MT
EXIT 0