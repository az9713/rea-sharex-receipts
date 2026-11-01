param([string]$Upstream = (Join-Path $PSScriptRoot '..\upstream'))
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$Upstream = [IO.Path]::GetFullPath($Upstream)
if (-not (Test-Path (Join-Path $Upstream '.git'))) {
    git clone --depth 1 --branch v21.0.0 https://github.com/ShareX/ShareX.git $Upstream
    if ($LASTEXITCODE) { throw 'Source acquisition failed' }
}
$revision = (git -C $Upstream rev-parse HEAD).Trim()
if ($revision -ne 'd2502561f63fc3ff502cacd91514e3f7f2948c74') { throw 'Unexpected ShareX revision' }
$env:SHAREX_FORK_ROOT = $Upstream
$env:SHAREX_RECEIPTS_ROOT = $projectRoot
python (Join-Path $PSScriptRoot 'patch_fork.py')
if ($LASTEXITCODE) { throw 'Source patch failed' }
