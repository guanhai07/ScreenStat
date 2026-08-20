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

有两个包，按是否愿意装运行时选：

| 包 | 下载体积 | 前置条件 | 适合 |
|---|---|---|---|
| `ScreenStat-win-x64.zip` | 约 94.7 MB | 无 | 解压即用，换机器直接拷 |
| `ScreenStat-win-x64-slim.zip` | 约 28.7 MB | 需装 .NET 10 Desktop Runtime (x64) | 下载体积敏感，或机器上已有运行时 |

两个包的识别能力完全一致。精简版另外不含数据采集功能 —— 那是维护回归数据集用的，对普通使用没有意义。

1. 解压，保留 `ScreenStat.exe` 和 `models` 目录的相对位置。
2. 精简版还需先安装 [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) —— 下载页上选 **Desktop Runtime**，不是 Runtime 也不是 SDK。包内 `先读我.txt` 也写了这一条；没装就运行的话 Windows 会弹出提示并给出下载链接。
3. 双击 `ScreenStat.exe`；启动后程序进入系统托盘。
4. 按 `Ctrl+Shift+X`，拖动框选一列或多列数字。
5. 在结果窗口复核低置信度项，然后复制统计或数字。

两个包都不要求安装 Python、PaddleOCR 或 ONNX Runtime，也不要求 Windows OCR 语言包 —— 该语言包仅影响备用引擎。

## 发行体积

2026-08-20 在 `win-x64` 上的实测值。

自包含版：

- `ScreenStat.exe`：约 88.3 MB，包含 .NET 10 Desktop Runtime、程序及本地推理运行库。
- `models/v5`：约 13.1 MB，包含 PP-OCRv5 Latin mobile 模型。
- 解压目录：约 101.4 MB，ZIP 约 94.7 MB。

精简版（框架依赖）：

- `ScreenStat.exe`：约 54.1 MB。
- 解压目录：约 64.7 MB，ZIP 约 28.7 MB。

注意两版的 EXE 大小不可直接相比：`EnableCompressionInSingleFile` 只支持自包含发布，所以自包含版那 88.3 MB 是**压缩过的**单文件包，精简版的 54.1 MB 没有压缩。ZIP 体积才是可比的口径 —— 下载量少了 3.3 倍。

精简版省掉的是 .NET/WPF 运行时；剩下的体积主要是 ONNX Runtime、SkiaSharp 的原生库和 Windows SDK 投影程序集，这些两版都要带。模型不能省：项目不在首次运行时下载模型。Fluent 主题资源、数据集采集和新增窗口对体积没有可测量的影响。

启动与识别耗时为 2026-08-17 的开发机基准，此后未重新测量：自包含 EXE 冷启动约 1.0–1.5 秒；16 行测试图首次 OCR 约 1.8 秒、后续约 1.3–1.4 秒。实际速度会随 CPU、选区尺寸和字体而变化。

## 开发

要求：Windows 10/11、.NET 10 SDK。

```powershell
dotnet restore
dotnet build ScreenStat.sln -c Debug
dotnet test ScreenStat.sln -c Debug
dotnet run --project src/ScreenStat.App
```

OCR V2 的阶段状态、设计边界和中断恢复方法见 [`OCR_V2_PLAN.md`](OCR_V2_PLAN.md)。

## 采集测试数据

合成图覆盖不了真实报表的字体、DPI、主题和行距组合。采集模式把日常使用变成回归数据：托盘菜单勾选**采集测试数据**后，每次框选都会落盘一份样本，在结果窗口复核并保存标注，就得到一条带 ground truth 的回归用例。

### 采集流程

1. 托盘右键 → 勾选**采集测试数据**（选择会记住；托盘菜单里的**打开测试数据目录**可直接跳转）。
2. `Ctrl+Shift+X` 正常框选。结果窗口标题会变成“ScreenStat · 采集中”，底部出现采集面板。
3. 复核识别结果：
   - 识别错的数字直接改；
   - 不该统计的行取消勾选；
   - OCR 整行漏掉的，选中它上一行后点**在选中行后插入**，补上正确值；
   - 在**场景备注**里写清场景，例如 `深色主题 / 125% DPI / 12px / 某某报表`。
4. 点**保存标注**。误触或不想留的样本点**作废样本**，直接删掉整个目录。

### 数据位置与结构

开发态写到仓库内的 `tests/data/captures/`（已在 `.gitignore` 中，截图不会进版本库）；发行版没有仓库上下文，改写到 `%LOCALAPPDATA%\ScreenStat\dataset`。`SCREENSTAT_DATASET_DIR` 可覆盖路径，`SCREENSTAT_DATASET=1` 可强制开启采集。

```
tests/data/captures/
  20260818-143012-7f3a/
    capture.png     框选原始像素
    capture.json    当时的识别结果（引擎、耗时、每个检测框的文本/坐标/置信度、分列结果）
    labels.json     你复核后的 ground truth
  baseline.json     已知不通过的样本台账
```

`labels.json` 每行都带 `origin`，直接标出失败模式：

| origin | 含义 |
|---|---|
| `ocr` | 识别正确，原样保留 |
| `edited` | 识别错了，你改对了 —— **误读** |
| `added` | OCR 整行没返回，你补的 —— **漏检** |
| `excluded` | 不该统计的行 —— **误检** |

只有 `included: true` 且能解析出数值的行参与比对。

### 回归与基线

`dotnet test` 会跑 `ScreenStat.DatasetTests`，用当前算法重跑每个样本的截图，逐列逐行和标注比对，并打印命中率和失败模式统计。

新采集的样本如果当前算法过不了，把它写进 `tests/data/captures/baseline.json`：

```json
{ "knownFailures": [ { "captureId": "20260818-143012-7f3a", "reason": "孤立 1 漏检" } ] }
```

台账里的样本只报告不失败，不在台账里的样本必须通过。算法改好后从台账里删掉对应条目即可 —— 测试输出也会主动提示哪些样本已经可以移除。

`tests/ScreenStat.DatasetTests/SeedCapture/` 是随仓库提交的种子样本（深色主题单列 5 行），保证在还没有采集任何数据时这套回归也是被真正验证过的。

## 发布

自包含版：

```powershell
.\scripts\Publish-Portable.ps1
```

精简版（框架依赖，且剥离数据采集）：

```powershell
.\scripts\Publish-Portable.ps1 -Slim
```

两者分别生成 `publish/ScreenStat-win-x64[-slim]/`、同名 ZIP，以及 ZIP 的 SHA-256 校验值，互不覆盖。精简版包内会附一份 `先读我.txt` 说明运行时要求。

`-Slim` 通过 MSBuild 属性 `SlimBuild` 同时切三件事：框架依赖发布、关掉单文件压缩（自包含专有）、定义 `SCREENSTAT_SLIM` 编译常量。采集功能是靠这个常量在编译期剔除的，不是运行时隐藏。

第三方组件与许可证来源见 [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)。
