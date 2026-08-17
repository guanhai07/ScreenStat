# ScreenStat OCR V2 开发计划

## 1. 目标

把 ScreenStat 从“Windows OCR + 文本启发式”升级为适合浏览器报表的完全本地小工具：

1. `Ctrl+Shift+X` 框选一列或多列数字。
2. 本地 OCR 返回文字、坐标和置信度，不调用云端 API。
3. 根据横坐标自动分列，根据纵坐标排序。
4. 每列独立计算 Count / Sum / Avg / Min / Max / Median / P90 / P95 / P99。
5. 低置信度与无法解析的内容可见、可修改，不静默丢弃。
6. Windows 10/11 x64 解压即用，不要求安装 Python、Paddle、ONNX Runtime 或 .NET Runtime。

## 2. 产品边界

V2 优先解决“框选报表中的数字列”，不实现完整电子表格理解：

- 支持纯数字、小数、负数、千分位、百分比和常见单位。
- 支持一个选区内的一列或多列。
- 使用坐标聚类恢复列，不根据表头语义猜测列。
- 不识别合并单元格、跨页表格或复杂财务报表结构。
- 不使用云端 OCR、LLM 或第三方在线 API。

## 3. 技术方案

### 3.1 运行时

- 目标框架：`.NET 10 LTS`
- UI：WPF
- 发布：`win-x64` self-contained portable 目录/ZIP
- GPU：不要求，默认 ONNX Runtime CPU

### 3.2 OCR

- 主引擎：`RapidOcrNet 3.0.0`
- 模型：包内 PP-OCRv5 Latin mobile ONNX 模型
- 备用引擎：现有 `WindowsOcrService`
- 主引擎输出：检测框、识别文本、字符置信度
- 模型与推理全部本地运行

选择 RapidOcrNet 的原因：

- 直接支持 .NET 10。
- 使用 ONNX Runtime，不依赖 Python 或本地 OCR 服务。
- 包含 PP-OCRv5 Latin 模型，适合英文、数字和符号。
- 返回文本框坐标及字符置信度。
- 不依赖 OpenCV，减少原生依赖和发布体积。

### 3.3 结构化边界

```text
ScreenCaptureService
        │ BGRA screenshot
        ▼
ILayoutOcrService
        │ OcrDocument { regions + bounds + confidence }
        ▼
NumericRegionParser
        │ NumericToken { value + bounds + confidence }
        ▼
ColumnClusterer
        │ NumericColumn[]
        ▼
ColumnStatisticsService
        │ ColumnStatistics[]
        ▼
ResultViewModel / ResultWindow
```

OCR 只负责检测与识别；数字解析、列分组和统计保持确定性，且可独立单元测试。

## 4. 领域模型

计划新增：

- `OcrBounds`：截图像素坐标。
- `OcrRegion`：文字、坐标、置信度、来源引擎。
- `OcrDocument`：完整 OCR 结果与诊断信息。
- `NumericToken`：解析后的数值、单位、原文、坐标、置信度。
- `NumericColumn`：按横坐标聚类后的有序数值集合。
- `ColumnStatisticsResult`：列信息与统计结果。

## 5. 阶段与提交点

每个阶段必须执行：

```powershell
dotnet build ScreenStat.sln -c Debug
dotnet test ScreenStat.sln -c Debug
git diff --check
```

通过后立即提交，并更新本文档状态。

| 阶段 | 内容 | 验收标准 | 计划提交信息 | 状态 |
|---|---|---|---|---|
| V2-0 | 保存现有 OCR 修复基线 | 31/31 测试通过 | `fix(ocr): improve small dark text recognition` | ✅ `5f86947` |
| V2-1 | 迁移 .NET 10 | 全项目以 net10 构建测试 | `chore: migrate ScreenStat to .NET 10` | ✅ `095c0b8` |
| V2-2 | OCR/列领域模型和聚类算法 | 单列、多列、错位、低置信度测试通过 | `feat(core): add coordinate based column analysis` | ✅ `29cecaa` |
| V2-3 | RapidOcrNet 本地引擎 | 真实截图返回坐标、文本、置信度；无网络 | `feat(ocr): add local ONNX layout OCR` | ✅ `753f804` |
| V2-4 | 工作流接入与 Windows OCR 回退 | 主引擎失败时可回退；错误可见 | `feat(app): integrate local OCR with fallback` | ✅ `7a244de` |
| V2-5 | 多列结果界面 | 每列独立统计；可编辑/排除低置信度项 | `feat(ui): add multi-column statistics review` | ✅ `ae10214` |
| V2-6 | 真机回归与离线发布 | 16 行浏览器样本完整；portable ZIP 可在无运行时机器启动 | `release: package offline OCR v2` | ✅ 本提交 |

## 6. 测试矩阵

### 自动化

- 16 行单列整数。
- 16 行小数、负数和百分比。
- 两列与三列，列间距不同。
- 行轻微错位、缺失单元格。
- OCR 框顺序随机，输出仍按列/行稳定排序。
- 低置信度数字进入复核列表。
- 同一行多个数字不再只保留一个。
- 主 OCR 初始化/推理失败时回退。
- 统计结果和手动修正保持同步。

### 真机

- Chrome / Edge，100% / 125% 页面缩放。
- Windows 125% / 150% DPI。
- 深色/浅色报表。
- 12px / 14px / 16px 字体。
- 单列、相邻多列、带表格线和隔行背景。
- 无网络环境启动与识别。

## 7. 发布要求

- 发布目录包含 `ScreenStat.exe`、本地原生库和 `models/`。
- 打包为一个 portable ZIP；用户只需解压并双击 exe。
- 不要求系统安装 .NET、Python、PaddleOCR 或 ONNX Runtime。
- 不在首次运行时下载模型。
- 不上传截图、OCR 文本或统计数据。
- 发布时记录 ZIP/解压体积、冷启动耗时和一次 16 行识别耗时。
- 保留第三方许可证与 NOTICE。

## 8. 中断恢复方法

新任务开始时按顺序执行：

```powershell
git status --short --branch
git log -8 --oneline
Get-Content -Raw OCR_V2_PLAN.md
dotnet --list-sdks
```

然后：

1. 找到阶段表中第一个 `⏳` 项。
2. 检查上一阶段提交是否存在。
3. 运行全套构建测试确认基线。
4. 只继续当前阶段，不跨阶段混合提交。
5. 完成后把状态更新为 `✅ <commit>` 并提交。

如果工作区有未提交内容，先检查 `git diff`，不要重置或覆盖未知修改。

## 9. 决策记录

### 2026-08-17

- 选择 .NET 10 LTS，不继续基于已停止支持的 .NET 7 发布。
- 选择完全本地 ONNX OCR，不调用第三方 API。
- 选择 PP-OCRv5 Latin mobile，优先数字/英文报表准确率和小体积。
- 不引入完整 Python/Paddle 运行环境。
- 不做重量级表格结构模型；通过检测框坐标聚类实现单列/多列统计。
- Windows OCR 保留为回退，不再作为主识别引擎。

### V2-6 验收记录

- Edge 无头模式按真实浏览器字体渲染 `tests/manual/v2-16-row-report.html`。
- 数字区框选像素由 PP-OCRv5 恢复为 3 列，每列 16 个值；`1111` 不再拆分。
- 自动化新增 14px、3 列 × 16 行表格回归测试。
- 发行版隐藏启动验证成功，冷启动写入 `Startup OK` 约 1.0–1.5 秒。
- 16 行单列测试首次 OCR 约 1.8 秒，复用模型后约 1.3–1.4 秒。
- 自包含发布：EXE 约 88.3 MB，模型约 13.1 MB，解压约 101.4 MB，ZIP 约 94.6 MB。
- 用户无需安装 .NET、Python、PaddleOCR、ONNX Runtime；不调用第三方识别 API。
