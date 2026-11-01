. (Join-Path $PSScriptRoot 'local-env.ps1')
$tools = Join-Path $projectRoot '.tools'
$sdk = Join-Path $tools 'dotnet\dotnet.exe'
if (Test-Path $sdk) { return }
New-Item -ItemType Directory -Force $tools | Out-Null
$metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/9.0/releases.json'
$release = $metadata.releases | Where-Object { $_.sdk.version -eq '9.0.320' } | Select-Object -First 1
$file = $release.sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
if (-not $file) { throw 'Pinned SDK absent from official release metadata' }
$zip = Join-Path $tools 'sdk-bootstrap.zip'
Invoke-WebRequest $file.url -OutFile $zip
if ((Get-FileHash $zip -Algorithm SHA512).Hash -ne $file.hash) { throw 'SDK SHA512 mismatch' }
Expand-Archive $zip (Join-Path $tools 'dotnet')
& $sdk --version
if ($LASTEXITCODE) { throw 'SDK bootstrap failed' }
