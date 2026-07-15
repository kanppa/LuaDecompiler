param(
  [string]$OutputRoot = (Join-Path $PSScriptRoot 'artifacts')
)

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'LuaDecompiler.csproj'
$outputRoot = [IO.Path]::GetFullPath($OutputRoot)
$publish = Join-Path $outputRoot 'app'
if (Test-Path -LiteralPath $publish) {
  Remove-Item -LiteralPath $publish -Recurse -Force
}
dotnet publish $project `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  --output $publish
if ($LASTEXITCODE -ne 0) {
  throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$archive = Join-Path $outputRoot 'LuaDecompiler-v0.0.1-win-x64.zip'
if (Test-Path -LiteralPath $archive) {
  Remove-Item -LiteralPath $archive -Force
}
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive -CompressionLevel Optimal
Write-Host "Published to: $publish"
Write-Host "Archive: $archive"
