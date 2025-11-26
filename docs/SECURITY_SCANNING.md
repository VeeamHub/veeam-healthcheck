# Security Scanning & False Positives

## Current Status

Our CI/CD pipeline scans releases with VirusTotal (76+ antivirus engines). We occasionally see false positives from less common AV vendors.

### Latest Results
- **Detection Rate**: 1/76 engines (Zillya)
- **All Major Vendors**: ✅ Clean (Microsoft, Kaspersky, Sophos, McAfee, Symantec, etc.)
- **Assessment**: False positive due to unsigned single-file .NET executable

## Why False Positives Occur

1. **Single-File Executable**: .NET's single-file publish uses self-extraction
2. **No Code Signing**: Unsigned binaries trigger heuristic detections
3. **Compression**: Heavy compression can look suspicious
4. **PowerShell Integration**: Tool executes PowerShell scripts internally

## Current Threshold Policy

The workflow allows up to 2 detections from obscure AV engines:
- **0 detections**: ✅ Pass (ideal)
- **1-2 detections**: ⚠️ Warning + Pass (likely false positive)
- **3+ detections**: ❌ Fail (needs investigation)

This prevents obscure AVs like Zillya from blocking legitimate releases while still catching real issues.

## Long-Term Solutions

### 1. Code Signing Certificate (Most Effective) ⭐

**Benefits:**
- Dramatically reduces false positives (typically to 0)
- Builds user trust
- Required for some enterprise environments
- Windows SmartScreen won't block downloads

**Options:**
- **Standard Code Signing**: ~$100-200/year (e.g., Sectigo, DigiCert)
- **EV Code Signing**: ~$300-500/year (includes hardware token, immediate trust)

**Implementation:**
```powershell
# Sign the executable before packaging
signtool sign /f certificate.pfx /p password /tr http://timestamp.digicert.com /td sha256 /fd sha256 "VeeamHealthCheck.exe"
```

**GitHub Actions Integration:**
```yaml
- name: Import code signing certificate
  run: |
    $pfxBytes = [Convert]::FromBase64String("${{ secrets.CODE_SIGNING_CERT }}")
    [IO.File]::WriteAllBytes("cert.pfx", $pfxBytes)

- name: Sign executable
  run: |
    & "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe" sign `
      /f cert.pfx `
      /p "${{ secrets.CERT_PASSWORD }}" `
      /tr http://timestamp.digicert.com `
      /td sha256 `
      /fd sha256 `
      "publish/out/VeeamHealthCheck.exe"
```

### 2. Submit False Positive Reports

When a false positive occurs, submit to the vendor:

- **Zillya**: https://zillya.com/support/false-positive
- **Most vendors**: VirusTotal has a "Request re-analysis" feature

### 3. Alternative Publishing Method

Instead of single-file publish, use framework-dependent with separate DLLs:

```xml
<!-- In .csproj -->
<PublishSingleFile>false</PublishSingleFile>
<SelfContained>false</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
```

**Pros:**
- Fewer false positives
- Smaller download size

**Cons:**
- Requires .NET 8 runtime on target machine
- Multiple files to manage

### 4. Windows Defender Scan (Pre-check)

Add a Windows Defender scan before VirusTotal:

```yaml
- name: Scan with Windows Defender
  run: |
    $exePath = "publish/out/VeeamHealthCheck.exe"
    & 'C:\Program Files\Windows Defender\MpCmdRun.exe' -Scan -ScanType 3 -File $exePath
    
    if ($LASTEXITCODE -eq 0) {
      Write-Host "✅ Windows Defender: Clean"
    } else {
      Write-Host "❌ Windows Defender detected a threat"
      exit 1
    }
```

## Recommended Path Forward

### Short Term (Current)
✅ Accept 1-2 false positives from obscure AVs
✅ Monitor major vendors (all must be clean)
✅ Document scan results in releases

### Medium Term (Next 3 months)
- [ ] Investigate code signing certificate options
- [ ] Submit false positives to vendors for whitelisting
- [ ] Add Windows Defender pre-check

### Long Term (Next 6 months)
- [ ] Obtain EV code signing certificate
- [ ] Implement automated signing in CI/CD
- [ ] Achieve consistent 0 detections

## Verification for Users

Users can verify downloads independently:

### Check VirusTotal
```powershell
# Get file hash
$hash = (Get-FileHash "VeeamHealthCheck-X.X.X.zip" -Algorithm SHA256).Hash

# Visit https://www.virustotal.com/gui/home/search
# Paste the hash to see scan results
```

### Windows Defender Scan
```powershell
& 'C:\Program Files\Windows Defender\MpCmdRun.exe' -Scan -ScanType 3 -File "VeeamHealthCheck.exe"
```

### Verify Release Hash
```powershell
$expected = "HASH_FROM_RELEASE_NOTES"
$actual = (Get-FileHash "VeeamHealthCheck-X.X.X.zip" -Algorithm SHA256).Hash

if ($expected -eq $actual) {
  Write-Host "✅ Hash verified - file is authentic"
} else {
  Write-Host "❌ Hash mismatch - file may be compromised"
}
```

## Resources

- [Microsoft: Code Signing Best Practices](https://docs.microsoft.com/en-us/windows-hardware/drivers/install/code-signing-best-practices)
- [VirusTotal API Documentation](https://docs.virustotal.com/reference/overview)
- [NIST: Software Supply Chain Security](https://csrc.nist.gov/Projects/ssdf)
