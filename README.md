# 桌面图标开关

一个轻量的 Windows 托盘程序。双击桌面空白处即可隐藏全部桌面图标，再次双击恢复显示；程序运行时底部任务栏自动变为全透明。

当前版本：**v1.2**

## 使用方法

1. 运行 `dist\DesktopIconToggle.exe`。
2. 双击桌面空白处切换图标显示状态。
3. 右键任务栏通知区域中的程序图标，可手动切换图标、开关任务栏透明、设置开机启动或退出。

程序只响应桌面空白区域；双击桌面图标、任务栏或普通应用窗口不会触发。

v1.2 新增任务栏全透明：程序运行时将 Windows 任务栏设为全透明。Win11（XAML 任务栏）通过注入
`taskbar_transparency.dll` 到 explorer.exe，借助 XAML Diagnostics 直接修改任务栏背景；Win10 回退到
`SetWindowCompositionAttribute`。托盘菜单可随时开关；退出程序（包括被强杀/崩溃）时任务栏自动恢复原样；
Explorer 重启后自动重新应用。

v1.1 对不同 Windows 10/11 环境进行了兼容性重构：鼠标钩子不再执行耗时的
Explorer/COM 查询，桌面命中测试直接限定到桌面列表，并在每次切换后回读验证状态。

## 技术实现

- **桌面图标切换**：全局低级鼠标钩子只负责检测双击坐标，命中测试通过桌面列表自身的
  无障碍接口区分图标与真正空白区域；切换走 Shell 文件夹视图接口并回读验证。
- **任务栏透明（Win11）**：将 `taskbar_transparency.dll` 注入 `explorer.exe`，
  通过 XAML Diagnostics 接口把任务栏 `BackgroundFill`/`BackgroundStroke` 的
  Fill 设为全透明；注入 DLL 常驻并以共享内存与主程序通信，检测到主进程退出时自动还原。
- **任务栏透明（Win10）**：回退到 `SetWindowCompositionAttribute` 透明渐变。

任务栏透明的 Win11 注入方案参考了 TranslucentTB（GPL-3.0）的 ExplorerTAP 思路，
本项目整体以 GPL-3.0 发布，第三方声明见 `THIRD_PARTY_NOTICES.md`。

## 从源码构建

在 Windows PowerShell 中运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

构建结果位于 `dist\DesktopIconToggle.exe`，同时会生成任务栏透明的注入模块
`dist\taskbar_transparency.dll`（两者需放在同一目录）。构建主程序使用 Windows 自带的
C# 编译能力；编译 `taskbar_transparency.dll` 需要 [MSYS2](https://www.msys2.org/)
（MinGW-w64 + cppwinrt），首次构建会自动生成 C++/WinRT 投影头。

运行系统测试（会短暂切换并恢复桌面图标）：

```powershell
powershell -ExecutionPolicy Bypass -File .\test.ps1
```

程序图标源文件位于 `assets\DesktopIconToggle.png`，多尺寸 Windows 图标为
`assets\DesktopIconToggle.ico`。如需从 PNG 重新生成 ICO，可运行 `python .\create_icon.py`。

## 系统要求

- Windows 10 或 Windows 11
- .NET Framework 4.x（Windows 10/11 通常已内置）

程序支持高 DPI 和多显示器缩放；鼠标位置按每个显示器的实际缩放比例判断。

## 诊断日志

如果在某台电脑上切换失败，可右键托盘图标并选择“打开诊断日志”。日志默认位于：

```text
%LOCALAPPDATA%\DesktopIconToggle\DesktopIconToggle.log
```

日志最大为 1 MB，不记录桌面文件名、内容或按键。
