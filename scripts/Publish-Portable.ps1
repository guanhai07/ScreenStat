param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "src\ScreenStat.App\ScreenStat.App.csproj"
$publishRoot = Join-Path $repositoryRoot "publish"
$publishDirectory = Join-Path $publishRoot "ScreenStat-win-x64"
$zipPath = Join-Path $publishRoot "ScreenStat-win-x64.zip"

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

if (Test-Path -LiteralPath $publishDirectory) {
    $resolvedPublishRoot = (Resolve-Path -LiteralPath $publishRoot).Path.TrimEnd('\') + '\'
    $resolvedPublishDirectory = (Resolve-Path -LiteralPath $publishDirectory).Path
    if (-not $resolvedPublishDirectory.StartsWith($resolvedPublishRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a directory outside publish root: $resolvedPublishDirectory"
    }

    Remove-Item -LiteralPath $resolvedPublishDirectory -Recurse -Force
}

& dotnet restore $projectPath -r win-x64
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE"
}

& dotnet publish $projectPath `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    --no-restore `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

# Use the absolute path: when this script is launched from a Git Bash shell,
# a bare "tar.exe" resolves to Git's /usr/bin/tar, which reads "D:\..." as a
# remote host and fails with "Cannot connect to D: resolve failed".
$tarPath = Join-Path $env:SystemRoot "System32\tar.exe"
& $tarPath -a -c -f $zipPath -C $publishDirectory .
if ($LASTEXITCODE -ne 0) {
    throw "ZIP packaging failed with exit code $LASTEXITCODE"
}

$unpackedBytes = (Get-ChildItem -LiteralPath $publishDirectory -File -Recurse | Measure-Object Length -Sum).Sum
$zip = Get-Item -LiteralPath $zipPath
$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256

Write-Host "Publish directory: $publishDirectory"
Write-Host ("Unpacked size: {0:N1} MB" -f ($unpackedBytes / 1MB))
Write-Host ("ZIP size: {0:N1} MB" -f ($zip.Length / 1MB))
Write-Host "SHA256: $($hash.Hash)"
