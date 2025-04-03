#!/bin/bash

# Path to your .csproj file (adjust if needed)
CSPROJ_FILE="/Users/adam.congdon/code/veeam-healthcheck/vHC/HC_Reporting/VeeamHealthCheck.csproj"

# Extract the current version from the .csproj file
# Extract the current version from the .csproj file using sed
CURRENT_VERSION=$(sed -n 's/.*<AssemblyVersion>\(.*\)<\/AssemblyVersion>.*/\1/p' "$CSPROJ_FILE")
echo "Current version: $CURRENT_VERSION"
# Split the version into an array (e.g., "1.0.0" -> [1, 0, 0])
IFS='.' read -r -a VERSION_PARTS <<< "$CURRENT_VERSION"

# Increment the patch version (last number)
((VERSION_PARTS[2]++))

# Join the parts back together
NEW_VERSION="${VERSION_PARTS[0]}.${VERSION_PARTS[1]}.${VERSION_PARTS[2]}"

# Replace the old version with the new one in the .csproj file
sed -i '' "s|<AssemblyVersion>$CURRENT_VERSION</AssemblyVersion>|<AssemblyVersion>$NEW_VERSION</AssemblyVersion>|" "$CSPROJ_FILE"

echo "Version updated to $NEW_VERSION"