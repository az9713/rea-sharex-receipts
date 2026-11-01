param([switch]$Publish)
. (Join-Path $PSScriptRoot 'local-env.ps1')
. (Join-Path $PSScriptRoot 'bootstrap.ps1')
Set-Location $projectRoot
$sdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
& (Join-Path $PSScriptRoot 'prepare-fork.ps1')
& $sdk build upstream\ShareX\ShareX.csproj -c Release -p:Platform=x64 --nologo
if ($LASTEXITCODE) { throw 'ShareX build failed' }
& $sdk build src\ShareX.Receipts.Cli\ShareX.Receipts.Cli.csproj -c Release --nologo
if ($LASTEXITCODE) { throw 'Verifier build failed' }
if ($Publish) {
 & $sdk publish upstream\ShareX\ShareX.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -o releases\ShareX-Receipts-win-x64 --nologo
 if ($LASTEXITCODE) { throw 'ShareX publish failed' }
 & $sdk publish src\ShareX.Receipts.Cli\ShareX.Receipts.Cli.csproj -c Release -r win-x64 --self-contained true -o releases\Receipt-CLI-win-x64 --nologo
 if ($LASTEXITCODE) { throw 'Verifier publish failed' }
 # Upstream portable marker keeps normal application data beside this release.
 New-Item -ItemType File -Force releases\ShareX-Receipts-win-x64\Portable | Out-Null
 Copy-Item LICENSE releases\ShareX-Receipts-win-x64\LICENSE.txt -Force
 Copy-Item NOTICE.md releases\ShareX-Receipts-win-x64\RECEIPTS-NOTICE.md -Force
 Copy-Item LICENSE releases\Receipt-CLI-win-x64\LICENSE.txt -Force
}
