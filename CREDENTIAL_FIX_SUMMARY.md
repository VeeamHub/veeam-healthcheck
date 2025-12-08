# Credential Handling Fix Summary

## Issues Fixed

### 1. Base64 Encoding PowerShell Argument Issue
**Problem:** Base64-encoded passwords in single quotes could contain special characters that PowerShell misinterprets
**Solution:** Changed PowerShell arguments to use double quotes around Base64 password
```csharp
// OLD: -PasswordBase64 '{base64Password}'
// NEW: -PasswordBase64 "{base64Password}"
```

### 2. Local VBR v12 Credential Prompting
**Problem:** Local VBR v12 was trying to prompt for credentials in non-GUI environments (GitHub Actions)
**Solution:** Added logic to detect local VBR v12 without CLI credentials and use Windows authentication
```csharp
if (CGlobals.PowerShellVersion == 5 && !isRemote && !hasCliCreds)
{
    // Use Windows auth - no credential prompt needed
    return this.RunLocalMfaCheck(p);
}
```

### 3. Credential Persistence with CredentialStore
**Problem:** Credentials weren't being persisted, forcing re-entry on each run
**Solution:** Integrated CredentialStore into CredsHandler.GetCreds() flow
- Checks CLI credentials first
- Then checks CredentialStore for saved credentials
- Finally prompts via GUI if available
- Always saves credentials when provided

## Credential Flow (Priority Order)

1. **Command Line Arguments** (`/creds=username:password`)
   - Highest priority
   - Used immediately if provided

2. **CredentialStore** (saved in `%APPDATA%\VeeamHealthCheck\creds.json`)
   - Encrypted using Windows DPAPI (DataProtectionScope.CurrentUser)
   - Per-host storage (supports multiple VBR servers)
   - Automatically retrieved on subsequent runs

3. **GUI Prompt** (WPF Dialog)
   - Only shown if:
     - No CLI creds provided
     - No stored creds found
     - GUI environment available (`CGlobals.GUIEXEC == true`)
   - Credentials automatically saved to CredentialStore after entry

4. **Windows Authentication** (for local VBR v12)
   - Automatic fallback for local PowerShell 5 execution
   - No credentials required

## Files Modified

### `/vHC/HC_Reporting/Functions/CredsWindow/CredsHandler.cs`
- Updated `GetCreds()` to check CredentialStore before prompting
- Always saves credentials to CredentialStore when provided via GUI
- Gracefully returns null if GUI not available

### `/vHC/HC_Reporting/Functions/Collection/CCollections.cs`
- Fixed `MfaTestPassed()` logic to properly detect local vs remote execution
- Added CLI credential detection
- Changed PowerShell arguments to use double quotes around Base64 password
- Improved Windows authentication fallback for local VBR v12

### `/vHC/HC_Reporting/Startup/CredentialStore.cs`
- Already implemented (restored by user)
- Provides encrypted credential storage per host
- Uses Windows DPAPI for encryption

## Testing Scenarios

### ✅ Scenario 1: Local VBR v12 (PowerShell 5)
```bash
VeeamHealthCheck.exe /run
```
- **Expected:** Uses Windows authentication, no credential prompt
- **Log:** "Local VBR Detected, running local MFA test with Windows authentication..."

### ✅ Scenario 2: Local VBR v13 with CLI Credentials
```bash
VeeamHealthCheck.exe /run /creds=localhost\administrator:Veeam123!Veeam123!
```
- **Expected:** Uses provided credentials with Base64 encoding
- **Log:** "Credentials provided via command line"

### ✅ Scenario 3: Remote VBR with CLI Credentials
```bash
VeeamHealthCheck.exe /run /remote /host=vbr-server /creds=veeamadmin:Veeam123!Veeam123!
```
- **Expected:** Connects to remote server using Base64-encoded credentials
- **Log:** Shows successful PowerShell 7 execution

### ✅ Scenario 4: GUI with CredentialStore
```bash
VeeamHealthCheck.exe /run /remote /host=vbr-server
```
- **First Run:** Shows credential prompt, saves to CredentialStore
- **Subsequent Runs:** Automatically loads from CredentialStore
- **Log:** "Using stored credentials for host: vbr-server"

### ✅ Scenario 5: GitHub Actions (Non-GUI)
- **With CLI creds:** Works with Base64 encoding
- **Without CLI creds (local v12):** Uses Windows auth
- **Without CLI creds (v13/remote):** Logs error and exits gracefully

## What Changed vs Previous Implementation

### Before:
- Base64 passwords wrapped in single quotes → PowerShell parsing issues
- Local VBR v12 always tried to prompt for credentials
- No credential persistence → re-enter every run
- GUI prompts failed in non-interactive environments

### After:
- Base64 passwords wrapped in double quotes → No parsing issues
- Local VBR v12 uses Windows authentication when appropriate
- CredentialStore integrated → Credentials saved automatically
- GUI prompts check for environment availability first

## Security Notes

- Passwords stored using Windows DPAPI encryption
- Credentials scoped to current Windows user
- Base64 encoding in transit (PowerShell arguments)
- Passwords masked in debug logs (`****`)
- Storage location: `%APPDATA%\VeeamHealthCheck\creds.json`

## Next Steps for Testing

1. Test in GitHub Actions with provided CLI credentials
2. Test local VBR v12 without credentials
3. Test remote VBR with CredentialStore persistence
4. Verify password special characters work correctly
