# 桌面图标开关

一个轻量的 Windows 托盘程序。双击桌面空白处即可隐藏全部桌面图标，再次双击恢复显示。

当前版本：**v1.1**

## 使用方法

1. 运行 `dist\DesktopIconToggle.exe`。
2. 双击桌面空白处切换图标显示状态。
3. 右键任务栏通知区域中的程序图标，可手动切换、设置开机启动或退出。

程序只响应桌面空白区域；双击桌面图标、任务栏或普通应用窗口不会触发。

v1.1 对不同 Windows 10/11 环境进行了兼容性重构：鼠标钩子不再执行耗时的
Explorer/COM 查询，桌面命中测试直接限定到桌面列表，并在每次切换后回读验证状态。

## 从源码构建

在 Windows PowerShell 中运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

构建结果位于 `dist\DesktopIconToggle.exe`。构建过程使用 Windows 自带的 C# 编译能力，无需安装额外依赖。

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
