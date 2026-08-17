# ScreenStat 手工测试清单

操作：托盘确认运行 → Ctrl+Shift+X → 框选 → 看 Count/数字列表/OCR原文

| # | 场景 | 材料 | 期望 | 结果 | 备注 |
|---|---|---|---|---|---|
| 1 | IDE/编辑器纯数字 | scenarios 已验证过 | 5 个数正确 | ✅/❌ | |
| 2 | 浏览器深色整数 | scenarios.html #1 | Count=5 Sum=753 | | |
| 3 | 浏览器浅色整数 | scenarios.html #2 | Count=5 Sum=150 | | |
| 4 | 小数负数 | html #3 或 txt B | 5 个数含负号小数 | | |
| 5 | 千分位 | html #4 或 txt C | 1234 / 12345.67 / 2000 | | |
| 6 | 日志单位 ms | html #5 或 txt D | 5 个耗时 | | |
| 7 | 百分比 | html #6 | 4 个数 | | |
| 8 | 中英混合 | html #7 或 txt E | >=4 个关键数字 | | |
| 9 | 日期IP过滤 | html #8 或 txt F | 主要是 42 | | |
| 10 | 表格列 | html #9 只框客流列 | 1234 1532 987 1821 | | |
| 11 | 记事本/VS Code 打开 txt | scenarios.txt | 同对应段 | | |
| 12 | Windows Terminal / PowerShell | type scenarios.txt | 日志段可识别 | | |
| 13 | 复制统计 | 任意成功结果 | 剪贴板有 Count/Sum... | | |
| 14 | 复制数字 / OCR文本 | 任意成功结果 | 可粘贴 | | |
| 15 | ESC 取消框选 | 任意 | 不弹错误结果 | | |
| 16 | 多显示器（如有） | 副屏框选 | 坐标不偏 | | |
| 17 | 125%/150% DPI（如可测） | 系统缩放 | 框选不偏移 | | |
| 18 | 16 行三列表格 | v2-16-row-report.html，只框数字区 | 3 列 × 16 行，1111 不拆分 | | |

诊断日志：
- `%TEMP%\ScreenStat-startup.log`（只记录启动状态与异常，不保存截图或 OCR 内容）
