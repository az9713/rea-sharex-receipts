# Investigation: from managed metadata to capture integration

## Evidence boundary

The existing REA analysis record `ev_befabb9d75ae6a55ac38b53d6a52590ea7fdf362c0c81389d92bb810b0c7fca8` reported assembly ShareX 21.0.0.0 and `.NETCoreApp,Version=v9.0`; SHA-256 `eae64fc357b3548b561b3c02820a552ac7264450971ba1e4c892c8455c9a7190`. The record explicitly excluded CLR loading, C# decompilation and execution. Public tag matching is version alignment, not binary provenance proof. No private settings/logs/history were consumed.

## Owning source route

All links pin upstream commit `d2502561f63fc3ff502cacd91514e3f7f2948c74`:

- [CaptureBase](https://github.com/ShareX/ShareX/blob/d2502561f63fc3ff502cacd91514e3f7f2948c74/ShareX/CaptureHelpers/CaptureBase.cs) dispatches accepted capture metadata into the task route. Earlier region-editor changes can already be in the bitmap.
- [WorkerTask](https://github.com/ShareX/ShareX/blob/d2502561f63fc3ff502cacd91514e3f7f2948c74/ShareX/WorkerTask.cs) owns after-capture processing, image preparation, file actions and subsequent OCR. The new session begins before optional beautify/effects/annotation and seals after DoFileJobs. A flag forces image preparation/file save even when SaveImageToFile is unset.
- [TaskHelpers](https://github.com/ShareX/ShareX/blob/d2502561f63fc3ff502cacd91514e3f7f2948c74/ShareX/TaskHelpers.cs) owns format/JPEG conversion and output path handling. The synthetic integration uses that real conversion path.
- [Enums](https://github.com/ShareX/ShareX/blob/d2502561f63fc3ff502cacd91514e3f7f2948c74/ShareX/Enums.cs) defines flags; the new flag is 1<<22, leaving existing bit meanings intact. DescriptionAttribute supplies its menu label.
- [MainForm](https://github.com/ShareX/ShareX/blob/d2502561f63fc3ff502cacd91514e3f7f2948c74/ShareX/Forms/MainForm.cs) supplies Tools and task context commands; the selected Worker's FilePath locates the sidecar. No history database schema migration is needed.

## Dependency repair

Baseline restore produced NU1903 warnings for SSH.NET 2025.1.0. [SCP remote-path advisory](https://github.com/sshnet/SSH.NET/security/advisories/GHSA-mggc-4xg6-vcxf) and [recursive-download advisory](https://github.com/sshnet/SSH.NET/security/advisories/GHSA-q939-rpr3-3284) identify 2026.0.0 as the patched release. The fork updates the centrally managed package to that version. Restore/build passed afterward without those warnings. Network transfer compatibility and complete dependency security were not tested.

## Execution evidence

The isolated entry intercepts custom receipt arguments before upstream StartupWork, configuration/profile discovery or normal startup. The actual compiled WorkerTask.DoThreadJob processes a synthetic bitmap with only SaveResearchReceipt enabled and JPEG40 configured. Verification asserts preserved original identity, a different lossy result identity, exact bundle hashes and no issues. The inspector was rendered by its own Form.DrawToBitmap; this is a programmatic render, not a desktop screenshot or a manual capture/hotkey acceptance test. No real screen capture, OCR, upload or installed-app mutation was performed.

## Limits and failure behavior

Receipt failures fail the owning task before OCR/upload. A sidecar collision preserves the old link and reports failure. File actions run before sealing, so their final bytes are committed but their internals are unknown. A crash can leave `.pending-*` debris or a valid bundle without a sidecar; no automatic deletion of prior receipts occurs. SHA-256 commits content without attesting an honest capture. The release is a tested prototype, not a forensic certification.
