#!/bin/bash

# Get the directory of the script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ_FILE="$SCRIPT_DIR/VeeamHealthCheck.csproj"

# Get current AssemblyVersion and FileVersion
CURRENT_ASSEMBLY_VERSION=$(sed -n 's/.*<AssemblyVersion>\(.*\)<\/AssemblyVersion>.*/\1/p' "$CSPROJ_FILE")
CURRENT_FILE_VERSION=$(sed -n 's/.*<FileVersion>\(.*\)<\/FileVersion>.*/\1/p' "$CSPROJ_FILE")
echo "Current AssemblyVersion: $CURRENT_ASSEMBLY_VERSION"
echo "Current FileVersion: $CURRENT_FILE_VERSION"

# Use AssemblyVersion as the source for increment
IFS='.' read -r -a VERSION_PARTS <<< "$CURRENT_ASSEMBLY_VERSION"

# Increment the last segment (build/revision)
LAST_INDEX=$((${#VERSION_PARTS[@]} - 1))
((VERSION_PARTS[LAST_INDEX]++))

NEW_VERSION="${VERSION_PARTS[*]}"
NEW_VERSION="${NEW_VERSION// /.}"

# Update both AssemblyVersion and FileVersion
sed -i '' "s|<AssemblyVersion>$CURRENT_ASSEMBLY_VERSION</AssemblyVersion>|<AssemblyVersion>$NEW_VERSION</AssemblyVersion>|" "$CSPROJ_FILE"
sed -i '' "s|<FileVersion>$CURRENT_FILE_VERSION</FileVersion>|<FileVersion>$NEW_VERSION</FileVersion>|" "$CSPROJ_FILE"

echo "Version updated to $NEW_VERSION (AssemblyVersion and FileVersion)"