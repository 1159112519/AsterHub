# Local native storage enumeration

## Scope
Only the local StorageScanJob enumeration entry point changes. Remote connections continue to use FileConnection.Enumerate. Existing scan limits, traversal order, exclusions, link handling, pause/cancel and snapshot behavior remain defined by the target branch. No MFT/USN parser or parallel traversal is introduced.

LocalStorageScan.cs is independently implemented against the public Windows directory-enumeration API. This change adds no third-party package, binary, driver or service dependency and does not copy Sigma or other file-manager implementations. This statement covers the new scanner, not a license audit of the rest of the application.

The enumerator requests FindExInfoBasic and FIND_FIRST_EX_LARGE_FETCH, falling back to ordinary enumeration on ERROR_INVALID_PARAMETER / ERROR_NOT_SUPPORTED. FindClose executes in a finally block. Unicode names, extended-length child paths, 64-bit file sizes, UTC modification times, read-only metadata and reparse-point flags are preserved. The existing scan layer continues to skip reparse points.

References:
- https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findfirstfileexw
- https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findnextfilew
- https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findclose
- https://learn.microsoft.com/en-us/windows/win32/api/minwinbase/ns-minwinbase-win32_find_dataw

## Verification
Run on Windows:

```powershell
./desktop-preview/scripts/bugfix-tests/run-storage-tests.ps1 -EvidenceRoot "$env:TEMP/AsterHub-native-storage-tests"
```

The runner compiles real backend sources and the real scanner. It runs the existing storage regressions, nine native-enumeration tests and a real directory-listing bridge check. New coverage includes Unicode/UTC metadata, empty and missing directories, cancellation before and during enumeration, early-dispose handle cleanup, a sparse file larger than 4 GiB, read-only attributes, and real StorageScanJob aggregation.

The dedicated GitHub Actions workflow runs the same runner and uploads text evidence. It does not publish installers, update channels or website content.

## Baseline and acceptance boundary
This draft targets the public source baseline, whose desktop-preview/VERSION is 0.3.0-preview.23. A separate local 0.4.3.2 candidate was built and tested before this PR. The public branch deliberately does not import that candidate's entire source snapshot, packaging isolation settings or unrelated product changes.

Reconcile the shared product baseline before final integration and rerun the scan/bridge regressions against the combined result. Do not replace a newer StorageIndex.cs with the old baseline file: port only the local/remote enumeration selection and the new helper. Do not infer full-drive coverage or a fixed performance gain from the small regression fixtures. No new full-drive benchmark, remote SSH session, Explorer interaction or complete GUI regression is claimed by this PR.
