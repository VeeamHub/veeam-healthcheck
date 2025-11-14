@echo off
REM Batch script to publish VeeamHealthCheck

set CONFIGURATION=Release
set RUNTIME=win-x64
set OUTPUT_PATH=..\..\..\..\publish

echo Publishing VeeamHealthCheck...
echo Configuration: %CONFIGURATION%
echo Runtime: %RUNTIME%
echo Output: %OUTPUT_PATH%

dotnet publish ..\..\VeeamHealthCheck.csproj ^
    -c %CONFIGURATION% ^
    -r %RUNTIME% ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -o %OUTPUT_PATH%

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Publish completed successfully!
    echo Output location: %OUTPUT_PATH%
) else (
    echo.
    echo Publish failed!
    exit /b %ERRORLEVEL%
)

pause
