param(
    [string]$Configuration = "Release",

    # Framework-dependent package: a small executable that expects the user to
    # install the .NET Desktop Runtime, without the dataset collection tooling.
    [switch]$Slim
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "src\ScreenStat.App\ScreenStat.App.csproj"
$publishRoot = Join-Path $repositoryRoot "publish"

$packageName = if ($Slim) { "ScreenStat-win-x64-slim" } else { "ScreenStat-win-x64" }
$publishDirectory = Join-Path $publishRoot $packageName
$zipPath = Join-Path $publishRoot "$packageName.zip"

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

$publishArguments = @(
    $projectPath,
    "-c", $Configuration,
    "-r", "win-x64",
    "--no-restore",
    "-o", $publishDirectory
)

if ($Slim) {
    # SlimBuild also drives the SCREENSTAT_SLIM compile constant, which strips
    # the collection wiring — see ScreenStat.App.csproj.
    $publishArguments += "-p:SlimBuild=true"
    $publishArguments += "--self-contained"
    $publishArguments += "false"
} else {
    $publishArguments += "--self-contained"
    $publishArguments += "true"
}

& dotnet publish @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if ($Slim) {
    # The .NET apphost already shows a dialog with a download link when the
    # runtime is missing, but that only appears after a failed launch. Say it
    # up front, in the package.
    $readmePath = Join-Path $publishDirectory "先读我.txt"
    # Single-quoted, and no typographic quotes in the text: PowerShell accepts
    # the curly quote characters as string delimiters, so a Chinese quoted
    # phrase inside a double-quoted line ends the string early and fails to
    # parse. Corner brackets are unambiguous.
    $readmeLines = @(
        'ScreenStat 精简版',
        '',
        '本版本不含 .NET 运行时，体积小，但需要先安装：',
        '',
        '    .NET 10 Desktop Runtime (x64)',
        '    https://dotnet.microsoft.com/download/dotnet/10.0',
        '',
        '下载页面上选「Desktop Runtime」的 x64 安装包 —— 注意不是「Runtime」，',
        '也不是 SDK，三个名字在页面上挨着，装错了程序照样起不来。',
        '',
        '装好后双击 ScreenStat.exe 即可，程序会常驻系统托盘。',
        '未安装运行时直接运行时，Windows 会弹出提示并给出下载链接。',
        '',
        '请保留 ScreenStat.exe 与 models 目录的相对位置，模型不随程序下载。',
        '',
        '不想装运行时的话，请改用自包含版 ScreenStat-win-x64.zip，解压即用。',
        '',
        '热键：Ctrl+Shift+X'
    )
    Set-Content -LiteralPath $readmePath -Value $readmeLines -Encoding UTF8
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

Write-Host ""
Write-Host ("Package: {0}" -f $(if ($Slim) { "slim (framework-dependent)" } else { "portable (self-contained)" }))
Write-Host "Publish directory: $publishDirectory"
Write-Host ("Unpacked size: {0:N1} MB" -f ($unpackedBytes / 1MB))
Write-Host ("ZIP size: {0:N1} MB" -f ($zip.Length / 1MB))
Write-Host "SHA256: $($hash.Hash)"
