# ScreenStat

ScreenStat 是一个 Windows 10/11 x64 屏幕框选数字统计工具。它常驻系统托盘，按 `Ctrl+Shift+X` 框选浏览器报表中的一列或多列数字后，在本机完成 OCR、分列和统计。

## 功能

- PP-OCRv5 ONNX 本地识别，返回文字坐标和置信度。
- 根据坐标自动恢复一列或多列，并分别计算 Count / Sum / Average / Min / Max / Median / P90 / P95 / P99。
- 低置信度项目明确标记，可直接修改或取消勾选，统计立即重算。
- 多列数字以 TSV 格式复制，可直接粘贴到 Excel。
- ONNX 主引擎失败时自动回退到 Windows OCR，并显示回退原因。
- 不调用第三方 API，不上传截图，不在首次运行时下载模型。

## 使用发行版

1. 解压 `ScreenStat-win-x64.zip`，保留 `ScreenStat.exe` 和 `models` 目录的相对位置。
2. 双击 `ScreenStat.exe`；启动后程序进入系统托盘。
3. 按 `Ctrl+Shift+X`，拖动框选一列或多列数字。
4. 在结果窗口复核低置信度项，然后复制统计或数字。

自包含发行版不要求安装 .NET、Python、PaddleOCR、ONNX Runtime，也不要求 Windows OCR 语言包。Windows OCR 语言包仅影响备用引擎。

## 发行体积

2026-08-17 在 `win-x64` 自包含发布上的实测值：

- `ScreenStat.exe`：约 88.3 MB，包含 .NET 10 Desktop Runtime、程序及本地推理运行库。
- `models/v5`：约 13.1 MB，包含 PP-OCRv5 Latin mobile 模型。
- 解压目录：约 101.4 MB。
- ZIP：约 94.6 MB。

体积主要来自自包含的 .NET/WPF 运行时和 ONNX Runtime。若改为依赖系统运行时可以更小，但会要求用户预装 .NET 10 Desktop Runtime，不符合“解压即用”的默认目标。

开发机基准：自包含 EXE 冷启动约 1.0–1.5 秒；16 行测试图首次 OCR 约 1.8 秒、后续约 1.3–1.4 秒。实际速度会随 CPU、选区尺寸和字体而变化。

## 开发

要求：Windows 10/11、.NET 10 SDK。

```powershell
dotnet restore
dotnet build ScreenStat.sln -c Debug
dotnet test ScreenStat.sln -c Debug
dotnet run --project src/ScreenStat.App
```

OCR V2 的阶段状态、设计边界和中断恢复方法见 [`OCR_V2_PLAN.md`](OCR_V2_PLAN.md)。

## 发布

```powershell
.\scripts\Publish-Portable.ps1
```

脚本会生成：

- `publish/ScreenStat-win-x64/`
- `publish/ScreenStat-win-x64.zip`
- ZIP 的 SHA-256 校验值

第三方组件与许可证来源见 [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)。
