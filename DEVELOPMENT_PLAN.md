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
- 单元测试：`dotnet test ScreenStat.sln -c Debug --no-build` → 18/18 通过
- 最近修复：
  - `NumberParser` 不再把 `-10 100.25` 误判为 IP
  - 日期/时间噪声不再吞掉末尾独立统计值
  - `app.manifest` 高 DPI 配置迁移到 `ApplicationHighDpiMode`

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
| Phase 8 | 测试 | 🔶 单元测试通过，手工清单待逐项执行 |
| Phase 9 | 发布 self-contained win-x64 | ⬜ 待完成 |

## 待办事项

### 提交

- [ ] 将当前实现与修复按步骤提交到 `main`

### OCR 真机复测

`pic/测试结果.txt` 中记录的问题需要真实截图复测，属于 OCR 识别质量问题，不能只靠文本解析修复：

- [ ] `-10` 被 OCR 读成 `10`（负号丢失）
- [ ] `0.5` 漏识别
- [ ] `12,345.67` 拆成 `345` / `67`
- [ ] `2,000` 误识别成 `2229`
- [ ] `8%` 漏识别
- [ ] `45ms` 误识别成 `4505`

### 手工验收

- [ ] 逐项执行 `tests/manual/TEST-CHECKLIST.md`
- [ ] 多显示器框选坐标验证
- [ ] 125% / 150% DPI 框选偏移验证

### 发布

- [ ] 生成 self-contained 单文件 `ScreenStat.exe`
- [ ] 在无 .NET Runtime 的 Windows 上验证启动
