# /credfile= credentials decrypt-portable but cross-machine DPAPI assumptions are inconsistent

**Category:** startup-cli
**Severity:** Medium
**Type:** Security
**File(s):** `vHC/HC_Reporting/Startup/CArgsParser.cs:467-570`, `vHC/HC_Reporting/Startup/CredentialStore.cs:159-173`

## Summary
The `/credfile=` JSON format carries the password as raw Base64 of the UTF-8 plaintext (`passwordBase64`), not DPAPI-protected. `LoadCredFile` Base64-decodes it to plaintext and calls `SetTransient`, which then DPAPI-`Protect`s it into the in-memory cache. So a credfile is an **unencrypted password at rest** in a JSON file on disk — only Base64-obscured. This is a meaningfully weaker storage model than the DPAPI `creds.json` the tool otherwise champions, and the help text ("Does NOT persist to DPAPI") understates that the source file itself is effectively cleartext.

## Evidence
```csharp
plain = Encoding.UTF8.GetString(Convert.FromBase64String(kvp.Value.PasswordBase64));
...
CredentialStore.SetTransient(kvp.Key, kvp.Value.Username, plain);
```
The credfile schema:
```
{ "<host>": { "username": "...", "passwordBase64": "<base64-of-plaintext>" } }
```

## Impact
Operators following the documented fleet workflow may store credfiles on shared paths assuming "passwordBase64" implies protection; Base64 is encoding, not encryption. Anyone who can read the credfile obtains the plaintext VBR password. There is also no `File.Exists`/permission check or warning that the file should be ACL-restricted, and no scrubbing of the decrypted `plain` string from memory.

## Suggested Fix
- Document explicitly (help menu + the LoadCredFile XML doc) that `passwordBase64` is NOT encrypted and the file must be ACL-locked / deleted after use.
- Strongly prefer accepting a DPAPI-protected blob (or an OS credential-manager reference) over Base64 plaintext for the credfile format.
- Warn at load time if the file is world/group-readable.

## Labels
security, credentials, plaintext, documentation, medium
