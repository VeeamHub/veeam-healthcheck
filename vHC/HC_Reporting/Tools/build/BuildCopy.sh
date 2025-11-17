#!/bin/bash

# Source path from build output
SOURCE="/Users/adam.congdon/code/veeam-healthcheck/vHC/HC_Reporting/bin/Debug/net8.0-windows7.0/win-x64/"
# Destination (mounted SMB share)
DEST="$HOME/vhc_mount/veeam-healthcheck/vHC/HC_Reporting/bin/"
# SMB details
SMB_SERVER="192.168.20.22"
SMB_SHARE="vhc"
SMB_USER="vhc"
SMB_PASS="${SMB_PASS:-}"  # Use an environment variable for the password

# Mount point
MOUNT_POINT="$HOME/vhc_mount"

# Ensure mount point exists as a directory
if [ ! -d "$MOUNT_POINT" ]; then
    mkdir -p "$MOUNT_POINT" || {
        echo "Error: Cannot create mount point $MOUNT_POINT"
        exit 1
    }
fi

# Check if the SMB share is mounted; mount it if not
if ! mount | grep -q "$MOUNT_POINT"; then
    mount -t smbfs "//$SMB_USER:$SMB_PASS@$SMB_SERVER/$SMB_SHARE" "$MOUNT_POINT"
    if [ $? -ne 0 ]; then
        echo "Error: Failed to mount SMB share at $MOUNT_POINT"
        exit 1
    fi
    echo "SMB share mounted at $MOUNT_POINT"
fi

# Ensure destination directory exists
mkdir -p "$DEST" || {
    echo "Error: Cannot create destination directory $DEST"
    exit 1
}

# Use rsync to copy files
rsync -avh --progress "$SOURCE" "$DEST"
if [ $? -ne 0 ]; then
    echo "Error: rsync failed"
    exit 1
fi

echo "Files successfully copied to $DEST"