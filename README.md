# 桌面图标开关

一个轻量的 Windows 托盘程序。双击桌面空白处即可隐藏全部桌面图标，再次双击恢复显示。

## 使用方法

1. 运行 `dist\DesktopIconToggle.exe`。
2. 双击桌面空白处切换图标显示状态。
3. 右键任务栏通知区域中的程序图标，可手动切换、设置开机启动或退出。

程序只响应桌面空白区域；双击桌面图标、任务栏或普通应用窗口不会触发。

## 从源码构建

在 Windows PowerShell 中运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

构建结果位于 `dist\DesktopIconToggle.exe`。构建过程使用 Windows 自带的 C# 编译能力，无需安装额外依赖。

程序图标源文件位于 `assets\DesktopIconToggle.png`，多尺寸 Windows 图标为
`assets\DesktopIconToggle.ico`。如需从 PNG 重新生成 ICO，可运行 `python .\create_icon.py`。

## 系统要求

- Windows 10 或 Windows 11
- .NET Framework 4.x（Windows 10/11 通常已内置）
