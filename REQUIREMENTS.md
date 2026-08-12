# ScreenStat —— Windows 屏幕数字统计工具

## 1. 项目概述

### 1.1 项目名称

ScreenStat

### 1.2 项目定位

ScreenStat 是一个运行在 Windows 上的桌面辅助工具。

用户可以通过全局快捷键，在屏幕上框选任意区域。程序自动截取选中区域，通过 OCR 提取文字和数字，然后自动识别其中的数值，并计算常用统计指标。

目标是解决以下问题：

> 在网页报表、日志、数据库客户端、监控页面、IDE、终端等地方看到一批数字时，不需要手工复制到 Excel，即可快速获得平均值、最大值、最小值、总和、P50、P95、P99 等统计结果。

### 1.3 典型使用场景

#### 场景一：网页报表

用户看到：

```text
1234
1532
987
1821
```

按快捷键并框选后，直接得到：

```text
Count     4
Sum       5574
Average   1393.50
Min       987
Max       1821
Median    1383
P90       ...
P95       ...
P99       ...
```

#### 场景二：日志

例如：

```text
query finished cost=123ms
query finished cost=156ms
query finished cost=98ms
query finished cost=231ms
query finished cost=145ms
```

用户框选日志区域后，程序识别：

```text
123ms
156ms
98ms
231ms
145ms
```

并统计：

```text
Count     5
Average   150.60 ms
Min       98 ms
Max       231 ms
P50       145 ms
P95       ...
P99       ...
```

#### 场景三：网页表格

例如：

```text
门店      客流量      平均停留时间
A         1234        35.6
B         1532        42.1
C         987         28.7
D         1821        51.2
```

第一阶段可以只识别所有数字。

后续版本支持识别表格列，并允许用户选择某一列进行统计。

---

# 2. 产品目标

## 2.1 MVP 目标

第一版必须实现：

1. Windows 常驻后台运行
2. 系统托盘图标
3. 全局快捷键启动截图选择
4. 鼠标拖动框选屏幕区域
5. 获取选中区域截图
6. OCR 识别文字
7. 从 OCR 结果中提取数字
8. 支持整数和小数
9. 支持负数
10. 计算基本统计数据
11. 显示统计结果
12. 支持复制统计结果
13. 支持复制 OCR 原文
14. 全程本地处理，不上传截图和数据
15. 程序可以打包为独立 Windows 可执行程序

## 2.2 MVP 不做

第一版明确不要实现：

* AI/LLM
* 云端 OCR
* 用户账号
* 数据同步
* 数据库
* Web 服务
* 自动更新
* 插件系统
* OCR 结果永久保存
* 复杂表格识别
* 自动识别数据列
* 图表
* Excel 文件导出
* OCR 云 API

这些功能为后续版本预留架构即可。

---

# 3. 技术选型

## 3.1 操作系统

目标平台：

```text
Windows 10/11
```

优先测试：

```text
Windows 11 x64
```

## 3.2 编程语言

C#

## 3.3 Framework

使用：

```text
.NET 10
```

如果开发环境实际只提供稳定的 .NET 8，则允许使用 .NET 8，但代码应该保持兼容性，不依赖特定版本的高级特性。

## 3.4 UI

使用：

```text
WPF
```

不要使用 WinForms 作为主 UI。

原因：

* 更适合现代 Windows 桌面 UI
* XAML 与 UI 逻辑分离
* 后续扩展结果窗口更加方便
* 方便实现透明覆盖层

## 3.5 架构

采用：

```text
MVVM + Service Layer
```

不要把业务逻辑全部写到 Window.xaml.cs。

---

# 4. 总体架构

```text
                         Windows
                            │
                            │ Global Hotkey
                            ▼
                  ┌─────────────────────┐
                  │    HotkeyService    │
                  └──────────┬──────────┘
                             │
                             ▼
                  ┌─────────────────────┐
                  │ SelectionWindow     │
                  │ 屏幕选择覆盖层       │
                  └──────────┬──────────┘
                             │
                             ▼
                  ┌─────────────────────┐
                  │ ScreenCaptureService│
                  └──────────┬──────────┘
                             │ Bitmap
                             ▼
                  ┌─────────────────────┐
                  │    OcrService       │
                  └──────────┬──────────┘
                             │ OCR Text
                             ▼
                  ┌─────────────────────┐
                  │   NumberParser      │
                  └──────────┬──────────┘
                             │
                             │ List<NumberValue>
                             ▼
                  ┌─────────────────────┐
                  │ StatisticsService   │
                  └──────────┬──────────┘
                             │
                             │ StatisticsResult
                             ▼
                  ┌─────────────────────┐
                  │   ResultWindow      │
                  └─────────────────────┘
```

---

# 5. 项目结构

推荐：

```text
ScreenStat/
│
├── ScreenStat.sln
│
├── README.md
├── REQUIREMENTS.md
├── LICENSE
│
├── src/
│   │
│   ├── ScreenStat.App/
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   │
│   │   ├── Views/
│   │   │   ├── SelectionWindow.xaml
│   │   │   ├── SelectionWindow.xaml.cs
│   │   │   ├── ResultWindow.xaml
│   │   │   └── ResultWindow.xaml.cs
│   │   │
│   │   ├── ViewModels/
│   │   │   └── ResultViewModel.cs
│   │   │
│   │   ├── Services/
│   │   │   ├── HotkeyService.cs
│   │   │   ├── ScreenCaptureService.cs
│   │   │   ├── OcrService.cs
│   │   │   ├── NumberParser.cs
│   │   │   ├── StatisticsService.cs
│   │   │   └── ClipboardService.cs
│   │   │
│   │   ├── Models/
│   │   │   ├── NumberValue.cs
│   │   │   ├── OcrResult.cs
│   │   │   └── StatisticsResult.cs
│   │   │
│   │   └── Infrastructure/
│   │       └── ...
│   │
│   └── ScreenStat.Core/
│       ├── Models/
│       │   ├── NumberValue.cs
│       │   └── StatisticsResult.cs
│       │
│       └── Statistics/
│           └── StatisticsCalculator.cs
│
└── tests/
    │
    └── ScreenStat.Tests/
        ├── NumberParserTests.cs
        └── StatisticsCalculatorTests.cs
```

如果 Codex 判断项目规模不需要拆成两个 Class Library，允许 MVP 阶段暂时只使用一个 WPF 项目，但必须保持上述逻辑分层。

---

# 6. 核心数据模型

## 6.1 NumberValue

表示 OCR 提取出的一个数字。

建议：

```csharp
public class NumberValue
{
    public double Value { get; init; }

    public string? OriginalText { get; init; }

    public string? Unit { get; init; }

    public int Position { get; init; }
}
```

例如：

```text
123ms
```

可以表示为：

```text
Value = 123
OriginalText = "123ms"
Unit = "ms"
```

第一版也可以暂时只使用：

```csharp
double
```

但建议从架构上预留 NumberValue。

---

# 7. OCR 设计

## 7.1 原则

第一版必须：

> 优先使用 Windows 本地 OCR。

禁止默认依赖云端 OCR。

禁止上传用户截图。

## 7.2 OCR Service

定义：

```csharp
public interface IOcrService
{
    Task<OcrResult> RecognizeAsync(Bitmap image);
}
```

具体实现与 OCR 技术解耦。

例如：

```text
IOcrService
    │
    └── WindowsOcrService
```

后续可以增加：

```text
TesseractOcrService
PaddleOcrService
CloudOcrService
```

而不影响其他模块。

## 7.3 OCR 输出

OCR 至少返回：

```csharp
public class OcrResult
{
    public string FullText { get; init; }

    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }
}
```

后续可以增加：

* 单词坐标
* 行坐标
* 置信度
* 表格结构

但 MVP 不要求。

---

# 8. 数字解析

这是整个项目的核心模块之一。

定义：

```csharp
public interface INumberParser
{
    IReadOnlyList<NumberValue> Parse(string text);
}
```

## 8.1 必须支持

### 整数

```text
123
456
```

### 小数

```text
123.45
0.123
```

### 负数

```text
-123
-45.6
```

### 千位分隔符

```text
1,234
12,345.67
```

应该解析为：

```text
1234
12345.67
```

### 百分比

```text
12.5%
```

第一版可以解析为：

```text
12.5
```

并记录 Unit：

```text
%
```

### 单位

例如：

```text
123ms
1.23s
456 MB
12.5%
```

第一版至少不要因为单位导致数字提取失败。

---

# 9. 数字过滤规则

必须避免把日期、时间、IP 地址等内容错误地全部当成统计数字。

例如：

```text
2026-08-12 10:30:25
192.168.1.100
```

不应该简单解析成：

```text
2026
08
12
10
30
25
192
168
1
100
```

因此 NumberParser 必须设计基础过滤逻辑。

第一版重点：

1. 支持纯数字列表
2. 支持数字 + 单位
3. 对明显的日期/时间/IP 进行过滤
4. 不要求完全理解自然语言

后续通过 AI 或更复杂规则解决。

---

# 10. 统计功能

MVP 必须提供：

```text
Count
Sum
Average
Min
Max
Median
```

建议同时实现：

```text
P50
P90
P95
P99
```

其中：

```text
P50 = Median
```

## 10.1 统计结果模型

```csharp
public class StatisticsResult
{
    public int Count { get; init; }

    public double Sum { get; init; }

    public double Average { get; init; }

    public double Min { get; init; }

    public double Max { get; init; }

    public double Median { get; init; }

    public double P90 { get; init; }

    public double P95 { get; init; }

    public double P99 { get; init; }
}
```

---

# 11. 百分位计算

必须统一百分位计算算法。

建议使用常见的线性插值方法。

实现：

```text
Percentile(values, percentile)
```

其中：

```text
percentile = 0.50
percentile = 0.90
percentile = 0.95
percentile = 0.99
```

需要编写单元测试。

例如：

```text
[1,2,3,4,5]
```

验证：

```text
P50
P90
P95
P99
```

结果不要求与 Excel 的具体百分位函数完全一致，但必须固定算法，并在代码中注明采用的算法。

---

# 12. 截图选择 UI

这是用户体验最重要的部分之一。

按快捷键：

```text
Ctrl + Shift + X
```

进入选择模式。

## 12.1 效果

整个屏幕显示半透明遮罩。

用户拖动：

```text
起点 → 终点
```

形成矩形：

```text
┌────────────────────────────┐
│                            │
│    ┌──────────────────┐    │
│    │ 123              │    │
│    │ 456              │    │
│    │ 789              │    │
│    └──────────────────┘    │
│                            │
└────────────────────────────┘
```

选区外变暗。

选区保持清晰。

## 12.2 操作

鼠标左键按下：

开始选择。

鼠标移动：

实时调整矩形。

鼠标左键释放：

完成选择。

ESC：

取消。

---

# 13. 多显示器

MVP 必须至少能够在多显示器环境正常工作。

要求：

* 获取所有屏幕
* 覆盖所有屏幕
* 支持跨屏幕选择
* 正确处理负坐标
* 正确处理不同 DPI

尤其注意：

```text
Windows DPI Scaling
```

不能简单假设：

```text
1 logical pixel = 1 physical pixel
```

---

# 14. 全局快捷键

默认：

```text
Ctrl + Shift + X
```

要求：

* 程序在后台时也可以触发
* 快捷键冲突时给出提示
* 后续可以在设置中修改

快捷键服务独立：

```csharp
IHotkeyService
```

不要将 Windows API 调用散落在 UI 代码里。

---

# 15. 结果窗口

截图完成并 OCR 后，显示结果。

建议设计成一个小型浮动窗口：

```text
┌─────────────────────────────────────┐
│ ScreenStat                     ×    │
├─────────────────────────────────────┤
│                                     │
│ 识别到 5 个数字                     │
│                                     │
│ Count       5                       │
│ Sum         753                     │
│ Average     150.60                  │
│ Min         98                      │
│ Max         231                     │
│ Median      145                     │
│ P90         xxx                     │
│ P95         xxx                     │
│ P99         xxx                     │
│                                     │
├─────────────────────────────────────┤
│ [复制统计] [复制数字] [OCR文本]     │
└─────────────────────────────────────┘
```

---

# 16. 复制功能

必须提供：

### 复制统计结果

复制：

```text
Count: 5
Sum: 753
Average: 150.60
Min: 98
Max: 231
Median: 145
P90: xxx
P95: xxx
P99: xxx
```

### 复制数字

复制：

```text
123
156
98
231
145
```

### 复制 OCR 文本

复制 OCR 原始结果。

---

# 17. 异常处理

以下情况必须友好处理：

## 没有选区

直接取消。

## OCR 失败

显示：

```text
OCR 识别失败
```

并提供：

```text
重新截图
关闭
```

## OCR 没有数字

显示：

```text
没有识别到可统计的数字
```

同时允许：

```text
查看 OCR 原文
```

## 只有一个数字

允许统计：

```text
Count = 1
Average = Number
Min = Number
Max = Number
Median = Number
```

百分位也正常返回该数字。

## OCR 出现乱码

不要让程序崩溃。

---

# 18. 性能要求

普通截图区域：

```text
1920x1080
```

从截图完成到显示结果：

目标：

```text
< 2 秒
```

OCR 属于主要耗时部分。

UI 在 OCR 期间不能卡死。

应该显示：

```text
正在识别...
```

OCR 和统计工作必须异步执行。

---

# 19. 隐私要求

这是重要要求。

MVP：

> 所有数据必须在本地处理。

禁止：

* 默认上传截图
* 默认上传 OCR 文本
* 默认访问第三方 API
* 默认保存截图

除非未来用户主动启用云端服务。

---

# 20. 系统托盘

程序启动后默认后台运行。

托盘菜单：

```text
ScreenStat

截图统计
设置
关于
退出
```

双击托盘图标：

执行：

```text
截图统计
```

---

# 21. 启动行为

MVP 默认：

> 程序启动后进入系统托盘，不显示主窗口。

未来增加：

```text
Windows 开机启动
```

但 MVP 不要求自动设置开机启动。

---

# 22. 设置

MVP 可以提供非常简单的设置。

至少预留：

```text
快捷键
OCR语言
结果窗口自动关闭时间
```

默认：

```text
快捷键：Ctrl + Shift + X
OCR：中文 + 英文
自动关闭：关闭
```

设置可以先使用简单配置文件。

不要为了 MVP 引入数据库。

---

# 23. 测试要求

必须编写单元测试。

## NumberParser

测试：

```text
123
123.45
-123
1,234
12,345.67
123ms
1.5s
12.5%
```

以及：

```text
2026-08-12
10:30:25
192.168.1.100
```

确保不会产生明显错误的数字列表。

## StatisticsCalculator

测试：

```text
[1,2,3,4,5]
```

```text
[10]
```

```text
[-10,0,10]
```

```text
[1.5,2.5,3.5]
```

```text
空集合
```

必须明确处理空集合。

---

# 24. 日志

程序内部使用结构化日志。

至少记录：

```text
程序启动
快捷键触发
截图开始
截图结束
OCR开始
OCR结束
数字解析数量
统计完成
异常
```

默认日志级别：

```text
Information
```

不要记录：

* 截图内容
* OCR 全文
* 用户敏感数据

除非用户主动开启 Debug 模式。

---

# 25. 后续版本规划

## V1.0

完成本文档中的 MVP。

核心链路：

```text
快捷键
 ↓
截图
 ↓
OCR
 ↓
数字提取
 ↓
统计
 ↓
结果
```

## V1.1

增加：

```text
单位识别
ms/s
KB/MB/GB
%
```

例如：

```text
1000ms
1.5s
```

可以统一成：

```text
1000ms
1500ms
```

然后统计。

## V1.2

增加表格识别。

例如：

```text
门店    客流    停留
A       100     20
B       200     30
C       300     40
```

识别为二维数据。

用户可以选择：

```text
客流
```

然后统计。

## V1.3

增加图表：

```text
Histogram
Box Plot
```

帮助快速观察：

* 数据分布
* 异常值
* 长尾
* 离群值

## V2.0

增加 AI 数据理解。

例如：

```text
框选日志

用户：
统计接口耗时，排除超过 10 秒的数据
```

AI 负责：

```text
识别字段
过滤数据
确定单位
```

统计引擎负责：

```text
实际计算
```

AI 不直接负责最终数学计算。

---

# 26. AI 架构预留

未来可以定义：

```csharp
public interface IDataAnalysisService
{
    Task<DataAnalysisPlan> AnalyzeAsync(
        string ocrText,
        string userInstruction);
}
```

例如用户：

```text
统计 query cost
```

AI 返回：

```json
{
  "field": "query cost",
  "unit": "ms",
  "filter": null
}
```

统计程序再根据结构化计划执行。

原则：

> LLM 负责理解，不负责最终数值计算。

这样可以避免 AI 算错。

---

# 27. 开发原则

## 原则 1

不要过度设计。

MVP 优先。

## 原则 2

OCR、解析、统计、UI 必须解耦。

## 原则 3

Windows API 集中封装。

## 原则 4

业务逻辑必须可以脱离 UI 单元测试。

## 原则 5

不要把截图数据写入日志。

## 原则 6

不要为了未来 AI 功能提前引入复杂 AI 框架。

## 原则 7

不要引入数据库。

## 原则 8

不要依赖云服务。

---

# 28. Codex 开发任务

Codex 应按照以下阶段执行。

## Phase 1：项目初始化

完成：

* 创建 WPF 项目
* 创建目录结构
* 配置 .NET
* 配置 Git
* 添加测试项目
* 建立基础 MVVM 结构

要求：

```text
dotnet build
```

成功。

```text
dotnet test
```

成功。

---

## Phase 2：统计核心

先不要做 UI。

实现：

```text
NumberParser
StatisticsCalculator
```

完成完整单元测试。

要求：

```text
dotnet test
```

全部通过。

---

## Phase 3：屏幕截图

实现：

```text
ScreenCaptureService
SelectionWindow
```

实现：

* 全屏遮罩
* 鼠标框选
* ESC取消
* 正确获取选区
* 多显示器
* DPI

---

## Phase 4：OCR

实现：

```text
OcrService
```

优先使用 Windows 本地 OCR。

实现：

```text
截图 → OCR → 文本
```

---

## Phase 5：完整流程

打通：

```text
Ctrl + Shift + X
       ↓
SelectionWindow
       ↓
ScreenCaptureService
       ↓
OcrService
       ↓
NumberParser
       ↓
StatisticsCalculator
       ↓
ResultWindow
```

---

## Phase 6：系统托盘

实现：

* 后台运行
* 托盘菜单
* 全局快捷键
* 双击触发截图

---

## Phase 7：复制

实现：

* 复制统计
* 复制数字
* 复制 OCR 文本

---

## Phase 8：测试

至少验证：

1. 普通网页数字
2. Chrome 页面
3. Edge 页面
4. Windows Terminal
5. PowerShell
6. VS Code
7. IDEA
8. 中文文字
9. 英文日志
10. 中英文混合日志
11. 多显示器
12. 125% DPI
13. 150% DPI
14. 负数
15. 小数
16. 千位分隔符
17. 带单位数字

---

## Phase 9：发布

生成：

```text
ScreenStat.exe
```

优先支持：

```text
Self-contained
win-x64
```

用户不需要单独安装 .NET Runtime。

---

# 29. Codex 工作方式

Codex 不应该一次性生成整个项目后就结束。

要求采用：

```text
实现一个阶段
 ↓
编译
 ↓
测试
 ↓
检查
 ↓
再进入下一阶段
```

每个阶段完成后：

```text
git diff
dotnet build
dotnet test
```

确保没有明显错误。

如果某个 Windows API 或 OCR API 不确定：

> 优先查阅官方 Microsoft 文档，而不是猜 API。

---

# 30. 第一阶段验收标准

MVP 最终必须可以完成下面的操作：

### 操作

1. 启动 ScreenStat
2. 程序进入系统托盘
3. 打开任意网页
4. 页面显示：

```text
123
156
98
231
145
```

5. 按：

```text
Ctrl + Shift + X
```

6. 鼠标框选这些数字
7. 松开鼠标
8. 程序执行 OCR
9. 自动提取：

```text
123
156
98
231
145
```

10. 弹出统计结果：

```text
Count
Sum
Average
Min
Max
Median
P90
P95
P99
```

11. 点击“复制统计”
12. 可以直接粘贴到其他程序。

整个过程不需要：

* 打开 Excel
* 手动复制数字
* 手动整理数据
* 联网
* 上传截图

---

# 31. 开发环境

推荐开发环境：

```text
OS:
Windows 11 x64

IDE:
Visual Studio 2022
或
VS Code

Runtime / SDK:
.NET 10 SDK

Language:
C#

UI:
WPF

Version Control:
Git

AI Coding:
Codex CLI
```

如果主要使用 Codex：

```text
Windows Terminal
PowerShell
Git
dotnet CLI
Codex CLI
```

即可完成大部分开发。

Visual Studio 主要用于：

* WPF UI 调试
* XAML 调试
* Windows API 调试
* 最终运行测试

---

# 32. Linux 开发环境的定位

Linux 不作为本项目的主要开发环境。

原因：

本项目核心功能依赖 Windows：

```text
WPF
Windows OCR
Windows API
屏幕捕获
全局快捷键
系统托盘
DPI
多显示器
```

在 Linux 开发会导致：

```text
代码可以写
    ↓
无法真实运行
    ↓
切换 Windows
    ↓
调试
    ↓
发现 Windows API 问题
    ↓
再回 Linux 修改
```

开发效率会明显降低。

因此：

> ScreenStat 主开发环境固定为 Windows 11。

Linux 以后只适合作为 Codex/代码编辑等辅助环境，而不是本项目的主要运行测试环境。

---

# 33. 第一版完成后的扩展方向

最终产品可以逐渐发展为：

```text
                    ScreenStat
                        │
        ┌───────────────┼────────────────┐
        │               │                │
      数字统计         表格分析          日志分析
        │               │                │
    Avg/Max/P95      列统计            字段识别
    Min/Median       排序              单位转换
    P99              分组              条件过滤
        │               │                │
        └───────────────┼────────────────┘
                        │
                       AI
                        │
              自然语言数据分析
```

例如最终可以实现：

```text
框选一段日志

“帮我统计 SQL 查询耗时，
排除超过 5 秒的请求，
告诉我平均值、P95、P99，
再找一下异常值。”
```

但这些能力都建立在第一版可靠的：

```text
截图
 ↓
OCR
 ↓
结构化数据
 ↓
统计引擎
```

之上。

因此第一版最重要的不是“AI”，而是把这条基础链路做好。
