# ScreenStat

Windows 屏幕框选数字统计工具。

## 功能

1. 系统托盘常驻
2. 全局热键 `Ctrl+Shift+X` 框选屏幕区域
3. Windows 本地 OCR 识别文字
4. 提取数字并计算 Count / Sum / Avg / Min / Max / Median / P90 / P95 / P99
5. 识别数字可手动修改，修改后自动重算统计
6. 一键复制统计结果、数字列表、OCR 原文
7. 全程本地处理，不上传截图

## 开发环境

- Windows 10/11
- .NET 7 SDK（当前仓库目标框架；可升级到 .NET 8/10）
- 需要系统安装 OCR 语言包（中文/英文光学字符识别）

## 构建

```powershell
dotnet restore
dotnet build
dotnet test
```

## 运行

```powershell
dotnet run --project src/ScreenStat.App
```

## 发布

自包含单文件版（推荐；目标机器无需安装 .NET Runtime，单文件约 77 MB）：

```powershell
dotnet publish src/ScreenStat.App/ScreenStat.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o publish/single-exe
```

生成的主程序：`publish/single-exe/ScreenStat.exe`（单个 exe，无需安装 .NET Runtime，约 77 MB）。

框架依赖版（约 21 MB，但目标机器需安装 .NET 7 Desktop Runtime）：

```powershell
dotnet publish src/ScreenStat.App -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/framework-dependent
```

生成的主程序：`publish/framework-dependent/ScreenStat.exe`。

体积说明：

- 自包含单文件版：启用 `EnableCompressionInSingleFile` 并把原生库压缩后内嵌进 exe，约 77 MB。
- 未压缩自包含版：`dotnet publish src/ScreenStat.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64`，约 172 MB。
- 框架依赖版：不打包 Runtime，约 21 MB，但目标机器必须安装 .NET 7 Desktop Runtime。

## 使用

1. 启动后进入系统托盘
2. 按 `Ctrl+Shift+X`（或托盘菜单 / 双击托盘图标）
3. 拖动选择屏幕区域
4. 查看统计结果并复制
