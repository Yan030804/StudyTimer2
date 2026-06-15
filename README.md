# 学习计时器

Windows 桌面学习时间记录工具，使用 WPF 和 .NET 8 开发。发布版本为自包含 Windows x64 单文件程序，电脑无需预装 .NET 环境。

## 主要功能

- 秒表计时、暂停、继续、停止保存和异常恢复。
- 科目新增、改名、颜色、归档和恢复；旧记录自动归入“未分类”。
- 紧凑置顶窗口，记住窗口位置和置顶状态。
- 关闭主窗口后隐藏到系统托盘，托盘可控制计时和退出。
- 统计页可切换“7天 / 本月 / 本年”，支持周期导航和科目筛选。
- 显示总时长、日均时长、最长学习日和最长连续学习天数。
- 历史页支持按日期和科目筛选，并可新增、修改或删除明细。
- 导出当前统计区间或全部历史为 UTF-8 BOM CSV 文件。
- 单实例运行，避免重复托盘图标和并发写入。

## 数据位置

数据默认保存在：

```text
文档\StudyTimerData\年份\月份\日期.txt
```

例如：

```text
文档\StudyTimerData\2026\06\2026-06-15.txt
```

每段学习记录保存为可直接阅读的文本：

```text
1. 2026-06-15 08:20:00 -> 2026-06-15 09:10:00 | 00:50:00 | 科目: 数学 | 科目ID: <guid>
```

科目、上次选择和紧凑窗口偏好保存在数据目录的 `.settings.json`。计时过程中每 5 秒更新 `.state.json`，异常退出后可恢复到最后一次可靠心跳。

## 使用方式

1. 双击 `StudyTimer.exe`。
2. 选择科目，点击“开始学习”。
3. 完成后点击“停止并保存”。
4. 点击“紧凑模式”切换为桌面悬浮计时器。
5. 点击窗口关闭按钮会隐藏到系统托盘；通过托盘菜单中的“退出”正常结束程序。

## 开发与发布

运行测试：

```powershell
C:\tmp\dotnet8\dotnet.exe run --project .\tests\StudyTimer.Tests\StudyTimer.Tests.csproj -c Release
```

发布自包含单文件程序：

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\publish.ps1
```

最终文件：

```text
dist\win-x64\StudyTimer.exe
```
