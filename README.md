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

```powershell
dotnet publish src/ScreenStat.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64
```

生成的主程序：`publish/win-x64/ScreenStat.exe`（自包含，无需安装 .NET Runtime）。

## 使用

1. 启动后进入系统托盘
2. 按 `Ctrl+Shift+X`（或托盘菜单 / 双击托盘图标）
3. 拖动选择屏幕区域
4. 查看统计结果并复制
