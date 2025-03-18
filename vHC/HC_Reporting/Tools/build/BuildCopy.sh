#!/bin/bash

# Source path from build output
SOURCE="/Users/adam.congdon/code/veeam-healthcheck/vHC/HC_Reporting/bin/Debug/net8.0-windows7.0/win-x64/"
# Remote SMB share details
SMB_SERVER="192.168.20.114"
SMB_SHARE="vhc"  # The share name (e.g., 'source' from '\\192.168.20.203\source\...')
SMB_PATH="veeam-healthcheck/vHC/HC_Reporting/bin/"
SMB_USER="administrator"  # Replace with your SMB username
SMB_PASS="Lanc3r4th3w!n"  # Replace with your SMB password

# Change to the source directory
cd "$SOURCE" || {
    echo "Error: Cannot change to source directory $SOURCE"
    exit 1
}

# Use smbclient to upload files to the remote share
smbclient "//$SMB_SERVER/$SMB_SHARE" "$SMB_PASS" -U "$SMB_USER" -c "prompt; recurse; mput *; exit"
if [ $? -ne 0 ]; then
    echo "Error: Failed to upload files to //$SMB_SERVER/$SMB_SHARE/$SMB_PATH"
    exit 1
fi

echo "Files successfully copied to //$SMB_SERVER/$SMB_SHARE/$SMB_PATH"