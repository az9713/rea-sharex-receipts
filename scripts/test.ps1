param([string]$Output = '')
. (Join-Path $PSScriptRoot 'local-env.ps1')
Set-Location $projectRoot
if (-not $Output) { $Output = Join-Path $projectRoot ('fixtures\run-' + [Guid]::NewGuid().ToString('N')) }
if (-not [IO.Path]::IsPathRooted($Output)) { $Output = Join-Path $projectRoot $Output }
$Output = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force $Output | Out-Null
$sdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
& $sdk run --project tests\ReceiptTests.csproj -c Release -- (Join-Path $Output 'core')
if ($LASTEXITCODE) { throw 'Receipt tests failed' }
& $sdk upstream\ShareX\bin\Release\win-x64\ShareX.dll --receipt-self-test (Join-Path $Output 'integration')
if ($LASTEXITCODE -or -not (Test-Path (Join-Path $Output 'integration\integration-result.json'))) { throw 'Modified ShareX integration test failed' }
$result = Get-Content (Join-Path $Output 'integration\integration-result.json') -Raw | ConvertFrom-Json
if (-not $result.success) { throw 'Integration result unsuccessful' }
Write-Output "Verified synthetic modified-app run: $Output"
