#!/bin/bash
# Run tests locally with detailed output

set -e

echo "Building solution..."
dotnet build vHC/HC.sln --configuration Debug

echo -e "\nRunning tests..."
dotnet test vHC/HC.sln --no-build --verbosity normal --logger "console;verbosity=detailed"

if [ $? -eq 0 ]; then
    echo -e "\nAll tests passed!"
else
    echo -e "\nSome tests failed!"
    exit 1
fi
