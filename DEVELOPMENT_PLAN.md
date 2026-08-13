# ScreenStat 开发计划与进度

## 目的

用可跟踪的任务清单推进开发，避免代码长期未提交。每个任务都必须走完：

```text
开发
  → dotnet build（0 错误）
  → dotnet test（全绿）
  → 手工验收（tests/manual/TEST-CHECKLIST.md）
  → git commit
```

## 状态总览

- 分支：`main`
- 构建：`dotnet build ScreenStat.sln -c Debug` → 0 警告 / 0 错误
- 单元测试：`dotnet test ScreenStat.sln -c Debug` → 20/20 通过
- OCR 集成测试：`dotnet test tests/ScreenStat.OcrTests/ScreenStat.OcrTests.csproj -c Debug` → 6/6 通过
- UI 启动 + 手动修正测试：`dotnet test tests/ScreenStat.SmokeTests/ScreenStat.SmokeTests.csproj -c Debug` → 2/2 通过
- 已修复并提交：
  - `NumberParser` 不再把 `-10 100.25` 误判为 IP
  - 日期/时间噪声不再吞掉末尾独立统计值
  - `app.manifest` 高 DPI 配置迁移到 `ApplicationHighDpiMode`
  - 识别数字支持手动修正，失焦后自动重算统计

## 验收规则

- 构建必须 0 错误、0 警告。
- 单元测试必须全绿。
- 涉及 OCR/框选/托盘等 GUI 功能时，按 `tests/manual/TEST-CHECKLIST.md` 手工验收。
- 每个已完成任务单独提交，提交信息说明改了什么。

## 阶段进度

| 阶段 | 内容 | 状态 |
|---|---|---|
| Phase 1 | 项目初始化 | ✅ 完成 |
| Phase 2 | 统计核心 | ✅ 完成 |
| Phase 3 | 屏幕截图/框选 | ✅ 完成（待真机验收） |
| Phase 4 | Windows 本地 OCR | ✅ 完成（待真机验收） |
| Phase 5 | 完整流程 | ✅ 完成（待真机验收） |
| Phase 6 | 系统托盘 + 全局热键 | ✅ 完成（待真机验收） |
| Phase 7 | 复制结果 | ✅ 完成（待真机验收） |
| Phase 8 | 测试 | ✅ 自动化测试通过；GUI/热键/DPI 仍需真机抽测 |
| Phase 9 | 发布 self-contained win-x64 | ✅ 完成 |

## 待办事项

### 提交

- [x] 将当前实现与修复按步骤提交到 `main`
  - `79c95d0` docs: 添加开发计划与进度跟踪
  - `42d9ff9` chore: 提交 ScreenStat 当前实现基线
  - `6339037` fix(core): 修复数字解析掩码误判
  - `999260a` fix(app): 修复高 DPI 配置构建警告

### OCR 真机复测

`pic/测试结果.txt` 中记录的问题已通过 OCR 集成测试覆盖并修复：

- [x] 小数 / 负数（`0.5`、`-10`、`100.25`）
- [x] 千分位（`1,234` / `12,345.67` / `2,000`）
- [x] 百分比（`12.5%` / `8%` / `99.9%` / `3.14%`）
- [x] 日期 / IP 过滤
- [x] 日志单位

仍需真机抽测的场景（依赖真实屏幕/输入设备）：

- [ ] 真实截图下的小字号、深色主题、多显示器、DPI 缩放
- [ ] 全局热键与托盘交互

### 手工验收

- [ ] 抽测 `tests/manual/TEST-CHECKLIST.md` 中自动化未覆盖的 GUI 场景
- [ ] 多显示器框选坐标验证
- [ ] 125% / 150% DPI 框选偏移验证

### 发布

- [x] 生成 self-contained 单文件 `publish/win-x64/ScreenStat.exe`
- [x] 启动验证通过（`STARTED=True`）

发布命令：

```powershell
dotnet publish src/ScreenStat.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64
```

小体积版本（需目标机安装 .NET 7 Desktop Runtime）：

```powershell
dotnet publish src/ScreenStat.App -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/framework-dependent
```
