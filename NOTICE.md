# Upstream and redistribution

ShareX Receipts is an experimental modified distribution of ShareX, not an official ShareX release. ShareX and its marks belong to their respective owners. No endorsement is implied.

Upstream: https://github.com/ShareX/ShareX ; tag v21.0.0 ; commit d2502561f63fc3ff502cacd91514e3f7f2948c74. Copyright 2007–2026 ShareX Team. The upstream root LICENSE.txt supplies GPL version 3; individual upstream source headers include version 2 or later. Existing headers and third-party notices are retained in corresponding source. New receipt code is GPL-3.0-or-later.

The binary release must be accompanied by the matching `ShareX-Receipts-corresponding-source.zip`, including the complete patched upstream tree, receipt projects and build scripts. That archive excludes generated binaries, caches and Git metadata. Public source acquisition plus the checked patcher also reproduces the fork. Dependencies retain their own licenses and notices; runtime and library notices included by publishing are retained.

Changes: opt-in original-pixel receipt capture, processing-boundary commitments, immutable bundle and independent-digest verification, Tools/task-list inspector, isolated synthetic test modes, and SSH.NET dependency update from 2025.1.0 to 2026.0.0.

Provided without warranty. Capture receipts contain pixels; treat them with the sensitivity of the captured content.
